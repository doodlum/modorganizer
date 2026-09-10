#include "healthcheckpanel.h"

#include "healthcheckmanager.h"

#include <QApplication>
#include <QCursor>
#include <QDateTime>
#include <QEvent>
#include <QGuiApplication>
#include <QHBoxLayout>
#include <QLabel>
#include <QPainter>
#include <QPushButton>
#include <QScreen>
#include <QScrollArea>
#include <QStackedWidget>
#include <QStyle>
#include <QToolButton>
#include <QVBoxLayout>

namespace HealthCheck
{

namespace
{

  constexpr int PANEL_WIDTH  = 520;
  constexpr int PANEL_HEIGHT = 560;

  /**
   * Severity accent, derived from the palette so an unstyled theme still reads
   * correctly. A stylesheet can override it by targeting the row's
   * `severity` dynamic property.
   *
   * Vortex maps the same three bands to its own tokens in
   * utils/shared/severityStyles.ts:5-24.
   */
  QColor severityColor(IssueSeverity severity, const QPalette& palette)
  {
    const bool dark = palette.color(QPalette::Window).lightness() < 128;
    switch (severity) {
    case IssueSeverity::Error:
      return dark ? QColor(0xE5, 0x74, 0x73) : QColor(0xC0, 0x39, 0x2B);
    case IssueSeverity::Warning:
      return dark ? QColor(0xE8, 0xB3, 0x39) : QColor(0xB7, 0x79, 0x0A);
    case IssueSeverity::Suggestion:
      return dark ? QColor(0x5D, 0xAD, 0xE2) : QColor(0x21, 0x71, 0xB5);
    }
    return palette.color(QPalette::Highlight);
  }

  QString severityName(IssueSeverity severity)
  {
    return issueSeverityToString(severity);
  }

  /** The action label for a category, matching Vortex's listing buttons.
   *  Vortex: components/file_requirement/ListingRow.tsx:118-192 */
  QString actionLabel(const IssueEntry& entry, int candidateCount)
  {
    switch (entry.category) {
    case RequirementCategory::Download:
    case RequirementCategory::DownloadReplace:
      return candidateCount == 1
                 ? QCoreApplication::translate("HealthCheck", "1-click install")
                 : QCoreApplication::translate("HealthCheck", "1-click install (%1)")
                       .arg(candidateCount);
    case RequirementCategory::Or:
      return QCoreApplication::translate("HealthCheck", "Pick mod install");
    case RequirementCategory::Toggle:
      return QCoreApplication::translate("HealthCheck", "Enable this version");
    case RequirementCategory::InstallUninstalled:
      return QCoreApplication::translate("HealthCheck", "Install (downloaded)");
    }
    return QString();
  }

  QLabel* makeLabel(const QString& text, const QString& objectName, bool wrap = true)
  {
    auto* label = new QLabel(text);
    label->setObjectName(objectName);
    label->setWordWrap(wrap);
    label->setTextInteractionFlags(Qt::TextSelectableByMouse);
    return label;
  }

  /** A relative "x minutes ago" string. Vortex: util/useRelativeTime */
  QString relativeTime(const QDateTime& when)
  {
    if (!when.isValid()) {
      return QString();
    }
    const qint64 seconds = when.secsTo(QDateTime::currentDateTimeUtc());
    if (seconds < 60) {
      return QCoreApplication::translate("HealthCheck", "just now");
    }
    if (seconds < 3600) {
      return QCoreApplication::translate("HealthCheck", "%n minute(s) ago", nullptr,
                                         static_cast<int>(seconds / 60));
    }
    if (seconds < 86400) {
      return QCoreApplication::translate("HealthCheck", "%n hour(s) ago", nullptr,
                                         static_cast<int>(seconds / 3600));
    }
    return QCoreApplication::translate("HealthCheck", "%n day(s) ago", nullptr,
                                       static_cast<int>(seconds / 86400));
  }

}  // namespace

PremiumBadge::PremiumBadge(QWidget* parent) : QWidget(parent), m_text(tr("Premium"))
{
  setObjectName(QStringLiteral("healthCheckPremiumBadge"));
  setToolTip(tr("Direct downloads from inside Mod Organizer need a Premium Nexus Mods "
                "account; free accounts authorise each download on the website."));

  // A pill, not a panel: without this the layout stretches it to fill its cell
  // and the rounded ends turn into an oval.
  setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Fixed);

