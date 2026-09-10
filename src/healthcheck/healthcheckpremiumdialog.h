#ifndef HEALTHCHECK_PREMIUMDIALOG_H
#define HEALTHCHECK_PREMIUMDIALOG_H

#include <QDialog>
#include <QString>

namespace HealthCheck
{

/**
 * Shown when a free account uses a 1-click action.
 *
 * Vortex's equivalent (components/premium_modal/PremiumModal.tsx) leads with
 * the pitch - "Skip the website and install instantly." - and makes "Unlock
 * 1-click installs" the primary button, with the route the user can actually
 * take right now as the secondary.
 *
 * This one keeps the same two choices and the same gate, but inverts the
 * emphasis. It opens by explaining why the button did not just download: Nexus
 * issues direct download links to the app for Premium accounts only, and
 * everyone else authorises each download on the mod page, which hands the link
 * straight back to Mod Organizer. It then says exactly what continuing does.
 *
 * The default button is that free route, because it is the one that resolves
 * the issue the user clicked on. Premium is presented as a factual difference
 * in how downloading works, not as a call to action.
 */
class HealthCheckPremiumDialog : public QDialog
{
  Q_OBJECT

public:
  /** Whether the gated action was one file or several. */
  enum class Scope
  {
    Single,
    Several
  };

  /** What the user chose. */
  enum class Choice
  {
    /** Cancel; nothing happens. */
    Dismissed,
    /** Open the mod page(s) so the download can be authorised there. */
    OpenModPages,
    /** Read about Premium; the gated action is not run. */
    ReadAboutPremium
  };

  HealthCheckPremiumDialog(Scope scope, int fileCount, QWidget* parent = nullptr);

  Choice choice() const { return m_choice; }

private:
  Choice m_choice = Choice::Dismissed;
};

}  // namespace HealthCheck

#endif  // HEALTHCHECK_PREMIUMDIALOG_H
