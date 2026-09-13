"""MO2's download directory plus its existing downloader/installer entry points."""
import configparser
import re
from pathlib import Path


class Downloads:
    def __init__(self, organizer):
        self.organizer = organizer
        self.window = None

    def game_domain(self):
        game = self.organizer.managedGame()
        return game.gameNexusName() or game.gameShortName()

    def snapshot(self):
        directory = Path(self.organizer.downloadsPath())
        result = []
        if not directory.is_dir():
            return result
        for path in sorted(directory.iterdir(), key=lambda item: item.name.casefold()):
            if not path.is_file() or path.suffix.casefold() == '.meta':
                continue
            meta = configparser.ConfigParser(interpolation=None, strict=False)
            try:
                meta.read(str(path) + '.meta', encoding='utf-8-sig')
                values = meta['General'] if meta.has_section('General') else {}
            except (configparser.Error, UnicodeError):
                values = {}
            partial = path.name.casefold().endswith(('.unfinished', '.part'))
            result.append({'name': path.name, 'path': str(path), 'bytes': path.stat().st_size,
                           'partial': partial, 'installed': values.get('installed', 'false') == 'true',
                           'hidden': values.get('removed', 'false') == 'true',
                           'paused': values.get('paused', 'false') == 'true'})
        return result

    def control(self, filename, action):
        from PyQt6.QtCore import QAbstractProxyModel, QMetaObject, Q_ARG, Qt
        from PyQt6.QtWidgets import QTreeView
        slots = {'pause': 'issuePause', 'resume': 'issueResume', 'cancel': 'issueCancel'}
        if action not in slots or not isinstance(filename, str):
            raise ValueError('Choose a supported download action and archive')
        if self.window is None:
            raise ValueError('MO2 download controls are not ready')
        view = self.window.findChild(QTreeView, 'downloadView')
        if view is None or not view.isEnabled():
            raise ValueError('MO2 download controls are unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel):
            model = model.sourceModel()
        if model is None or model.metaObject().className() != 'DownloadList':
            raise ValueError('This MO2 version has an unsupported download model')
        requested = str(Path(filename)).casefold()
        manager = self.organizer.downloadManager()
        for row in range(model.rowCount()):
            try:
                host_path = str(Path(manager.downloadPath(row)))
            except RuntimeError:
                # Pending Nexus lookups have rows but no archive/download index yet.
                continue
            if requested not in (host_path.casefold(), (host_path + '.unfinished').casefold()):
                continue
            # Same slots used by the original context menu. The manager validates
            # the current transfer state. Resolve the row immediately before use;
            # never store row numbers in frontend state or requests.
            QMetaObject.invokeMethod(view, slots[action], Qt.ConnectionType.DirectConnection, Q_ARG(int, row))
            return {'requested': action}
        raise ValueError('MO2 no longer manages this download; refresh the list')

    def start_nexus(self, mod_id, file_id, game):
        if not isinstance(game, str) or game.casefold() != self.game_domain().casefold():
            raise ValueError("The Nexus file belongs to a different game")
        if any(type(value) is not int or value <= 0 for value in (mod_id, file_id)):
            raise ValueError('Positive Nexus mod and file IDs are required')
        identifier = self.organizer.downloadManager().startDownloadNexusFile(mod_id, file_id)
        if identifier < 0:
            raise ValueError('MO2 did not accept the Nexus download')
        return {'downloadId': identifier, 'queued': True}

    def validate_nxm(self, url):
        # Preserve the original query verbatim: free-account keys are signed.
        if not isinstance(url, str) or len(url) > 8192 or any(c.isspace() or ord(c) < 32 for c in url):
            raise ValueError('Invalid NXM file link')
        match = re.fullmatch(r'nxm://([a-zA-Z0-9_-]+)/mods/([1-9][0-9]*)/files/([1-9][0-9]*)(?:\?[^#]*)?', url, re.IGNORECASE)
        if match is None:
            raise ValueError('Invalid NXM file link')
        if match[1].casefold() != self.game_domain().casefold():
            raise ValueError('The Nexus file belongs to a different game')
        return url

    def start_nxm(self, url):
        url = self.validate_nxm(url)
        from PyQt6.QtCore import QCoreApplication, QEventLoop, QProcess, QTimer
        # MO2's secondary process forwards the exact URL to its primary process.
        # This retains addNXMDownload's account checks and signed-link handling.
        process = QProcess()
        process.setProgram(QCoreApplication.applicationFilePath())
        process.setArguments([url])
        process.setStandardOutputFile(QProcess.nullDevice())
        process.setStandardErrorFile(QProcess.nullDevice())
        loop = QEventLoop(); timer = QTimer(); timer.setSingleShot(True)
        process.finished.connect(loop.quit); process.errorOccurred.connect(loop.quit)
        timer.timeout.connect(loop.quit)
        process.start(); timer.start(10000); loop.exec(); timer.stop()
        if process.state() != QProcess.ProcessState.NotRunning:
            process.kill(); process.waitForFinished(1000)
            raise ValueError('MO2 NXM handoff timed out; outcome unknown. Check Downloads before retrying.')
        if process.exitStatus() != QProcess.ExitStatus.NormalExit or process.exitCode() != 0 or process.error() == QProcess.ProcessError.FailedToStart:
            raise ValueError('MO2 could not accept the NXM handoff')
        return {'forwarded': True}

    def install(self, filename):
        if not isinstance(filename, str) or not Path(filename).is_file():
            raise ValueError('The mod archive does not exist')
        if filename.casefold().endswith(('.unfinished', '.part', '.meta')):
            raise ValueError('The selected file is not a completed archive')
        # MO2 chooses its existing installer plugin and owns any Qt dialogs.
        installed = self.organizer.installMod(filename)
        return {'installed': installed is not None, 'modName': installed.name() if installed is not None else None}
