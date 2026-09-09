#ifndef HEALTHCHECK_REQUIREMENTSREPORTMAPPER_H
#define HEALTHCHECK_REQUIREMENTSREPORTMAPPER_H

#include "healthchecktypes.h"

#include <QString>
#include <functional>
#include <optional>
#include <variant>

namespace HealthCheck
{

/**
 * A file's display data resolved from its composite file UID.
 * Vortex: mapRequirementsReport.ts:171-176 (HydratedFile / HydrateFile)
 */
struct HydratedFile
{
  enum class Kind
  {
    Installed,
    Downloaded
  };

  Kind kind = Kind::Installed;
  InstalledFileInfo installed;
  DownloadedFileInfo downloaded;
};

using HydrateFile = std::function<std::optional<HydratedFile>(const QString&)>;

/** Context carried into the mapping pass.
 *  Vortex: mapRequirementsReport.ts:358-362 (the `context` parameter) */
struct MapperContext
{
  QString gameId;
  int modsChecked = 0;
  QStringList errors;

  /**
   * FEATURE FLAG (MO2 addition, off in the parity suite):
   * also treat a dependency on Mod Organizer 2's own Nexus listing as always
   * satisfied, the way Vortex does for its own listing.
   *
   * Vortex only suppresses VORTEX_MOD_UID
   * (mapRequirementsReport.ts:211-214); suppressing MO2's is the equivalent
   * behaviour for this app but is *not* Vortex parity, so it is gated. The
   * parity harness sets this false so both implementations classify the same
   * fixture identically.
   *
   * Settings key: HealthCheck/suppressSelfRequirement
   */
  bool suppressMO2SelfRequirement = false;
};

/**
 * Map the resolver report onto the surfaced requirement kinds, hydrating files
 * only for surfaced dependencies.
 *
 * Port of
 *   src/renderer/src/extensions/health_check/utils/fileRequirements/
 *   mapRequirementsReport.ts:200-389
 */
FileRequirementsMetadata mapRequirementsReport(const FileRequirementsReport& report,
                                               const HydrateFile& hydrate,
                                               const MapperContext& context);

}  // namespace HealthCheck

#endif  // HEALTHCHECK_REQUIREMENTSREPORTMAPPER_H
