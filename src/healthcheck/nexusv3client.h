#ifndef HEALTHCHECK_NEXUSV3CLIENT_H
#define HEALTHCHECK_NEXUSV3CLIENT_H

#include "healthchecktypes.h"

#include <QByteArray>
#include <QHash>
#include <QJsonObject>
#include <QObject>
#include <QString>
#include <QStringList>
#include <atomic>
#include <memory>
#include <stdexcept>

class QNetworkAccessManager;

namespace HealthCheck
{

/**
 * Cancellation flag, shared between the thread that owns the client and the
 * one that wants to stop it.
 *
 * The client is created on, and only ever touched from, the worker thread, so
 * the manager cannot hold a pointer to it to call an abort method. A shared
 * atomic is the whole of the cross-thread surface.
 */
using AbortFlag = std::shared_ptr<std::atomic<bool>>;

/** Thrown by the client on transport or API errors; the check reports it. */
class ApiError : public std::runtime_error
{
public:
  explicit ApiError(const QString& message, int httpStatus = 0)
      : std::runtime_error(message.toStdString()), m_httpStatus(httpStatus)
  {}

  int httpStatus() const { return m_httpStatus; }

private:
  int m_httpStatus;
};

/**
 * Minimal client for the three Nexus v3 batch endpoints the health check needs.
 *
 * Mirrors Vortex's client surface:
 *   packages/nexus-api-v3/src/client.ts:141-159 (dependency candidates, paged)
 *   packages/nexus-api-v3/src/client.ts:162-168 (mod file versions)
 *   packages/nexus-api-v3/src/client.ts:192-198 (mods)
 * and its auth handling:
 *   packages/nexus-api-v3/src/client.ts:28-41 (bearer, else apikey)
 *
 * Requests block on a nested event loop, so this must be used from a worker
 * thread, never the GUI thread. The owning check runs on one.
 */
class NexusV3Client : public QObject
{
  Q_OBJECT

public:
  /** Per-request id limits and page size imposed by the v3 batch endpoints.
   *  Vortex: fileDependencyPorts.ts:21-23 */
  static constexpr int MAX_CANDIDATE_SOURCE_IDS = 5000;
  static constexpr int MAX_DETAIL_IDS           = 2000;
  static constexpr int CANDIDATE_PAGE_SIZE      = 5000;

  /** Candidate and version details rarely change between runs; cache for a
   *  while. Vortex: fileDependencyPorts.ts:26 (4 hours) */
  static constexpr qint64 CACHE_TTL_MS = 4LL * 60 * 60 * 1000;

  // `abortFlag` may be null, in which case the client cannot be cancelled.
  explicit NexusV3Client(AbortFlag abortFlag = {}, QObject* parent = nullptr);
  ~NexusV3Client() override;

  /** Personal API key, sent as the `apikey` header. */
  void setApiKey(const QString& apiKey);
  /** OAuth token; takes precedence over the API key, as in Vortex. */
  void setBearerToken(const QString& token);
  void setUserAgent(const QString& userAgent);
  /** Base URL, default https://api.nexusmods.com/v3 (overridable for tests). */
  void setBaseUrl(const QString& baseUrl);

  /** Whether cancellation has been requested through the shared flag. */
  bool isAborted() const;

  /**
   * Every candidate row for the given source versions, across id chunks and
   * pages. Vortex: fileDependencyPorts.ts:66-87 (fetchCandidateRows)
   */
  QList<CandidateRow> fetchCandidates(const QStringList& fileVersionUids);

  /**
   * Version details for the given versions, across id chunks.
   * Vortex: fileDependencyPorts.ts:90-99 (fetchVersionDetailRows)
   */
  QList<FileVersionDetail> fetchFileVersionDetails(const QStringList& fileVersionUids);

  /** Mod-level display details, across id chunks. */
  QList<ModDetail> fetchModDetails(const QStringList& modUids);

  /**
   * Numeric Nexus game id for a domain name (e.g. "fallout4" -> 1151), from the
   * v1 games list, fetched once and cached for the session.
   *
   * The composite UID encoding needs it; Vortex gets the same mapping from its
   * cached games list (UIDs.ts:10-32).
   */
  int numericGameId(const QString& domainName);

  /**
   * The resolved domain -> numeric id map, once the games list has been
   * fetched. Empty before then. Used to turn a candidate's composite UID
   * back into a Nexus domain for building links.
   */
  QHash<QString, int> gameIdMap() const { return m_gameIds; }

  /** Drop every cached response (used when the user forces a refresh). */
  void clearCache();

private:
  QByteArray post(const QString& path, const QJsonObject& body);
  QByteArray get(const QString& url);

  QNetworkAccessManager* m_manager = nullptr;
  QString m_apiKey;
  QString m_bearerToken;
  QString m_userAgent;
  QString m_baseUrl;
  AbortFlag m_abortFlag;

  struct CandidateCacheEntry
  {
    qint64 storedAt = 0;
    QList<CandidateRow> rows;
  };
  struct DetailCacheEntry
  {
    qint64 storedAt = 0;
    FileVersionDetail detail;
  };
  struct ModCacheEntry
  {
    qint64 storedAt = 0;
    ModDetail mod;
  };

  // Keyed by the (globally unique) source/version/mod uids.
  QHash<QString, CandidateCacheEntry> m_candidateCache;
  QHash<QString, DetailCacheEntry> m_detailCache;
  QHash<QString, ModCacheEntry> m_modCache;
  QHash<QString, int> m_gameIds;
  bool m_gamesLoaded = false;
};

}  // namespace HealthCheck

#endif  // HEALTHCHECK_NEXUSV3CLIENT_H
