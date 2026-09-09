#ifndef HEALTHCHECK_TYPES_H
#define HEALTHCHECK_TYPES_H

#include <QDateTime>
#include <QList>
#include <QMap>
#include <QSet>
#include <QString>
#include <QStringList>
#include <optional>

namespace HealthCheck
{

// ---------------------------------------------------------------------------
// Resolver-facing rows.
//
// Direct port of Vortex's standalone resolver package:
//   packages/file-dependency-resolver/src/types.ts
// The field names follow the TypeScript ones so the two implementations can be
// diffed against a shared fixture corpus (see tools/healthcheck-parity).
// ---------------------------------------------------------------------------

// Nexus numeric file-category codes.
// Vortex: packages/file-dependency-resolver/src/checkFileLevelRequirements.ts:12-20
enum class FileCategory
{
  Main          = 1,
  Update        = 2,
  Optional      = 3,
  OldVersion    = 4,
  Miscellaneous = 5,
  Removed       = 6,
  Archived      = 7,
  Unknown       = 0
};

// One materialized dependency candidate row.
// Vortex: packages/file-dependency-resolver/src/types.ts:6-15
struct CandidateRow
{
  QString sourceFileVersionUid;
  QString definitionId;
  // The update group / chain the candidate belongs to.
  QString modFileId;
  QString fileVersionUid;
  // Decimal string; higher = newer within the chain. Compared numerically.
  QString position;
  int category = 0;
  QString modStatus;
  QString modUid;
};

// Vortex: packages/file-dependency-resolver/src/types.ts:17-23
struct FileVersionDetail
{
  QString fileVersionUid;
  QString modUid;
  QString modFileId;
  QString name;
  QString version;
};

// Vortex: packages/file-dependency-resolver/src/types.ts:25-31
struct ModDetail
{
  QString modUid;
  QString name;
  QString summary;
  QString thumbnailUrl;
  bool adultContent = false;
};

// Vortex: packages/file-dependency-resolver/src/types.ts:41-46
struct InstalledFile
{
  QString fileVersionUid;
  bool enabled = false;
  // Collection-managed files satisfy requirements but emit none of their own.
  bool emitRequirements = true;
};

// A file the user does not have; the only hydrated payload in the report.
// Vortex: packages/file-dependency-resolver/src/types.ts:59-72
struct Candidate
{
  QString fileVersionUid;
  QString modUid;
  QString modFileId;
  int category = 0;
  QString position;
  QString fileName;
  QString version;
  QString modName;
  QString modSummary;
  QString thumbnailUrl;
  bool adultContent = false;
};

// One OR alternative (a single update group).
// Vortex: packages/file-dependency-resolver/src/types.ts:75-87
struct DependencyBranch
{
  QString modFileId;
  QStringList satisfyingEnabled;
  QStringList satisfyingDisabled;
  QStringList satisfyingUninstalled;
  QStringList wrongEnabled;
  QStringList wrongDisabled;
  // Set only when no acceptable version is owned.
  std::optional<Candidate> recommended;
};

// Vortex: packages/file-dependency-resolver/src/types.ts:89-92
struct DependencyResult
{
  QString definitionId;
  QList<DependencyBranch> branches;
};

// Vortex: packages/file-dependency-resolver/src/types.ts:94-97
struct SourceResult
{
  QString sourceFileVersionUid;
  QList<DependencyResult> dependencies;
};

// Vortex: packages/file-dependency-resolver/src/types.ts:99-101
struct FileRequirementsReport
{
  QList<SourceResult> sources;
};

// ---------------------------------------------------------------------------
// Owned-file display shapes.
//
// Vortex: src/renderer/src/extensions/health_check/utils/fileRequirements/
//         installedFiles.ts:17-36 (IInstalledFile) and :41-60 (IDownloadedFile)
// ---------------------------------------------------------------------------

// A file the user already has installed (an MO2 mod).
struct InstalledFileInfo
{
  // MO2 mod name, the key into the mod list (Vortex uses its own mod id).
  QString modId;
  QString fileUID;
  QString modUID;
  QString modName;
  QString thumbnailUrl;
  QString fileName;
  QString version;
  bool adultContent = false;
  bool enabled      = false;
};

// A file the user has downloaded but not yet installed.
struct DownloadedFileInfo
{
  // MO2 download index / archive identity, used to start an install.
  QString downloadId;
  QString fileUID;
  QString modUID;
  QString modName;
  QString modSummary;
  QString fileName;
  QString version;
  bool adultContent = false;
  QString thumbnailUrl;
};

// ---------------------------------------------------------------------------
// Surfaced requirement kinds.
//
// Vortex: src/renderer/src/extensions/health_check/utils/fileRequirements/
//         mapRequirementsReport.ts:37-139
// ---------------------------------------------------------------------------

// Vortex: mapRequirementsReport.ts:15-32 (IFileRequirementCandidate)
struct RequirementCandidate
{
  QString fileUID;
  QString modUID;
  QString modName;
  QString modSummary;
  QString thumbnailUrl;
  QString fileName;
  QString version;
  bool adultContent = false;
};

// The discriminant of IFileRequirement.
// Vortex: mapRequirementsReport.ts:134-139
enum class RequirementKind
{
  Missing,                    // "missing"
  WrongVersionInstalled,      // "wrong-version-installed"
  WrongVersionEnabled,        // "wrong-version-enabled"
  CorrectVersionUninstalled,  // "correct-version-uninstalled"
  Or                          // "or"
};

// The action one OR alternative needs if chosen.
// Vortex: mapRequirementsReport.ts:77-104 (IFileRequirementBranch)
enum class BranchKind
{
  Download,
  Install,
  Enable
};

struct RequirementBranch
{
  BranchKind kind = BranchKind::Download;
  QString modFileId;
  // kind == Download
  std::optional<RequirementCandidate> candidate;
  // kind == Install
  std::optional<DownloadedFileInfo> uninstalledFile;
  // kind == Enable
  std::optional<InstalledFileInfo> correctFile;
  // A wrong version of the same chain currently enabled, if any.
  std::optional<InstalledFileInfo> enabledFile;
};

// A single surfaced dependency of a source file.
struct FileRequirement
{
  RequirementKind kind = RequirementKind::Missing;
  QString requirementDefId;

