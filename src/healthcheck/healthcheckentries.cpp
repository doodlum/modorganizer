#include "healthcheckentries.h"

#include "filerequirementscheck.h"

#include <QCoreApplication>
#include <QObject>

namespace HealthCheck
{

namespace
{

  /**
   * Group one source file's (visible or hidden) requirements into per-category
   * entries, preserving first-seen category order.
   *
   * Vortex: fileRequirementEntries.ts:49-84 (pushReportEntries)
   */
  void pushReportEntries(QList<IssueEntry>& entries,
                         const FileLevelRequirements& source,
                         const QList<FileRequirement>& requirements, bool hidden)
  {
    QList<RequirementCategory> order;
    QHash<int, QList<FileRequirement>> byCategory;

    for (const FileRequirement& requirement : requirements) {
      const RequirementCategory category = categoryOf(requirement);
      const int key                      = static_cast<int>(category);
      if (!byCategory.contains(key)) {
        order.append(category);
      }
      byCategory[key].append(requirement);
    }

    for (const RequirementCategory category : order) {
      const QString categoryName = categoryToString(category);

      IssueEntry entry;
      // Vortex: fileIssueId / fileRowKey
      entry.issueId = QStringLiteral("%1:%2").arg(source.sourceFileUID, categoryName);
      entry.id      = hidden ? entry.issueId + QStringLiteral("::hidden") : entry.issueId;
      entry.checkId = FILE_REQUIREMENTS_CHECK_ID;
      // File-level requirements are the higher-confidence band.
      // Vortex: fileRequirementEntries.ts:73 and utils/shared/tracking.ts:48-51
      entry.severity      = IssueSeverity::Warning;
      entry.category      = category;
      entry.sourceFileUID = source.sourceFileUID;
      entry.sourceModName = source.sourceModName;
      entry.sourceModUID  = source.sourceModUID;
      entry.requirements  = byCategory.value(static_cast<int>(category));
      entry.hidden        = hidden;

      entries.append(entry);
    }
  }

