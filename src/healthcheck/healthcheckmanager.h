#ifndef HEALTHCHECK_MANAGER_H
#define HEALTHCHECK_MANAGER_H

#include "filerequirementscheck.h"
#include "healthcheckentries.h"
#include "healthchecktypes.h"

#include <QObject>
#include <QSet>
#include <QString>
#include <QTimer>
#include <atomic>
#include <functional>
#include <memory>
#include <utility>

class QSettings;
class QThread;

namespace HealthCheck
{

class NexusV3Client;
class CheckWorker;

/**
 * Feature flags, all stored under the [HealthCheck] settings group.
 *
 * Everything that is not a faithful port of Vortex behaviour is gated here, so
 * the default configuration matches Vortex and each deviation can be turned off
 * independently.
 */
struct FeatureFlags
{
  /** Master switch for the whole feature. Key: HealthCheck/enabled */
  bool enabled = true;

  /**
   * The file-level requirements check.
   * Vortex gates this on both an Unleash flag and a user setting
   * (fileRequirementsCheck.ts:147-159); MO2 has no Unleash, so the user
   * setting alone stands in.
   * Key: HealthCheck/fileRequirementsEnabled
   */
  bool fileRequirementsEnabled = true;

  /**
   * MO2 ADDITION - treat a dependency on Mod Organizer 2's own Nexus listing
   * as always satisfied, mirroring what Vortex does for its own listing.
   * Off by default because it is not Vortex-identical behaviour.
   * Key: HealthCheck/suppressSelfRequirement
   */
  bool suppressSelfRequirement = false;

  /**
   * MO2 ADDITION - flag mods in the mod list that have an unresolved issue.
   * Vortex has no mod-list equivalent, so this is additive.
   * Key: HealthCheck/modListIndicator
   */
  bool modListIndicator = true;

  /**
   * MO2 ADDITION - raise an MO2 notification when a run finds new issues.
   * Key: HealthCheck/notifications
   */
  bool notifications = true;

  /**
   * MO2 ADDITION - re-run automatically when the mod list or downloads change.
   * Vortex always does this (triggers.ts:60-126); made optional here because
   * MO2 users often make long bursts of changes.
   * Key: HealthCheck/autoRun
   */
  bool autoRun = true;

  static FeatureFlags load(QSettings& settings);
  void save(QSettings& settings) const;
};

/**
 * Owns the health check lifecycle: scheduling, running it off the GUI thread,
 * holding the last result, and tracking which issues the user has dismissed.
 *
 * The scheduling semantics are ported from
 *   src/renderer/src/extensions/health_check/core/HealthCheckRegistry.ts:147-325
 * - a run already in flight is never interrupted;
 * - any number of requests arriving during a run coalesce into exactly one
 *   rerun, fired 500ms after that run settles;
 * - a run that exceeds the timeout is abandoned and reported, and its slot is
 *   only released once the body actually finishes.
 */
class HealthCheckManager : public QObject
{
  Q_OBJECT

public:
  /** How long to wait, once a busy check settles, before running a rerun
   *  requested while it was busy.
   *  Vortex: HealthCheckRegistry.ts:23 (RERUN_DEBOUNCE_MS) */
  static constexpr int RERUN_DEBOUNCE_MS = 500;

  /** Vortex: HealthCheckRegistry.ts:201 (default timeout) */
  static constexpr int DEFAULT_TIMEOUT_MS = 30'000;

  /** Debounce on mod/download changes. Vortex: triggers.ts:64-67 */
  static constexpr int MODS_CHANGED_DEBOUNCE_MS = 500;

  explicit HealthCheckManager(QObject* parent = nullptr);
  ~HealthCheckManager() override;

  /** Supplies a snapshot of MO2 state. Called on the GUI thread. */
  void setStateProvider(std::function<GatheredState()> provider);

  /**
   * Nexus credentials, read afresh at the start of every run rather than
   * cached: the user can sign in, sign out or have a token refreshed at any
   * point, and a stale key would silently turn every run into a
   * "not logged in" pass. Returns {apiKey, bearerToken}.
   */
  void setCredentialProvider(std::function<std::pair<QString, QString>()> provider);

  /** Set credentials directly; mainly for tests. */
  void setCredentials(const QString& apiKey, const QString& bearerToken);
  void setUserAgent(const QString& userAgent);
  bool hasCredentials() const;

  void setFlags(const FeatureFlags& flags);
  const FeatureFlags& flags() const { return m_flags; }

  /** Load/store dismissed issues and flags. */
  void loadState(QSettings& settings);
  void saveState(QSettings& settings) const;

  /** Request a run. Coalesces per the registry semantics above. */
  void run(Trigger trigger);
  /** Request a run after the mods-changed debounce. */
  void scheduleModsChangedRun();

  bool isRunning() const { return m_running; }

  /**
   * Nexus domain name for a numeric game id, from the games list the last
   * run resolved. Empty when the list has not been fetched yet or the id is
   * unknown. Used to build download links from a candidate composite UID.
   */
  QString nexusDomainForGameId(quint32 numericGameId) const;
  const CheckResult& lastResult() const { return m_lastResult; }
  bool hasResult() const { return m_hasResult; }

  /** Listing entries for the last result, honouring dismissals. */
  QList<IssueEntry> entries() const;
  std::optional<IssueSeverity> badgeSeverity() const;
  IssueCounts counts() const;
  /** MO2 mod names with an unresolved issue, for the mod list indicator. */
  QSet<QString> flaggedMods() const;

  /** Dismiss or restore every requirement in an entry.
   *  Vortex: FileRequirementsContent.tsx:48-56 (toggleHide) */
  void setEntryHidden(const IssueEntry& entry, bool hidden);
  bool isEntryHidden(const IssueEntry& entry) const;
  void clearAllHidden();

signals:
  void resultChanged();
  void runningChanged(bool running);
  /** Raised for the MO2 notification system when a run finds issues. */
  void issuesFound(int count);
  void checkFailed(const QString& message, const QString& details);
  void timedOut();

private slots:
  void onCheckFinished(const HealthCheck::CheckResult& result);
  void onGamesResolved(const QHash<QString, int>& gameIds);

private:
  void startRun();
  void finishRun();

  FeatureFlags m_flags;
  std::function<GatheredState()> m_stateProvider;
  std::function<std::pair<QString, QString>()> m_credentialProvider;

  QThread* m_thread     = nullptr;
  CheckWorker* m_worker = nullptr;

  QString m_apiKey;
  QString m_bearerToken;
  QString m_userAgent;

  bool m_running   = false;
  bool m_hasResult = false;
  CheckResult m_lastResult;

  /** Set when a request collides with a run already in flight. */
  bool m_rerunRequested = false;
  QTimer m_rerunTimer;
  QTimer m_modsChangedTimer;
  QTimer m_timeoutTimer;

  /** Dismissed requirement definition ids, keyed by source file UID. */
  HiddenMap m_hidden;

  // Numeric game id -> Nexus domain, captured after each run.
  QHash<quint32, QString> m_domainByGameId;

  // Shared with the worker's client; the only cross-thread handle to it.
  std::shared_ptr<std::atomic<bool>> m_abortFlag;
};

}  // namespace HealthCheck

#endif  // HEALTHCHECK_MANAGER_H
