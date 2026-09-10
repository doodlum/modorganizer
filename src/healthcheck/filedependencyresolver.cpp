#include "filedependencyresolver.h"

#include <QHash>
#include <QPair>
#include <QSet>

namespace HealthCheck
{

namespace
{

  // Categories treated as "active" (preferred when recommending a download).
  // Vortex: checkFileLevelRequirements.ts:22-28
  bool isActiveCategory(int category)
  {
    return category == static_cast<int>(FileCategory::Main) ||
           category == static_cast<int>(FileCategory::Update) ||
           category == static_cast<int>(FileCategory::Optional) ||
           category == static_cast<int>(FileCategory::Miscellaneous);
  }

  // Categories still available to download/match even if not active.
  // Vortex: checkFileLevelRequirements.ts:29-37
  bool isAvailableCategory(int category)
  {
    return category == static_cast<int>(FileCategory::Main) ||
           category == static_cast<int>(FileCategory::Update) ||
           category == static_cast<int>(FileCategory::Optional) ||
           category == static_cast<int>(FileCategory::OldVersion) ||
           category == static_cast<int>(FileCategory::Miscellaneous) ||
           category == static_cast<int>(FileCategory::Archived);
  }

  // Vortex: checkFileLevelRequirements.ts:38
  bool isAvailableStatus(const QString& status)
  {
    return status == QLatin1String("published") || status == QLatin1String("hidden");
  }

  // Computed from the raw server signals; gates what we recommend, not matching.
  // Vortex: checkFileLevelRequirements.ts:204-206 (isAvailable)
  bool isAvailable(const CandidateRow& row)
  {
    return isAvailableCategory(row.category) && isAvailableStatus(row.modStatus);
  }

  // Vortex: checkFileLevelRequirements.ts:208-210 (isActive)
  bool isActive(const CandidateRow& row)
  {
    return isActiveCategory(row.category);
  }

  /**
   * Insertion-ordered group-by over row indices, matching JavaScript's `Map`
   * iteration order (insertion order).
   *
   * Vortex: checkFileLevelRequirements.ts:245-254 (groupBy)
   *
   * Indices rather than pointers/copies: the backing QList outlives every
   * grouping level, so nothing can dangle and no row is duplicated.
   */
  template <typename KeyFn>
  QList<QPair<QString, QList<int>>> groupIndices(const QList<CandidateRow>& rows,
                                                 const QList<int>& subset, KeyFn key)
  {
    QList<QPair<QString, QList<int>>> out;
    QHash<QString, int> slotOf;

    for (int index : subset) {
      const QString k = key(rows.at(index));
      const auto it   = slotOf.find(k);
      if (it != slotOf.end()) {
        out[it.value()].second.append(index);
      } else {
        slotOf.insert(k, static_cast<int>(out.size()));
        out.append({k, QList<int>{index}});
      }
    }

    return out;
  }

  // Vortex: checkFileLevelRequirements.ts:212-214 (highestPosition)
  //
  // The TypeScript reduce keeps `best` unless the next row is *strictly*
  // greater, so ties resolve to the earliest row. Preserved deliberately.
  int highestPosition(const QList<CandidateRow>& rows, const QList<int>& subset)
  {
    int best = subset.first();
    for (int index : subset) {
      if (rows.at(index).position.toDouble() > rows.at(best).position.toDouble()) {
        best = index;
      }
    }
    return best;
  }

  // Highest-position available candidate (preferring active categories).
  // Eligibility gates recommendations only, not matching.
  // Vortex: checkFileLevelRequirements.ts:218-223 (selectRecommended)
  int selectRecommended(const QList<CandidateRow>& rows, const QList<int>& branchRows)
  {
    QList<int> available;
    for (int index : branchRows) {
      if (isAvailable(rows.at(index))) {
        available.append(index);
      }
    }
    if (available.isEmpty()) {
      return -1;
    }

    QList<int> active;
    for (int index : available) {
      if (isActive(rows.at(index))) {
        active.append(index);
      }
    }

    return highestPosition(rows, active.isEmpty() ? available : active);
  }

  // Vortex: checkFileLevelRequirements.ts:256-260 (mapByKey) - last write wins.
  QHash<QString, FileVersionDetail>
  mapDetailsByUid(const QList<FileVersionDetail>& details)
  {
    QHash<QString, FileVersionDetail> out;
    for (const FileVersionDetail& detail : details) {
      out.insert(detail.fileVersionUid, detail);
    }
    return out;
  }

  QHash<QString, ModDetail> mapModsByUid(const QList<ModDetail>& mods)
  {
    QHash<QString, ModDetail> out;
    for (const ModDetail& mod : mods) {
      out.insert(mod.modUid, mod);
    }
    return out;
  }

  // Vortex: checkFileLevelRequirements.ts:262-264 (unique) - insertion ordered.
  QStringList uniqueStrings(const QStringList& values)
  {
    QStringList out;
    QSet<QString> seen;
    for (const QString& value : values) {
      if (!seen.contains(value)) {
        seen.insert(value);
        out.append(value);
      }
    }
    return out;
  }

