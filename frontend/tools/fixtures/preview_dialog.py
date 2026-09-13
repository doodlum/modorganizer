"""Temporary original DDS preview verification in the isolated FNV host."""
import json
from pathlib import Path
import mobase
from PyQt6.QtCore import QObject, QEvent, QTimer, Qt
from PyQt6.QtGui import QIcon, QAction
from PyQt6.QtWidgets import QApplication, QDialog, QTabWidget, QWidget, QTreeView
from PyQt6.QtOpenGLWidgets import QOpenGLWidget


class PreviewCheck(mobase.IPluginTool):
    def init(self, organizer):
        self.root = Path(__file__).parent.parent
        if self.root.name != 'mo2-fnv-host': return False
        self.report = self.root / 'frontend-preview-result.json'
        organizer.onUserInterfaceInitialized(self.ready)
        return True

    def ready(self, window):
        self.window = window
        plugin = self
        class Capture(QObject):
            def eventFilter(self, watched, event):
                if isinstance(watched, QDialog) and event.type() == QEvent.Type.Show:
                    if watched.objectName() == 'ModInfoDialog':
                        QTimer.singleShot(300, lambda: plugin.open_preview(watched))
                    elif watched.objectName() == 'PreviewDialog':
                        QTimer.singleShot(1500, lambda: plugin.inspect(watched))
                return False
        self.capture = Capture()
        QApplication.instance().installEventFilter(self.capture)

    def open_preview(self, dialog):
        self.details = dialog
        self.tabs = dialog.findChild(QTabWidget, 'tabWidget')
        self.previous = self.tabs.currentIndex()
        try:
            self.tabs.setCurrentWidget(dialog.findChild(QWidget, 'tabFiles'))
            tree = dialog.findChild(QTreeView, 'filetree')
            filename = self.root / 'mods' / 'The Mod Configuration Menu' / 'textures' / 'MCM' / 'Check1.dds'
            index = tree.model().index(str(filename))
            if not index.isValid(): raise ValueError('Native file tree did not resolve the test texture')
            tree.setCurrentIndex(index)
            preview = next(a for a in tree.findChildren(QAction) if a.text().replace('&', '') == 'Preview')
            preview.trigger()
        except Exception as error:
            self.report.write_text(json.dumps({'error': str(error)}))
        finally:
            self.tabs.setCurrentIndex(self.previous)
            dialog.reject()

    def inspect(self, dialog):
        try:
            widgets = dialog.findChildren(QOpenGLWidget)
            result = {'visible': dialog.isVisible(),
                      'suppressed': dialog.testAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen),
                      'mainSuppressed': self.window.testAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen),
                      'glWidgets': [w.metaObject().className() for w in widgets],
                      'glValid': [w.isValid() for w in widgets]}
            # This is only the known mod texture preview, never an account/editor window.
            result['captured'] = dialog.grab().save(str(self.root.parent / 'native-dds-preview.png'))
            self.report.write_text(json.dumps(result))
        finally:
            dialog.reject()

    def name(self): return 'Frontend DDS preview verification'
    def localizedName(self): return self.name()
    def author(self): return 'MO2 frontend verification'
    def description(self): return 'Temporary original DDS preview dialog check.'
    def version(self): return mobase.VersionInfo(1, 0, 0)
    def settings(self): return []
    def displayName(self): return self.name()
    def tooltip(self): return self.description()
    def icon(self): return QIcon()
    def setParentWidget(self, parent): pass
    def display(self): pass


def createPlugin(): return PreviewCheck()
