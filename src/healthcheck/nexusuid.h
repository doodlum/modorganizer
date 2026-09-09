#ifndef HEALTHCHECK_NEXUSUID_H
#define HEALTHCHECK_NEXUSUID_H

#include <QString>
#include <optional>

namespace HealthCheck
{

/**
 * Composite Nexus UID helpers.
 *
 * A Nexus "UID" packs a numeric game id and a game-scoped mod/file id into a
 * single 64-bit value, rendered as a decimal string: (gameId << 32) | id.
 *
 * Ported verbatim from Vortex:
 *   src/renderer/src/extensions/nexus_integration/util/UIDs.ts:34-50  (makeFileUID)
 *   src/renderer/src/extensions/nexus_integration/util/UIDs.ts:52-68  (makeModUID)
 *   src/renderer/src/extensions/nexus_integration/util/UIDs.ts:115-122 (decodeUID)
 */

/** Decoded halves of a composite UID. */
struct DecodedUID
{
  quint32 gameId;
  quint32 id;
};

/**
 * Build a composite UID from a numeric game id and a game-scoped id.
 * Returns nullopt when either part is unusable, matching Vortex's
 * `undefined` returns in makeFileUID/makeModUID.
 */
std::optional<QString> makeUID(qint64 numericGameId, qint64 id);

/**
 * Vortex's own Nexus listing (nexusmods.com/site/mods/1). A requirement that
 * resolves to it means "requires Vortex", not an installable mod, so Vortex
 * treats it as always satisfied.
 *
 * Vortex: src/renderer/src/extensions/nexus_integration/util/UIDs.ts:112
 *
 * Kept so the ported resolver classifies identically; MO2 additionally
 * suppresses its own equivalent (see MO2_MOD_UID).
 */
extern const QString VORTEX_MOD_UID;

/**
 * Mod Organizer 2's Nexus listing (nexusmods.com/site/mods/6), i.e.
 * game 2295 ("site") mod 6. The MO2 analogue of VORTEX_MOD_UID: a mod
 * declaring "requires Mod Organizer 2" is always satisfied here.
 */
extern const QString MO2_MOD_UID;

/** Decode a composite UID back into its numeric game id and low id. */
std::optional<DecodedUID> decodeUID(const QString& uid);

}  // namespace HealthCheck

#endif  // HEALTHCHECK_NEXUSUID_H
