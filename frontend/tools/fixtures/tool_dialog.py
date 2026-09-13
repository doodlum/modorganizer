"""Temporary, read-only original-tool UI check for the isolated FNV host."""
import json
from pathlib import Path
import mobase
from PyQt6.QtCore import QObject, QEvent, QTimer, Qt
from PyQt6.QtGui import QAction
from PyQt6.QtWidgets import QApplication, QDialog, QPlainTextEdit, QTextEdit, QTabWidget


class ToolDialog(mobase.IPluginTool):
    def init(self, organizer):
        self.root = Path(__file__).parent.parent
        if self.root.name != 'mo2-fnv-host': return False
        self.report = self.root / 'frontend-tool-dialog-result.json'
        organizer.onUserInterfaceInitialized(self.ready)
        return True

    def ready(self, window):
        self.window = window
        QTimer.singleShot(2000, self.verify)

    def verify(self):
        plugin = self
        tool = self.window.findChild(QAction, 'actionTool')
        # MO2 populates extension actions only when its native menu opens.
        if tool is not None and tool.menu() is not None:
            tool.menu().aboutToShow.emit()
        actions = [a for a in self.window.findChildren(QAction) if a.text().replace('&', '') == 'INI Editor']
        if len(actions) != 1:
            self.report.write_text(json.dumps({'error': 'Expected exactly one native INI Editor action', 'count': len(actions)}))
            return
        class Capture(QObject):
            def eventFilter(self, watched, event):
                if isinstance(watched, QDialog) and event.type() == QEvent.Type.Show:
                    def inspect():
                        result = {
                            'dialogClass': watched.metaObject().className(),
                            'visible': watched.isVisible(),
                            'suppressed': watched.testAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen),
                            'mainSuppressed': plugin.window.testAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen),
                            'plainTextEditors': len(watched.findChildren(QPlainTextEdit)),
                            'richTextEditors': len(watched.findChildren(QTextEdit)),
                            'tabWidgets': len(watched.findChildren(QTabWidget)),
                        }
                        # Do not read, log, edit or save any editor contents.
                        plugin.report.write_text(json.dumps(result))
                        QTimer.singleShot(10000, watched.reject)
                    QTimer.singleShot(200, inspect)
                return False
        self.capture = Capture()
        QApplication.instance().installEventFilter(self.capture)
        actions[0].trigger()
        QTimer.singleShot(15000, lambda: QApplication.instance().removeEventFilter(self.capture))

    def name(self): return 'Frontend original tool verification'
    def localizedName(self): return self.name()
    def author(self): return 'MO2 frontend verification'
    def description(self): return 'Temporary read-only native INI Editor dialog check.'
    def version(self): return mobase.VersionInfo(1, 0, 0)
    def settings(self): return []
    def displayName(self): return self.name()
    def tooltip(self): return self.description()
    def icon(self):
        from PyQt6.QtGui import QIcon
        return QIcon()
    def setParentWidget(self, parent): pass
    def display(self): pass


def createPlugin(): return ToolDialog()
