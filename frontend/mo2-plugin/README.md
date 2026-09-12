# MO2 host bridge

Copy the `nexus_frontend_bridge` directory into an MO2 instance's `plugins`
directory and restart MO2. This is a normal `mobase.IPluginTool` extension; MO2
continues hosting its existing game, installer and other extensions. It has been
loaded by the installed Windows MO2 2.5.2 under GE-Proton10-4 on Linux.

The Tools menu entry displays the endpoint directory, normally
`plugins/data/frontend-bridge`. The native frontend can read it through the
corresponding Linux path:

```sh
.tools/dotnet/dotnet frontend/MockHost/bin/Debug/net9.0/MockHost.dll \
  --mo2-bridge-snapshot /path/to/mo2/plugins/data/frontend-bridge
```

The JSON mailbox uses protocol 1 and a new session UUID on every host startup.
Write each request atomically to `requests/<uuid>.json` with `protocol`,
`session`, and `action`; read the matching response in `responses`. Supported
actions are `snapshot`, `setModActive`, `setModPriority`, `setPluginActive`, and
`setPluginPriority`. Mutations require the exact `profilePath` from the latest
snapshot, `name`, and either boolean `enabled` or integer `priority`. These call
the existing MO2 list APIs on its UI thread. Responses include host state after
the operation, which may differ from the requested state due to host rules.

Plugin states and mod state flags are the host API's numeric values. Plugin
`priority` and `loadOrder` are distinct; inactive plugins may have load order -1.
The bridge makes no assumptions about activation files or game-specific rules.
`instance.name` is nullable because MO2 2.5.2's Python API lacks `instanceName`.

Requests are serviced only after MO2's UI initialization callback. A timeout
does not establish that a mutation failed: reconnect and refresh before retrying.
Responses retained while a request exists prevent duplicate execution of that
request. This is a local extension endpoint with the same access as the user's
MO2 files; it is not a network service.

Validation: seven Python contract checks (`python3 frontend/tools/check_bridge.py`)
cover authoritative state, mutations, stale profile/session rejection, invalid
values, older-host metadata and mailbox replay. A C# client read a real isolated
New Vegas `Frontend Test` profile under Proton: nine DLC mod entries and ten
plugins. A live plugin activation and restoration also succeeded through the
host API. These are bridge checks, not evidence of a modded game launch.

The live visual frontend is available through
`frontend/run-live.sh /path/to/mo2/plugins/data/frontend-bridge`. It displays
the host profile's mods and priorities on the left and the original NMA plugin
order editor on the right. It polls MO2 every two seconds and sends changes to
MO2, then displays the response. No profile files are written by the frontend.
The mod table uses MO2 columns, without NMA collection or installation-date
semantics. Essential content has no activation toggle.

With `MO2_VERIFY_LIVE=1` and `MO2_SCREENSHOT=/path/to/image.png`, the live mode
checks the isolated `frontend/artifacts/mo2-fnv-host/profiles/Frontend Test`
profile, executes the native plugin row's move command, independently checks
MO2's resulting priority, and restores the original order. This check passed;
the rendered tables contained nine real mod rows and ten real plugin rows.
It does not install a mod or launch FNV.

On this Steam Deck, MO2/Qt under GE-Proton10-4 crashed on Wine's unimplemented
`USER32.GetPointerFrameTouchInfo`. Setting `QT_QPA_PLATFORM=windows:nowmpointer`
inside a Windows `.cmd` launcher before starting MO2 avoided that failure during
the live checks. Setting it only in the outer Linux launch environment did not
work in the already-running prefix. Do not pass `-platform` to MO2: its command
parser treats that as a profile selection.

Profile management, downloads/installers, plugin activation controls, mod
priority editing and launch remain to be connected. Nexus account access will
reuse MO2's credential and download workflow; no credential belongs in this
repository or in the bridge's diagnostic snapshots.

## Nexus account

Import an existing key file into the running host's credential store:

```sh
.tools/dotnet/dotnet frontend/MockHost/bin/Debug/net9.0/MockHost.dll \
  --mo2-import-nexus-key /path/to/mo2/plugins/data/frontend-bridge /private/key.txt
```

The mailbox contains only the path. The host reads the file and uses Windows
`CredWriteW` with MO2's existing `ModOrganizer2_APIKEY` target, UTF-16 blob and
local-machine persistence. It checks the stored value without returning it.
Restart MO2 afterward so its existing Nexus access manager loads the key.
On Linux the command maps the file through the same machine's Wine `Z:` drive.
The account belongs to that Windows/Proton prefix; other prefixes are separate.

Credential import does not validate the key with Nexus or claim that downloads
work. The supplied test key was separately validated against Nexus's official
API, then credential import/readback succeeded in Windows MO2 2.5.2 under Proton.
After restarting, original MO2 displayed the account in its window title and
populated its Nexus API quota indicator, confirming it loaded the stored key.
The contract test checks path-only dispatch and stale-session rejection; the
Windows credential implementation additionally requires the live host check.
