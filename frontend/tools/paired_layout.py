#!/usr/bin/env python3
"""Build the isolated paired layout the list checks need.

Several checks refuse to run without an ``MO2_FRONTEND_LAYOUT`` under the
temporary directory, and then need the mod list and the plugin list side by
side: given a fresh single-panel layout they report the responsive columns
missing and find no mod row attached, which is the layout rather than the
pages.  This writes that layout for one MO2 connection, taken from the
frontend's own saved layout so the endpoint and profile are the ones in use.

    frontend/tools/paired_layout.py [--endpoint SUBSTRING] [--output PATH]
"""
import argparse
import copy
import json
import os
import sys

MODS = "bcde2778-955d-4b57-a14e-85a878b82101"
PLUGINS = "bcde2778-955d-4b57-a14e-85a878b82102"


def saved_layout_path() -> str:
    config = os.environ.get("XDG_CONFIG_HOME") or os.path.expanduser("~/.config")
    return os.path.join(config, "mo2-nexus-frontend", "workspace-layout.json")


def main() -> int:
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--endpoint", default="", help="substring of the bridge directory to pick a connection")
    parser.add_argument("--output", default="")
    # One panel holding both tabs, rather than two panels side by side.
    #
    # MO2_VERIFY_TAB_RESTORE switches between two tabs of one panel and asserts on
    # Panels.Single(); given the paired layout it waits for a panel with two tabs
    # that the fixture can never contain, times out, and reports "Restored tab
    # state did not settle" — which reads as the frontend failing to restore a tab
    # rather than as the layout being the wrong shape for the question.
    parser.add_argument("--tabbed", action="store_true",
                        help="one panel carrying both tabs, for the tab-restoration check")
    options = parser.parse_args()

    source = saved_layout_path()
    if not os.path.exists(source):
        print(f"No saved layout at {source}; open the frontend against the instance once first", file=sys.stderr)
        return 2
    with open(source) as handle:
        layout = json.load(handle)
    connections = [w for w in layout.get("Workspaces", []) if (w.get("Endpoint") or "")]
    chosen = next((w for w in connections if options.endpoint in w["Endpoint"]), None)
    if chosen is None:
        print("No connection matched; the saved layout holds: " +
              ", ".join(w["Endpoint"] for w in connections), file=sys.stderr)
        return 2
    # A tab carries more than its factory — search text, sub-tab, the data
    # directory a file page is on. Copying one of the saved tabs keeps whatever
    # the current layout format has rather than writing out the fields known
    # when this was written, which would fail validation the next time one is
    # added.
    template = chosen["Panels"][0]["Tabs"][0]

    def tab(factory):
        return {**copy.deepcopy(template), "Factory": factory}

    if options.tabbed:
        panels = [{"X": 0, "Y": 0, "Width": 1, "Height": 1, "Selected": 0, "Tabs": [tab(MODS), tab(PLUGINS)]}]
    else:
        panels = [
            {"X": 0, "Y": 0, "Width": 0.5, "Height": 1, "Selected": 0, "Tabs": [tab(MODS)]},
            {"X": 0.5, "Y": 0, "Width": 0.5, "Height": 1, "Selected": 0, "Tabs": [tab(PLUGINS)]},
        ]
    paired = {"Version": 2, "Workspaces": [{
        "Endpoint": chosen["Endpoint"], "ProfilePath": chosen["ProfilePath"],
        "Panels": panels}]}
    default = "verify-tabbed-layout.json" if options.tabbed else "verify-paired-layout.json"
    output = os.path.abspath(options.output or os.path.join(here, "..", "artifacts", default))
    os.makedirs(os.path.dirname(output), exist_ok=True)
    with open(output, "w") as handle:
        json.dump(paired, handle, indent=1)
    print(f"Wrote {output} for {chosen['Endpoint']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
