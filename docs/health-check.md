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
| `showPremiumInfo` | `true` | **MO2 only.** Explain the website step before opening mod pages |
| `simulateFreeAccount` | `false` | Testing aid: report the account as free. Only ever downgrades |

The MO2-only options are additive: turning them all off leaves the check
behaving exactly as Vortex does. `suppressSelfRequirement` defaults to off for
that reason — Vortex suppresses requirements on *its* own Nexus listing
(`mapRequirementsReport.ts:211-214`) but not on MO2's, so matching it means
leaving MO2's unsuppressed.

## Premium and free accounts

Nexus Mods issues direct download links to a mod manager only for Premium
accounts. Everyone else authorises each download in the browser: pressing
**Mod Manager Download** on the file hands the link back through `nxm://`,
and Mod Organizer downloads and installs it as usual. That is a property of
the API, not a policy invented here, and it is why the 1-click actions are
gated.

Free accounts therefore see:

- a standing note at the top of the listing saying install buttons will open
  the mod page;
- a **Premium** pill on any gated button;
- an explanation when such a button is pressed, before anything opens.

Pressing a gated button opens the file's Nexus page with `?tab=files&file_id=…&nmm=1`
— the same URL Vortex builds in
`fileRequirementActions.ts:259-270` (`openFilePage`), including the `nmm=1`
that `show_file` would not give.

The gate lives on the single install path in `MainWindow`, not only in the
button handler, mirroring Vortex putting it inside `downloadFileRequirement`
(`fileRequirementActions.ts:55-58`) so no route can bypass it.

### How the upsell differs from Vortex's

Vortex's modal (`components/premium_modal/PremiumModal.tsx`) opens with the
pitch — *"Skip the website and install instantly."* — lists four membership
benefits, and makes **Unlock 1-click installs** the primary button, with the
route the user can actually take now as the secondary.

This one keeps the same gate and the same two choices, and inverts the
emphasis:

- it opens by explaining *why* the button did not download, in terms of how
  Nexus authorises downloads;
- it states exactly what continuing will do ("opens 2 mod pages in your
  browser, one per file");
- the default button is the free route, because that is what resolves the
  issue the user clicked on;
- Premium appears as **What Premium changes** — two factual lines about
  downloading, the two of Vortex's four that are relevant next to a download
  that was just gated — followed by "Both routes install the same files. The
  free route takes more clicks; it is not otherwise limited.";
- **Read about Premium** is a plainly-labelled third option linking to
  `nexusmods.com/premium`, with no campaign or referral parameters. Vortex tags
  its link for attribution (`PremiumModal.tsx:158-164`).

Vortex also re-checks the membership while its modal is open and runs the
gated action if it changes (`PremiumModal.tsx:75-100`). The equivalent here is
that the account tier is read fresh on every use rather than cached, so a
membership bought mid-session takes effect on the next press without a
restart.

To exercise the free route from a premium account, set
`HealthCheck/simulateFreeAccount=true`. It only ever downgrades, so it cannot
make a free account look premium and slip past the gate.

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
#healthCheckSeverityAccent      #healthCheckPremiumBadge   #healthCheckPremiumBanner
#healthCheckPremiumBannerText   #healthCheckPremiumBannerLink
#healthCheckPremiumDialog       #healthCheckPremiumHeading #healthCheckPremiumDifference
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

#healthCheckPremiumBadge { qproperty-badgeColor: #7a5cff; }
```

The premium badge is painted for the same reason as the severity stripe.

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

4. **Supporters get a 1-click install that cannot work.** `shouldShowPremiumAd`
   is `!isPremium && !isSupporter` (`selectors.ts:38-47`), so a supporter sees
   no badge and no modal, and `ListingRow.tsx:110-115` falls straight through to
   `runQuickInstall()`. But `PremiumModal.tsx:80-81` states plainly that
   "supporters can't download through the client either", and
   `downloadFileRequirement` gates on the same selector
   (`fileRequirementActions.ts:55-58`), so the download is attempted rather than
   redirected. MO2's account model has no supporter tier, so a supporter reads
   as free here and is offered the website route — which works.

5. **A timed-out check's result is dropped, but the check keeps running.**
   `HealthCheckRegistry.ts:206-231` aborts and reports, then releases the slot
   only when the body finally settles. That is deliberate and documented, but it
   means a check that ignores its `AbortSignal` can hold the slot indefinitely;
   `runFileLevelRequirements` only polls the signal between stages, so a single
   slow request inside the resolver is not interruptible.
