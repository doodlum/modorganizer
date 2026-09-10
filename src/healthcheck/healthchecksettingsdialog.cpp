#include "healthchecksettingsdialog.h"

#include <QCheckBox>
#include <QDialogButtonBox>
#include <QGroupBox>
#include <QLabel>
#include <QVBoxLayout>

namespace HealthCheck
{

namespace
{

  /** A checkbox with an indented explanatory line under it. */
  QCheckBox* addOption(QVBoxLayout* layout, const QString& objectName,
                       const QString& text, const QString& description, bool checked)
  {
    auto* box = new QCheckBox(text);
    box->setObjectName(objectName);
    box->setChecked(checked);
    layout->addWidget(box);

    auto* hint = new QLabel(description);
    hint->setObjectName(objectName + QStringLiteral("Hint"));
    hint->setWordWrap(true);
    hint->setEnabled(false);
    hint->setContentsMargins(20, 0, 0, 6);
    layout->addWidget(hint);

    return box;
  }

}  // namespace

HealthCheckSettingsDialog::HealthCheckSettingsDialog(const FeatureFlags& flags,
                                                     QWidget* parent)
    : QDialog(parent), m_initial(flags)
{
  setObjectName(QStringLiteral("healthCheckSettingsDialog"));
  setWindowTitle(tr("Health Check Settings"));
  setMinimumWidth(520);

  auto* root = new QVBoxLayout(this);

  // Vortex: locales/en/health_check.json -> settings.description
  auto* intro = new QLabel(
      tr("Detect issues with your mod list and suggest fixes."));
  intro->setObjectName(QStringLiteral("healthCheckSettingsIntro"));
  intro->setWordWrap(true);
  root->addWidget(intro);

  // --- checks -------------------------------------------------------------

  auto* checks = new QGroupBox(tr("Checks"));
  checks->setObjectName(QStringLiteral("healthCheckSettingsChecks"));
  auto* checksLayout = new QVBoxLayout(checks);

  m_enabled = addOption(
      checksLayout, QStringLiteral("healthCheckEnabled"), tr("Enable health check"),
      tr("Turn the whole feature off, including the toolbar indicator and the "
         "mod list flags."),
      flags.enabled);

  // Vortex: settings.file_requirements
  m_fileRequirements = addOption(
      checksLayout, QStringLiteral("healthCheckFileRequirements"),
      tr("Missing file requirements warnings"),
      tr("Check your enabled mods against the file-level requirements their "
         "authors declared on Nexus Mods."),
      flags.fileRequirementsEnabled);

  root->addWidget(checks);

  // --- MO2-specific -------------------------------------------------------

  auto* extras = new QGroupBox(tr("Mod Organizer behaviour"));
  extras->setObjectName(QStringLiteral("healthCheckSettingsExtras"));
  auto* extrasLayout = new QVBoxLayout(extras);

  auto* extrasHint = new QLabel(
      tr("These options are specific to Mod Organizer and have no equivalent in "
         "Vortex. Turning them all off leaves the check behaving exactly as "
         "Vortex does."));
  extrasHint->setObjectName(QStringLiteral("healthCheckSettingsExtrasHint"));
  extrasHint->setWordWrap(true);
  extrasHint->setEnabled(false);
  extrasLayout->addWidget(extrasHint);

  m_autoRun = addOption(
      extrasLayout, QStringLiteral("healthCheckAutoRun"),
      tr("Re-check automatically when mods change"),
      tr("Run again shortly after mods are installed, enabled, disabled or "
         "removed, and when downloads change."),
      flags.autoRun);

  m_notifications = addOption(
      extrasLayout, QStringLiteral("healthCheckNotifications"),
      tr("Include issues in notifications"),
      tr("Count health check issues towards the notification button and list "
         "them in the notifications dialog."),
      flags.notifications);

  m_modListIndicator = addOption(
      extrasLayout, QStringLiteral("healthCheckModListIndicator"),
      tr("Flag affected mods in the mod list"),
      tr("Show an icon in the flags column of any mod with an unresolved issue."),
      flags.modListIndicator);

  m_suppressSelfRequirement = addOption(
      extrasLayout, QStringLiteral("healthCheckSuppressSelf"),
      tr("Ignore requirements on Mod Organizer itself"),
      tr("Treat a mod that requires Mod Organizer 2 as already satisfied. Vortex "
         "does this for its own listing but not for Mod Organizer's, so leaving "
         "this off keeps the two in step."),
      flags.suppressSelfRequirement);

  root->addWidget(extras);
  root->addStretch(1);

  // Dependent options read as disabled when the master switch is off.
  const auto syncEnabled = [this] {
    const bool on = m_enabled->isChecked();
    for (QCheckBox* box : {m_fileRequirements, m_autoRun, m_notifications,
                           m_modListIndicator, m_suppressSelfRequirement}) {
      box->setEnabled(on);
    }
  };
  connect(m_enabled, &QCheckBox::toggled, this, syncEnabled);
  syncEnabled();

  auto* buttons =
      new QDialogButtonBox(QDialogButtonBox::Ok | QDialogButtonBox::Cancel);
  buttons->setObjectName(QStringLiteral("healthCheckSettingsButtons"));
  connect(buttons, &QDialogButtonBox::accepted, this, &QDialog::accept);
  connect(buttons, &QDialogButtonBox::rejected, this, &QDialog::reject);
  root->addWidget(buttons);
}

FeatureFlags HealthCheckSettingsDialog::flags() const
{
  FeatureFlags out            = m_initial;
  out.enabled                 = m_enabled->isChecked();
  out.fileRequirementsEnabled = m_fileRequirements->isChecked();
  out.autoRun                 = m_autoRun->isChecked();
  out.notifications           = m_notifications->isChecked();
  out.modListIndicator        = m_modListIndicator->isChecked();
  out.suppressSelfRequirement = m_suppressSelfRequirement->isChecked();
  return out;
}

}  // namespace HealthCheck
