# FNV in-game acceptance — 2026-09-12

The alternate frontend launched Fallout: New Vegas through MO2/USVFS and
xNVSE under Proton. The isolated Frontend Test profile reached Doc Mitchell’s
room, displayed the gameplay HUD, accepted movement, and opened MCM with its
three author example menus. Disabling MCM through the frontend removed its ESP
from MO2 and the next running game; restoring it restored the in-game menu.

This verifies the FNV acceptance run. It does not establish full MO2 feature
parity or Skyrim gameplay.

## Enabled, disabled, restored

| Check | Observed result | Local evidence |
| --- | --- | --- |
| Enabled MCM | In-game ESP index `0A`; all three example ESPs loaded | [Console](artifacts/fnv-examples-esp-positive.png) |
| Open mod UI | Mod Configuration opens and lists three example menus | [MCM](artifacts/fnv-mcm-menu-open.png) |
| Disable in frontend | Native mod toggle disables MCM and removes its plugin; 13 plugins remain | [Frontend](artifacts/fnv-mcm-disabled-frontend.png) |
| Next game with MCM disabled | `GetModIndex "The Mod Configuration Menu.esp"` returns `FF`; another example ESP still loads | [Console](artifacts/fnv-mcm-disabled-console.png) |
| Disabled game UI | Mod Configuration is absent from the in-game pause menu | [Pause menu](artifacts/fnv-mcm-disabled-menu.png) |
| Restore MCM | Native mod toggle restores it; 14 plugins are active | [Frontend](artifacts/fnv-mcm-restored-frontend.png) |
| Restore game UI | MCM opens again with all three example menus after reordering | [MCM](artifacts/fnv-mcm-restored-menu.png) |
| Gameplay | HUD visible; forward movement changed the player’s view in Doc Mitchell’s room | [Gameplay](artifacts/fnv-restored-gameplay.png) |

The mod is the original MCM plus its author’s example ESPs, installed with MO2’s
existing installers. The examples register consumers with MCM; without a
registered consumer, MCM’s own script leaves its menu unavailable. No mod script
or XML was patched to manufacture the observed menu.

## Load order

All nine DLCs have `.NAM` files in this game installation (five use uppercase
extensions). FNV can activate such plugins independently of the explicit
plugin list, a behavior also documented by [LOOT](https://loot.readthedocs.io/en/latest/app/changelog.html).
That explained the initial extra nine active plugins and offset between MO2’s
inactive-DLC display and the game. The game’s `.NAM` files were left intact.
The frontend’s Enable selected control explicitly enabled the nine DLCs in the
isolated profile so its declared state agrees with the game.

After restoring MCM, the native plugin row command moved MCM Example Menu.esp
from priority 10 to 11. An independent MO2 snapshot was saved before launching.
The following values were visually read from the running game’s GetModIndex
output and compared with that snapshot. Every plugin was active, and every
MO2 priority/loadOrder equaled the observed game index.

| Plugin | MO2 index (decimal) | Game index (hex) |
| --- | ---: | ---: |
| FalloutNV.esm | 0 | 00 |
| TribalPack.esm | 1 | 01 |
| MercenaryPack.esm | 2 | 02 |
| ClassicPack.esm | 3 | 03 |
| CaravanPack.esm | 4 | 04 |
| DeadMoney.esm | 5 | 05 |
| HonestHearts.esm | 6 | 06 |
| OldWorldBlues.esm | 7 | 07 |
| LonesomeRoad.esm | 8 | 08 |
| GunRunnersArsenal.esm | 9 | 09 |
| MCM Example Menu3.esp | 10 | 0A |
| MCM Example Menu.esp | 11 | 0B |
| MCM Example Menu2.esp | 12 | 0C |
| The Mod Configuration Menu.esp | 13 | 0D |

Evidence: [00–04](artifacts/fnv-order-game-00-04.png),
[05–09](artifacts/fnv-order-game-05-09.png),
[0A–0D](artifacts/fnv-order-game-0a-0d.png),
[host snapshot](artifacts/fnv-reordered-host.json), and
[comparison](artifacts/fnv-game-order-comparison.json).
These are runtime observations, not inferred from a successful process exit.

## Isolation and reproduction

The run used `artifacts/mo2-fnv-host`, its separate Proton prefix, and the
existing MO2 2.5.2 game/installer extensions. The only loaded save was the
previously game-created `mo2-integration-test.fos` in that isolated prefix.
The enabled/disabled/reordered runs did not save over it; its SHA256 remained
`b4d5d1799d6e7e788ac7731aba608ae86b65430905286f778a033f842d523651`.
The game was closed normally with its console quit command. The temporary
controller-disable preference and ineffective intro-movie override were restored
to their original test-profile values. MCM and all 14 plugins remain enabled in
the isolated profile, in the verified order. No Skyrim game was
launched or its profiles modified by these checks.

Verification hooks require the isolated Frontend Test path. Set MO2_SCREENSHOT
and MO2_BRIDGE_DIRECTORY alongside MO2_VERIFY_DISABLE_MOD or
MO2_VERIFY_ENABLE_MOD to invoke the real mod-row action; MO2_VERIFY_ENABLE_FNV_DLCS
uses the native Enable selected button, and MO2_VERIFY_PLUGIN_DOWN uses the
native ordering-row command. These hooks change the isolated profile and are
preparation steps, not substitutes for the in-game checks. Launch separately
with MO2_VERIFY_LAUNCH=NVSE or the normal Run through MO2 button.

Runtime screenshots, downloaded mods, and local snapshots are ignored artifacts
kept in this workspace, not redistributed in the source repository.
