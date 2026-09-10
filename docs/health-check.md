# Health check

A port of Vortex's health check feature. It checks your enabled mods against the
file-level requirements their authors declared on Nexus Mods, and surfaces
anything missing, out of date, disabled or waiting in the downloads folder.

## What you see

**Toolbar button.** A clipboard-and-pulse icon next to the notifications button.
It carries a coloured dot when there are unresolved issues, coloured by the worst
severity present — the same three bands Vortex uses
(`components/menu_badge/HealthCheckMenuBadge.tsx:11-27`).

**Pop-out panel.** Clicking the button opens a panel under it. Vortex has a
full page for this; MO2 has a pop-out, but the contents are the same: the
Active/Hidden split, one row per source file per category, and a per-category
action — 1-click install, Pick mod install, Enable this version, or Install
(downloaded).

**Mod list flag.** Mods with an unresolved issue get an emblem in the Flags
column. MO2-only; Vortex has no mod-list equivalent.

**Notifications.** Issues count towards MO2's notification button and are listed
in the notifications dialog alongside plugin diagnoses.

## When it runs

Once at startup, and then after mods are installed, enabled, disabled or
removed, and when downloads change — debounced by 500ms, as Vortex debounces the
same events (`api/triggers.ts:60-126`). Refresh in the panel forces a run.

A run already in flight is never interrupted. Requests arriving during one
coalesce into a single rerun fired 500ms after it settles, and a run that
exceeds 30 seconds is abandoned and reported. All three behaviours are ported
from `core/HealthCheckRegistry.ts:147-325`.

The check needs Nexus credentials; without them it reports a clean pass rather
than an error, as Vortex does (`checks/fileRequirementsCheck.ts:75-82`).
Credentials are re-read at the start of every run, so signing in between runs is
picked up without restarting.

## Settings

Settings live behind the panel's Settings button. Everything is stored under
`[HealthCheck]` in the instance ini.

| Key | Default | Meaning |
| --- | --- | --- |
| `enabled` | `true` | Master switch for the whole feature |
| `fileRequirementsEnabled` | `true` | The file-level requirements check |
| `autoRun` | `true` | **MO2 only.** Re-check when mods or downloads change |
| `notifications` | `true` | **MO2 only.** Count issues towards notifications |
| `modListIndicator` | `true` | **MO2 only.** Flag affected mods in the mod list |
| `suppressSelfRequirement` | `false` | **MO2 only.** Treat "requires Mod Organizer 2" as satisfied |

The MO2-only options are additive: turning them all off leaves the check
behaving exactly as Vortex does. `suppressSelfRequirement` defaults to off for
that reason — Vortex suppresses requirements on *its* own Nexus listing
(`mapRequirementsReport.ts:211-214`) but not on MO2's, so matching it means
leaving MO2's unsuppressed.

## Theming

The panel is styled by whatever MO2 stylesheet is active; the defaults are
derived from the palette, so an unstyled theme still reads correctly in both
light and dark. Themes can target these object names:

```
#healthCheckPanel            #healthCheckRow            #healthCheckRowTitle
#healthCheckHeader           #healthCheckRowSummary     #healthCheckRowDetail
#healthCheckTitle            #healthCheckRowAction      #healthCheckRowDetails
#healthCheckSubtitle         #healthCheckRowHide        #healthCheckEmpty
#healthCheckLastUpdated      #healthCheckEmptyTitle     #healthCheckEmptyMessage
#healthCheckTabs             #healthCheckTabActive      #healthCheckTabHidden
#healthCheckInstallAll       #healthCheckRefresh        #healthCheckSettings
#healthCheckDetailCard       #healthCheckDetailTitle    #healthCheckBranchAction
#healthCheckSeverityAccent
```

Rows carry a `severity` property (`suggestion`, `warning`, `error`) and a
`category` property (`download`, `download-replace`, `install-uninstalled`,
`toggle`, `or`); detail cards carry a `kind` property. The severity stripe is a
painted widget rather than a filled frame, because a stylesheet takes precedence
over a widget palette and the stripe would otherwise disappear under any theme;
its colour is a Q_PROPERTY so themes can still set it:

```css
QFrame#healthCheckRow[severity="warning"] { background-color: #2b2b2b; }

#healthCheckSeverityAccent[severity="warning"] {
    qproperty-accentColor: #e8a317;
}
```

## Verification

`tools/healthcheck-parity/` runs MO2's implementation and Vortex's own code over
the same inputs and diffs the results — 36 fixtures, 9 of them captured from the
live Nexus API, covering the resolver, the requirement-kind mapping and the
listing rows. A mutation self-test proves the suite can actually fail. See that
directory's README.

## What is not ported

Vortex's health check registers two checks. This ports the file-level
requirements check (`checks/fileRequirementsCheck.ts`). The Nexus *mod*-level
requirements check (`checks/modRequirementsCheck.ts`) is not ported: it is the
lower-confidence "suggestion" band, it depends on Vortex's GraphQL
`nexusGetModRequirements` extension API, and Vortex itself gates it behind a
separate setting. The architecture has room for it — severity already carries a
`Suggestion` band and the listing groups by check id.

Also not ported, and deliberately so:

- **Premium gating.** Vortex shows a premium upsell modal before a 1-click
  install for non-premium users (`components/premium_modal/PremiumModal.tsx`).
  MO2 hands downloads to its own download manager, which already handles the
  free/premium distinction.
- **Feedback prompts.** Vortex's per-issue "was this helpful?" controls
  (`components/entry_actions/EntryActions.tsx`) feed its analytics pipeline.
- **Analytics.** The whole tracking layer (`utils/shared/tracking.ts`,
  `hooks/healthCheckTracker.ts`). The vocabulary it defines — `issue_id`,
  `resolution_type`, the issue-type bands — *is* ported, because the listing
  layer is built on it and the parity suite compares it.

## Notes on Vortex

Observations from reading the source. Nothing here was changed in Vortex.

1. **External requirements are collected but never shown.** `modRequirementsCheck.ts:444-454`
   skips every requirement with `externalRequirement` set, with a comment saying
   the listing and detail UI for them is left in place. The locale file still
   ships the strings (`listing.external_mod_install`,
   `detail.item.open_external_mod_page`), and `ModRequirementsContent.tsx:62-72`
   still branches on `mod.externalRequirement`, so there is dead UI behind a
   condition that cannot currently be true.

2. **DLC requirements are gathered, stored, and never rendered.**
   `modRequirementsCheck.ts:486-494` populates `dlcRequirements`, and
   `buildDetailsString` writes them into the result details, but the comment at
   `:500-501` notes no UI renders them, so they are excluded from the issue
   count. A user whose only unmet requirement is a missing DLC sees a clean pass.

3. **`getSummary()` counts a status that cannot occur.** `HealthCheckRegistry.ts:472-487`
   tallies `passed/failed/warning/error`, but `IHealthCheckResult["status"]`
   allows `failed` and no check ever produces it, so that bucket is always zero.

4. **A timed-out check's result is dropped, but the check keeps running.**
   `HealthCheckRegistry.ts:206-231` aborts and reports, then releases the slot
   only when the body finally settles. That is deliberate and documented, but it
   means a check that ignores its `AbortSignal` can hold the slot indefinitely;
   `runFileLevelRequirements` only polls the signal between stages, so a single
   slow request inside the resolver is not interruptible.
