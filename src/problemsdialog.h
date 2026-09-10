#ifndef PROBLEMSDIALOG_H
#define PROBLEMSDIALOG_H

#include <QDialog>
#include <QUrl>
#include <iplugindiagnose.h>

namespace Ui
{
class ProblemsDialog;
}

class PluginContainer;

namespace HealthCheck
{
class HealthCheckManager;
}

class ProblemsDialog : public QDialog
{
  Q_OBJECT

public:
  // `healthCheck` is optional; when given, its unresolved issues are listed
  // alongside the plugin diagnoses so MO2's notification button covers both.
  explicit ProblemsDialog(PluginContainer const& pluginContainer,
                          HealthCheck::HealthCheckManager* healthCheck = nullptr,
                          QWidget* parent = 0);
  ~ProblemsDialog();

  // also saves and restores geometry
  //
  int exec() override;

  bool hasProblems() const;

private:
  void runDiagnosis();

private slots:
  void selectionChanged();
  void urlClicked(const QUrl& url);

  void startFix();

private:
  Ui::ProblemsDialog* ui;
  const PluginContainer& m_PluginContainer;
  HealthCheck::HealthCheckManager* m_HealthCheck;
  bool m_hasProblems;
};

#endif  // PROBLEMSDIALOG_H
