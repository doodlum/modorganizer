#include "healthchecktypes.h"

namespace HealthCheck
{

// Vortex: checks/fileRequirementsCheck.ts:27
const QString FILE_REQUIREMENTS_CHECK_ID =
    QStringLiteral("check-file-level-requirements");

// Vortex: fileRequirementReport.ts:49-62 (categoryOf)
RequirementCategory categoryOf(const FileRequirement& requirement)
{
  switch (requirement.kind) {
  case RequirementKind::Missing:
    return RequirementCategory::Download;
  case RequirementKind::WrongVersionInstalled:
    return RequirementCategory::DownloadReplace;
  case RequirementKind::CorrectVersionUninstalled:
    return RequirementCategory::InstallUninstalled;
  case RequirementKind::WrongVersionEnabled:
    return RequirementCategory::Toggle;
  case RequirementKind::Or:
    return RequirementCategory::Or;
  }
  return RequirementCategory::Download;
}

QString categoryToString(RequirementCategory category)
{
  switch (category) {
  case RequirementCategory::Download:
    return QStringLiteral("download");
  case RequirementCategory::DownloadReplace:
    return QStringLiteral("download-replace");
  case RequirementCategory::InstallUninstalled:
    return QStringLiteral("install-uninstalled");
  case RequirementCategory::Toggle:
    return QStringLiteral("toggle");
  case RequirementCategory::Or:
    return QStringLiteral("or");
  }
  return QString();
}

QString kindToString(RequirementKind kind)
{
  switch (kind) {
  case RequirementKind::Missing:
    return QStringLiteral("missing");
  case RequirementKind::WrongVersionInstalled:
    return QStringLiteral("wrong-version-installed");
  case RequirementKind::WrongVersionEnabled:
    return QStringLiteral("wrong-version-enabled");
  case RequirementKind::CorrectVersionUninstalled:
    return QStringLiteral("correct-version-uninstalled");
  case RequirementKind::Or:
    return QStringLiteral("or");
  }
  return QString();
}

QString branchKindToString(BranchKind kind)
{
  switch (kind) {
  case BranchKind::Download:
    return QStringLiteral("download");
  case BranchKind::Install:
    return QStringLiteral("install");
  case BranchKind::Enable:
    return QStringLiteral("enable");
  }
  return QString();
}

QString statusToString(CheckStatus status)
{
  switch (status) {
  case CheckStatus::Passed:
    return QStringLiteral("passed");
  case CheckStatus::Failed:
    return QStringLiteral("failed");
  case CheckStatus::Warning:
    return QStringLiteral("warning");
  case CheckStatus::Error:
    return QStringLiteral("error");
  }
  return QString();
}

QString severityToString(Severity severity)
{
  switch (severity) {
  case Severity::Info:
    return QStringLiteral("info");
  case Severity::Warning:
    return QStringLiteral("warning");
  case Severity::Error:
    return QStringLiteral("error");
  case Severity::Critical:
    return QStringLiteral("critical");
  }
  return QString();
}

// Vortex: utils/shared/severityStyles.ts:3
QString issueSeverityToString(IssueSeverity severity)
{
  switch (severity) {
  case IssueSeverity::Suggestion:
    return QStringLiteral("suggestion");
  case IssueSeverity::Warning:
    return QStringLiteral("warning");
  case IssueSeverity::Error:
    return QStringLiteral("error");
  }
  return QString();
}

}  // namespace HealthCheck
