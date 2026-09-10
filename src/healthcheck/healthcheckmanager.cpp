#include "healthcheckmanager.h"

#include "nexusv3client.h"

#include <QMetaObject>
#include <QSettings>
#include <QThread>

namespace HealthCheck
{

// ---------------------------------------------------------------------------
// Feature flags
// ---------------------------------------------------------------------------

FeatureFlags FeatureFlags::load(QSettings& settings)
{
  FeatureFlags flags;
  settings.beginGroup(QStringLiteral("HealthCheck"));
  flags.enabled = settings.value(QStringLiteral("enabled"), flags.enabled).toBool();
  flags.fileRequirementsEnabled =
      settings
          .value(QStringLiteral("fileRequirementsEnabled"), flags.fileRequirementsEnabled)
          .toBool();
  flags.suppressSelfRequirement =
      settings
          .value(QStringLiteral("suppressSelfRequirement"), flags.suppressSelfRequirement)
          .toBool();
  flags.modListIndicator =
      settings.value(QStringLiteral("modListIndicator"), flags.modListIndicator).toBool();
  flags.notifications =
      settings.value(QStringLiteral("notifications"), flags.notifications).toBool();
  flags.autoRun = settings.value(QStringLiteral("autoRun"), flags.autoRun).toBool();
  settings.endGroup();
  return flags;
}

void FeatureFlags::save(QSettings& settings) const
{
  settings.beginGroup(QStringLiteral("HealthCheck"));
  settings.setValue(QStringLiteral("enabled"), enabled);
  settings.setValue(QStringLiteral("fileRequirementsEnabled"), fileRequirementsEnabled);
  settings.setValue(QStringLiteral("suppressSelfRequirement"), suppressSelfRequirement);
  settings.setValue(QStringLiteral("modListIndicator"), modListIndicator);
  settings.setValue(QStringLiteral("notifications"), notifications);
  settings.setValue(QStringLiteral("autoRun"), autoRun);
  settings.endGroup();
}

// ---------------------------------------------------------------------------
// Worker
// ---------------------------------------------------------------------------

/**
 * Runs one check on the worker thread. The client lives here so every network
 * request (and its nested event loop) stays off the GUI thread, and its
 * response caches survive between runs.
 */
class CheckWorker : public QObject
{
  Q_OBJECT

public:
  CheckWorker() : m_client(std::make_unique<NexusV3Client>()) {}

  NexusV3Client& client() { return *m_client; }

public slots:
  void configure(const QString& apiKey, const QString& bearerToken,
                 const QString& userAgent)
  {
    m_client->setApiKey(apiKey);
    m_client->setBearerToken(bearerToken);
    m_client->setUserAgent(userAgent);
  }

  void runCheck(const HealthCheck::GatheredState& state,
                const HealthCheck::CheckOptions& options)
  {
    const HealthCheck::CheckResult result =
        checkFileRequirements(state, *m_client, options);
    // The games list is only resolved as a side effect of a run, so publish
    // it here for the manager's reverse lookup.
    emit gamesResolved(m_client->gameIdMap());
    emit finished(result);
  }

