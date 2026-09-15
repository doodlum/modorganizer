"""Use MO2's existing Windows credential entry, including under Wine/Proton."""
import ctypes
from ctypes import wintypes
from pathlib import Path


class Credential(ctypes.Structure):
    _fields_ = [
        ('Flags', wintypes.DWORD), ('Type', wintypes.DWORD),
        ('TargetName', wintypes.LPWSTR), ('Comment', wintypes.LPWSTR),
        ('LastWritten', wintypes.FILETIME), ('CredentialBlobSize', wintypes.DWORD),
        ('CredentialBlob', ctypes.POINTER(wintypes.BYTE)), ('Persist', wintypes.DWORD),
        ('AttributeCount', wintypes.DWORD), ('Attributes', ctypes.c_void_p),
        ('TargetAlias', wintypes.LPWSTR), ('UserName', wintypes.LPWSTR),
    ]


class NexusCredentials:
    TARGET = 'ModOrganizer2_APIKEY'

    def __init__(self):
        self.api = ctypes.WinDLL('Advapi32', use_last_error=True)
        self.api.CredWriteW.argtypes = [ctypes.POINTER(Credential), wintypes.DWORD]
        self.api.CredWriteW.restype = wintypes.BOOL
        self.api.CredReadW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.POINTER(ctypes.POINTER(Credential))]
        self.api.CredReadW.restype = wintypes.BOOL
        self.api.CredFree.argtypes = [ctypes.c_void_p]
        self.api.CredFree.restype = None

    @staticmethod
    def _key_from_file(filename):
        path = Path(filename)
        if not path.is_file() or path.stat().st_size > 4096:
            raise ValueError('Expected a Nexus API key file of at most 4096 bytes')
        try:
            key = path.read_text(encoding='utf-8-sig').strip()
        except (OSError, UnicodeError):
            raise ValueError('Unable to read the Nexus API key file') from None
        if not key or any(not 33 <= ord(character) <= 126 for character in key):
            raise ValueError('Expected a single ASCII Nexus API key')
        return key

    def _read_key(self):
        saved = ctypes.POINTER(Credential)()
        if not self.api.CredReadW(self.TARGET, 1, 0, ctypes.byref(saved)):
            if ctypes.get_last_error() == 1168: return None  # ERROR_NOT_FOUND
            raise OSError('Unable to read the MO2 Nexus credential')
        try:
            size = saved.contents.CredentialBlobSize
            if size == 0: return None
            if size > 8192 or size % 2: raise ValueError('Invalid MO2 Nexus credential format')
            try: key = ctypes.string_at(saved.contents.CredentialBlob, size).decode('utf-16-le')
            except UnicodeError: raise ValueError('Invalid MO2 Nexus credential format') from None
            if any(not 33 <= ord(c) <= 126 for c in key):
                raise ValueError('Invalid MO2 Nexus credential format')
            return key
        finally:
            self.api.CredFree(saved)

    @staticmethod
    def _account_from_reply(data):
        # The validation response can itself contain an API key. Never forward
        # the response wholesale into bridge receipts, logs or frontend state.
        user_id = data.get('user_id')
        name = data.get('name')
        if type(user_id) is not int or user_id <= 0 or not isinstance(name, str) or not name:
            raise ValueError('Nexus returned an incomplete account response')
        return {'userId': user_id, 'name': name,
                'premium': data.get('is_premium') is True,
                'supporter': data.get('is_supporter') is True}

    @classmethod
    def _validate_key(cls, key):
        import json
        from PyQt6.QtCore import QEventLoop, QTimer, QUrl
        from PyQt6.QtNetwork import QNetworkAccessManager, QNetworkRequest
        manager = QNetworkAccessManager()
        request = QNetworkRequest(QUrl('https://api.nexusmods.com/v1/users/validate.json'))
        request.setAttribute(QNetworkRequest.Attribute.RedirectPolicyAttribute,
                             QNetworkRequest.RedirectPolicy.ManualRedirectPolicy)
        request.setRawHeader(b'apikey', key.encode('ascii'))
        request.setRawHeader(b'Accept', b'application/json')
        request.setTransferTimeout(10000)
        reply = manager.get(request)
        loop = QEventLoop(); deadline = QTimer(); deadline.setSingleShot(True)
        reply.finished.connect(loop.quit)
        deadline.timeout.connect(reply.abort)
        deadline.start(10000)
        if not reply.isFinished(): loop.exec()
        deadline.stop()
        try:
            status = reply.attribute(QNetworkRequest.Attribute.HttpStatusCodeAttribute)
            if status in (401, 403): raise ValueError('Nexus rejected this API key')
            if status != 200: raise ValueError('Could not validate the Nexus account; check the connection and try again')
            raw = bytes(reply.readAll())
            if len(raw) > 65536: raise ValueError('Nexus account response exceeded the expected size')
            try: data = json.loads(raw)
            except (ValueError, UnicodeError): raise ValueError('Nexus returned an invalid account response') from None
            if not isinstance(data, dict): raise ValueError('Nexus returned an invalid account response')
            return cls._account_from_reply(data)
        finally:
            reply.deleteLater()

    def account_status(self):
        key = self._read_key()
        if key is None: return {'stored': False, 'authenticated': False, 'restartRequired': False}
        account = self._validate_key(key)
        return {'stored': True, 'authenticated': True, 'account': account,
                'restartRequired': getattr(self, '_restart_required', False)}

    def import_validated_file(self, filename):
        key = self._key_from_file(filename)
        account = self._validate_key(key)
        # Validate before mutation, and write the exact validated value rather
        # than reading a potentially changed file for a second time.
        previous = self._read_key()
        result = self._write_key(key)
        self._restart_required = getattr(self, '_restart_required', False) or previous != key
        return {**result, 'restartRequired': self._restart_required, 'account': account}

    def import_file(self, filename):
        return self._write_key(self._key_from_file(filename))

    def _write_key(self, key):
        # MO2 stores QString's UTF-16 data, without a terminating NUL.
        encoded = key.encode('utf-16-le')
        blob = (wintypes.BYTE * len(encoded)).from_buffer_copy(encoded)
        credential = Credential()
        credential.Type = 1  # CRED_TYPE_GENERIC
        credential.TargetName = self.TARGET
        credential.CredentialBlobSize = len(encoded)
        credential.CredentialBlob = blob
        credential.Persist = 2  # CRED_PERSIST_LOCAL_MACHINE, as in MO2
        if not self.api.CredWriteW(ctypes.byref(credential), 0):
            raise OSError('Windows credential storage failed (code %d)' % ctypes.get_last_error())
        saved = ctypes.POINTER(Credential)()
        if not self.api.CredReadW(self.TARGET, 1, 0, ctypes.byref(saved)):
            raise OSError('Windows credential readback failed (code %d)' % ctypes.get_last_error())
        try:
            if ctypes.string_at(saved.contents.CredentialBlob, saved.contents.CredentialBlobSize) != encoded:
                raise ValueError('Windows credential readback did not match')
        finally:
            self.api.CredFree(saved)
        # Never return the key or blob, including in diagnostic snapshots.
        return {'stored': True, 'restartRequired': True}
