#include "requirementsreportmapper.h"

#include "nexusuid.h"

namespace HealthCheck
{

namespace
{

  // Vortex: mapRequirementsReport.ts:178-189 (toCandidate)
  RequirementCandidate toRequirementCandidate(const Candidate& candidate)
  {
    RequirementCandidate out;
    out.fileUID      = candidate.fileVersionUid;
    out.modUID       = candidate.modUid;
    out.modName      = candidate.modName;
    out.modSummary   = candidate.modSummary;
    out.thumbnailUrl = candidate.thumbnailUrl;
    out.fileName     = candidate.fileName;
    out.version      = candidate.version;
    out.adultContent = candidate.adultContent;
    return out;
  }

  /**
   * The wrong version currently enabled on a branch, when there is one we can
   * display. A hydrate miss, or a file that hydrates as a download rather than
   * an install, yields nothing.
   *
   * Vortex: mapRequirementsReport.ts:191-198 (enabledWrongFile)
   */
  std::optional<InstalledFileInfo> enabledWrongFile(const DependencyBranch& branch,
                                                    const HydrateFile& hydrate)
  {
    if (branch.wrongEnabled.isEmpty()) {
      return std::nullopt;
    }
    const std::optional<HydratedFile> hydrated = hydrate(branch.wrongEnabled.first());
    if (!hydrated.has_value() || hydrated->kind != HydratedFile::Kind::Installed) {
      return std::nullopt;
    }
    return hydrated->installed;
  }

  /**
   * Classify one OR alternative into the action it needs if the user picks it.
   * Returns nullopt when the branch isn't actionable.
   *
   * Vortex: mapRequirementsReport.ts:308-352 (classifyOrBranch)
   */
  std::optional<RequirementBranch> classifyOrBranch(const DependencyBranch& branch,
                                                    const HydrateFile& hydrate)
  {
    // Owned-but-disabled alternative: enabling it satisfies the OR without a
    // download.
    if (!branch.satisfyingDisabled.isEmpty()) {
      const std::optional<HydratedFile> correct =
          hydrate(branch.satisfyingDisabled.first());
      if (!correct.has_value() || correct->kind != HydratedFile::Kind::Installed) {
        return std::nullopt;
      }
      RequirementBranch out;
      out.kind        = BranchKind::Enable;
      out.modFileId   = branch.modFileId;
      out.correctFile = correct->installed;
      out.enabledFile = enabledWrongFile(branch, hydrate);
      return out;
    }

    // Downloaded but not installed: installing it satisfies the OR without a
    // download.
    if (!branch.satisfyingUninstalled.isEmpty()) {
      const std::optional<HydratedFile> downloaded =
          hydrate(branch.satisfyingUninstalled.first());
      if (!downloaded.has_value() ||
          downloaded->kind != HydratedFile::Kind::Downloaded) {
        return std::nullopt;
      }
      RequirementBranch out;
      out.kind            = BranchKind::Install;
      out.modFileId       = branch.modFileId;
      out.uninstalledFile = downloaded->downloaded;
      out.enabledFile     = enabledWrongFile(branch, hydrate);
      return out;
    }

    // Otherwise offer the recommended download for this alternative.
    if (branch.recommended.has_value()) {
      RequirementBranch out;
      out.kind        = BranchKind::Download;
      out.modFileId   = branch.modFileId;
      out.candidate   = toRequirementCandidate(*branch.recommended);
      out.enabledFile = enabledWrongFile(branch, hydrate);
      return out;
    }

    return std::nullopt;
  }

