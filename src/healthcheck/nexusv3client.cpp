#include "nexusv3client.h"

#include <QDateTime>
#include <QEventLoop>
#include <QJsonArray>
#include <QJsonDocument>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QTimer>

namespace HealthCheck
{

namespace
{

  // Vortex: src/renderer/src/extensions/nexus_integration/constants.ts:10-11
  const QString DEFAULT_BASE_URL = QStringLiteral("https://api.nexusmods.com/v3");
  const QString GAMES_URL        = QStringLiteral("https://api.nexusmods.com/v1/games.json");

  // A single request should not hang the check indefinitely; the registry's own
  // timeout is the outer bound, this keeps one stalled socket from consuming it.
  constexpr int REQUEST_TIMEOUT_MS = 60'000;

  // Legacy numeric file category codes the resolver classifies on.
  // Vortex: fileDependencyPorts.ts:29-38 (CATEGORY_CODES)
  int categoryCode(const QString& category)
  {
    if (category == QLatin1String("main"))
      return 1;
    if (category == QLatin1String("update"))
      return 2;
    if (category == QLatin1String("optional"))
      return 3;
    if (category == QLatin1String("old_version"))
      return 4;
    if (category == QLatin1String("miscellaneous"))
      return 5;
    if (category == QLatin1String("removed"))
      return 6;
    if (category == QLatin1String("archived"))
      return 7;
    return 0;  // "unknown"
  }

  // Vortex: fileDependencyPorts.ts:40-51 (toCandidateRow)
  CandidateRow toCandidateRow(const QJsonObject& object)
  {
    CandidateRow row;
    row.sourceFileVersionUid = object.value(QStringLiteral("source_version_id")).toString();
    row.definitionId         = object.value(QStringLiteral("definition_id")).toString();
    row.modFileId            = object.value(QStringLiteral("mod_file_id")).toString();
    row.fileVersionUid       = object.value(QStringLiteral("version_id")).toString();
    row.position             = object.value(QStringLiteral("position")).toString();
    row.category    = categoryCode(object.value(QStringLiteral("category")).toString());
    row.modStatus   = object.value(QStringLiteral("mod_status")).toString();
    row.modUid      = object.value(QStringLiteral("mod_id")).toString();
    return row;
  }

  // Vortex: fileDependencyPorts.ts:53-61 (toFileVersionDetail)
  FileVersionDetail toFileVersionDetail(const QJsonObject& object)
  {
    FileVersionDetail detail;
    detail.fileVersionUid = object.value(QStringLiteral("id")).toString();
    detail.modUid         = object.value(QStringLiteral("mod_id")).toString();
    detail.modFileId      = object.value(QStringLiteral("mod_file_id")).toString();
    detail.name           = object.value(QStringLiteral("name")).toString();
    detail.version        = object.value(QStringLiteral("version")).toString();
    return detail;
  }

  // Vortex maps the v3 ModDetail onto its IModDetails in
  // utils/shared/modDetails.ts, then onto the resolver's ModDetail in
  // fileDependencyPorts.ts:143-154. Collapsed into one step here.
  ModDetail toModDetail(const QJsonObject& object)
  {
    ModDetail mod;
    mod.modUid       = object.value(QStringLiteral("id")).toString();
    mod.name         = object.value(QStringLiteral("name")).toString();
    mod.summary      = object.value(QStringLiteral("summary")).toString();
    mod.thumbnailUrl = object.value(QStringLiteral("thumbnail_url")).toString();
    mod.adultContent = object.value(QStringLiteral("adult_content")).toBool();
    return mod;
  }

  QJsonArray toJsonArray(const QStringList& values)
  {
    QJsonArray array;
    for (const QString& value : values) {
      array.append(value);
    }
    return array;
  }

  /** Split into chunks of at most `size`. Vortex: utils/shared/batchCache.ts */
  QList<QStringList> chunked(const QStringList& values, int size)
  {
    QList<QStringList> chunks;
    for (int i = 0; i < values.size(); i += size) {
      chunks.append(values.mid(i, size));
    }
    return chunks;
  }

