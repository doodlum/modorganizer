#include "healthcheckpremiumdialog.h"

#include <QDialogButtonBox>
#include <QFrame>
#include <QLabel>
#include <QPushButton>
#include <QVBoxLayout>

namespace HealthCheck
{

namespace
{

  QLabel* paragraph(const QString& text, const QString& objectName)
  {
    auto* label = new QLabel(text);
    label->setObjectName(objectName);
    label->setWordWrap(true);
    label->setTextInteractionFlags(Qt::TextSelectableByMouse);
    return label;
  }

}  // namespace

HealthCheckPremiumDialog::HealthCheckPremiumDialog(Scope scope, int fileCount,
                                                   QWidget* parent)
    : QDialog(parent)
{
  setObjectName(QStringLiteral("healthCheckPremiumDialog"));
  setWindowTitle(tr("How this download works"));
  setMinimumWidth(560);

  auto* root = new QVBoxLayout(this);
  root->setSpacing(10);

  // --- what happened, and why ---------------------------------------------

  auto* heading = paragraph(
      scope == Scope::Single
          ? tr("This file has to be authorised on its mod page first.")
          : tr("These %1 files have to be authorised on their mod pages first.")
                .arg(fileCount),
      QStringLiteral("healthCheckPremiumHeading"));
  QFont headingFont = heading->font();
  headingFont.setBold(true);
  headingFont.setPointSizeF(headingFont.pointSizeF() + 1.0);
  heading->setFont(headingFont);
  root->addWidget(heading);

  root->addWidget(paragraph(
      tr("Nexus Mods issues direct download links to a mod manager only for Premium "
         "accounts. On a free account, each download is authorised in your browser: "
         "you press Mod Manager Download on the file, and the page hands the link "
         "straight back to Mod Organizer, which then downloads and installs it as "
         "usual."),
      QStringLiteral("healthCheckPremiumExplanation")));

  root->addWidget(
      paragraph(scope == Scope::Single
                    ? tr("Continuing opens that file's page in your browser.")
                    : tr("Continuing opens %1 mod pages in your browser, one per file.")
                          .arg(fileCount),
                QStringLiteral("healthCheckPremiumNextStep")));

  // --- the factual difference ---------------------------------------------

  auto* rule = new QFrame;
  rule->setObjectName(QStringLiteral("healthCheckPremiumRule"));
  rule->setFrameShape(QFrame::HLine);
  rule->setFrameShadow(QFrame::Sunken);
  root->addWidget(rule);

  auto* differenceTitle = paragraph(tr("What Premium changes"),
                                    QStringLiteral("healthCheckPremiumDiffTitle"));
  QFont diffFont        = differenceTitle->font();
  diffFont.setBold(true);
  differenceTitle->setFont(diffFont);
  root->addWidget(differenceTitle);

  // Kept to what is true and relevant to this dialog: how downloading behaves.
  // Vortex lists four benefits (locales/en/health_check.json, premium.modal.benefits);
  // the two that describe downloading are the ones that belong next to a
  // download that was just gated, so the rest are left to the Nexus page.
  root->addWidget(paragraph(tr("\xE2\x80\xA2  Downloads start from inside Mod "
                               "Organizer, with no browser step.\n"
                               "\xE2\x80\xA2  Download speeds are not capped."),
                            QStringLiteral("healthCheckPremiumDifference")));

  root->addWidget(paragraph(
      tr("Both routes install the same files. The free route takes more clicks; it is "
         "not otherwise limited."),
      QStringLiteral("healthCheckPremiumParity")));

  root->addStretch(1);

  // --- actions ------------------------------------------------------------
  //
  // The website route is the default: it is the one that resolves the issue the
  // user clicked on. Vortex makes "Unlock 1-click installs" the primary button
  // (PremiumModal.tsx:146-168); here that is a plainly-labelled third option.

  auto* buttons = new QDialogButtonBox;
  buttons->setObjectName(QStringLiteral("healthCheckPremiumButtons"));

  auto* openPages = buttons->addButton(scope == Scope::Single
                                           ? tr("Open mod page")
                                           : tr("Open %1 mod pages").arg(fileCount),
                                       QDialogButtonBox::AcceptRole);
  openPages->setObjectName(QStringLiteral("healthCheckPremiumOpenPages"));
  openPages->setDefault(true);

  auto* readAbout =
      buttons->addButton(tr("Read about Premium"), QDialogButtonBox::HelpRole);
  readAbout->setObjectName(QStringLiteral("healthCheckPremiumReadAbout"));

  auto* cancel = buttons->addButton(QDialogButtonBox::Cancel);
  cancel->setObjectName(QStringLiteral("healthCheckPremiumCancel"));

  connect(openPages, &QPushButton::clicked, this, [this] {
    m_choice = Choice::OpenModPages;
    accept();
  });
  connect(readAbout, &QPushButton::clicked, this, [this] {
    m_choice = Choice::ReadAboutPremium;
    accept();
  });
  connect(cancel, &QPushButton::clicked, this, [this] {
    m_choice = Choice::Dismissed;
    reject();
  });

  root->addWidget(buttons);
}

}  // namespace HealthCheck
