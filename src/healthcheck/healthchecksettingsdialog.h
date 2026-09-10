#ifndef HEALTHCHECK_SETTINGSDIALOG_H
#define HEALTHCHECK_SETTINGSDIALOG_H

#include "healthcheckmanager.h"

#include <QDialog>

class QCheckBox;

namespace HealthCheck
{

/**
 * Health check settings.
 *
 * Vortex puts the equivalent toggles on its own settings tab
 * (health_check/index.ts:59, views/SettingsHealthCheck.tsx). MO2's settings
 * dialog is a different shape, and the health check is reached from a pop-out
 * rather than a page, so its settings live in their own small dialog opened
 * from that pop-out.
 *
 * The first group mirrors Vortex. The second holds MO2-only behaviour, each
 * item gated by its own flag so the default configuration stays
 * Vortex-identical - see FeatureFlags.
 */
class HealthCheckSettingsDialog : public QDialog
{
  Q_OBJECT

public:
  explicit HealthCheckSettingsDialog(const FeatureFlags& flags,
                                     QWidget* parent = nullptr);

  /** The flags as edited. Only meaningful after exec() returns Accepted. */
  FeatureFlags flags() const;

private:
  FeatureFlags m_initial;

  QCheckBox* m_enabled                 = nullptr;
  QCheckBox* m_fileRequirements        = nullptr;
  QCheckBox* m_autoRun                 = nullptr;
  QCheckBox* m_notifications           = nullptr;
  QCheckBox* m_modListIndicator        = nullptr;
  QCheckBox* m_showPremiumInfo         = nullptr;
  QCheckBox* m_suppressSelfRequirement = nullptr;
};

}  // namespace HealthCheck

#endif  // HEALTHCHECK_SETTINGSDIALOG_H
