#include "filerequirementscheck.h"

#include "filedependencyresolver.h"
#include "nexusuid.h"
#include "nexusv3client.h"
#include "requirementsreportmapper.h"

#include <QDateTime>
#include <QObject>
#include <QHash>
#include <QSet>

namespace HealthCheck
{

namespace
{

  // Vortex: fileRequirementsCheck.ts:35-55 (createResult)
  CheckResult createResult(qint64 startTime, CheckStatus status, Severity severity,
                           const QString& message)
  {
    CheckResult result;
    result.checkId       = FILE_REQUIREMENTS_CHECK_ID;
    result.status        = status;
    result.severity      = severity;
    result.message       = message;
    result.executionTime = QDateTime::currentMSecsSinceEpoch() - startTime;
    result.timestamp     = QDateTime::currentDateTimeUtc();
    return result;
  }

  /** Display shape for an installed mod. Vortex: installedFiles.ts:293-317 */
  InstalledFileInfo toInstalledFile(const GatheredMod& mod, const QString& fileUID,
                                    const QString& modUID, const ModDetail* details)
  {
    InstalledFileInfo info;
    info.modId   = mod.modName;
    info.fileUID = fileUID;
    info.modUID  = modUID;
    info.modName = mod.displayName.isEmpty() ? mod.modName : mod.displayName;
    // logicalFileName is the friendly display name; the archive name is the
    // fallback when no display name was stored.
    info.fileName = mod.fileName.isEmpty() ? info.modName : mod.fileName;
    info.version  = mod.version;
    info.thumbnailUrl =
        mod.thumbnailUrl.isEmpty() && details ? details->thumbnailUrl : mod.thumbnailUrl;
    // The fetched flag wins: a mod can be flagged adult after it was installed.
    info.adultContent = details ? details->adultContent : mod.adultContent;
    info.enabled      = mod.enabled;
    return info;
  }

  /** Display shape for a downloaded archive. Vortex: installedFiles.ts:221-249 */
  DownloadedFileInfo toDownloadedFile(const GatheredDownload& download,
                                      const QString& fileUID, const QString& modUID,
                                      const ModDetail* details)
  {
    DownloadedFileInfo info;
    info.downloadId = download.downloadId;
    info.fileUID    = fileUID;
    info.modUID     = modUID;
    info.modName    = !download.modName.isEmpty() ? download.modName
                      : details                   ? details->name
                                                  : download.downloadId;
    info.modSummary = !download.modSummary.isEmpty() ? download.modSummary
                      : details                      ? details->summary
                                                     : QString();
    info.fileName   = download.fileName.isEmpty() ? info.modName : download.fileName;
    info.version    = download.version;
    info.thumbnailUrl = !download.thumbnailUrl.isEmpty() ? download.thumbnailUrl
                        : details                        ? details->thumbnailUrl
                                                         : QString();
    info.adultContent = details ? details->adultContent : download.adultContent;
    return info;
  }