  /**
   * Classify a resolved dependency into a surfaced requirement, or nullopt to
   * drop it.
   *
   * Vortex: mapRequirementsReport.ts:205-301 (classifyDependency)
   */
  std::optional<FileRequirement> classifyDependency(const DependencyResult& dependency,
                                                    const HydrateFile& hydrate,
                                                    const MapperContext& context)
  {
    const QList<DependencyBranch>& branches = dependency.branches;

    // Consider any dependencies targeting Vortex as always satisfied.
    // Vortex: mapRequirementsReport.ts:211-214
    for (const DependencyBranch& branch : branches) {
      if (branch.recommended.has_value()) {
        const QString& modUid = branch.recommended->modUid;
        if (modUid == VORTEX_MOD_UID) {
          return std::nullopt;
        }
        // FEATURE FLAG: the MO2 equivalent, see MapperContext.
        if (context.suppressMO2SelfRequirement && modUid == MO2_MOD_UID) {
          return std::nullopt;
        }
      }
    }

    // Satisfied: an acceptable version is enabled on some branch.
    // Vortex: mapRequirementsReport.ts:216-220
    for (const DependencyBranch& branch : branches) {
      if (!branch.satisfyingEnabled.isEmpty()) {
        return std::nullopt;
      }
    }

    // OR: more than one alternative update group.
    // Vortex: mapRequirementsReport.ts:222-238
    if (branches.size() > 1) {
      // A deliberately disabled alternative (nothing wrong enabled to explain
      // it) counts as satisfied - enabling it would clear the OR - matching the
      // single-branch rule below.
      for (const DependencyBranch& branch : branches) {
        if (!branch.satisfyingDisabled.isEmpty() && branch.wrongEnabled.isEmpty()) {
          return std::nullopt;
        }
      }

      FileRequirement requirement;
      requirement.kind             = RequirementKind::Or;
      requirement.requirementDefId = dependency.definitionId;
      for (const DependencyBranch& branch : branches) {
        const std::optional<RequirementBranch> orBranch =
            classifyOrBranch(branch, hydrate);
        if (orBranch.has_value()) {
          requirement.branches.append(*orBranch);
        }
      }

      // An OR with no actionable branch is dropped.
      if (requirement.branches.isEmpty()) {
        return std::nullopt;
      }
      return requirement;
    }

    // Vortex: mapRequirementsReport.ts:240-243
    if (branches.isEmpty()) {
      return std::nullopt;
    }
    const DependencyBranch& branch = branches.first();

    // Correct version owned but disabled.
    // Vortex: mapRequirementsReport.ts:245-264
    if (!branch.satisfyingDisabled.isEmpty()) {
      // A wrong version is enabled too: offer switching the active version.
      const std::optional<InstalledFileInfo> enabled =
          enabledWrongFile(branch, hydrate);
      if (enabled.has_value()) {
        const std::optional<HydratedFile> correct =
            hydrate(branch.satisfyingDisabled.first());
        if (!correct.has_value() || correct->kind != HydratedFile::Kind::Installed) {
          return std::nullopt;
        }
        FileRequirement requirement;
        requirement.kind             = RequirementKind::WrongVersionEnabled;
        requirement.requirementDefId = dependency.definitionId;
        requirement.enabledFile      = enabled;
        requirement.correctFile      = correct->installed;
        return requirement;
      }
      // Owned-but-disabled with nothing wrong enabled is a deliberate choice.
      return std::nullopt;
    }

    // Correct version downloaded but not yet installed.
    // Vortex: mapRequirementsReport.ts:266-278
    if (!branch.satisfyingUninstalled.isEmpty()) {
      const std::optional<HydratedFile> hydrated =
          hydrate(branch.satisfyingUninstalled.first());
      if (!hydrated.has_value() || hydrated->kind != HydratedFile::Kind::Downloaded) {
        return std::nullopt;
      }
      FileRequirement requirement;
      requirement.kind             = RequirementKind::CorrectVersionUninstalled;
      requirement.requirementDefId = dependency.definitionId;
      requirement.uninstalledFile  = hydrated->downloaded;
      requirement.enabledFile      = enabledWrongFile(branch, hydrate);
      return requirement;
    }

    // No acceptable version owned: download one, when the resolver found a
    // candidate.
    // Vortex: mapRequirementsReport.ts:280-300
    if (!branch.recommended.has_value()) {
      return std::nullopt;
    }
    const RequirementCandidate candidate = toRequirementCandidate(*branch.recommended);

    // A wrong version is enabled: this is a "requires a different version"
    // download.
    if (!branch.wrongEnabled.isEmpty()) {
      const std::optional<HydratedFile> hydrated = hydrate(branch.wrongEnabled.first());
      if (!hydrated.has_value() || hydrated->kind != HydratedFile::Kind::Installed) {
        return std::nullopt;
      }
      FileRequirement requirement;
      requirement.kind             = RequirementKind::WrongVersionInstalled;
      requirement.requirementDefId = dependency.definitionId;
      requirement.installedFile    = hydrated->installed;
      requirement.candidate        = candidate;
      return requirement;
    }

    FileRequirement requirement;
    requirement.kind             = RequirementKind::Missing;
    requirement.requirementDefId = dependency.definitionId;
    requirement.candidate        = candidate;
    return requirement;
  }

}  // namespace

FileRequirementsMetadata mapRequirementsReport(const FileRequirementsReport& report,
                                               const HydrateFile& hydrate,
                                               const MapperContext& context)
{
  FileRequirementsMetadata metadata;
  metadata.gameId      = context.gameId;
  metadata.modsChecked = context.modsChecked;
  metadata.errors      = context.errors;

  // Vortex: mapRequirementsReport.ts:365-381
  for (const SourceResult& source : report.sources) {
    QList<FileRequirement> requirements;
    for (const DependencyResult& dependency : source.dependencies) {
      const std::optional<FileRequirement> requirement =
          classifyDependency(dependency, hydrate, context);
      if (requirement.has_value()) {
        requirements.append(*requirement);
      }
    }

    if (requirements.isEmpty()) {
      continue;
    }

    const std::optional<HydratedFile> sourceFile = hydrate(source.sourceFileVersionUid);

    FileLevelRequirements entry;
    entry.sourceFileUID = source.sourceFileVersionUid;
    if (sourceFile.has_value()) {
      entry.sourceModName = sourceFile->kind == HydratedFile::Kind::Installed
                                ? sourceFile->installed.modName
                                : sourceFile->downloaded.modName;
      entry.sourceModUID  = sourceFile->kind == HydratedFile::Kind::Installed
                                ? sourceFile->installed.modUID
                                : sourceFile->downloaded.modUID;
    }
    entry.requirements = requirements;

    metadata.fileRequirements.insert(source.sourceFileVersionUid, entry);
  }

  return metadata;
}

}  // namespace HealthCheck
