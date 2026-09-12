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

    def import_file(self, filename):
        path = Path(filename)
        if not path.is_file() or path.stat().st_size > 4096:
            raise ValueError('Expected a Nexus API key file of at most 4096 bytes')
        try:
            key = path.read_text(encoding='utf-8-sig').strip()
        except (OSError, UnicodeError):
            raise ValueError('Unable to read the Nexus API key file') from None
        if not key or any(character.isspace() for character in key) or not key.isascii():
            raise ValueError('Expected a single ASCII Nexus API key')
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
