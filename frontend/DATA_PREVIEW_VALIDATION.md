# Data preview and keyboard activation

`MO2_VERIFY_DATA_PREVIEW=/absolute/spec.json` runs the rendered Data check on
the connected host. The specification contains four strings:

```json
{
  "supported": "textures/MCM/Check1.dds",
  "unsupported": "FalloutNV.esm",
  "request": "/absolute/new-preview.request",
  "report": "/absolute/new-preview.json"
}
```

Choose existing files in that profile, with an installed preview handler for the
supported file and none for the unsupported one. The example uses the isolated
FNV profile with MCM installed. Both marker/report paths must be new for every
run. The verifier checks native support before using either case.

Start `tools/fixtures/observe_data_preview.py` using Windows Python in the same
Wine prefix as the host, before starting the frontend check. Its three arguments
are the exact Windows path to that host's `ModOrganizer.exe`, and Windows paths
to the request marker and report. For Linux paths exposed by Wine's Z: drive,
prefix the absolute paths with `Z:`. The observer refuses a pre-existing Preview
and only closes a visible Preview belonging to that exact executable. It must
remain alive alongside the frontend until the check completes.

The frontend compares enabled states, invokes the actual menu item, waits for
independent native-window evidence, and verifies that closing the dialog releases
the busy state, restores the Data view and preserves the active profile.

Add `MO2_VERIFY_DATA_PREVIEW_ENTER=1` to activate the selected file with a routed
Enter key instead of the Preview menu. This requires the profile's existing
preview-on-activation preference; the check verifies MO2's corresponding menu
ordering and does not change that preference. It fails if Enter is unhandled.

These checks prove visible native dialog activation and frontend recovery. They
do not prove DDS rendering correctness, every third-party preview extension,
the preference-disabled fallback, or successful executable launch. See
[native launch validation](NATIVE_LAUNCH_VALIDATION.md) for the separate launch
regression and native build requirement.
