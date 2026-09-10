#ifndef HEALTHCHECK_FILEREQUIREMENTSCHECK_H
#define HEALTHCHECK_FILEREQUIREMENTSCHECK_H

#include "healthchecktypes.h"

#include <QList>
#include <QString>

namespace HealthCheck
{

class NexusV3Client;

/** The check's stable id. Vortex: fileRequirementsCheck.ts:27 */
extern const QString FILE_REQUIREMENTS_CHECK_ID;

/**
 * One installed mod, as the check needs to see it.
 *
 * A deliberately flat snapshot of MO2 state rather than a live view of
 * ModInfo: the check runs on a worker thread, and MO2's mod list is not safe
 * to touch from one. The GUI thread fills these in, the worker consumes them.
 *
 * Vortex's equivalent inputs come from its redux store, read synchronously in
 * installedFiles.ts:118-154 (gatherInstalledFiles).
 */
struct GatheredMod
{
  /** MO2 mod name; the identity used to act on the mod later. */
  QString modName;
  /** Nexus domain the mod was downloaded for (meta.ini `gameName`). */
  QString gameDomain;
  int nexusModId  = 0;
  int nexusFileId = 0;
  bool enabled    = false;
  /**
   * Whether this mod is managed by a collection/profile automation and so
   * should satisfy other mods' requirements without emitting its own.
   * Vortex: installedFiles.ts:149 (emitRequirements)
   */
  bool collectionManaged = false;

  QString displayName;
  QString fileName;
  QString version;
  QString thumbnailUrl;
  bool adultContent = false;
};

/**
 * One downloaded-but-not-installed archive.
 * Vortex: installedFiles.ts:160-167 (IDownloadedFileRef)
 */
struct GatheredDownload
{
  /** MO2 download index, as a string; used to start an install. */
  QString downloadId;
  QString gameDomain;
  int nexusModId  = 0;
  int nexusFileId = 0;

  QString modName;
  QString fileName;
  QString version;
  QString modSummary;
  QString thumbnailUrl;
  bool adultContent = false;
};

/** A snapshot of everything the check reads from MO2. */
struct GatheredState
{
  /** Active game's Nexus domain name, e.g. "fallout4". */
  QString gameDomain;
  QList<GatheredMod> mods;
  QList<GatheredDownload> downloads;
};

/** Options controlling MO2-specific deviations, all flag-gated. */
struct CheckOptions
{
  /** See MapperContext::suppressMO2SelfRequirement. */
  bool suppressMO2SelfRequirement = false;
};

/**
 * Run the file-level requirements check over a snapshot of MO2 state.
 *
 * Port of
 *   src/renderer/src/extensions/health_check/utils/fileRequirements/
 *   runFileLevelRequirements.ts:86-154
 * wrapped in the result shaping of
 *   src/renderer/src/extensions/health_check/checks/
 *   fileRequirementsCheck.ts:60-126
 *
 * Blocking; call from a worker thread. Network failures are caught and
 * reported as an error result rather than thrown, matching Vortex.
 */
CheckResult checkFileRequirements(const GatheredState& state, NexusV3Client& client,
                                  const CheckOptions& options = {});

}  // namespace HealthCheck

#include <QMetaType>

Q_DECLARE_METATYPE(HealthCheck::GatheredState)
Q_DECLARE_METATYPE(HealthCheck::CheckOptions)

#endif  // HEALTHCHECK_FILEREQUIREMENTSCHECK_H
