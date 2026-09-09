#ifndef HEALTHCHECK_FILEDEPENDENCYRESOLVER_H
#define HEALTHCHECK_FILEDEPENDENCYRESOLVER_H

#include "healthchecktypes.h"

#include <QList>
#include <QString>
#include <QStringList>
#include <functional>

namespace HealthCheck
{

/**
 * Data sources the resolver needs, injected so the algorithm stays free of
 * HTTP, auth and Nexus specifics - exactly as the Vortex package does it.
 *
 * Vortex: packages/file-dependency-resolver/src/types.ts:33-39 (ResolverPorts)
 *
 * Implementations must throw ResolverError on failure; the resolver does not
 * catch, matching the TypeScript version where a rejected promise propagates.
 */
struct ResolverPorts
{
  std::function<QList<CandidateRow>(const QStringList&)> fetchCandidates;
  std::function<QList<FileVersionDetail>(const QStringList&)> fetchFileVersionDetails;
  std::function<QList<ModDetail>(const QStringList&)> fetchModDetails;
};

/**
 * Vortex: packages/file-dependency-resolver/src/types.ts:48-56
 * (FileRequirementsContext)
 */
struct ResolverContext
{
  // Enabled AND disabled installed files for the active game.
  QList<InstalledFile> installedFiles;
  // File version UIDs downloaded but not yet installed.
  QSet<QString> uninstalledFileVersionUids;
  ResolverPorts ports;
};

/**
 * Resolve which of the installed files' declared dependencies are unsatisfied.
 *
 * A line-for-line port of
 *   packages/file-dependency-resolver/src/checkFileLevelRequirements.ts:40-140
 *
 * Iteration order is significant: the TypeScript version groups with `Map`,
 * whose iteration order is insertion order, and the report is compared against
 * it field-by-field in the parity suite. The ordered grouping helpers below
 * reproduce that, so do not swap them for QHash.
 */
FileRequirementsReport checkFileLevelRequirements(const ResolverContext& context);

}  // namespace HealthCheck

#endif  // HEALTHCHECK_FILEDEPENDENCYRESOLVER_H
