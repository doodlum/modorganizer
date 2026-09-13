"""Temporary original INI Bakery mapping check in the isolated FNV host."""
import hashlib
import json
from pathlib import Path
import mobase
from PyQt6.QtCore import QDir, QTimer
from PyQt6.QtGui import QIcon


class FileMapperCheck(mobase.IPluginTool):
    def init(self, organizer):
        self.root = Path(__file__).parent.parent
        if self.root.name != 'mo2-fnv-host': return False
        self.organizer = organizer
        self.callbacks = {'aboutToRun': False, 'finishedRun': False}
        self.probe_running = False
        def about(binary, *args):
            if Path(binary).name.casefold() == 'pythonw.exe': self.callbacks['aboutToRun'] = True
            return True
        def finished(binary, *args):
            if self.probe_running:
                self.callbacks['finishedRun'] = True
                self.callbacks['finishedBinaryEmpty'] = not bool(binary)
                self.callbacks['finishedBinaryMatchesProbe'] = Path(binary).name.casefold() == 'pythonw.exe'
        organizer.onAboutToRun(about)
        organizer.onFinishedRun(finished)
        organizer.onUserInterfaceInitialized(lambda window: QTimer.singleShot(2000, self.verify))
        return True

    def verify(self):
        artifacts = self.root.parent
        report = artifacts / 'native-file-mapper-result.json'
        digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
        try:
            if self.organizer.profileName() != 'Frontend Test' or not self.organizer.profile().localSettingsEnabled():
                raise ValueError('Mapping check requires isolated Frontend Test with local game settings')
            game = self.organizer.managedGame()
            files = []
            for name in game.iniFiles():
                target = Path(game.documentsDirectory().absoluteFilePath(name))
                source = Path(self.organizer.profilePath()) / Path(name).name
                files.append({'name': Path(name).name, 'target': str(target), 'source': str(source), 'outside': digest(target)})
            request = artifacts / 'mapped-ini-request.json'
            output = artifacts / 'mapped-ini-probe-result.json'
            if output.exists(): raise ValueError('Remove the stale mapped INI probe report first')
            request.write_text(json.dumps({'files': [{'name': f['name'], 'target': f['target']} for f in files], 'output': str(output)}))
            binary = artifacts / 'python-win' / 'pythonw.exe'
            script = artifacts / 'mapped_ini_probe.py'
            self.probe_running = True
            handle = self.organizer.startApplication(str(binary), [str(script), str(request)], QDir(str(artifacts)))
            if not handle: raise ValueError('MO2 did not start the read-only mapping probe')
            completed, exit_code = self.organizer.waitForApplication(handle)
            self.probe_running = False
            if not completed or exit_code != 0: raise ValueError('MO2 mapping probe failed or did not finish')
            probe = json.loads(output.read_text())
            for file in files:
                file['profile'] = digest(Path(file.pop('source')))
                file['hooked'] = next(f['sha256'] for f in probe['files'] if f['name'] == file['name'])
            passed = bool(files) and probe['usvfsLoaded'] and all(f['profile'] is not None and f['profile'] == f['hooked'] for f in files) and any(f['outside'] != f['hooked'] for f in files) and self.callbacks['aboutToRun'] and self.callbacks['finishedRun']
            report.write_text(json.dumps({'passed': passed, 'completed': completed, 'exitCode': exit_code,
                                         'usvfsLoaded': probe['usvfsLoaded'], 'callbacks': self.callbacks, 'files': files}))
        except Exception as error:
            report.write_text(json.dumps({'error': str(error)}))
        finally:
            self.probe_running = False

    def name(self): return 'Frontend original file mapper verification'
    def localizedName(self): return self.name()
    def author(self): return 'MO2 frontend verification'
    def description(self): return 'Temporary read-only original INI Bakery mapping check.'
    def version(self): return mobase.VersionInfo(1, 0, 0)
    def settings(self): return []
    def displayName(self): return self.name()
    def tooltip(self): return self.description()
    def icon(self): return QIcon()
    def setParentWidget(self, parent): pass
    def display(self): pass


def createPlugin(): return FileMapperCheck()
