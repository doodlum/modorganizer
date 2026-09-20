# Save hover preview — implementation and current evidence

The frontend now requests the original game extension's SaveGameInfo widget on a
save-row hover. Mo2SaveHover resolves the row under the pointer without selecting
it, retains one owner per hover, renews every 400 ms, and dismisses on pointer
leave, deactivation or disposal. Visibility, connection, foreground-action and
profile guards stop renewals. Requests stay serialized within each hover controller;
a late show response is followed by dismissal of that same owner.

The native save_preview.py uses the existing extension API without modifying the
extension. Its nonmodal Qt tool window does not accept focus or pointer input.
The request carries the frontend pointer's screen position: Wine's own cursor
position can be stale over a Linux window. The widget is placed within the current
screen using MO2's usual offset/right-and-bottom fallback. Mixed-DPI and multiple
monitor coordinate mapping have not been validated.

Each request expires after 1.5 seconds. Expired queued shows are rejected, and the
native bridge poll expires displayed widgets even if the frontend has stopped.
Profile changes dismiss the widget. Owner-scoped dismissal cannot close a newer
owner's preview. Mutating/native-dialog actions dismiss the current preview first.
Hosts advertise canPreviewSaves; older bridges retain the existing explicit
save-details path and receive no hover requests.

## Verified

- Release build: eight existing warnings, zero errors.
- tools/check_save_preview.py: eight lease checks, including renewal, expiry,
  owner isolation, stale requests, slow creation, profile changes and absent widgets.
- Existing bridge contracts: 40 pass (save-hover-final-contracts.log in /tmp).
- The isolated FNV host was normally closed, its old core.py backed up under
  artifacts/save-hover-bridge-backup/, updated and restarted. It matches source.
- Direct native preview requests rendered the actual FNV extension widget,
  including save image, character/location and missing-ESP information.
- artifacts/save-hover-native-result.json confirms frontend focus retained,
  stale-owner dismissal ignored and automatic expiry. A supplied point of
  850,300 produced a 278x332 window at 855,320 on this 1280x800 desktop.
- artifacts/save-hover-native-restoration.json confirms original native profile,
  mod/plugin states, save list and every save/companion byte unchanged.

The compositor images are save-hover-native-first.png and
save-hover-native-positioned.png. The latter contains the frontend's early startup
content: the driver waited for its window/focus, not its tables to become ready.
It verifies native widget placement/focus only, not usable frontend startup or
save-row hover integration.

## Outstanding

Actual frontend pointer entry/leave, tab changes, deactivation and rapid row
changes still need end-to-end verification. The existing filename tooltip may
need suppression while the extension preview is active. Normal startup content
must be checked after readiness, rather than accepted from the early screenshot.
Skyrim's installed bridge has not yet received this feature; its existing
functionality remains on the previously verified package. Native oversized
widgets and mixed-DPI layouts also remain unverified. This is not full Saves or
production acceptance.

## Pointer driver and normal startup follow-up

`tools/check_save_hover_ui.py` now drives `MO2_VERIFY_SAVE_HOVER` through a
pointer-only Linux evdev device. It does not emit clicks or keyboard events. The
frontend checker opens the populated Saves tab, provides actual screen row
coordinates, and checks that hover never selects a save. The external driver
observes only preview windows owned by the selected native host, verifies a new
window for a changed row, waits beyond the lease duration to check renewal,
checks frontend focus, and captures visible phases. Hidden-tab, pointer-leave and
deactivation phases require disappearance within one second.

After closing the interactive frontend, the intended invocation is:

```sh
python frontend/tools/check_save_hover_ui.py \
  --instance frontend/artifacts/mo2-fnv-host \
  --output frontend/artifacts/save-hover-pointer-new-run
```

Use a fresh output directory. The driver checks the 1280x800 desktop mapping,
requires an already-running native host, refuses an existing frontend, and ends
only its own test frontend. It never starts or stops MO2. Python compilation and
argument handling pass; a mocked preflight confirms refusal occurs before output
or input-device creation. **The pointer test has not been run yet:** the user
requested the app remain open for manual testing.

`native-frame-fnv-startup-15s.png` was inspected before that manual session. Normal
startup had populated readable Mods and Plugins by this capture, with the raw
Collection placeholder gone. This establishes eventual startup rendering, not a
15-second startup requirement or acceptable latency. The interrupted checker
build's missing WorkspaceSystem import was fixed; the subsequent Release build
passed with eight warnings and zero errors before launching the user's app.