  // Nexus renders premium in a violet; derived per theme so it stays legible on
  // both a light and a dark panel.
  const bool dark = palette().color(QPalette::Window).lightness() < 128;
  m_color         = dark ? QColor(0x9B, 0x82, 0xFF) : QColor(0x6B, 0x4B, 0xE8);
}

void PremiumBadge::setBadgeColor(const QColor& color)
{
  if (m_color != color) {
    m_color = color;
    update();
  }
}

QSize PremiumBadge::sizeHint() const
{
  QFont font = this->font();
  font.setBold(true);
  font.setPointSizeF(qMax(6.0, font.pointSizeF() - 1.0));

  const QSize text = QFontMetrics(font).size(Qt::TextSingleLine, m_text);
  return {text.width() + 12, text.height() + 4};
}

void PremiumBadge::paintEvent(QPaintEvent*)
{
  QPainter painter(this);
  painter.setRenderHint(QPainter::Antialiasing, true);

  const QRectF pill  = QRectF(rect()).adjusted(0.5, 0.5, -0.5, -0.5);
  const qreal radius = pill.height() / 2.0;

  painter.setPen(Qt::NoPen);
  painter.setBrush(m_color);
  painter.drawRoundedRect(pill, radius, radius);

  QFont font = this->font();
  font.setBold(true);
  font.setPointSizeF(qMax(6.0, font.pointSizeF() - 1.0));
  painter.setFont(font);

  // Pick the text colour off the fill rather than the palette, so a theme that
  // sets badgeColor does not have to also fix the contrast.
  painter.setPen(m_color.lightness() < 140 ? Qt::white : Qt::black);
  painter.drawText(rect(), Qt::AlignCenter, m_text);
}

SeverityAccent::SeverityAccent(QWidget* parent) : QWidget(parent)
{
  setObjectName(QStringLiteral("healthCheckSeverityAccent"));
  setFixedWidth(4);
}

void SeverityAccent::setAccentColor(const QColor& color)
{
  if (m_color != color) {
    m_color = color;
    update();
  }
}

void SeverityAccent::paintEvent(QPaintEvent*)
{
  if (!m_color.isValid()) {
    return;
  }
  QPainter painter(this);
  painter.fillRect(rect(), m_color);
}

HealthCheckPanel::HealthCheckPanel(HealthCheckManager& manager, QWidget* parent)
    : QFrame(parent, Qt::Popup), m_manager(manager)
{
  setObjectName(QStringLiteral("healthCheckPanel"));
  setFrameShape(QFrame::StyledPanel);
  setAttribute(Qt::WA_StyledBackground, true);
  setFixedSize(PANEL_WIDTH, PANEL_HEIGHT);

  // A popup is transparent to the palette by default in some styles; make the
  // window brush explicit so a theme's colours actually paint here.
  setAutoFillBackground(true);

  buildChrome();
  refreshContents();
}