  /** A file the user owns, for the surfaced-mod-UID sweep. */
  void collectOwnedModUIDs(const FileRequirement& requirement, QSet<QString>& uids)
  {
    const auto addInstalled = [&uids](const std::optional<InstalledFileInfo>& file) {
      if (file.has_value() && !file->modUID.isEmpty()) {
        uids.insert(file->modUID);
      }
    };
    const auto addDownloaded = [&uids](const std::optional<DownloadedFileInfo>& file) {
      if (file.has_value() && !file->modUID.isEmpty()) {
        uids.insert(file->modUID);
      }
    };

    // Vortex: runFileLevelRequirements.ts:46-62 (ownedFiles)
    switch (requirement.kind) {
    case RequirementKind::Missing:
      break;
    case RequirementKind::WrongVersionInstalled:
      addInstalled(requirement.installedFile);
      break;
    case RequirementKind::WrongVersionEnabled:
      addInstalled(requirement.correctFile);
      addInstalled(requirement.enabledFile);
      break;
    case RequirementKind::CorrectVersionUninstalled:
      addDownloaded(requirement.uninstalledFile);
      addInstalled(requirement.enabledFile);
      break;
    case RequirementKind::Or:
      // Vortex: runFileLevelRequirements.ts:34-43 (branchOwnedFiles)
      for (const RequirementBranch& branch : requirement.branches) {
        switch (branch.kind) {
        case BranchKind::Download:
          break;
        case BranchKind::Install:
          addDownloaded(branch.uninstalledFile);
          break;
        case BranchKind::Enable:
          addInstalled(branch.correctFile);
          break;
        }
        addInstalled(branch.enabledFile);
      }
      break;
    }
  }

}  // namespace

CheckResult checkFileRequirements(const GatheredState& state, NexusV3Client& client,
                                  const CheckOptions& options)
{
  const qint64 startTime = QDateTime::currentMSecsSinceEpoch();

  if (state.gameDomain.isEmpty()) {
    return createResult(startTime, CheckStatus::Passed, Severity::Info,
                        QObject::tr("No game selected"));
  }

  try {
    // --- resolve UIDs -------------------------------------------------------
    //
    // Vortex builds these from its cached Nexus games list (UIDs.ts:10-32);
    // here the numeric id comes from the v1 games list, cached per session.

    struct ModRef
    {
      QString fileUID;
      QString modUID;
      int index = -1;  // into state.mods
    };
    struct DownloadRef
    {
      QString fileUID;
      QString modUID;
      int index = -1;  // into state.downloads
    };

    QList<InstalledFile> installedFiles;
    QList<ModRef> modRefs;
    QHash<QString, int> modRefByFileUID;

    for (int i = 0; i < state.mods.size(); ++i) {
      const GatheredMod& mod = state.mods.at(i);
      // Not a Nexus mod, or MO2 never recorded which file it came from: there
      // is no file version to check. Vortex skips the same case at
      // installedFiles.ts:102-103 and :140-143.
      if (mod.nexusModId <= 0 || mod.nexusFileId <= 0) {
        continue;
      }

      const QString domain =
          mod.gameDomain.isEmpty() ? state.gameDomain : mod.gameDomain;
      const int numericGame = client.numericGameId(domain);
      if (numericGame <= 0) {
        continue;
      }

      const std::optional<QString> fileUID = makeUID(numericGame, mod.nexusFileId);
      const std::optional<QString> modUID  = makeUID(numericGame, mod.nexusModId);
      if (!fileUID.has_value()) {
        continue;
      }

      InstalledFile installed;
      installed.fileVersionUid = *fileUID;
      installed.enabled        = mod.enabled;
      // Collection-managed files satisfy but don't emit.
      installed.emitRequirements = !mod.collectionManaged;
      installedFiles.append(installed);

      modRefByFileUID.insert(*fileUID, modRefs.size());
      modRefs.append(ModRef{*fileUID, modUID.value_or(QString()), i});
    }

    if (installedFiles.isEmpty()) {
      // Vortex: runFileLevelRequirements.ts:97-99
      CheckResult result =
          createResult(startTime, CheckStatus::Passed, Severity::Info,
                       QObject::tr("All file requirements satisfied (checked 0 files)"));
      result.hasFileMetadata      = true;
      result.fileMetadata.gameId  = state.gameDomain;
      return result;
    }

    QSet<QString> uninstalledUids;
    QList<DownloadRef> downloadRefs;
    QHash<QString, int> downloadRefByFileUID;

    for (int i = 0; i < state.downloads.size(); ++i) {
      const GatheredDownload& download = state.downloads.at(i);
      if (download.nexusFileId <= 0) {
        continue;
      }

      const QString domain =
          download.gameDomain.isEmpty() ? state.gameDomain : download.gameDomain;
      const int numericGame = client.numericGameId(domain);
      if (numericGame <= 0) {
        continue;
      }

      const std::optional<QString> fileUID = makeUID(numericGame, download.nexusFileId);
      if (!fileUID.has_value()) {
        continue;
      }
      // An archive whose file is already installed is not an "uninstalled"
      // candidate. Vortex: installedFiles.ts:196-201
      if (modRefByFileUID.contains(*fileUID)) {
        continue;
      }

      const std::optional<QString> modUID = makeUID(numericGame, download.nexusModId);

      uninstalledUids.insert(*fileUID);
      downloadRefByFileUID.insert(*fileUID, downloadRefs.size());
      downloadRefs.append(DownloadRef{*fileUID, modUID.value_or(QString()), i});
    }

    // --- run the resolver ---------------------------------------------------

    ResolverPorts ports;
    ports.fetchCandidates = [&client](const QStringList& uids) {
      return client.fetchCandidates(uids);
    };
    ports.fetchFileVersionDetails = [&client](const QStringList& uids) {
      return client.fetchFileVersionDetails(uids);
    };
    ports.fetchModDetails = [&client](const QStringList& uids) {
      return client.fetchModDetails(uids);
    };

    ResolverContext context;
    context.installedFiles             = installedFiles;
    context.uninstalledFileVersionUids = uninstalledUids;
    context.ports                      = ports;

    const FileRequirementsReport report = checkFileLevelRequirements(context);

    // --- map, twice, to backfill display data -------------------------------
    //
    // Vortex runs the mapper once with no mod details to learn which owned
    // files are actually surfaced, fetches details for just those, then maps
    // again (runFileLevelRequirements.ts:129-153). Same two passes here.

    QHash<QString, ModDetail> modDetailsByUID;

    const auto makeHydrator = [&]() -> HydrateFile {
      return [&](const QString& fileUID) -> std::optional<HydratedFile> {
        const auto modIt = modRefByFileUID.find(fileUID);
        if (modIt != modRefByFileUID.end()) {
          const ModRef& ref     = modRefs.at(modIt.value());
          const auto detailIt   = modDetailsByUID.find(ref.modUID);
          HydratedFile hydrated;
          hydrated.kind      = HydratedFile::Kind::Installed;
          hydrated.installed = toInstalledFile(
              state.mods.at(ref.index), ref.fileUID, ref.modUID,
              detailIt == modDetailsByUID.end() ? nullptr : &detailIt.value());
          return hydrated;
        }

        const auto downloadIt = downloadRefByFileUID.find(fileUID);
        if (downloadIt != downloadRefByFileUID.end()) {
          const DownloadRef& ref = downloadRefs.at(downloadIt.value());
          const auto detailIt    = modDetailsByUID.find(ref.modUID);
          HydratedFile hydrated;
          hydrated.kind       = HydratedFile::Kind::Downloaded;
          hydrated.downloaded = toDownloadedFile(
              state.downloads.at(ref.index), ref.fileUID, ref.modUID,
              detailIt == modDetailsByUID.end() ? nullptr : &detailIt.value());
          return hydrated;
        }

        return std::nullopt;
      };
    };

    MapperContext mapperContext;
    mapperContext.gameId      = state.gameDomain;
    mapperContext.modsChecked = static_cast<int>(installedFiles.size());
    mapperContext.suppressMO2SelfRequirement = options.suppressMO2SelfRequirement;

    FileRequirementsMetadata metadata =
        mapRequirementsReport(report, makeHydrator(), mapperContext);

    // Mod UIDs of the owned files the check surfaces; their display data comes
    // from the local record, which can be stale, so backfill from the API.
    // Vortex: runFileLevelRequirements.ts:69-79 (surfacedModUIDs)
    QSet<QString> surfaced;
    for (const FileLevelRequirements& entry : metadata.fileRequirements) {
      for (const FileRequirement& requirement : entry.requirements) {
        collectOwnedModUIDs(requirement, surfaced);
      }
    }

    if (!surfaced.isEmpty()) {
      QStringList uids(surfaced.begin(), surfaced.end());
      uids.sort();  // deterministic request order
      try {
        for (const ModDetail& detail : client.fetchModDetails(uids)) {
          modDetailsByUID.insert(detail.modUid, detail);
        }
        if (!modDetailsByUID.isEmpty()) {
          metadata = mapRequirementsReport(report, makeHydrator(), mapperContext);
        }
      } catch (const ApiError&) {
        // Nothing to backfill; the first pass stands, as in Vortex where the
        // detail fetch failing returns the initial mapping.
      }
    }

    // --- shape the result ---------------------------------------------------
    // Vortex: fileRequirementsCheck.ts:86-111

    int totalRequirements = 0;
    for (const FileLevelRequirements& entry : metadata.fileRequirements) {
      totalRequirements += static_cast<int>(entry.requirements.size());
    }
    const int sourcesWithRequirements =
        static_cast<int>(metadata.fileRequirements.size());

    CheckResult result =
        totalRequirements == 0 && metadata.errors.isEmpty()
            ? createResult(startTime, CheckStatus::Passed, Severity::Info,
                           QObject::tr("All file requirements satisfied (checked %1 "
                                       "files)")
                               .arg(metadata.modsChecked))
            : createResult(startTime,
                           totalRequirements > 0 ? CheckStatus::Warning
                                                 : CheckStatus::Passed,
                           totalRequirements > 0 ? Severity::Warning : Severity::Info,
                           QObject::tr("Found %1 file requirements across %2 files")
                               .arg(totalRequirements)
                               .arg(sourcesWithRequirements));

    result.fileMetadata    = metadata;
    result.hasFileMetadata = true;
    return result;

  } catch (const ApiError& error) {
    // Vortex: fileRequirementsCheck.ts:112-125
    CheckResult result = createResult(startTime, CheckStatus::Error, Severity::Error,
                                      QObject::tr("Failed to check file requirements"));
    result.details     = QString::fromStdString(error.what());
    return result;
  } catch (const std::exception& error) {
    CheckResult result = createResult(startTime, CheckStatus::Error, Severity::Error,
                                      QObject::tr("Failed to check file requirements"));
    result.details     = QString::fromStdString(error.what());
    return result;
  }
}

}  // namespace HealthCheck