  // The intermediate branch plan; `recRow` is an index into the candidate list.
  // Vortex: checkFileLevelRequirements.ts:142-150 (BranchPlan)
  struct BranchPlan
  {
    QString modFileId;
    QStringList satisfyingEnabled;
    QStringList satisfyingDisabled;
    QStringList satisfyingUninstalled;
    QStringList wrongEnabled;
    QStringList wrongDisabled;
    int recRow = -1;
  };

  struct DefPlan
  {
    QString definitionId;
    QList<BranchPlan> branches;
  };

  struct SourcePlan
  {
    QString sourceFileVersionUid;
    QList<DefPlan> defs;
  };

  /**
   * Classify one branch: a single update group within a dependency definition.
   * Vortex: checkFileLevelRequirements.ts:152-201 (classifyBranch)
   */
  BranchPlan
  classifyBranch(const QList<CandidateRow>& rows, const QString& modFileId,
                 const QList<int>& branchRows, const QHash<QString, bool>& enabledByUid,
                 const QHash<QString, QList<InstalledFile>>& installedByChain,
                 const QSet<QString>& uninstalledUids)
  {
    // `new Set(...)` keeps first-insertion order; reproduce that so the
    // satisfying* lists come out in the same order as Vortex's.
    QStringList candidateUidOrder;
    QSet<QString> candidateUids;
    for (int index : branchRows) {
      const QString& uid = rows.at(index).fileVersionUid;
      if (!candidateUids.contains(uid)) {
        candidateUids.insert(uid);
        candidateUidOrder.append(uid);
      }
    }

    BranchPlan plan;
    plan.modFileId = modFileId;

    // Acceptable versions the user already has, by enabled state.
    for (const QString& uid : candidateUidOrder) {
      const auto it = enabledByUid.find(uid);
      if (it == enabledByUid.end()) {
        // Not installed; check if it is downloaded but not yet installed.
        if (uninstalledUids.contains(uid)) {
          plan.satisfyingUninstalled.append(uid);
        }
        continue;
      }
      if (it.value()) {
        plan.satisfyingEnabled.append(uid);
      } else {
        plan.satisfyingDisabled.append(uid);
      }
    }

    // Other (non-acceptable) versions of the same chain the user has installed.
    const auto chainIt = installedByChain.find(modFileId);
    if (chainIt != installedByChain.end()) {
      for (const InstalledFile& file : chainIt.value()) {
        if (candidateUids.contains(file.fileVersionUid)) {
          continue;
        }
        if (file.enabled) {
          plan.wrongEnabled.append(file.fileVersionUid);
        } else {
          plan.wrongDisabled.append(file.fileVersionUid);
        }
      }
    }

    // Recommend a download only when this branch has no acceptable version
    // owned or downloaded.
    const bool owned = (plan.satisfyingEnabled.size() + plan.satisfyingDisabled.size() +
                        plan.satisfyingUninstalled.size()) > 0;
    plan.recRow      = owned ? -1 : selectRecommended(rows, branchRows);

    return plan;
  }

