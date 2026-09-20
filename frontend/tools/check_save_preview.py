#!/usr/bin/env python3
"""Lease lifecycle checks; actual Qt rendering still requires a native host."""
import importlib.util
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[1] / 'mo2-plugin/nexus_frontend_bridge/save_preview.py'
spec = importlib.util.spec_from_file_location('save_preview', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class SavePreviewTests(unittest.TestCase):
    def setUp(self):
        self.now = 1000
        self.profile = 'first'
        self.created, self.closed = [], []
        self.lease = module.SavePreviewLease(self.create, lambda: self.profile, lambda: self.now)

    def create(self, filename, position=None):
        self.created.append(filename)
        return lambda: self.closed.append(filename)

    def test_renew_retains_widget_then_expires(self):
        self.assertTrue(self.lease.request('a', 'one.fos', 2500)['visible'])
        self.now = 2000
        self.lease.request('a', 'one.fos', 3500)
        self.assertEqual(self.created, ['one.fos'])
        self.now = 3499
        self.lease.tick()
        self.assertEqual(self.closed, [])
        self.now = 3500
        self.lease.tick()
        self.assertEqual(self.closed, ['one.fos'])
        self.lease.tick()
        self.assertEqual(self.closed, ['one.fos'])

    def test_owner_scoped_dismiss(self):
        self.lease.request('old', 'one.fos', 2500)
        self.lease.request('new', 'two.fos', 2500)
        self.lease.request('old', None, None)
        self.assertEqual(self.closed, ['one.fos'])
        self.assertEqual(self.lease.owner, 'new')
        self.lease.request('new', None, None)
        self.assertEqual(self.closed, ['one.fos', 'two.fos'])

    def test_profile_change_closes(self):
        self.lease.request('a', 'one.fos', 2500)
        self.profile = 'second'
        self.lease.tick()
        self.assertEqual(self.closed, ['one.fos'])

    def test_stale_or_unbounded_requests_never_create(self):
        for expiry in (999, 1000, 3001, None, True, '2500'):
            self.assertFalse(self.lease.request('a', 'one.fos', expiry)['visible'])
        self.assertEqual(self.created, [])

    def test_slow_creation_does_not_leave_stale_widget(self):
        def slow(filename, position):
            dismiss = self.create(filename)
            self.now = 2600
            return dismiss
        self.lease.create = slow
        self.assertFalse(self.lease.request('a', 'one.fos', 2500)['visible'])
        self.assertEqual(self.closed, ['one.fos'])
        self.assertIsNone(self.lease.owner)

    def test_profile_change_during_creation(self):
        def changing(filename, position):
            dismiss = self.create(filename)
            self.profile = 'second'
            return dismiss
        self.lease.create = changing
        self.assertFalse(self.lease.request('a', 'one.fos', 2500)['visible'])
        self.assertEqual(self.closed, ['one.fos'])

    def test_extension_without_widget_is_not_repeatedly_constructed(self):
        self.lease.create = lambda filename, position: self.created.append(filename)
        self.assertFalse(self.lease.request('a', 'one.fos', 2500)['visible'])
        self.lease.request('a', 'one.fos', 2500)
        self.assertEqual(self.created, ['one.fos'])

    def test_bad_owner_rejected(self):
        for owner in (None, '', 42, 'a' * 129):
            with self.assertRaises(ValueError):
                self.lease.request(owner, 'one.fos', 2500)
        self.assertEqual(self.created, [])


if __name__ == '__main__':
    unittest.main()