  void clearCache() { m_client->clearCache(); }

signals:
  void finished(const HealthCheck::CheckResult& result);
  void gamesResolved(const QHash<QString, int>& gameIds);

private:
  std::unique_ptr<NexusV3Client> m_client;
};

// ---------------------------------------------------------------------------
// Manager
// ---------------------------------------------------------------------------

HealthCheckManager::HealthCheckManager(QObject* parent) : QObject(parent)
{
  qRegisterMetaType<HealthCheck::CheckResult>("HealthCheck::CheckResult");
  qRegisterMetaType<HealthCheck::GatheredState>("HealthCheck::GatheredState");
  qRegisterMetaType<HealthCheck::CheckOptions>("HealthCheck::CheckOptions");

  m_thread = new QThread(this);
  m_worker = new CheckWorker();
  m_worker->moveToThread(m_thread);
  connect(m_thread, &QThread::finished, m_worker, &QObject::deleteLater);
  connect(m_worker, &CheckWorker::finished, this, &HealthCheckManager::onCheckFinished);
  connect(m_worker, &CheckWorker::gamesResolved, this,
          &HealthCheckManager::onGamesResolved);
  m_thread->start();

  m_rerunTimer.setSingleShot(true);
  m_rerunTimer.setInterval(RERUN_DEBOUNCE_MS);
  connect(&m_rerunTimer, &QTimer::timeout, this, [this] {
    startRun();
  });

  m_modsChangedTimer.setSingleShot(true);
  m_modsChangedTimer.setInterval(MODS_CHANGED_DEBOUNCE_MS);
  connect(&m_modsChangedTimer, &QTimer::timeout, this, [this] {
    run(Trigger::ModsChanged);
  });

  // A run that overruns is reported and abandoned; the worker keeps going but
  // its result is ignored, matching the registry's abandon-and-notify path
  // (HealthCheckRegistry.ts:206-231, :272-276).
  m_timeoutTimer.setSingleShot(true);
  m_timeoutTimer.setInterval(DEFAULT_TIMEOUT_MS);
  connect(&m_timeoutTimer, &QTimer::timeout, this, [this] {
    if (!m_running) {
      return;
    }
    QMetaObject::invokeMethod(m_worker, [this] {
      m_worker->client().abort();
    });
    emit timedOut();
  });
}

HealthCheckManager::~HealthCheckManager()
{
  if (m_thread != nullptr) {
    if (m_worker != nullptr) {
      m_worker->client().abort();
    }
    m_thread->quit();
    m_thread->wait(5000);
  }
}

void HealthCheckManager::setStateProvider(std::function<GatheredState()> provider)
{
  m_stateProvider = std::move(provider);
}

void HealthCheckManager::setCredentialProvider(
    std::function<std::pair<QString, QString>()> provider)
{
  m_credentialProvider = std::move(provider);
}

void HealthCheckManager::setCredentials(const QString& apiKey, const QString& bearerToken)
{
  m_apiKey      = apiKey;
  m_bearerToken = bearerToken;
  QMetaObject::invokeMethod(m_worker, "configure", Qt::QueuedConnection,
                            Q_ARG(QString, m_apiKey), Q_ARG(QString, m_bearerToken),
                            Q_ARG(QString, m_userAgent));
}

void HealthCheckManager::setUserAgent(const QString& userAgent)
{
  m_userAgent = userAgent;
  QMetaObject::invokeMethod(m_worker, "configure", Qt::QueuedConnection,
                            Q_ARG(QString, m_apiKey), Q_ARG(QString, m_bearerToken),
                            Q_ARG(QString, m_userAgent));
}

bool HealthCheckManager::hasCredentials() const
{
  if (m_credentialProvider) {
    const auto [apiKey, bearer] = m_credentialProvider();
    return !apiKey.isEmpty() || !bearer.isEmpty();
  }
  return !m_apiKey.isEmpty() || !m_bearerToken.isEmpty();
}

void HealthCheckManager::setFlags(const FeatureFlags& flags)
{
  const bool wasEnabled = m_flags.enabled && m_flags.fileRequirementsEnabled;
  m_flags               = flags;
  const bool nowEnabled = m_flags.enabled && m_flags.fileRequirementsEnabled;

  if (!nowEnabled) {
    // Dropping the result stops a stale listing outliving the setting.
    m_hasResult = false;
    m_lastResult = CheckResult{};
    emit resultChanged();
    return;
  }

  // Vortex re-runs when either requirements setting changes
  // (index.ts:94-101, SettingsChanged trigger).
  if (!wasEnabled) {
    run(Trigger::SettingsChanged);
  }
}

void HealthCheckManager::loadState(QSettings& settings)
{
  m_flags = FeatureFlags::load(settings);

  m_hidden.clear();
  settings.beginGroup(QStringLiteral("HealthCheckHidden"));
  for (const QString& sourceFileUID : settings.childKeys()) {
    const QStringList defs = settings.value(sourceFileUID).toStringList();
    if (!defs.isEmpty()) {
      m_hidden.insert(sourceFileUID, QSet<QString>(defs.begin(), defs.end()));
    }
  }
  settings.endGroup();
}

void HealthCheckManager::saveState(QSettings& settings) const
{
  m_flags.save(settings);

  settings.beginGroup(QStringLiteral("HealthCheckHidden"));
  settings.remove(QString());
  for (auto it = m_hidden.constBegin(); it != m_hidden.constEnd(); ++it) {
    if (it.value().isEmpty()) {
      continue;
    }
    QStringList defs(it.value().begin(), it.value().end());
    defs.sort();
    settings.setValue(it.key(), defs);
  }
  settings.endGroup();
}

void HealthCheckManager::run(Trigger trigger)
{
  Q_UNUSED(trigger)

  if (!m_flags.enabled || !m_flags.fileRequirementsEnabled) {
    return;
  }
  if (!m_stateProvider) {
    return;
  }

  // Already running: don't start a second one. Let it finish undisturbed, and
  // make sure exactly one fresh run happens once it settles - no matter how
  // many requests land meanwhile, they all coalesce into that one rerun.
  // Vortex: HealthCheckRegistry.ts:177-183
  if (m_running) {
    m_rerunRequested = true;
    return;
  }

  startRun();
}

void HealthCheckManager::scheduleModsChangedRun()
{
  if (!m_flags.autoRun) {
    return;
  }
  m_modsChangedTimer.start();
}

void HealthCheckManager::startRun()
{
  if (m_running || !m_stateProvider) {
    return;
  }

  if (m_credentialProvider) {
    const auto [apiKey, bearer] = m_credentialProvider();
    if (apiKey != m_apiKey || bearer != m_bearerToken) {
      setCredentials(apiKey, bearer);
    }
  }

  // Not signed in: Vortex reports a clean pass rather than an error
  // (fileRequirementsCheck.ts:75-82).
  if (!hasCredentials()) {
    CheckResult result;
    result.checkId   = FILE_REQUIREMENTS_CHECK_ID;
    result.status    = CheckStatus::Passed;
    result.severity  = Severity::Info;
    result.message   = tr("Not logged into Nexus Mods");
    result.timestamp = QDateTime::currentDateTimeUtc();
    onCheckFinished(result);
    return;
  }

  const GatheredState state = m_stateProvider();

  CheckOptions options;
  options.suppressMO2SelfRequirement = m_flags.suppressSelfRequirement;

  m_running = true;
  emit runningChanged(true);
  m_timeoutTimer.start();

  QMetaObject::invokeMethod(m_worker, "runCheck", Qt::QueuedConnection,
                            Q_ARG(HealthCheck::GatheredState, state),
                            Q_ARG(HealthCheck::CheckOptions, options));
}

void HealthCheckManager::onGamesResolved(const QHash<QString, int>& gameIds)
{
  m_domainByGameId.clear();
  for (auto it = gameIds.constBegin(); it != gameIds.constEnd(); ++it) {
    m_domainByGameId.insert(static_cast<quint32>(it.value()), it.key());
  }
}

QString HealthCheckManager::nexusDomainForGameId(quint32 numericGameId) const
{
  return m_domainByGameId.value(numericGameId);
}

void HealthCheckManager::onCheckFinished(const CheckResult& result)
{
  m_timeoutTimer.stop();

  const int previousIssues = m_hasResult ? countIssues(entries()).total : 0;

  m_lastResult = result;
  m_hasResult  = true;

  if (m_running) {
    m_running = false;
    emit runningChanged(false);
  }

  emit resultChanged();

  if (result.status == CheckStatus::Error) {
    emit checkFailed(result.message, result.details);
  } else if (m_flags.notifications) {
    const int issues = countIssues(entries()).total;
    if (issues > 0 && issues != previousIssues) {
      emit issuesFound(issues);
    }
  }

  finishRun();
}

void HealthCheckManager::finishRun()
{
  // Fire a coalesced rerun if any request collided with the run that just
  // settled. Vortex: HealthCheckRegistry.ts:300-315
  if (!m_rerunRequested) {
    return;
  }
  m_rerunRequested = false;
  m_rerunTimer.start();
}

QList<IssueEntry> HealthCheckManager::entries() const
{
  if (!m_hasResult || !m_lastResult.hasFileMetadata) {
    return {};
  }
  return buildEntries(m_lastResult.fileMetadata, m_hidden);
}

std::optional<IssueSeverity> HealthCheckManager::badgeSeverity() const
{
  return highestActiveSeverity(entries());
}

IssueCounts HealthCheckManager::counts() const
{
  return countIssues(entries());
}

QSet<QString> HealthCheckManager::flaggedMods() const
{
  if (!m_flags.modListIndicator) {
    return {};
  }
  return modsWithIssues(entries());
}

bool HealthCheckManager::isEntryHidden(const IssueEntry& entry) const
{
  // Hidden means every requirement in the entry is dismissed.
  // Vortex: fileRequirementEntries.ts:17-27 (isFileEntryHidden)
  const QSet<QString> hiddenDefs = m_hidden.value(entry.sourceFileUID);
  if (entry.requirements.isEmpty()) {
    return false;
  }
  for (const FileRequirement& requirement : entry.requirements) {
    if (!hiddenDefs.contains(requirement.requirementDefId)) {
      return false;
    }
  }
  return true;
}

void HealthCheckManager::setEntryHidden(const IssueEntry& entry, bool hidden)
{
  // Per-definition storage means a later, newly-unsatisfied dependency on the
  // same file still surfaces. Vortex: FileRequirementsContent.tsx:48-56
  QSet<QString>& defs = m_hidden[entry.sourceFileUID];
  for (const FileRequirement& requirement : entry.requirements) {
    if (hidden) {
      defs.insert(requirement.requirementDefId);
    } else {
      defs.remove(requirement.requirementDefId);
    }
  }
  if (defs.isEmpty()) {
    m_hidden.remove(entry.sourceFileUID);
  }
  emit resultChanged();
}

void HealthCheckManager::clearAllHidden()
{
  m_hidden.clear();
  emit resultChanged();
}

}  // namespace HealthCheck

#include "healthcheckmanager.moc"