void HealthCheckPanel::buildChrome()
{
  auto* root = new QVBoxLayout(this);
  root->setContentsMargins(0, 0, 0, 0);
  root->setSpacing(0);

  // --- header -------------------------------------------------------------

  auto* header = new QWidget;
  header->setObjectName(QStringLiteral("healthCheckHeader"));
  auto* headerLayout = new QVBoxLayout(header);
  headerLayout->setContentsMargins(14, 12, 14, 10);
  headerLayout->setSpacing(2);

  auto* titleRow = new QHBoxLayout;
  titleRow->setSpacing(6);

  m_backButton = new QPushButton(QStringLiteral("<"));
  m_backButton->setObjectName(QStringLiteral("healthCheckBack"));
  m_backButton->setFixedWidth(26);
  m_backButton->setToolTip(tr("Back to the issue list"));
  m_backButton->setVisible(false);
  connect(m_backButton, &QPushButton::clicked, this, [this] {
    showListing();
  });
  titleRow->addWidget(m_backButton);

  // Vortex: locales/en/health_check.json -> listing.title
  m_title = makeLabel(tr("Health Check"), QStringLiteral("healthCheckTitle"), false);
  QFont titleFont = m_title->font();
  titleFont.setBold(true);
  titleFont.setPointSizeF(titleFont.pointSizeF() + 1.5);
  m_title->setFont(titleFont);
  titleRow->addWidget(m_title);
  titleRow->addStretch(1);

  m_refresh = new QPushButton(tr("Refresh"));
  m_refresh->setObjectName(QStringLiteral("healthCheckRefresh"));
  m_refresh->setToolTip(tr("Run the health check again"));
  connect(m_refresh, &QPushButton::clicked, this, &HealthCheckPanel::refreshRequested);
  titleRow->addWidget(m_refresh);

  m_settings = new QPushButton(tr("Settings"));
  m_settings->setObjectName(QStringLiteral("healthCheckSettings"));
  connect(m_settings, &QPushButton::clicked, this,
          &HealthCheckPanel::settingsRequested);
  titleRow->addWidget(m_settings);

  headerLayout->addLayout(titleRow);

  // Vortex: listing.subtitle
  m_subtitle = makeLabel(tr("Review your mod list for any issues and learn how to "
                            "resolve them if needed."),
                         QStringLiteral("healthCheckSubtitle"));
  m_subtitle->setEnabled(false);
  headerLayout->addWidget(m_subtitle);

  m_lastUpdated = makeLabel(QString(), QStringLiteral("healthCheckLastUpdated"), false);
  m_lastUpdated->setEnabled(false);
  headerLayout->addWidget(m_lastUpdated);

  root->addWidget(header);

  // --- tabs ---------------------------------------------------------------

  auto* tabRow = new QWidget;
  tabRow->setObjectName(QStringLiteral("healthCheckTabs"));
  auto* tabLayout = new QHBoxLayout(tabRow);
  tabLayout->setContentsMargins(14, 0, 14, 8);
  tabLayout->setSpacing(6);

  m_activeTab = new QPushButton(tr("Active"));
  m_activeTab->setObjectName(QStringLiteral("healthCheckTabActive"));
  m_activeTab->setCheckable(true);
  m_activeTab->setChecked(true);
  connect(m_activeTab, &QPushButton::clicked, this, [this] {
    m_showingHidden = false;
    m_activeTab->setChecked(true);
    m_hiddenTab->setChecked(false);
    showListing();
  });
  tabLayout->addWidget(m_activeTab);

  m_hiddenTab = new QPushButton(tr("Hidden"));
  m_hiddenTab->setObjectName(QStringLiteral("healthCheckTabHidden"));
  m_hiddenTab->setCheckable(true);
  connect(m_hiddenTab, &QPushButton::clicked, this, [this] {
    m_showingHidden = true;
    m_activeTab->setChecked(false);
    m_hiddenTab->setChecked(true);
    showListing();
  });
  tabLayout->addWidget(m_hiddenTab);

  tabLayout->addStretch(1);

  // Vortex: HealthCheckPage's "1-click install all".
  m_installAll = new QPushButton(tr("Install all"));
  m_installAll->setObjectName(QStringLiteral("healthCheckInstallAll"));
  m_installAll->setToolTip(
      tr("Download and install every requirement that needs no choice"));
  connect(m_installAll, &QPushButton::clicked, this, [this] {
    // De-duplicated across entries by candidate file UID, as Vortex does
    // across checks (HealthCheckPage.tsx:84-98).
    QList<DownloadTarget> all;
    QSet<QString> seen;
    for (const IssueEntry& entry : m_manager.entries()) {
      if (entry.hidden || !canQuickInstall(entry.category)) {
        continue;
      }
      for (const DownloadTarget& target : downloadTargets(entry.requirements)) {
        if (!seen.contains(target.candidate.fileUID)) {
          seen.insert(target.candidate.fileUID);
          all.append(target);
        }
      }
    }
    if (!all.isEmpty()) {
      emit installRequested(all);
    }
  });
  tabLayout->addWidget(m_installAll);

  root->addWidget(tabRow);

  // --- body ---------------------------------------------------------------

  m_stack = new QStackedWidget;
  m_stack->setObjectName(QStringLiteral("healthCheckStack"));

  m_scroll = new QScrollArea;
  m_scroll->setObjectName(QStringLiteral("healthCheckScroll"));
  m_scroll->setWidgetResizable(true);
  m_scroll->setFrameShape(QFrame::NoFrame);
  m_scroll->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);

  m_listContainer = new QWidget;
  m_listContainer->setObjectName(QStringLiteral("healthCheckList"));
  m_listLayout = new QVBoxLayout(m_listContainer);
  m_listLayout->setContentsMargins(10, 0, 10, 10);
  m_listLayout->setSpacing(8);
  m_listLayout->addStretch(1);
  m_scroll->setWidget(m_listContainer);
  m_stack->addWidget(m_scroll);

  m_detailScroll = new QScrollArea;
  m_detailScroll->setObjectName(QStringLiteral("healthCheckDetailScroll"));
  m_detailScroll->setWidgetResizable(true);
  m_detailScroll->setFrameShape(QFrame::NoFrame);
  m_detailScroll->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);

  m_detailContainer = new QWidget;
  m_detailContainer->setObjectName(QStringLiteral("healthCheckDetail"));
  m_detailLayout = new QVBoxLayout(m_detailContainer);
  m_detailLayout->setContentsMargins(10, 0, 10, 10);
  m_detailLayout->setSpacing(8);
  m_detailLayout->addStretch(1);
  m_detailScroll->setWidget(m_detailContainer);
  m_stack->addWidget(m_detailScroll);

  root->addWidget(m_stack, 1);
}