  // Missing / WrongVersionInstalled
  std::optional<RequirementCandidate> candidate;
  // WrongVersionInstalled
  std::optional<InstalledFileInfo> installedFile;
  // WrongVersionEnabled
  std::optional<InstalledFileInfo> enabledFile;
  std::optional<InstalledFileInfo> correctFile;
  // CorrectVersionUninstalled
  std::optional<DownloadedFileInfo> uninstalledFile;
  // Or
  QList<RequirementBranch> branches;
};

// Unsatisfied requirements for one source file.
// Vortex: mapRequirementsReport.ts:145-154 (IFileLevelRequirements)
struct FileLevelRequirements
{
  QString sourceFileUID;
  QString sourceModName;
  QString sourceModUID;
  QList<FileRequirement> requirements;
};

// Vortex: mapRequirementsReport.ts:159-168 (IFileRequirementsCheckMetadata)
struct FileRequirementsMetadata
{
  QString gameId;
  int modsChecked = 0;
  // Keyed by source file UID.
  QMap<QString, FileLevelRequirements> fileRequirements;
  QStringList errors;
};

// ---------------------------------------------------------------------------
// Report categories (the UI grouping).
//
// Vortex: src/renderer/src/extensions/health_check/utils/fileRequirements/
//         fileRequirementReport.ts:19-29
// ---------------------------------------------------------------------------

enum class RequirementCategory
{
  Download,            // "download"
  DownloadReplace,     // "download-replace"
  InstallUninstalled,  // "install-uninstalled"
  Toggle,              // "toggle"
  Or                   // "or"
};

// Vortex: fileRequirementReport.ts:49-62 (categoryOf)
RequirementCategory categoryOf(const FileRequirement& requirement);

// Stable string form of a category, matching Vortex's literal union values.
// Used for the shared parity fixtures and for issue ids.
QString categoryToString(RequirementCategory category);

// Stable string form of a requirement kind, matching Vortex's literals.
QString kindToString(RequirementKind kind);

// Stable string form of an OR branch kind, matching Vortex's literals.
QString branchKindToString(BranchKind kind);

// ---------------------------------------------------------------------------
// Check results.
//
// Vortex: src/renderer/src/types/IHealthCheck.ts:13-44
// ---------------------------------------------------------------------------

// Vortex: IHealthCheck.ts:13-18
enum class Severity
{
  Info,
  Warning,
  Error,
  Critical
};

// Vortex: IHealthCheck.ts:35 (IHealthCheckResult.status)
enum class CheckStatus
{
  Passed,
  Failed,
  Warning,
  Error
};

// Vortex: IHealthCheck.ts:20-31
enum class Trigger
{
  Manual,
  Startup,
  GameChanged,
  ProfileChanged,
  ModsChanged,
  LoginChanged,
  SettingsChanged,
  PluginsChanged,
  Scheduled
};

// The severity band the listing/badge uses.
// Vortex: utils/shared/severityStyles.ts:3 (Severity) - note this is the *UI*
// severity, distinct from the check-result severity above.
enum class IssueSeverity
{
  Suggestion,
  Warning,
  Error
};

QString statusToString(CheckStatus status);
QString severityToString(Severity severity);
QString issueSeverityToString(IssueSeverity severity);

// The result of running one check.
// Vortex: IHealthCheck.ts:33-44 (IHealthCheckResult)
struct CheckResult
{
  QString checkId;
  CheckStatus status = CheckStatus::Passed;
  Severity severity  = Severity::Info;
  QString message;
  QString details;
  qint64 executionTime = 0;
  QDateTime timestamp;

  // Populated for the file-level requirements check.
  FileRequirementsMetadata fileMetadata;
  bool hasFileMetadata = false;
};

}  // namespace HealthCheck

#endif  // HEALTHCHECK_TYPES_H
