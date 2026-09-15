#!/usr/bin/env python3
"""Exercise host routing and actual MO2-style archive metadata without network."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

source = Path(__file__).resolve().parents[1] / 'mo2-plugin/nexus_frontend_bridge/downloads.py'
spec = importlib.util.spec_from_file_location('downloads', source)
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)

class Organizer:
    def __init__(self, path): self.path = path; self.calls = []
    def downloadsPath(self): return self.path
    def onDownloadFailed(self, callback): self.failed = callback; return True
    def onDownloadComplete(self, callback): self.completed = callback; return True
    def managedGame(self): return self
    def gameNexusName(self): return 'newvegas'
    def downloadManager(self): return self
    def startDownloadNexusFile(self, mod, file): self.calls.append((mod, file)); return 7
    def installMod(self, path): self.calls.append(path); return self
    def name(self): return 'Installed by host'
    def absolutePath(self): return str(Path(self.path) / 'native-mod-location')

class DownloadsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.organizer = Organizer(self.temp.name); self.downloads = module.Downloads(self.organizer)
    def test_failure_history_survives_removed_rows_and_resolves_on_completion(self):
        self.organizer.downloadPath = lambda identifier: 'Z:/private/folder/archive.zip'
        self.organizer.failed(7)
        self.organizer.failed(7)
        self.assertEqual(len(self.downloads.warnings()), 1)
        warning = self.downloads.warnings()[0]
        self.assertIn('archive.zip', warning['details'])
        self.assertNotIn('private', warning['details'])
        self.assertNotIn('Z:', warning['details'])
        self.organizer.downloadPath = lambda identifier: ''
        self.assertEqual(self.downloads.warnings()[0], warning)
        self.organizer.completed(7)
        self.assertEqual(self.downloads.warnings(), [])
        for identifier in range(60): self.organizer.failed(identifier)
        self.assertEqual(len(self.downloads._failures), 50)
        self.assertNotIn(0, self.downloads._failures)
    def test_frontend_cancellation_does_not_create_failure_warning(self):
        self.organizer.downloadPath = lambda identifier: 'Z:/downloads/canceled.zip'
        self.downloads._canceled.add('z:/downloads/canceled.zip')
        self.organizer.failed(8)
        self.assertEqual(self.downloads.warnings(), [])
    def test_archive_metadata_does_not_become_profile_membership(self):
        path = Path(self.temp.name) / 'Test.7z'; path.write_bytes(b'archive')
        Path(str(path) + '.meta').write_text('[General]\ninstalled=true\nurl=private-download-link\n')
        (path.parent / 'Partial.zip.unfinished').write_bytes(b'x')
        items = self.downloads.snapshot()
        self.assertEqual(len(items), 2)
        self.assertTrue(items[0]['partial'])
        self.assertTrue(items[1]['installed'])
        self.assertNotIn('url', items[1])
        self.assertEqual(items[1]['bytes'], 7)
    def test_signed_nxm_link_is_preserved_and_wrong_targets_are_rejected(self):
        url = 'nxm://newvegas/mods/42507/files/1?key=synthetic%2Bvalue%2F&expires=123&user_id=456'
        self.assertEqual(self.downloads.validate_nxm(url),url)
        for invalid in [url.replace('newvegas','skyrimspecialedition'), url + '#fragment',
                        url.replace('newvegas','user@newvegas'), url.replace('/1?', '/0?'),
                        url + '\n', 'file:///tmp/mod.zip', 'nxm://newvegas:123/mods/1/files/1']:
            with self.assertRaises(ValueError): self.downloads.validate_nxm(invalid)
        self.assertEqual(self.organizer.calls,[])
    def test_live_translated_error_is_distinct_from_paused_metadata(self):
        root = Path(self.temp.name)
        for name in ['Broken.zip', 'Broken.zip.unfinished', 'Paused.zip.unfinished']:
            (root / name).write_bytes(b'fixture')
            Path(str(root / name) + '.meta').write_text('[General]\npaused=true\n')
        class Proxy:
            def __init__(self, model): self.inner = model
            def sourceModel(self): return self.inner
        class Model:
            def metaObject(self): return SimpleNamespace(className=lambda: 'DownloadList')
            def rowCount(self): return 3
            def index(self, row, column):
                self_test.assertEqual(column, 1)
                return row
            def data(self, row, role): return ['Fehler', 'Pausiert', 'Fehler'][row]
        self_test = self
        self.downloads.window = SimpleNamespace(findChild=lambda *args: SimpleNamespace(model=lambda: Proxy(Model())))
        def download_path(row):
            if row == 2: raise RuntimeError('Pending row has no path')
            return str(root / ['Broken.zip', 'Paused.zip'][row])
        self.organizer.downloadPath = download_path
        core = SimpleNamespace(QAbstractProxyModel=Proxy,
            QCoreApplication=SimpleNamespace(translate=lambda context, text: 'Fehler'),
            Qt=SimpleNamespace(ItemDataRole=SimpleNamespace(DisplayRole=0)))
        with patch.dict('sys.modules', {'PyQt6.QtCore': core, 'PyQt6.QtWidgets': SimpleNamespace(QTreeView=object)}):
            rows = {row['name']: row for row in self.downloads.snapshot()}
        self.assertTrue(rows['Broken.zip.unfinished']['failed'])
        self.assertTrue(rows['Broken.zip.unfinished']['paused'])
        self.assertFalse(rows['Paused.zip.unfinished']['failed'])
        self.assertFalse(rows['Broken.zip']['failed'])
        self.assertEqual(self.organizer.calls, [])
    def test_delete_resolves_current_row_and_uses_native_confirmation_slot(self):
        calls = []
        root = Path(self.temp.name)
        class Proxy:
            def __init__(self, model): self.inner = model
            def sourceModel(self): return self.inner
        model = SimpleNamespace(metaObject=lambda: SimpleNamespace(className=lambda: 'DownloadList'), rowCount=lambda: 2)
        view = SimpleNamespace(model=lambda: Proxy(model), isEnabled=lambda: True)
        self.downloads.window = SimpleNamespace(findChild=lambda *args: view)
        self.organizer.downloadPath = lambda row: str(root / ['other.zip', 'chosen.zip'][row])
        core = SimpleNamespace(QAbstractProxyModel=Proxy, Q_ARG=lambda kind, value: value,
            QMetaObject=SimpleNamespace(invokeMethod=lambda *args: calls.append(args)),
            Qt=SimpleNamespace(ConnectionType=SimpleNamespace(DirectConnection=0)))
        with patch.dict('sys.modules', {'PyQt6.QtCore': core, 'PyQt6.QtWidgets': SimpleNamespace(QTreeView=object)}):
            result = self.downloads.control(str(root / 'chosen.zip'), 'delete')
            self.assertEqual(result, {'requested': 'delete'})
            self.assertEqual(calls, [(view, 'issueDelete', 0, 1)])
            with self.assertRaises(ValueError): self.downloads.control(str(root / 'missing.zip'), 'delete')
            self.assertEqual(len(calls), 1)
        self.assertEqual(self.organizer.calls, [])
    def test_download_uses_host_and_rejects_wrong_game(self):
        self.assertEqual(self.downloads.start_nexus(42507, 1, 'newvegas')['downloadId'], 7)
        with self.assertRaises(ValueError): self.downloads.start_nexus(42507, 1, 'skyrimspecialedition')
        with self.assertRaises(ValueError): self.downloads.start_nexus(True, 1, 'newvegas')
        self.assertEqual(self.organizer.calls, [(42507, 1)])
    def test_installer_is_host_owned_and_rejects_partial_files(self):
        path = Path(self.temp.name) / 'Test.zip'; path.write_bytes(b'x')
        result = self.downloads.install(str(path))
        self.assertEqual(result['modName'], 'Installed by host')
        self.assertEqual(result['modPath'], self.organizer.absolutePath())
        partial = path.with_suffix('.unfinished'); partial.write_bytes(b'x')
        with self.assertRaises(ValueError): self.downloads.install(str(partial))
        self.assertEqual(self.organizer.calls, [str(path)])

    def test_cancelled_install_has_no_mod_destination(self):
        path = Path(self.temp.name) / 'Canceled.zip'; path.write_bytes(b'x')
        self.organizer.installMod = lambda _: None
        self.assertEqual(self.downloads.install(str(path)), {'installed': False, 'modName': None, 'modPath': None})

    def test_install_reports_mapping_from_its_host_only(self):
        path = Path(self.temp.name) / 'Test.zip'; path.write_bytes(b'x')
        with patch.object(module, 'wine_unix_path', return_value='/prefix/drive_c/mods/example') as resolve:
            result = self.downloads.install(str(path))
            resolve.assert_called_once_with(self.organizer.absolutePath())
            self.assertEqual(result['modUnixPath'], '/prefix/drive_c/mods/example')
        with patch.object(module, 'wine_unix_path', return_value=None):
            self.assertNotIn('modUnixPath', self.downloads.install(str(path)))
        self.organizer.installMod = lambda _: None
        with patch.object(module, 'wine_unix_path') as resolve:
            self.downloads.install(str(path))
            resolve.assert_not_called()

if __name__ == '__main__': unittest.main()