void HealthCheckPanel::popupAt(QWidget* anchor)
{
  refreshContents();

  QPoint topLeft;
  if (anchor != nullptr) {
    topLeft = anchor->mapToGlobal(QPoint(0, anchor->height() + 2));
    // Right-align to the anchor when that keeps more of the panel on screen.
    topLeft.setX(anchor->mapToGlobal(QPoint(anchor->width(), 0)).x() - width());
  } else {
    topLeft = QCursor::pos();
  }

  QScreen* screen = QGuiApplication::screenAt(topLeft);
  if (screen == nullptr) {
    screen = QGuiApplication::primaryScreen();
  }
  if (screen != nullptr) {
    const QRect available = screen->availableGeometry();
    topLeft.setX(qBound(available.left(), topLeft.x(), available.right() - width()));
    topLeft.setY(qBound(available.top(), topLeft.y(), available.bottom() - height()));
  }

  move(topLeft);
  show();
  raise();
  activateWindow();
}

void HealthCheckPanel::changeEvent(QEvent* event)
{
  // Repaint severity accents when the theme changes under us.
  if (event->type() == QEvent::PaletteChange || event->type() == QEvent::StyleChange) {
    refreshContents();
  }
  QFrame::changeEvent(event);
}

void HealthCheckPanel::updateHeader()
{
  const IssueCounts counts = m_manager.counts();

  if (m_manager.isRunning()) {
    m_lastUpdated->setText(tr("Checking..."));
  } else if (m_manager.hasResult()) {
    // Vortex: listing.last_updated
    const QString when = relativeTime(m_manager.lastResult().timestamp);
    m_lastUpdated->setText(
        counts.total > 0
            ? tr("Last updated: %1  -  %n issue(s) found", nullptr, counts.total)
                  .arg(when)
            : tr("Last updated: %1").arg(when));
  } else {
    m_lastUpdated->setText(tr("Not run yet"));
  }

  m_refresh->setEnabled(!m_manager.isRunning());

  int quickInstallable = 0;
  QSet<QString> seen;
  for (const IssueEntry& entry : m_manager.entries()) {
    if (entry.hidden || !canQuickInstall(entry.category)) {
      continue;
    }
    for (const DownloadTarget& target : downloadTargets(entry.requirements)) {
      if (!seen.contains(target.candidate.fileUID)) {
        seen.insert(target.candidate.fileUID);
        quickInstallable += 1;
      }
    }
  }
  m_installAll->setVisible(quickInstallable > 0);
  m_installAll->setText(tr("Install all (%1)").arg(quickInstallable));
}

