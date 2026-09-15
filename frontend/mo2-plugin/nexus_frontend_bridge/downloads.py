"""MO2's download directory plus its existing downloader/installer entry points."""
from collections import OrderedDict
import configparser
import re
from pathlib import Path


def wine_unix_path(path):
    """Ask this host's Wine runtime to resolve its own drive mappings."""
    import ctypes
    import os
    if os.name != 'nt':
        return None
    try:
        # Same CDECL export and heap ownership used by Wine's winepath utility.
        convert = ctypes.CDLL('kernel32').wine_get_unix_file_name
        convert.argtypes = [ctypes.c_wchar_p]
        convert.restype = ctypes.c_void_p
        kernel = ctypes.WinDLL('kernel32')
        kernel.GetProcessHeap.restype = ctypes.c_void_p
        kernel.HeapFree.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.c_void_p]
        kernel.HeapFree.restype = ctypes.c_int
        pointer = convert(path)
        if not pointer:
            return None
        try:
            resolved = ctypes.string_at(pointer).decode('utf-8')
            return resolved if resolved.startswith('/') else None
        finally:
            kernel.HeapFree(kernel.GetProcessHeap(), 0, pointer)
    except (AttributeError, OSError, UnicodeError):
        # Native Windows and unavailable mappings retain the original result.
        return None


class Downloads:
    def __init__(self, organizer):
        self.organizer = organizer
        self.window = None
        self._failures = OrderedDict()
        self._canceled = set()
        manager = organizer.downloadManager()
        manager.onDownloadFailed(self._on_failed)
        manager.onDownloadComplete(self._on_complete)

    def _on_failed(self, identifier):
        # Callbacks run before MO2 can remove a retry-exhausted row. Keep only
        # a filename: paths, signed URLs and metadata never enter notifications.
        try:
            path = str(self.organizer.downloadManager().downloadPath(identifier))
        except RuntimeError:
            path = ''  # Pending Nexus lookups may have no file yet.
        key = path.casefold()
        if key and key in self._canceled:
            self._canceled.discard(key)
            return
        name = path.replace('\\', '/').rsplit('/', 1)[-1] if path else 'A download'
        self._failures[identifier] = name
        self._failures.move_to_end(identifier)
        while len(self._failures) > 50:
            self._failures.popitem(last=False)

    def _on_complete(self, identifier):
        self._failures.pop(identifier, None)

    def warnings(self):
        return [{'title': 'Download interrupted',
                 'details': name + ': MO2 reported an unsuccessful download. It may retry automatically. '
                            'Check Downloads or Logs; a download removed after its retries must be downloaded again.'}
                for name in reversed(self._failures.values())]


    def game_domain(self):
        game = self.organizer.managedGame()
        return game.gameNexusName() or game.gameShortName()

    def snapshot(self):
        directory = Path(self.organizer.downloadsPath())
        result = []
        if not directory.is_dir():
            return result
        failed = self._failed_paths()
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
            stat = path.stat()
            result.append({'name': path.name, 'path': str(path), 'bytes': stat.st_size,
                           'partial': partial, 'installed': values.get('installed', 'false') == 'true',
                           'hidden': values.get('removed', 'false') == 'true',
                           'paused': values.get('paused', 'false') == 'true',
                           'failed': partial and str(path).casefold() in failed,
                           # The rest of MO2's own download columns (downloadlist.cpp):
                           # Filetime, Mod name, Version, Nexus ID and Source Game. MO2
                           # keeps them in the .meta beside the archive, which is already
                           # read above, so a download without one sends "".
                           'filetime': str(int(stat.st_mtime)),
                           'modName': values.get('modname', ''),
                           'version': values.get('version', ''),
                           'modId': values.get('modid', ''),
                           'sourceGame': values.get('gamename', '')})
        return result

    def _failed_paths(self):
        # Metadata writes paused=true for both STATE_PAUSED and STATE_ERROR.
        # Read the original model's translated status rather than guessing from it.
        if self.window is None:
            return set()
        from PyQt6.QtCore import QAbstractProxyModel, QCoreApplication, Qt
        from PyQt6.QtWidgets import QTreeView
        view = self.window.findChild(QTreeView, 'downloadView')
        if view is None:
            return set()
        model = view.model()
        while isinstance(model, QAbstractProxyModel):
            model = model.sourceModel()
        if model is None or model.metaObject().className() != 'DownloadList':
            return set()
        error_label = QCoreApplication.translate('DownloadList', 'Error')
        failed = set()
        manager = self.organizer.downloadManager()
        for row in range(model.rowCount()):
            if model.data(model.index(row, 1), Qt.ItemDataRole.DisplayRole) != error_label:
                continue
            try:
                path = str(Path(manager.downloadPath(row))).casefold()
            except RuntimeError:
                continue  # Pending rows do not have an archive/download index.
            failed.update((path, path + '.unfinished', path + '.part'))
        return failed

    def control(self, filename, action):
        from PyQt6.QtCore import QAbstractProxyModel, QMetaObject, Q_ARG, Qt
        from PyQt6.QtWidgets import QTreeView
        slots = {'pause': 'issuePause', 'resume': 'issueResume', 'cancel': 'issueCancel', 'delete': 'issueDelete'}
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
            if action == 'cancel':
                if len(self._canceled) >= 50: self._canceled.clear()
                self._canceled.add(host_path.casefold())
            elif action == 'resume':
                self._canceled.discard(host_path.casefold())
            QMetaObject.invokeMethod(view, slots[action], Qt.ConnectionType.DirectConnection, Q_ARG(int, row))
            return {'requested': action}
        raise ValueError('MO2 no longer manages this download; refresh the list')

    def query_metadata(self):
        """Press MO2's own Query Metadata button.

        DownloadsTab::queryInfos asks the download manager to fill in what its
        archives are missing, over the network, and MO2 writes the answers into
        the .meta files the snapshot already reads. Pressed rather than
        reimplemented so the offline-mode guard, the login prompt and the
        progress it shows are MO2's own.

        MO2 asks, in a message box, whether to leave offline mode. That question
        is the user's to answer in MO2, so the box is dismissed and the refusal
        reported rather than answered here.
        """
        from PyQt6.QtCore import QEvent, QObject, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QMessageBox, QPushButton
        if self.window is None:
            raise ValueError('MO2 download controls are not ready')
        button = self.window.findChild(QPushButton, 'btnQueryDownloadsInfo')
        if button is None or not self.window.isEnabled() or not button.isEnabled():
            raise ValueError('MO2 cannot query download metadata right now')
        refused = []

        class Capture(QObject):
            def eventFilter(inner, watched, event):
                if isinstance(watched, QMessageBox) and event.type() == QEvent.Type.Polish:
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def dismiss():
                        refused.append(watched.text())
                        watched.reject()
                    QTimer.singleShot(0, dismiss)
                return False

        app = QApplication.instance()
        capture = Capture()
        app.installEventFilter(capture)
        try:
            button.click()
        finally:
            app.removeEventFilter(capture)
        if refused:
            raise ValueError(refused[0].replace('\n', ' '))
        return {'queried': True}

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
        result = {'installed': installed is not None,
                'modName': installed.name() if installed is not None else None,
                'modPath': installed.absolutePath() if installed is not None else None}
        if installed is not None:
            unix_path = wine_unix_path(result['modPath'])
            if unix_path is not None:
                result['modUnixPath'] = unix_path
        return result
