#!/usr/bin/env python3
"""Exercise host routing and actual MO2-style archive metadata without network."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

source = Path(__file__).resolve().parents[1] / 'mo2-plugin/nexus_frontend_bridge/downloads.py'
spec = importlib.util.spec_from_file_location('downloads', source)
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)

class Organizer:
    def __init__(self, path): self.path = path; self.calls = []
    def downloadsPath(self): return self.path
    def managedGame(self): return self
    def gameNexusName(self): return 'newvegas'
    def downloadManager(self): return self
    def startDownloadNexusFile(self, mod, file): self.calls.append((mod, file)); return 7
    def installMod(self, path): self.calls.append(path); return self
    def name(self): return 'Installed by host'

class DownloadsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.organizer = Organizer(self.temp.name); self.downloads = module.Downloads(self.organizer)
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
    def test_download_uses_host_and_rejects_wrong_game(self):
        self.assertEqual(self.downloads.start_nexus(42507, 1, 'newvegas')['downloadId'], 7)
        with self.assertRaises(ValueError): self.downloads.start_nexus(42507, 1, 'skyrimspecialedition')
        with self.assertRaises(ValueError): self.downloads.start_nexus(True, 1, 'newvegas')
        self.assertEqual(self.organizer.calls, [(42507, 1)])
    def test_installer_is_host_owned_and_rejects_partial_files(self):
        path = Path(self.temp.name) / 'Test.zip'; path.write_bytes(b'x')
        self.assertEqual(self.downloads.install(str(path))['modName'], 'Installed by host')
        partial = path.with_suffix('.unfinished'); partial.write_bytes(b'x')
        with self.assertRaises(ValueError): self.downloads.install(str(partial))
        self.assertEqual(self.organizer.calls, [str(path)])

if __name__ == '__main__': unittest.main()