void HealthCheckPanel::refreshContents()
{
  updateHeader();
  if (m_openEntryId.isEmpty()) {
    rebuildList();
  } else {
    // Re-resolve the open detail against the live result; if the issue is gone,
    // fall back to the listing. Vortex does the same on its detail page.
    for (const IssueEntry& entry : m_manager.entries()) {
      if (entry.id == m_openEntryId) {
        showDetail(entry);
        return;
      }
    }
    showListing();
  }
}

void HealthCheckPanel::showListing()
{
  m_openEntryId.clear();
  m_backButton->setVisible(false);
  updateHeader();
  rebuildList();
  m_stack->setCurrentWidget(m_scroll);
}

void HealthCheckPanel::rebuildList()
{
  // Clear everything except the trailing stretch.
  while (m_listLayout->count() > 1) {
    QLayoutItem* item = m_listLayout->takeAt(0);
    if (QWidget* widget = item->widget()) {
      widget->deleteLater();
    }
    delete item;
  }

  // `shown` counts every inserted widget, `rows` only the issues: the banner is
  // a widget but not an issue, and an empty list still has to say so.
  int shown = 0;
  int rows  = 0;

  // Only against the active list: the hidden tab is a review surface, not a
  // place anything gets installed from.
  if (!m_showingHidden && m_manager.shouldShowPremiumUpsell()) {
    m_listLayout->insertWidget(shown++, createPremiumBanner());
  }

  for (const IssueEntry& entry : m_manager.entries()) {
    if (entry.hidden != m_showingHidden) {
      continue;
    }
    m_listLayout->insertWidget(shown, createEntryRow(entry));
    shown += 1;
    rows += 1;
  }

  if (rows == 0) {
    m_listLayout->insertWidget(shown, createEmptyState());
  }
}

/**
 * A short standing note for free accounts, so the website step is not a
 * surprise sprung by a button press.
 *
 * Vortex shows a banner in the same place
 * (components/premium_banner/PremiumBanner.tsx), worded as an ad -
 * "Download requirements in 1-click. No page visits or waiting. Go premium".
 * This states what will happen instead, and offers the explanation rather than
 * the purchase.
 */
QWidget* HealthCheckPanel::createPremiumBanner()
{
  auto* banner = new QFrame;
  banner->setObjectName(QStringLiteral("healthCheckPremiumBanner"));
  banner->setFrameShape(QFrame::StyledPanel);
  banner->setAttribute(Qt::WA_StyledBackground, true);

  auto* layout = new QHBoxLayout(banner);
  layout->setContentsMargins(10, 6, 10, 6);
  layout->setSpacing(8);

  layout->addWidget(createPremiumBadge(), 0, Qt::AlignVCenter);

  auto* text = makeLabel(
      tr("Your account authorises downloads on the Nexus Mods website, so the install "
         "buttons open the mod page instead of downloading directly."),
      QStringLiteral("healthCheckPremiumBannerText"));
  text->setEnabled(false);
  layout->addWidget(text, 1);

  auto* explain = new QPushButton(tr("Why?"));
  explain->setObjectName(QStringLiteral("healthCheckPremiumBannerLink"));
  explain->setFlat(true);
  explain->setCursor(Qt::PointingHandCursor);
  connect(explain, &QPushButton::clicked, this,
          &HealthCheckPanel::premiumInfoRequested);
  layout->addWidget(explain);

  return banner;
}

/** The "Premium" pill Vortex puts on gated buttons (ui/components/premium_badge). */
QWidget* HealthCheckPanel::createPremiumBadge()
{
  return new PremiumBadge;
}

