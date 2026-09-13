"""Temporary original FNIS requirement/master checks through native settings."""
import json
from pathlib import Path
import mobase
from PyQt6.QtCore import QObject, QEvent, QTimer, Qt
from PyQt6.QtGui import QAction, QIcon
from PyQt6.QtWidgets import QApplication, QCheckBox, QDialog, QTabWidget, QTreeWidget, QWidget

MASTER = 'FNIS Integration Tool'
CHILDREN = ['FNIS Integration Tool Reset', 'FNIS Patches Tool']


class ExtensionRules(mobase.IPluginTool):
    def init(self, organizer):
        self.root = Path(__file__).parent.parent
        self.request = self.root / 'frontend-extension-rules-request.json'
        if not self.request.exists(): return False
        self.expected_game = json.loads(self.request.read_text())['game']
        if self.expected_game not in ('Fallout: New Vegas', 'Skyrim Special Edition'): return False
        self.organizer = organizer
        self.report = self.root / 'frontend-extension-rules-result.json'
        self.ran = False
        organizer.onUserInterfaceInitialized(self.ready)
        return True

    def ready(self, window):
        self.window = window
        plugin = self
        class Capture(QObject):
            def eventFilter(self, watched, event):
                if isinstance(watched, QDialog) and watched.objectName() == 'SettingsDialog' and event.type() == QEvent.Type.Show and not plugin.ran:
                    plugin.ran = True
                    QTimer.singleShot(100, lambda: plugin.inspect(watched))
                return False
        self.capture = Capture()
        QApplication.instance().installEventFilter(self.capture)
        QTimer.singleShot(2000, self.open)

    def open(self):
        action = self.window.findChild(QAction, 'actionSettings')
        if action is None:
            self.report.write_text(json.dumps({'error': 'Native settings action missing'}))
            return
        action.trigger()

    def inspect(self, dialog):
        result = {}
        original = None
        checkbox = None
        master_item = None
        tree = None
        try:
            game = self.organizer.managedGame().gameName()
            result['nativeGameName'] = game
            if game not in ({'New Vegas', 'Fallout: New Vegas', 'Fallout New Vegas'} if self.expected_game == 'Fallout: New Vegas' else {self.expected_game}):
                raise ValueError('Unexpected managed game')
            tabs = dialog.findChild(QTabWidget, 'tabWidget')
            tabs.setCurrentWidget(dialog.findChild(QWidget, 'pluginsTab'))
            tree = dialog.findChild(QTreeWidget, 'pluginsList')
            checkbox = dialog.findChild(QCheckBox, 'enabledCheckbox')
            def item(name):
                found = tree.findItems(name, Qt.MatchFlag.MatchExactly | Qt.MatchFlag.MatchRecursive, 0)
                if len(found) != 1: raise ValueError('Original extension row missing: ' + name)
                return found[0]
            def states():
                return {n: self.organizer.isPluginEnabled(n) for n in [MASTER] + CHILDREN}
            master_item = item(MASTER)
            tree.setCurrentItem(master_item)
            original = states()
            result.update(game=game, mainSuppressed=self.window.testAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen), initial=original,
                          masterControlEnabled=checkbox.isEnabled(), childRowsGrouped=all(item(n).parent() is master_item for n in CHILDREN))
            if not result['childRowsGrouped']: raise ValueError('Original children are not grouped under their master')
            if self.expected_game == 'Fallout: New Vegas':
                if checkbox.isEnabled() or any(original.values()): raise ValueError('FNIS requirement did not block FNV')
                result['requirementBlocked'] = True
            else:
                if not checkbox.isEnabled(): raise ValueError('FNIS requirement did not permit Skyrim')
                result['requirementAllowed'] = True
                # Invoke MO2's own checkbox handler, never the FNIS executable.
                if not checkbox.isChecked(): checkbox.click()
                result['enabled'] = states()
                if not all(result['enabled'].values()): raise ValueError('Enabling master did not enable children')
                checkbox.click()
                result['disabled'] = states()
                if any(result['disabled'].values()): raise ValueError('Disabling master did not disable children')
                if original[MASTER]: checkbox.click()
            for name in CHILDREN:
                tree.setCurrentItem(item(name))
                if not checkbox.isHidden(): raise ValueError('Child can be enabled independently')
            result['childControlsHidden'] = True
        except Exception as error:
            result['error'] = str(error)
        finally:
            if original is not None and checkbox is not None and master_item is not None:
                tree.setCurrentItem(master_item)
                if self.organizer.isPluginEnabled(MASTER) != original[MASTER] and checkbox.isEnabled(): checkbox.click()
                result['restored'] = {n: self.organizer.isPluginEnabled(n) for n in [MASTER] + CHILDREN} == original
            self.report.write_text(json.dumps(result))
            dialog.reject()
            QApplication.instance().removeEventFilter(self.capture)

    def name(self): return 'Frontend original extension rule verification'
    def localizedName(self): return self.name()
    def author(self): return 'MO2 frontend verification'
    def description(self): return 'Temporary native FNIS requirement/master check.'
    def version(self): return mobase.VersionInfo(1, 0, 0)
    def settings(self): return []
    def displayName(self): return self.name()
    def tooltip(self): return self.description()
    def icon(self): return QIcon()
    def setParentWidget(self, parent): pass
    def display(self): pass


def createPlugin(): return ExtensionRules()
