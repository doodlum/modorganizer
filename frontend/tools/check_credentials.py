#!/usr/bin/env python3
"""Credential boundary tests; no real credential store or network access."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

path = Path(__file__).resolve().parents[1] / 'mo2-plugin/nexus_frontend_bridge/credentials.py'
spec = importlib.util.spec_from_file_location('credentials', path)
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)


class FakeCredentials(module.NexusCredentials):
    def __init__(self): self.value = 'old-test-value'; self.writes = []; self.reject = False; self.after_validation = lambda: None
    def _read_key(self): return self.value
    def _validate_key(self, key):
        if self.reject: raise ValueError('Rejected test credential')
        self.after_validation()
        return {'userId': 7, 'name': 'Fixture', 'premium': False, 'supporter': False}
    def _write_key(self, key):
        self.writes.append(key); self.value = key
        return {'stored': True, 'restartRequired': True}


class CredentialTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.file = Path(self.temp.name) / 'key'; self.file.write_text('new-test-value')
        self.credentials = FakeCredentials()
    def test_control_characters_cannot_enter_request_headers(self):
        for value in ('test\x00key', 'test\nkey', 'test\x7fkey'):
            self.file.write_text(value)
            with self.assertRaises(ValueError): self.credentials.import_validated_file(self.file)
        self.assertEqual(self.credentials.writes, [])
    def test_response_whitelist_excludes_credentials(self):
        result = module.NexusCredentials._account_from_reply({'user_id': 7, 'name': 'Fixture', 'is_premium': True,
            'key': 'test-secret', 'api_key': 'test-secret', 'token': 'test-secret', 'email': 'private@example.invalid'})
        self.assertEqual(result, {'userId': 7, 'name': 'Fixture', 'premium': True, 'supporter': False})
    def test_validation_failure_preserves_existing_credential(self):
        self.credentials.reject = True
        with self.assertRaises(ValueError): self.credentials.import_validated_file(self.file)
        self.assertEqual(self.credentials.value, 'old-test-value'); self.assertEqual(self.credentials.writes, [])
    def test_writes_validated_value_despite_file_change(self):
        self.credentials.after_validation = lambda: self.file.write_text('changed-after-validation')
        result = self.credentials.import_validated_file(self.file)
        self.assertEqual(self.credentials.writes, ['new-test-value']); self.assertTrue(result['restartRequired'])
        self.assertNotIn('new-test-value', repr(result))
    def test_repeated_import_does_not_forget_pending_restart(self):
        self.assertTrue(self.credentials.import_validated_file(self.file)['restartRequired'])
        self.assertTrue(self.credentials.import_validated_file(self.file)['restartRequired'])
    def test_same_existing_key_needs_no_restart(self):
        self.file.write_text(self.credentials.value)
        self.assertFalse(self.credentials.import_validated_file(self.file)['restartRequired'])
    def test_missing_account_is_not_authenticated(self):
        self.credentials.value = None
        self.assertEqual(self.credentials.account_status(), {'stored': False, 'authenticated': False, 'restartRequired': False})
    def test_malformed_account_response_is_not_success(self):
        for data in ({}, {'user_id': True, 'name':'Fixture'}, {'user_id':7,'name':''}):
            with self.assertRaises(ValueError): module.NexusCredentials._account_from_reply(data)

if __name__ == '__main__': unittest.main()
