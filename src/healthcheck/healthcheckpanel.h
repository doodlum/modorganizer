#ifndef HEALTHCHECK_PANEL_H
#define HEALTHCHECK_PANEL_H

#include "healthcheckentries.h"

#include <QColor>
#include <QFrame>
#include <QList>
#include <QString>

class QLabel;
class QPushButton;
class QScrollArea;
class QStackedWidget;
class QVBoxLayout;
class QWidget;

namespace HealthCheck
{

class HealthCheckManager;

/**
 * The coloured stripe down the left of a listing row.
 *
 * Painted rather than filled via the palette: once a theme installs an
 * application stylesheet, Qt gives that precedence over a widget's palette,
 * and the stripe disappears. Painting it keeps the default visible under every
 * shipped theme, and exposing the colour as a Q_PROPERTY keeps it themeable -
 * a stylesheet can override it per severity:
 *
 *   #healthCheckSeverityAccent[severity="warning"] {
 *       qproperty-accentColor: #e8a317;
 *   }
 */
class SeverityAccent : public QWidget
{
  Q_OBJECT
  Q_PROPERTY(QColor accentColor READ accentColor WRITE setAccentColor)

public:
  explicit SeverityAccent(QWidget* parent = nullptr);

  QColor accentColor() const { return m_color; }
  void setAccentColor(const QColor& color);

protected:
  void paintEvent(QPaintEvent* event) override;

private:
  QColor m_color;
};

/**
 * The small "Premium" pill shown against gated actions.
 *
 * Painted for the same
 * reason as SeverityAccent: an application stylesheet
 * takes precedence over a widget
 * palette, so a themed MO2 would drop the
 * background and leave bare text. The colour
 * is a Q_PROPERTY, so a theme can
 * still set it:
 *
 *   #healthCheckPremiumBadge {
 * qproperty-badgeColor: #7a5cff; }
 *
 * Vortex's equivalent is
 * ui/components/premium_badge/PremiumBadge.
 */
class PremiumBadge : public QWidget
{
  Q_OBJECT
  Q_PROPERTY(QColor badgeColor READ badgeColor WRITE setBadgeColor)

public:
  explicit PremiumBadge(QWidget* parent = nullptr);

  QColor badgeColor() const { return m_color; }
  void setBadgeColor(const QColor& color);

  QSize sizeHint() const override;

protected:
  void paintEvent(QPaintEvent* event) override;

private:
  QColor m_color;
  QString m_text;
};

/**
 * The health check pop-out.
 *
 * A frameless panel anchored under the toolbar button, closing when focus
 * leaves it. It plays the role of Vortex's Health Check page
 * (views/HealthCheckPage.tsx) in the space MO2 has for it: the same listing,
 * Active/Hidden split, per-entry actions and empty states, in a popover rather
 * than a full page.
 *
 * Theming: every widget carries an objectName, and rows carry a dynamic
 * "severity" property, so an MO2 stylesheet can target them
 * (e.g. `#healthCheckPanel`, `QFrame#healthCheckRow[severity="warning"]`).
 * Nothing here hardcodes a colour that a theme cannot override - the defaults
 * are derived from the active palette so an unstyled theme still reads
 * correctly in both light and dark.
 */
class HealthCheckPanel : public QFrame
{
  Q_OBJECT

public:
  explicit HealthCheckPanel(HealthCheckManager& manager, QWidget* parent = nullptr);

  /** Show the panel anchored below `anchor`, kept inside the screen. */
  void popupAt(QWidget* anchor);

  /** Rebuild from the manager's current result. */
  void refreshContents();

signals:
  /** The user asked to download/install these files. */
  void installRequested(const QList<HealthCheck::DownloadTarget>& targets);
  /** The user asked to enable `correctModName` in place of `wrongModName`. */
  void versionSwitchRequested(const QString& wrongModName,
                              const QString& correctModName);
  /** The user asked to install an already-downloaded archive. */
  void installDownloadedRequested(const QString& downloadId);
  /** The user asked to reveal a mod in the mod list. */
  void revealModRequested(const QString& modName);
  /** The user asked to open a mod page. */
  void openModPageRequested(const QString& modUID);
  /** The user asked what the premium/free difference means. */
  void premiumInfoRequested();
  /** The user opened health check settings. */
  void settingsRequested();
  /** The user asked for a fresh run. */
  void refreshRequested();

protected:
  void changeEvent(QEvent* event) override;

private:
  void buildChrome();
  void rebuildList();
  QWidget* createEntryRow(const IssueEntry& entry);
  QWidget* createEmptyState();
  // Free accounts download through the website; the badge and banner say
  // so before a 1-click button is pressed rather than after.
  QWidget* createPremiumBanner();
  QWidget* createPremiumBadge();
  void updateHeader();
  void showDetail(const IssueEntry& entry);
  void showListing();

  HealthCheckManager& m_manager;

  QLabel* m_title           = nullptr;
  QLabel* m_subtitle        = nullptr;
  QLabel* m_lastUpdated     = nullptr;
  QPushButton* m_refresh    = nullptr;
  QPushButton* m_settings   = nullptr;
  QPushButton* m_activeTab  = nullptr;
  QPushButton* m_hiddenTab  = nullptr;
  QPushButton* m_installAll = nullptr;
  QWidget* m_premiumBanner  = nullptr;
  QPushButton* m_backButton = nullptr;

  QStackedWidget* m_stack   = nullptr;
  QScrollArea* m_scroll     = nullptr;
  QWidget* m_listContainer  = nullptr;
  QVBoxLayout* m_listLayout = nullptr;

  QScrollArea* m_detailScroll = nullptr;
  QWidget* m_detailContainer  = nullptr;
  QVBoxLayout* m_detailLayout = nullptr;

  bool m_showingHidden = false;
  QString m_openEntryId;
};

}  // namespace HealthCheck

#endif  // HEALTHCHECK_PANEL_H
