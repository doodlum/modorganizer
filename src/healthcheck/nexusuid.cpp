#include "nexusuid.h"

namespace HealthCheck
{

// nexusmods.com/site/mods/1 -> (2295 << 32) | 1
const QString VORTEX_MOD_UID = QStringLiteral("9856949944321");

// nexusmods.com/site/mods/6 -> (2295 << 32) | 6
const QString MO2_MOD_UID = QStringLiteral("9856949944326");

std::optional<QString> makeUID(qint64 numericGameId, qint64 id)
{
  // Vortex bails out when the game id can't be resolved or the id doesn't
  // parse; the same inputs must produce the same "no UID" outcome here.
  // UIDs.ts:40-47
  if (numericGameId <= 0 || id < 0) {
    return std::nullopt;
  }

  const quint64 value =
      (static_cast<quint64>(numericGameId) << 32) | static_cast<quint64>(id);

  return QString::number(value);
}

std::optional<DecodedUID> decodeUID(const QString& uid)
{
  bool ok = false;
  const quint64 value = uid.toULongLong(&ok);
  if (!ok) {
    return std::nullopt;
  }

  return DecodedUID{static_cast<quint32>(value >> 32),
                    static_cast<quint32>(value & 0xFFFFFFFFULL)};
}

}  // namespace HealthCheck