  /** The required mod's display name for one OR alternative.
   *  Vortex: fileRequirementReport.ts:108-117 (branchModName) */
  QString branchModName(const RequirementBranch& branch)
  {
    switch (branch.kind) {
    case BranchKind::Download:
      return branch.candidate.has_value() ? branch.candidate->modName : QString();
    case BranchKind::Install:
      return branch.uninstalledFile.has_value() ? branch.uninstalledFile->modName
                                                : QString();
    case BranchKind::Enable:
      return branch.correctFile.has_value() ? branch.correctFile->modName : QString();
    }
    return QString();
  }

}  // namespace

QList<IssueEntry> buildEntries(const FileRequirementsMetadata& metadata,
                               const HiddenMap& hidden)
{
  QList<IssueEntry> entries;

  for (auto it = metadata.fileRequirements.constBegin();
       it != metadata.fileRequirements.constEnd(); ++it) {
    const FileLevelRequirements& source = it.value();
    const QSet<QString> hiddenDefs       = hidden.value(source.sourceFileUID);

    QList<FileRequirement> visible;
    QList<FileRequirement> dismissed;
    for (const FileRequirement& requirement : source.requirements) {
      if (hiddenDefs.contains(requirement.requirementDefId)) {
        dismissed.append(requirement);
      } else {
        visible.append(requirement);
      }
    }

    pushReportEntries(entries, source, visible, false);
    pushReportEntries(entries, source, dismissed, true);
  }

  return entries;
}

std::optional<IssueSeverity> highestActiveSeverity(const QList<IssueEntry>& entries)
{
  // Vortex: HealthCheckMenuBadge.tsx:11 (SEVERITY_RANK)
  const auto rank = [](IssueSeverity severity) {
    switch (severity) {
    case IssueSeverity::Suggestion:
      return 0;
    case IssueSeverity::Warning:
      return 1;
    case IssueSeverity::Error:
      return 2;
    }
    return 0;
  };

  std::optional<IssueSeverity> worst;
  for (const IssueEntry& entry : entries) {
    if (entry.hidden) {
      continue;
    }
    if (!worst.has_value() || rank(entry.severity) > rank(*worst)) {
      worst = entry.severity;
    }
  }
  return worst;
}

IssueCounts countIssues(const QList<IssueEntry>& entries)
{
  IssueCounts counts;
  for (const IssueEntry& entry : entries) {
    if (entry.hidden) {
      continue;
    }
    counts.total += 1;
    if (entry.severity == IssueSeverity::Warning ||
        entry.severity == IssueSeverity::Error) {
      counts.warning += 1;
    } else {
      counts.suggestion += 1;
    }
  }
  return counts;
}

QString entryTitle(const IssueEntry& entry)
{
  // Vortex: useReportCopy.ts:25-27, strings from locales/en/health_check.json
  // ("listing.item.missing_for" / "_plural").
  return entry.requirements.size() > 1
             ? QCoreApplication::translate("HealthCheck",
                                           "Missing required mods for: %1")
                   .arg(entry.sourceModName)
             : QCoreApplication::translate("HealthCheck",
                                           "Missing required mod for: %1")
                   .arg(entry.sourceModName);
}

QString entrySummary(const IssueEntry& entry)
{
  // Vortex: useReportCopy.ts:9-15 (summaryKeyByCategory) plus the "shared.*"
  // strings in locales/en/health_check.json.
  const int count = static_cast<int>(entry.requirements.size());
  const bool many = count > 1;

  switch (entry.category) {
  case RequirementCategory::Download:
    return many ? QCoreApplication::translate(
                      "HealthCheck",
                      "Requires %1 additional mod files to be installed to work "
                      "correctly:")
                      .arg(count)
                : QCoreApplication::translate(
                      "HealthCheck",
                      "Requires %1 additional mod file to be installed to work "
                      "correctly:")
                      .arg(count);
  case RequirementCategory::DownloadReplace:
    return many ? QCoreApplication::translate(
                      "HealthCheck",
                      "Requires installing a different version of %1 existing mod "
                      "files to work correctly:")
                      .arg(count)
                : QCoreApplication::translate(
                      "HealthCheck",
                      "Requires installing a different version of this existing mod "
                      "file to work correctly:");
  case RequirementCategory::InstallUninstalled:
    return many ? QCoreApplication::translate(
                      "HealthCheck",
                      "Requires installing %1 previously downloaded mod files to "
                      "work correctly:")
                      .arg(count)
                : QCoreApplication::translate(
                      "HealthCheck",
                      "Requires installing this previously downloaded mod file to "
                      "work correctly:");
  case RequirementCategory::Toggle:
    return many ? QCoreApplication::translate(
                      "HealthCheck",
                      "Requires enabling a different version of %1 installed mod "
                      "files to work correctly:")
                      .arg(count)
                : QCoreApplication::translate(
                      "HealthCheck",
                      "Requires enabling a different version of this installed mod "
                      "file to work correctly:");
  case RequirementCategory::Or:
    return many ? QCoreApplication::translate(
                      "HealthCheck",
                      "Requires %1 additional mod files to be picked to work "
                      "correctly:")
                      .arg(count)
                : QCoreApplication::translate(
                      "HealthCheck",
                      "Requires %1 additional mod file to be picked to work "
                      "correctly:")
                      .arg(count);
  }
  return QString();
}

QString requirementModName(const FileRequirement& requirement, const QString& orJoin)
{
  // Vortex: fileRequirementReport.ts:120-133
  switch (requirement.kind) {
  case RequirementKind::Missing:
    return requirement.candidate.has_value() ? requirement.candidate->modName
                                             : QString();
  case RequirementKind::WrongVersionInstalled: {
    const QString candidateName =
        requirement.candidate.has_value() ? requirement.candidate->modName : QString();
    if (!candidateName.isEmpty()) {
      return candidateName;
    }
    return requirement.installedFile.has_value() ? requirement.installedFile->modName
                                                 : QString();
  }
  case RequirementKind::CorrectVersionUninstalled:
    return requirement.uninstalledFile.has_value()
               ? requirement.uninstalledFile->modName
               : QString();
  case RequirementKind::WrongVersionEnabled:
    return requirement.correctFile.has_value() ? requirement.correctFile->modName
                                               : QString();
  case RequirementKind::Or: {
    QStringList names;
    for (const RequirementBranch& branch : requirement.branches) {
      names.append(branchModName(branch));
    }
    return names.join(orJoin);
  }
  }
  return QString();
}

QString entryDetailLine(const IssueEntry& entry)
{
  // Vortex: ListingRow.tsx:74-81 - the first required mod, plus a "+N more"
  // count when the entry names several.
  const QString orJoin =
      QStringLiteral(" %1 ").arg(QCoreApplication::translate("HealthCheck", "or"));

  QStringList names;
  for (const FileRequirement& requirement : entry.requirements) {
    const QString name = requirementModName(requirement, orJoin);
    if (!name.isEmpty()) {
      names.append(name);
    }
  }

  if (names.isEmpty()) {
    return QString();
  }
  if (names.size() == 1) {
    return names.first();
  }
  return QStringLiteral("%1 %2").arg(
      names.first(),
      QCoreApplication::translate("HealthCheck", "+%1 more").arg(names.size() - 1));
}

QSet<QString> modsWithIssues(const QList<IssueEntry>& entries)
{
  // The source mod is the one whose requirements are unmet, so it is the mod
  // the list should flag. MO2 keys its mod list by name.
  QSet<QString> mods;
  for (const IssueEntry& entry : entries) {
    if (entry.hidden) {
      continue;
    }
    if (!entry.sourceModName.isEmpty()) {
      mods.insert(entry.sourceModName);
    }
  }
  return mods;
}

QList<DownloadTarget> downloadTargets(const QList<FileRequirement>& requirements)
{
  // Vortex: fileRequirementReport.ts:72-82
  QList<DownloadTarget> targets;
  for (const FileRequirement& requirement : requirements) {
    switch (requirement.kind) {
    case RequirementKind::Missing:
      if (requirement.candidate.has_value()) {
        targets.append(DownloadTarget{*requirement.candidate, std::nullopt});
      }
      break;
    case RequirementKind::WrongVersionInstalled:
      if (requirement.candidate.has_value()) {
        targets.append(DownloadTarget{*requirement.candidate, requirement.installedFile});
      }
      break;
    default:
      break;
    }
  }
  return targets;
}

bool canQuickInstall(RequirementCategory category)
{
  // Vortex: fileRequirementReport.ts:85-86
  return category == RequirementCategory::Download ||
         category == RequirementCategory::DownloadReplace;
}

}  // namespace HealthCheck
