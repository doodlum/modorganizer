#ifndef HEALTHCHECK_FLAGSTORE_H
#define HEALTHCHECK_FLAGSTORE_H

#include <QSet>
#include <QString>

namespace HealthCheck
{

/**
 * The set of mods the health check currently has an unresolved issue against.
 *
 * A tiny process-wide store rather than a lookup through OrganizerCore: it is
 * read from ModInfoRegular::getFlags(), which is called for every visible row
 * on every repaint and has no route back to the manager. Keeping it here means
 * the flags column costs one hash lookup and the mod layer gains no dependency
 * on the health check module beyond this header.
 *
 * GUI-thread only: the manager publishes from its result handler, the mod list
 * reads while painting, and both run on the GUI thread.
 *
 * This is an MO2 addition with no Vortex counterpart - Vortex surfaces issues
 * only on its own page - so it is gated behind the
 * HealthCheck/modListIndicator flag, which the manager honours before
 * publishing here.
 */
class FlagStore
{
public:
  static FlagStore& instance();

  /** Replace the flagged set. Empty clears every indicator. */
  void setFlaggedMods(const QSet<QString>& modNames);

  /** Whether this mod (by MO2 mod name) has an unresolved issue. */
  bool isFlagged(const QString& modName) const;

  /** Number of flagged mods; used for the summary tooltip. */
  int count() const;

  void clear();

private:
  FlagStore() = default;

  QSet<QString> m_flagged;
};

}  // namespace HealthCheck

#endif  // HEALTHCHECK_FLAGSTORE_H
