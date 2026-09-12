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

Validation: six Python contract checks (`python3 frontend/tools/check_bridge.py`)
cover authoritative state, mutations, stale profile/session rejection, invalid
values, older-host metadata and mailbox replay. A C# client read a real isolated
New Vegas `Frontend Test` profile under Proton: nine DLC mod entries and ten
plugins. A live plugin activation and restoration also succeeded through the
host API. These are bridge checks, not evidence of a modded game launch.

The visual frontend still uses its scenario providers. Profile management,
downloads/installers, launch and real UI bindings remain to be implemented.
