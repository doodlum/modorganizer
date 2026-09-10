#include "healthcheckflagstore.h"

namespace HealthCheck
{

FlagStore& FlagStore::instance()
{
  static FlagStore store;
  return store;
}

void FlagStore::setFlaggedMods(const QSet<QString>& modNames)
{
  m_flagged = modNames;
}

bool FlagStore::isFlagged(const QString& modName) const
{
  return !m_flagged.isEmpty() && m_flagged.contains(modName);
}

int FlagStore::count() const
{
  return static_cast<int>(m_flagged.size());
}

void FlagStore::clear()
{
  m_flagged.clear();
}

}  // namespace HealthCheck