  // Vortex: checkFileLevelRequirements.ts:225-243 (toCandidate)
  Candidate toCandidate(const CandidateRow& row, const FileVersionDetail* detail,
                        const ModDetail* mod)
  {
    Candidate candidate;
    candidate.fileVersionUid = row.fileVersionUid;
    candidate.modUid         = row.modUid;
    candidate.modFileId      = row.modFileId;
    candidate.category       = row.category;
    candidate.position       = row.position;
    candidate.fileName       = detail ? detail->name : QString();
    candidate.version        = detail ? detail->version : QString();
    candidate.modName        = mod ? mod->name : QString();
    candidate.modSummary     = mod ? mod->summary : QString();
    candidate.thumbnailUrl   = mod ? mod->thumbnailUrl : QString();
    candidate.adultContent   = mod ? mod->adultContent : false;
    return candidate;
  }

}  // namespace

FileRequirementsReport checkFileLevelRequirements(const ResolverContext& context)
{
  FileRequirementsReport report;

  // Excluded files still satisfy others below; they just don't emit their own.
  // Vortex: checkFileLevelRequirements.ts:45-46
  QStringList sourceUids;
  for (const InstalledFile& file : context.installedFiles) {
    if (file.enabled && file.emitRequirements) {
      sourceUids.append(file.fileVersionUid);
    }
  }
  if (sourceUids.isEmpty()) {
    return report;
  }

  // Get the installed files' update group ids (modFileId).
  // Vortex: checkFileLevelRequirements.ts:48-52
  QStringList installedUids;
  for (const InstalledFile& file : context.installedFiles) {
    installedUids.append(file.fileVersionUid);
  }
  const QHash<QString, FileVersionDetail> chainOf =
      mapDetailsByUid(context.ports.fetchFileVersionDetails(installedUids));

  // Index installed files for quick lookup.
  // Vortex: checkFileLevelRequirements.ts:54-67
  QHash<QString, bool> enabledByUid;
  QHash<QString, QList<InstalledFile>> installedByChain;
  for (const InstalledFile& file : context.installedFiles) {
    enabledByUid.insert(file.fileVersionUid, file.enabled);

    const auto detailIt = chainOf.find(file.fileVersionUid);
    if (detailIt == chainOf.end()) {
      continue;
    }
    installedByChain[detailIt.value().modFileId].append(file);
  }

  // Vortex: checkFileLevelRequirements.ts:69-70
  const QList<CandidateRow> candidates = context.ports.fetchCandidates(sourceUids);
  if (candidates.isEmpty()) {
    return report;
  }

  QList<int> allIndices;
  allIndices.reserve(candidates.size());
  for (int i = 0; i < candidates.size(); ++i) {
    allIndices.append(i);
  }

  // Classify per branch (update group / OR alternative) so one branch never
  // suppresses the others; a non-OR requirement is just a single branch.
  // Vortex: checkFileLevelRequirements.ts:88-109
  QList<SourcePlan> plan;
  for (const auto& sourceGroup :
       groupIndices(candidates, allIndices, [](const CandidateRow& row) {
         return row.sourceFileVersionUid;
       })) {
    SourcePlan sourcePlan;
    sourcePlan.sourceFileVersionUid = sourceGroup.first;

    for (const auto& defGroup :
         groupIndices(candidates, sourceGroup.second, [](const CandidateRow& row) {
           return row.definitionId;
         })) {
      DefPlan defPlan;
      defPlan.definitionId = defGroup.first;

      for (const auto& branchGroup :
           groupIndices(candidates, defGroup.second, [](const CandidateRow& row) {
             return row.modFileId;
           })) {
        defPlan.branches.append(classifyBranch(
            candidates, branchGroup.first, branchGroup.second, enabledByUid,
            installedByChain, context.uninstalledFileVersionUids));
      }

      // OR satisfied: one branch has an enabled acceptable version, so don't
      // recommend (or hydrate) downloads for the alternatives.
      // Vortex: checkFileLevelRequirements.ts:102-105
      bool anySatisfyingEnabled = false;
      for (const BranchPlan& branch : defPlan.branches) {
        if (!branch.satisfyingEnabled.isEmpty()) {
          anySatisfyingEnabled = true;
          break;
        }
      }
      if (anySatisfyingEnabled) {
        for (BranchPlan& branch : defPlan.branches) {
          branch.recRow = -1;
        }
      }

      sourcePlan.defs.append(defPlan);
    }

    plan.append(sourcePlan);
  }

  // Hydrate only the recommended candidates (files the user doesn't have).
  // Vortex: checkFileLevelRequirements.ts:111-124
  QStringList recFileVersionUids;
  QStringList recModUids;
  for (const SourcePlan& source : plan) {
    for (const DefPlan& def : source.defs) {
      for (const BranchPlan& branch : def.branches) {
        if (branch.recRow >= 0) {
          recFileVersionUids.append(candidates.at(branch.recRow).fileVersionUid);
          recModUids.append(candidates.at(branch.recRow).modUid);
        }
      }
    }
  }
  recFileVersionUids = uniqueStrings(recFileVersionUids);
  recModUids         = uniqueStrings(recModUids);

  const QHash<QString, FileVersionDetail> detailByUid =
      recFileVersionUids.isEmpty()
          ? QHash<QString, FileVersionDetail>()
          : mapDetailsByUid(context.ports.fetchFileVersionDetails(recFileVersionUids));
  const QHash<QString, ModDetail> modByUid =
      recModUids.isEmpty() ? QHash<QString, ModDetail>()
                           : mapModsByUid(context.ports.fetchModDetails(recModUids));

  // Vortex: checkFileLevelRequirements.ts:126-139
  for (const SourcePlan& sourcePlan : plan) {
    SourceResult source;
    source.sourceFileVersionUid = sourcePlan.sourceFileVersionUid;

    for (const DefPlan& defPlan : sourcePlan.defs) {
      DependencyResult dependency;
      dependency.definitionId = defPlan.definitionId;

      for (const BranchPlan& branchPlan : defPlan.branches) {
        DependencyBranch branch;
        branch.modFileId             = branchPlan.modFileId;
        branch.satisfyingEnabled     = branchPlan.satisfyingEnabled;
        branch.satisfyingDisabled    = branchPlan.satisfyingDisabled;
        branch.satisfyingUninstalled = branchPlan.satisfyingUninstalled;
        branch.wrongEnabled          = branchPlan.wrongEnabled;
        branch.wrongDisabled         = branchPlan.wrongDisabled;

        if (branchPlan.recRow >= 0) {
          const CandidateRow& row = candidates.at(branchPlan.recRow);
          const auto detailIt     = detailByUid.find(row.fileVersionUid);
          const auto modIt        = modByUid.find(row.modUid);
          branch.recommended      = toCandidate(
              row, detailIt == detailByUid.end() ? nullptr : &detailIt.value(),
              modIt == modByUid.end() ? nullptr : &modIt.value());
        }

        dependency.branches.append(branch);
      }

      source.dependencies.append(dependency);
    }

    report.sources.append(source);
  }

  return report;
}

}  // namespace HealthCheck