  qint64 nowMs()
  {
    return QDateTime::currentMSecsSinceEpoch();
  }

}  // namespace

NexusV3Client::NexusV3Client(AbortFlag abortFlag, QObject* parent)
    : QObject(parent), m_manager(new QNetworkAccessManager(this)),
      m_baseUrl(DEFAULT_BASE_URL), m_abortFlag(std::move(abortFlag))
{}

NexusV3Client::~NexusV3Client() = default;

void NexusV3Client::setApiKey(const QString& apiKey)
{
  m_apiKey = apiKey;
}

void NexusV3Client::setBearerToken(const QString& token)
{
  m_bearerToken = token;
}

void NexusV3Client::setUserAgent(const QString& userAgent)
{
  m_userAgent = userAgent;
}

void NexusV3Client::setBaseUrl(const QString& baseUrl)
{
  m_baseUrl = baseUrl;
}

bool NexusV3Client::isAborted() const
{
  return m_abortFlag && m_abortFlag->load();
}

void NexusV3Client::clearCache()
{
  m_candidateCache.clear();
  m_detailCache.clear();
  m_modCache.clear();
}

QByteArray NexusV3Client::get(const QString& url)
{
  if (isAborted()) {
    throw ApiError(QStringLiteral("aborted"));
  }

  QNetworkRequest request{QUrl(url)};
  if (!m_userAgent.isEmpty()) {
    request.setHeader(QNetworkRequest::UserAgentHeader, m_userAgent);
  }
  // Bearer wins over the API key, matching Vortex's authHeaders().
  if (!m_bearerToken.isEmpty()) {
    request.setRawHeader("Authorization", ("Bearer " + m_bearerToken).toUtf8());
  } else if (!m_apiKey.isEmpty()) {
    request.setRawHeader("apikey", m_apiKey.toUtf8());
  }

  QNetworkReply* reply = m_manager->get(request);

  QEventLoop loop;
  QTimer timer;
  timer.setSingleShot(true);
  QObject::connect(&timer, &QTimer::timeout, &loop, [&loop, reply] {
    reply->abort();
    loop.quit();
  });
  QObject::connect(reply, &QNetworkReply::finished, &loop, &QEventLoop::quit);
  timer.start(REQUEST_TIMEOUT_MS);
  loop.exec();

  const int status =
      reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
  const QByteArray body = reply->readAll();
  const auto error      = reply->error();
  const QString errorString = reply->errorString();
  reply->deleteLater();

  if (error != QNetworkReply::NoError) {
    throw ApiError(QStringLiteral("GET %1 failed: %2").arg(url, errorString), status);
  }

  return body;
}

QByteArray NexusV3Client::post(const QString& path, const QJsonObject& body)
{
  if (isAborted()) {
    throw ApiError(QStringLiteral("aborted"));
  }

  QNetworkRequest request{QUrl(m_baseUrl + path)};
  request.setHeader(QNetworkRequest::ContentTypeHeader,
                    QStringLiteral("application/json"));
  if (!m_userAgent.isEmpty()) {
    request.setHeader(QNetworkRequest::UserAgentHeader, m_userAgent);
  }
  if (!m_bearerToken.isEmpty()) {
    request.setRawHeader("Authorization", ("Bearer " + m_bearerToken).toUtf8());
  } else if (!m_apiKey.isEmpty()) {
    request.setRawHeader("apikey", m_apiKey.toUtf8());
  }

  QNetworkReply* reply =
      m_manager->post(request, QJsonDocument(body).toJson(QJsonDocument::Compact));

  QEventLoop loop;
  QTimer timer;
  timer.setSingleShot(true);
  QObject::connect(&timer, &QTimer::timeout, &loop, [&loop, reply] {
    reply->abort();
    loop.quit();
  });
  QObject::connect(reply, &QNetworkReply::finished, &loop, &QEventLoop::quit);
  timer.start(REQUEST_TIMEOUT_MS);
  loop.exec();

  const int status =
      reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
  const QByteArray responseBody = reply->readAll();
  const auto error              = reply->error();
  const QString errorString     = reply->errorString();
  reply->deleteLater();

  if (error != QNetworkReply::NoError) {
    // The body often carries the API's own message; include a trimmed form.
    const QString detail = QString::fromUtf8(responseBody.left(300));
    throw ApiError(QStringLiteral("POST %1 failed (%2): %3 %4")
                       .arg(path, QString::number(status), errorString, detail),
                   status);
  }

  return responseBody;
}

QList<CandidateRow> NexusV3Client::fetchCandidates(const QStringList& fileVersionUids)
{
  const qint64 now = nowMs();

  // Serve what the cache still holds and fetch only the misses.
  // Vortex: fileDependencyPorts.ts:128-133 via resolveCached.
  QStringList missing;
  QList<CandidateRow> rows;
  for (const QString& uid : fileVersionUids) {
    const auto it = m_candidateCache.find(uid);
    if (it != m_candidateCache.end() && (now - it->storedAt) < CACHE_TTL_MS) {
      rows.append(it->rows);
    } else {
      missing.append(uid);
    }
  }

  if (missing.isEmpty()) {
    return rows;
  }

  // Seed every requested source so empty results cache too, otherwise a source
  // with no dependencies is refetched on every run.
  // Vortex: fileDependencyPorts.ts:102-110 (groupBySource)
  QHash<QString, QList<CandidateRow>> bySource;
  for (const QString& uid : missing) {
    bySource.insert(uid, {});
  }

  for (const QStringList& ids : chunked(missing, MAX_CANDIDATE_SOURCE_IDS)) {
    int page       = 1;
    int fetched    = 0;
    bool hasMore   = true;
    while (hasMore) {
      if (isAborted()) {
        throw ApiError(QStringLiteral("aborted"));
      }

      QJsonObject body;
      body.insert(QStringLiteral("version_ids"), toJsonArray(ids));
      body.insert(QStringLiteral("page"), page);
      body.insert(QStringLiteral("page_size"), CANDIDATE_PAGE_SIZE);

      const QJsonObject response =
          QJsonDocument::fromJson(
              post(QStringLiteral(
                       "/mod-file-versions/dependencies/ranges/materialized/batch"),
                   body))
              .object();

      const QJsonArray candidates = response.value(QStringLiteral("data"))
                                        .toObject()
                                        .value(QStringLiteral("candidates"))
                                        .toArray();
      const int totalCount = response.value(QStringLiteral("meta"))
                                 .toObject()
                                 .value(QStringLiteral("total_count"))
                                 .toInt();

      for (const QJsonValue& value : candidates) {
        const CandidateRow row = toCandidateRow(value.toObject());
        auto it                = bySource.find(row.sourceFileVersionUid);
        if (it != bySource.end()) {
          it->append(row);
        }
      }

      fetched += static_cast<int>(candidates.size());
      hasMore = !candidates.isEmpty() && fetched < totalCount;
      page += 1;
    }
  }

  for (auto it = bySource.constBegin(); it != bySource.constEnd(); ++it) {
    m_candidateCache.insert(it.key(), CandidateCacheEntry{now, it.value()});
    rows.append(it.value());
  }

  return rows;
}

QList<FileVersionDetail>
NexusV3Client::fetchFileVersionDetails(const QStringList& fileVersionUids)
{
  const qint64 now = nowMs();

  QStringList missing;
  QList<FileVersionDetail> details;
  for (const QString& uid : fileVersionUids) {
    const auto it = m_detailCache.find(uid);
    if (it != m_detailCache.end() && (now - it->storedAt) < CACHE_TTL_MS) {
      details.append(it->detail);
    } else {
      missing.append(uid);
    }
  }

  if (missing.isEmpty()) {
    return details;
  }

  for (const QStringList& ids : chunked(missing, MAX_DETAIL_IDS)) {
    if (isAborted()) {
      throw ApiError(QStringLiteral("aborted"));
    }

    QJsonObject body;
    body.insert(QStringLiteral("version_ids"), toJsonArray(ids));

    const QJsonArray versions =
        QJsonDocument::fromJson(post(QStringLiteral("/mod-file-versions/batch"), body))
            .object()
            .value(QStringLiteral("data"))
            .toObject()
            .value(QStringLiteral("versions"))
            .toArray();

    for (const QJsonValue& value : versions) {
      const FileVersionDetail detail = toFileVersionDetail(value.toObject());
      m_detailCache.insert(detail.fileVersionUid, DetailCacheEntry{now, detail});
      details.append(detail);
    }
    // Unknown or non-visible versions are simply omitted by the endpoint, so
    // they stay uncached and are retried next run - matching Vortex, whose
    // resolveCached only stores what came back.
  }

  return details;
}

QList<ModDetail> NexusV3Client::fetchModDetails(const QStringList& modUids)
{
  const qint64 now = nowMs();

  QStringList missing;
  QList<ModDetail> mods;
  for (const QString& uid : modUids) {
    const auto it = m_modCache.find(uid);
    if (it != m_modCache.end() && (now - it->storedAt) < CACHE_TTL_MS) {
      mods.append(it->mod);
    } else {
      missing.append(uid);
    }
  }

  if (missing.isEmpty()) {
    return mods;
  }

  for (const QStringList& ids : chunked(missing, MAX_DETAIL_IDS)) {
    if (isAborted()) {
      throw ApiError(QStringLiteral("aborted"));
    }

    QJsonObject body;
    body.insert(QStringLiteral("mod_ids"), toJsonArray(ids));

    const QJsonArray modArray =
        QJsonDocument::fromJson(post(QStringLiteral("/mods/batch"), body))
            .object()
            .value(QStringLiteral("data"))
            .toObject()
            .value(QStringLiteral("mods"))
            .toArray();

    for (const QJsonValue& value : modArray) {
      const ModDetail mod = toModDetail(value.toObject());
      m_modCache.insert(mod.modUid, ModCacheEntry{now, mod});
      mods.append(mod);
    }
  }

  return mods;
}

int NexusV3Client::numericGameId(const QString& domainName)
{
  if (!m_gamesLoaded) {
    const QJsonArray games = QJsonDocument::fromJson(get(GAMES_URL)).array();
    for (const QJsonValue& value : games) {
      const QJsonObject game = value.toObject();
      m_gameIds.insert(game.value(QStringLiteral("domain_name")).toString(),
                       game.value(QStringLiteral("id")).toInt());
    }
    m_gamesLoaded = true;
  }

  return m_gameIds.value(domainName, 0);
}

}  // namespace HealthCheck
