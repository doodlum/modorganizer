# Category filter verification — 2026-09-20

MO2's primary Category display cell is distinct from its complete category list.
The bridge reads complete names from `ModList::GroupingRole` (`Qt::UserRole`).
It also reads `FilterList::CriteriaItem` IDs, types, names and recursive children
from the native `filters` QTreeWidget, without changing its selected state.

`Mo2CategoryFilterList` is the shared rendered component. It preserves the
reported hierarchy, provides independent expansion, updates descendant counts
without replacing unchanged rows, clears removed category selections, and resets
transient selection/expansion when switching profile targets. Category work runs
on profile changes rather than each layout event. Selected parent matching
includes descendants, following `ModInfo::categorySet`.

## Evidence

- `--check-category-membership`: secondary membership, And/Or, comma-containing
  names, explicit empty membership, old-bridge fallback, value-equal records,
  parent/grandchild matching and exclusion of unrelated siblings.
- `frontend/tools/check_bridge.py`: 39 passing contract tests, including exact
  nested criterion IDs/types/parent structure and membership validation.
- `MO2_VERIFY_CATEGORY_TREE=1`: the actual component rendered with controlled
  three-level data. Checks expansion without changing selection, nested collapse
  retention, updating counts while retaining selected row identity, narrow fit,
  removed selections and profile reset. Screenshot:
  `artifacts/category-tree-controlled.png`. Final passing log:
  `artifacts/category-tree-accepted.log`.
- `MO2_VERIFY_CATEGORY_UI=1`: real FNV bridge membership and rendered category
  selection, counts, And/Or row identities, Clear and restored list. Latest
  populated run: `artifacts/category-ui-hierarchy-final.log`.

The first component check failed because the extracted ItemsControl subclass had
no theme and its rows measured zero. Explicitly using the ItemsControl style key
fixed it; `category-tree-themed.log` records the corrected rendering check.

The installed FNV binary reports Animations, Poses and Armour as flat siblings.
The frontend follows that actual tree; this run is not proof of native nested
interaction. Nested display and ancestor matching are controlled verification
against the newer source behavior. `category-tree-native-readback.json` records
what the installed host actually returned.

The native test used two newly named, disabled, disposable mods. They were removed
through guarded cleanup and the native All Mods / Refresh action. The restoration
record confirms the original 14 mod and 11 plugin names, activation states and
priorities, unchanged profile, and absence of the disposable mods:
`artifacts/category-tree-restoration.json`. Raw enumeration order is not treated
as load order.

## Remaining gates

The FNV bridge matches current source; Skyrim deployment remains pending.
Native special/content criteria are not implemented by this category component. Selection and matching still use category names, so
ambiguous duplicate names and renaming a selected category need an identity-based
contract before full fidelity can be accepted. Physical pointer/keyboard behavior,
nested native-host interaction and full-page performance review remain open.

Final Release build: eight warnings, zero errors. Contract tests, controlled
membership and rendered component checks pass; diff hygiene is clean.


## Inverted category selection

The component now cycles inactive → included → inverted → inactive. Right-click
and Shift+Space reverse the cycle, following FilterList::CriteriaItem and its event
filter. Inversion is applied per criterion before And/Or, as in
ModListSortProxy::criteriaMatchMod; exclusion does not unconditionally override Or.
The summary shows `Not <category>` and Clear removes both signs of selection.
Reset and category removal also clear inverted selections.

Controlled membership checks cover descendant exclusion, mixed And/Or,
contradictory criteria, and unassigned mods. The rendered check exercises the real
button click method, routed keyboard and right-pointer events, refresh retention,
removed filters and profile reset. This is routed-event evidence, not a physical
Wayland keyboard/pointer acceptance claim.

The live FNV run in `artifacts/category-ui-inverted.log` checks actual row identities
for inverse-only and mixed And/Or, summary visibility, Clear, and restoration.
Two disabled disposable mods supplied distinct native memberships. Both were
removed; `category-inverse-restoration.json` confirms original mod/plugin names,
activation states, priorities and profile. No bridge changes/restart were required.

Final routed-input/component run: `artifacts/category-tree-inverted-final.log`,
including forward Space, reversed Shift+Space and right-pointer cycling. Final
Release build: eight warnings, zero errors; membership and diff hygiene pass.

## Native criterion identities and visible status/content filters — 2026-09-20

The current component keys selection by native `(type, id)` and preserves it when
a label is renamed. Equal numeric IDs across types and duplicate labels remain
independent. `category-tree-native-visible-ready.log` verifies those cases plus
disabled loading counts and retained component behavior. Legacy hosts without a
native tree still use local name-based membership with stable fallback IDs.

The complete native tree is now visible, with counts and matching sets supplied
by MO2. See [native filter validation](NATIVE_FILTER_QUERY_VALIDATION.md) for the
live 24-criterion check, cache lifecycle tests, separator fixes, state preservation
and remaining physical-input/Skyrim acceptance limits. The subsequent
`category-tree-continuity.log` verifies retained counts, row identity and selection
while controls are disabled during refresh, followed by the new counts. Live
mutation continuity and native separator behavior are covered in the same native
filter validation document.

## Current two-game verification — 2026-09-20

The populated Skyrim installation now runs the current bridge. The expanded
sidebar round trip checks every unfiltered mod identity and the native criteria
on FNV, Skyrim and FNV again, together with Plugins and Data. It also verifies
selection of the running Skyrim installation when an older installation is
registered first. Evidence and native state preservation are recorded in
[native filter validation](NATIVE_FILTER_QUERY_VALIDATION.md#current-bridge-on-fnv-and-skyrim--2026-09-20).