QWidget* HealthCheckPanel::createEmptyState()
{
  auto* empty = new QWidget;
  empty->setObjectName(QStringLiteral("healthCheckEmpty"));
  auto* layout = new QVBoxLayout(empty);
  layout->setContentsMargins(20, 40, 20, 20);
  layout->setSpacing(6);

  QString title;
  QString message;

  if (m_showingHidden) {
    // Vortex: listing.no_results_hidden.title
    title = tr("No hidden items");
  } else if (!m_manager.hasCredentials()) {
    // Vortex: listing.no_results_logged_out
    title   = tr("Additional checks available");
    message = tr("Health Check found no issues in the checks it could run. Set your "
                 "Nexus Mods API key to also check for missing mod requirements.");
  } else if (!m_manager.hasResult()) {
    title   = tr("Health check has not run yet");
    message = tr("Use Refresh to check your mod list.");
  } else {
    // Vortex: listing.no_results_active
    title   = tr("Health check passed");
    message = tr("Ready for gaming");
  }

  auto* titleLabel = makeLabel(title, QStringLiteral("healthCheckEmptyTitle"), false);
  QFont font       = titleLabel->font();
  font.setBold(true);
  titleLabel->setFont(font);
  titleLabel->setAlignment(Qt::AlignHCenter);
  layout->addWidget(titleLabel);

  if (!message.isEmpty()) {
    auto* messageLabel = makeLabel(message, QStringLiteral("healthCheckEmptyMessage"));
    messageLabel->setAlignment(Qt::AlignHCenter);
    messageLabel->setEnabled(false);
    layout->addWidget(messageLabel);
  }

  layout->addStretch(1);
  return empty;
}

QWidget* HealthCheckPanel::createEntryRow(const IssueEntry& entry)
{
  auto* row = new QFrame;
  row->setObjectName(QStringLiteral("healthCheckRow"));
  row->setFrameShape(QFrame::StyledPanel);
  row->setAttribute(Qt::WA_StyledBackground, true);
  // Theme hook: `QFrame#healthCheckRow[severity="warning"] { ... }`
  row->setProperty("severity", severityName(entry.severity));
  row->setProperty("category", categoryToString(entry.category));

  auto* layout = new QHBoxLayout(row);
  layout->setContentsMargins(10, 8, 10, 8);
  layout->setSpacing(10);

  // Severity accent stripe. The default colour comes from the palette so an
  // unstyled theme still reads correctly; a stylesheet can override it through
  // the accentColor property (see SeverityAccent).
  auto* accent = new SeverityAccent;
  accent->setProperty("severity", severityName(entry.severity));
  accent->setAccentColor(severityColor(entry.severity, palette()));
  layout->addWidget(accent);

  auto* text = new QVBoxLayout;
  text->setSpacing(1);

  auto* title     = makeLabel(entryTitle(entry), QStringLiteral("healthCheckRowTitle"));
  QFont titleFont = title->font();
  titleFont.setBold(true);
  title->setFont(titleFont);
  text->addWidget(title);

  auto* summary =
      makeLabel(entrySummary(entry), QStringLiteral("healthCheckRowSummary"));
  summary->setEnabled(false);
  text->addWidget(summary);

  const QString detail = entryDetailLine(entry);
  if (!detail.isEmpty()) {
    text->addWidget(makeLabel(detail, QStringLiteral("healthCheckRowDetail")));
  }

  layout->addLayout(text, 1);

  auto* buttons = new QVBoxLayout;
  buttons->setSpacing(4);

  const QList<DownloadTarget> targets = downloadTargets(entry.requirements);

  auto* action = new QPushButton(actionLabel(entry, static_cast<int>(targets.size())));
  action->setObjectName(QStringLiteral("healthCheckRowAction"));
  // A 1-click action on a free account routes through the website; say so
  // on the button, as Vortex does (ListingRow.tsx:127).
  const bool gated = canQuickInstall(entry.category) && !targets.isEmpty() &&
                     m_manager.shouldShowPremiumUpsell();
  if (gated) {
    action->setToolTip(tr("Opens the mod page so the download can be "
                          "authorised there."));
  }
  connect(action, &QPushButton::clicked, this, [this, entry, targets] {
    switch (entry.category) {
    case RequirementCategory::Download:
    case RequirementCategory::DownloadReplace:
      if (!targets.isEmpty()) {
        emit installRequested(targets);
      }
      break;
    case RequirementCategory::Or:
      // A pick needs the detail view; there is no single right answer.
      showDetail(entry);
      break;
    case RequirementCategory::Toggle:
      for (const FileRequirement& requirement : entry.requirements) {
        if (requirement.kind == RequirementKind::WrongVersionEnabled &&
            requirement.enabledFile.has_value() &&
            requirement.correctFile.has_value()) {
          emit versionSwitchRequested(requirement.enabledFile->modId,
                                      requirement.correctFile->modId);
        }
      }
      break;
    case RequirementCategory::InstallUninstalled:
      for (const FileRequirement& requirement : entry.requirements) {
        if (requirement.kind == RequirementKind::CorrectVersionUninstalled &&
            requirement.uninstalledFile.has_value()) {
          emit installDownloadedRequested(requirement.uninstalledFile->downloadId);
        }
      }
      break;
    }
  });
  if (gated) {
    auto* actionRow = new QHBoxLayout;
    actionRow->setSpacing(4);
    actionRow->addWidget(createPremiumBadge(), 0, Qt::AlignVCenter);
    actionRow->addWidget(action, 1);
    buttons->addLayout(actionRow);
  } else {
    buttons->addWidget(action);
  }

  auto* details = new QPushButton(tr("Details"));
  details->setObjectName(QStringLiteral("healthCheckRowDetails"));
  connect(details, &QPushButton::clicked, this, [this, entry] {
    showDetail(entry);
  });
  buttons->addWidget(details);

  // Vortex: EntryActions' hide/unhide.
  auto* hide = new QPushButton(entry.hidden ? tr("Restore") : tr("Dismiss"));
  hide->setObjectName(QStringLiteral("healthCheckRowHide"));
  hide->setToolTip(entry.hidden ? tr("Show this issue again")
                                : tr("Hide this issue from the active list"));
  connect(hide, &QPushButton::clicked, this, [this, entry] {
    m_manager.setEntryHidden(entry, !entry.hidden);
  });
  buttons->addWidget(hide);

  buttons->addStretch(1);
  layout->addLayout(buttons);

  return row;
}

