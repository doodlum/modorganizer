# Data Hide / Un-Hide acceptance

Prepare a fresh temporary probe with `tools/data_launch_probe.py prepare`, using
the selected native bridge endpoint, a new `--spec` JSON path, and the matching
physical game `Data` directory as `--game-data`. The helper creates its own unique
Overwrite folder and waits for MO2's asynchronous refresh to publish it.

Run the Release frontend with the matching `MO2_BRIDGE_DIRECTORY` and
`MO2_VERIFY_DATA_VISIBILITY=/absolute/probe-spec.json`.

The check selects the temporary text file in the rendered Data table and invokes
the actual Hide menu item. It requires the physical `.mohidden` rename, native
merged-tree readback, unchanged bytes, and disappearance with Hidden Files off.
It then turns Hidden Files on, invokes Un-Hide, and verifies file bytes and the
native row are restored. The active profile must remain unchanged.

Failure recovery attempts native Un-Hide only for the same profile and unchanged
temporary file. Always inspect the verdict and run `data_launch_probe.py cleanup`
with the same endpoint/spec afterward. Cleanup refuses changed files and requires
the original profile and Overwrite location; it then verifies disappearance from
native Data. Neither helper nor check deletes installed mods or game files.

This covers ordinary loose-file visibility. It does not establish collision
confirmation/cancellation, archive-file mutation, every source-mod combination,
or full Data-page acceptance.

## Logs checks

`MO2_VERIFY_LOGS_LIFECYCLE=1` runs independent controlled-read cases for late
success/error after close and reopen, off-thread execution, and changing files
during an outstanding refresh. It verifies level/text filters, tail truncation,
and redaction using disposable synthetic log files. Finally it renders the
connected host's real log and checks the line limit without emitting log text.
The native portion requires a connected host with a nonempty log. This check
does not mutate MO2 or change the host's log configuration.
