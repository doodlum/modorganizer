#ifndef HEALTHCHECK_ENTRIES_H
#define HEALTHCHECK_ENTRIES_H

#include "healthchecktypes.h"

#include <QHash>
#include <QList>
#include <QSet>
#include <QString>
#include <QStringList>

namespace HealthCheck
{

/**
 * The resolution flow an issue offers, reported alongside the entry.
 * Vortex: utils/shared/tracking.ts:17 (ResolutionType)
 */
enum class ResolutionType
{
  Install,
  Enable,
  Pick,
  Update
};

/** Vortex: tracking.ts:138-150 (resolutionTypeForCategory) */
ResolutionType resolutionTypeForCategory(RequirementCategory category);

QString resolutionTypeToString(ResolutionType type);

/**
 * One row in the health check listing.
 *
 * A source file's unsatisfied requirements are split per category, and each
 * category is split again into a showing and a dismissed entry, so a partially
 * dismissed file shows its live issues under Active and its hidden ones under
 * Hidden.
 *
 * Vortex: views/content/FileRequirementsContent.tsx:26-41 (selectEntries)
 *         views/content/fileRequirementEntries.ts:49-84 (pushReportEntries)
 */
struct IssueEntry
{
  /**
   * Listing-row key. A partially dismissed file contributes both a showing and
   * a dismissed entry for the same category, so this carries a suffix the
   * stable issueId does not.
   * Vortex: fileRequirementEntries.ts:42-46 (fileRowKey)
   */
  QString id;
  /**
   * The issue's stable identity: `${sourceFileUID}:${category}`. Unchanged as
   * requirements come and go underneath and as the issue is hidden and
   * restored.
   * Vortex: fileRequirementEntries.ts:34-35 (fileIssueId)
   */
  QString issueId;
  QString checkId;
  IssueSeverity severity        = IssueSeverity::Warning;
  RequirementCategory category  = RequirementCategory::Download;
  ResolutionType resolutionType = ResolutionType::Install;

  QString sourceFileUID;
  QString sourceModName;
  QString sourceModUID;

  /** Homogeneous to `category`. */
  QList<FileRequirement> requirements;

  /** Whether this entry is the dismissed half of its category. */
  bool hidden = false;
};

/** Hidden requirement definition ids, keyed by source file UID.
 *  Vortex: reducers/persistent.ts (hiddenFileRequirements) */
using HiddenMap = QHash<QString, QSet<QString>>;

/**
 * Build the listing entries for a check result.
 * Vortex: FileRequirementsContent.tsx:26-41 (selectEntries)
 */
QList<IssueEntry> buildEntries(const FileRequirementsMetadata& metadata,
                               const HiddenMap& hidden);

/**
 * The worst severity across non-hidden entries, or nullopt when there are none.
 * Drives the indicator on the health check icon.
 *
 * Vortex: components/menu_badge/HealthCheckMenuBadge.tsx:11-27
 */
std::optional<IssueSeverity> highestActiveSeverity(const QList<IssueEntry>& entries);

/** Issue totals split by confidence band.
 *  Vortex: utils/shared/listedEntries.ts:29-42 (IIssueCounts / countIssues) */
struct IssueCounts
{
  int total      = 0;
  int warning    = 0;
  int suggestion = 0;
};

IssueCounts countIssues(const QList<IssueEntry>& entries);

/** The localized title and summary for an entry, by category and count.
 *  Vortex: hooks/useReportCopy.ts:9-30 */
QString entryTitle(const IssueEntry& entry);
QString entrySummary(const IssueEntry& entry);

/** The required mods named by an entry, as the listing's detail line shows
 *  them. Vortex: components/file_requirement/ListingRow.tsx:74-81 */
QString entryDetailLine(const IssueEntry& entry);

/** The required mod's display name for one requirement.
 *  Vortex: fileRequirementReport.ts:120-133 (requirementModName) */
QString requirementModName(const FileRequirement& requirement, const QString& orJoin);

/** Set of MO2 mod names that currently have an unresolved issue against them,
 *  for the mod list indicator. */
QSet<QString> modsWithIssues(const QList<IssueEntry>& entries);

/** Files a category can download in one click (no user choice needed).
 *  Vortex: fileRequirementReport.ts:72-86 (downloadTargets / canQuickInstall) */
struct DownloadTarget
{
  RequirementCandidate candidate;
  /** The wrong version this download replaces, when it is a version change. */
  std::optional<InstalledFileInfo> enabledFile;
};

QList<DownloadTarget> downloadTargets(const QList<FileRequirement>& requirements);
bool canQuickInstall(RequirementCategory category);

}  // namespace HealthCheck

#endif  // HEALTHCHECK_ENTRIES_H