void HealthCheckPanel::showDetail(const IssueEntry& entry)
{
  m_openEntryId = entry.id;
  m_backButton->setVisible(true);

  while (m_detailLayout->count() > 1) {
    QLayoutItem* item = m_detailLayout->takeAt(0);
    if (QWidget* widget = item->widget()) {
      widget->deleteLater();
    }
    delete item;
  }

  int index = 0;

  auto* heading =
      makeLabel(entryTitle(entry), QStringLiteral("healthCheckDetailTitle"));
  QFont headingFont = heading->font();
  headingFont.setBold(true);
  heading->setFont(headingFont);
  m_detailLayout->insertWidget(index++, heading);

  auto* summary =
      makeLabel(entrySummary(entry), QStringLiteral("healthCheckDetailSummary"));
  summary->setEnabled(false);
  m_detailLayout->insertWidget(index++, summary);

  auto* reveal =
      new QPushButton(tr("View \"%1\" in the mod list").arg(entry.sourceModName));
  reveal->setObjectName(QStringLiteral("healthCheckDetailReveal"));
  connect(reveal, &QPushButton::clicked, this, [this, entry] {
    emit revealModRequested(entry.sourceModName);
  });
  m_detailLayout->insertWidget(index++, reveal);

  for (const FileRequirement& requirement : entry.requirements) {
    auto* card = new QFrame;
    card->setObjectName(QStringLiteral("healthCheckDetailCard"));
    card->setFrameShape(QFrame::StyledPanel);
    card->setAttribute(Qt::WA_StyledBackground, true);
    card->setProperty("kind", kindToString(requirement.kind));

    auto* cardLayout = new QVBoxLayout(card);
    cardLayout->setContentsMargins(10, 8, 10, 8);
    cardLayout->setSpacing(4);

    if (requirement.kind == RequirementKind::Or) {
      // Vortex: detail.item.pick_one
      auto* pick     = makeLabel(tr("Pick one of these"),
                                 QStringLiteral("healthCheckDetailPick"), false);
      QFont pickFont = pick->font();
      pickFont.setBold(true);
      pick->setFont(pickFont);
      cardLayout->addWidget(pick);

      for (const RequirementBranch& branch : requirement.branches) {
        auto* line = new QHBoxLayout;

        QString name;
        QString version;
        QString buttonText;
        switch (branch.kind) {
        case BranchKind::Download:
          name       = branch.candidate ? branch.candidate->modName : QString();
          version    = branch.candidate ? branch.candidate->version : QString();
          buttonText = tr("1-click install");
          break;
        case BranchKind::Install:
          name = branch.uninstalledFile ? branch.uninstalledFile->modName : QString();
          version =
              branch.uninstalledFile ? branch.uninstalledFile->version : QString();
          buttonText = tr("Install (downloaded)");
          break;
        case BranchKind::Enable:
          name       = branch.correctFile ? branch.correctFile->modName : QString();
          version    = branch.correctFile ? branch.correctFile->version : QString();
          buttonText = tr("Enable this version");
          break;
        }

        line->addWidget(makeLabel(version.isEmpty()
                                      ? name
                                      : QStringLiteral("%1  (%2)").arg(name, version),
                                  QStringLiteral("healthCheckBranchName")),
                        1);

        auto* branchButton = new QPushButton(buttonText);
        branchButton->setObjectName(QStringLiteral("healthCheckBranchAction"));
        connect(branchButton, &QPushButton::clicked, this, [this, branch] {
          switch (branch.kind) {
          case BranchKind::Download:
            if (branch.candidate.has_value()) {
              emit installRequested(
                  {DownloadTarget{*branch.candidate, branch.enabledFile}});
            }
            break;
          case BranchKind::Install:
            if (branch.uninstalledFile.has_value()) {
              emit installDownloadedRequested(branch.uninstalledFile->downloadId);
            }
            break;
          case BranchKind::Enable:
            if (branch.correctFile.has_value()) {
              emit versionSwitchRequested(branch.enabledFile.has_value()
                                              ? branch.enabledFile->modId
                                              : QString(),
                                          branch.correctFile->modId);
            }
            break;
          }
        });
        line->addWidget(branchButton);

        cardLayout->addLayout(line);
      }
    } else {
      const QString name = requirementModName(requirement, tr(" or "));
      auto* nameLabel =
          makeLabel(name, QStringLiteral("healthCheckDetailModName"), false);
      QFont nameFont = nameLabel->font();
      nameFont.setBold(true);
      nameLabel->setFont(nameFont);
      cardLayout->addWidget(nameLabel);

      if (requirement.candidate.has_value()) {
        const RequirementCandidate& candidate = *requirement.candidate;
        if (!candidate.fileName.isEmpty()) {
          cardLayout->addWidget(
              makeLabel(tr("File: %1  (%2)").arg(candidate.fileName, candidate.version),
                        QStringLiteral("healthCheckDetailFile")));
        }
        if (!candidate.modSummary.isEmpty()) {
          auto* summaryLabel = makeLabel(candidate.modSummary,
                                         QStringLiteral("healthCheckDetailSummary"));
          summaryLabel->setEnabled(false);
          cardLayout->addWidget(summaryLabel);
        }
        if (candidate.adultContent) {
          // Vortex: detail.item.adult
          cardLayout->addWidget(makeLabel(
              tr("Adult content"), QStringLiteral("healthCheckAdultBadge"), false));
        }
      }

      // Vortex: detail.item.current_version / required_version
      if (requirement.installedFile.has_value()) {
        cardLayout->addWidget(
            makeLabel(tr("Current version: %1").arg(requirement.installedFile->version),
                      QStringLiteral("healthCheckDetailCurrent")));
      }
      if (requirement.enabledFile.has_value()) {
        cardLayout->addWidget(makeLabel(tr("Currently enabled: %1  (%2)")
                                            .arg(requirement.enabledFile->modName,
                                                 requirement.enabledFile->version),
                                        QStringLiteral("healthCheckDetailCurrent")));
      }
      if (requirement.correctFile.has_value()) {
        cardLayout->addWidget(
            makeLabel(tr("Required version: %1").arg(requirement.correctFile->version),
                      QStringLiteral("healthCheckDetailRequired")));
      }
      if (requirement.uninstalledFile.has_value()) {
        cardLayout->addWidget(makeLabel(tr("Downloaded: %1  (%2)")
                                            .arg(requirement.uninstalledFile->fileName,
                                                 requirement.uninstalledFile->version),
                                        QStringLiteral("healthCheckDetailDownloaded")));
      }
    }

    m_detailLayout->insertWidget(index++, card);
  }

  m_stack->setCurrentWidget(m_detailScroll);
}

}  // namespace HealthCheck
