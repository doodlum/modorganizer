# Goal checkpoint — 2026-09-14

The full goal is **not complete**. This checkpoint separates current verification
from recorded evidence; it does not replace the user's requested feature scope.
The chronological [acceptance audit](ACCEPTANCE_AUDIT.md) includes superseded
statements and should be read with their dates and later corrections.

## Open work

- First-time game switching still blocks input for roughly 0.35–0.55 seconds in
  recorded runs. A runtime trace points to first-display styling and layout,
  especially around Plugins. View/column caching has not resolved this.
- Fresh popup login, all-instance account readback, desktop NXM download and
  frontend install/uninstall now pass in one session. Modded FNV gameplay remains
  a separate run; the newly downloaded test mod was not enabled or launched.
- Final screen-by-screen review of the requested UI, keyboard/pointer actions,
  narrow layouts and panel combinations remains necessary before completion.

## Requirement checkpoints

| Requirement | Implementation / evidence | Current conclusion |
| --- | --- | --- |
| MO2 state authority and original extensions | Native bridge and Proton-hosted MO2; original game, installer, tools and preview workflows documented in the acceptance audit. | Implemented with recorded workflow evidence; universal third-party extension compatibility is not claimed. |
| Mods and Plugins in independent panels; FNV/Skyrim switching | Native NMA workspace controls, MO2 adapters and per-profile layouts. [Latest round-trip check](artifacts/toggle-lifecycle-live.log) covers retained pages/columns, filters, scrolling, row identities, conflict colors and unchanged native order. | Current functional checks pass; fluidity remains open. |
| Responsive and stable interaction | Deferred construction, page reuse, cached view resolution, guarded asynchronous reads and lifecycle fixes. [Latest pointer measurement](artifacts/bounded-cache-measurement.json). | Unmet first-display responsiveness requirement. Small repeat-switch measurements are not a general latency guarantee. |
| NMA popup login and shared account | [Fresh real browser SSO](artifacts/current-browser-sso.log) returned through the popup and confirmed native account readback in 3/3 instances. [Popup sizing check](artifacts/login-fit-diagnostic.log). | Fresh login followed by desktop NXM download and frontend install/uninstall passed in one session. |
| System NXM routing and MO2 downloads | [Fresh FNV desktop-handler download/install](artifacts/current-nxm-install-evidence.json), [restoration](artifacts/current-nxm-install-restoration.json), plus [earlier two-game transfers](artifacts/nxm-live-two-game-downloads.json). | Fresh FNV routing, duplicate-download confirmation, completed archive, install and uninstall verified. Skyrim transfer remains earlier evidence. |
| Overwrite operations | Frontend actions use original native create/move/clear/sync workflows. [Operation record](artifacts/overwrite-workflows-check.json) records exact bytes, cancellation and restoration. | Recorded operation evidence; current read/reattachment checks pass. |
| Archive/BSA browsing and extraction | Archives panel and original archive workflows. [Skyrim extraction record](artifacts/skyrim-archive-extraction-check.json) reports 14,031 files with matching paths/bytes and unchanged native state. | Recorded extraction evidence; current selection, action availability and native readback checks pass. |
| External files in Steam game folders | Exact installed Steam/DLC depot manifests distinguish extra physical files. Both user game paths scanned successfully; missing manifests fail without partial classification. | Detection/search/reveal implemented; [current lifecycle check](artifacts/external-files-lifecycle.log) covers stale success/failure and closing/reopening. No modified-file hash check or cleaning claim. |
| Profiles, Tools/pins, Downloads, Logs and native warnings | Game-scoped pages and original MO2 actions. [Tools lifecycle check](artifacts/tools-lifecycle-live.log) and [current rendered Tools page](artifacts/screen-review-tools-fixed.png). Other action checks are recorded in the audit. | Full-width Mods/Profiles/Tools navigation and Tools refresh reviewed at 1280×750; remaining UI/action review is still open. |
| Saves and Data/VFS | Native read/actions with stale-target guards. [Latest file-page suite](artifacts/archive-selection-lifecycle.log) checks selection fixtures, delayed success/failure, reopening and native table readback. | Current read/lifecycle checks pass; prior mutation evidence is separate. |
| Modded FNV gameplay | Recorded native-run/modded-gameplay evidence, including `fnv-native-run-gameplay.png`, with original extension checks in the audit. | Fresh current-build save load, xNVSE, MCM ESP index and movement verified; MCM pause-menu entry absent in this run. |
| Match the FNV-enabled NMA fork where shared | [Fidelity comparison](NMA_FIDELITY.md), [hashes](artifacts/current-fnv-source-comparison.json), [exact differences](artifacts/current-fnv-shared-components.patch). | Current hashes rechecked: 14 shared files, 6 identical and 8 documented differences, including the StandardButton label-refresh fix. Host-specific redesigns are not claimed identical. |

## Latest functional fixes

Every list carries the menu MO2 puts on it. MO2's lists are worked from their
right-click menus — the controls around a list are a handful of the same actions —
and four pages had no menu at all: **Downloads, Data, Archives and Saves**. Most of
what MO2 can do with a download could not be reached from this frontend, because
almost all of it is on that menu.

The mod and plugin menus are no longer a chosen subset either. A mod's menu is
MO2's own, entry for entry: **All Mods** (install or create beside this row,
collapse and expand, enable and disable everything, check for updates, auto-assign
categories, refresh, export to csv), **Send to...** with MO2's five destinations
including its two conflict ends, the update actions, **Enable/Disable selected**,
Rename/Reinstall/Remove/Create Backup, the endorsement and tracking entries,
**Visit on Nexus** and the uploader's profile, and **Information...**. A plugin's
menu gains Enable all, Disable all, MO2's own **Send to...** and **Open Origin
Info...**.

They appear under the conditions MO2 applies, read from what MO2 reports about the
mod — a mod it does not know on Nexus has no Nexus entries here either, Restore
hidden files is there only for a mod that has them, and the two conflict
destinations only for a mod that overwrites or is overwritten. An ordinary mod's
colour actions moved to its **Notes cell**, which is the only place MO2 offers
them; a separator keeps them on its row, as MO2 does.

Nothing about what those actions do is reimplemented. A new bridge call opens MO2's
own context menu for the same row, off screen, finds the action by the wording MO2
gave it and triggers it where MO2 built it — so MO2 asks its own questions, owns
its own dialogs, and does the work. The same call can instead **report** that menu,
which is how the check below compares the two.

`MO2_VERIFY_ROW_MENUS` asks MO2 to build its own menu for a row of each of its six
lists and compares it with the frontend's **both ways round** — an entry the frontend
offers that MO2 does not is as much a difference as one it lacks — so this cannot
drift as MO2 changes. It reports MO2's own 33-entry mod menu, 9-entry plugin menu,
13 entries on a download, 7 on a Data file, 1 on an archive and 3 on a save, each
matched against the row MO2 was asked about.

What it turned up on the way: the All Mods submenu is empty until MO2 is told it is
about to be shown, the origin entries on a plugin's menu were drawn greyed out where
MO2 leaves them off entirely, and a table's menu built nothing at all unless a
pointer was over one of its rows.

Two differences are deliberate and are not defects. MO2's Open File, Open Meta File,
Reveal in Explorer and Open in Explorer hand a path to the file handlers inside its
Windows prefix; the frontend opens the same file with the handlers this desk
actually has. And a mod's categories are assigned in MO2's own category editor,
which the mod pane's Edit... opens, rather than from the two category submenus.

### What the checks were saying that was not true

The checks above are the evidence for everything on this page, so a run of them
was audited as a thing in its own right. Four of them were reporting results they
had not established. Each was found by running the suite live against the FNV host
and reading the numbers against what MO2 actually holds, not by reading the code.

**The density check was measuring two pages at once and passing.** Scoped to the
panel rather than to the page in it, and with the panel keeping the view it showed
before, every page came out as its own rows plus the page before it: 24 rows for
the eleven-plugin list after My Mods' thirteen, 32 for the twenty-one archives
after that, 23 for the two saves. Every one of those numbers is wrong and every one
of them passed, because the assertion is row *height* and both lists draw the same
22px row. It now measures the view built for the page the panel is showing, found
by that page's own view model. The counts read 13, 11, 21, 21, 2, 19 and 6 —
matching MO2 — and did so identically across four consecutive runs. Two earlier
attempts at this were verified and rejected: scoping to the panel with a
whole-window fallback, and requiring the row count to hold still. Both produced
correct numbers on one run and wrong ones on the next.

**A run could abort part-way through and still print passes.** The screenshot
path's gate waited on `ModsPage`/`PluginsPage`, which resolve to whatever page is
in an open tab, so any check that navigates — `MO2_VERIFY_QT_WIDGETS` walks all
five tabs by design — left it waiting for a page no longer open. It threw on the
dispatcher and took the process down, discarding every check queued behind it
while the ones before it had already printed `PASS`. The gate now reads MO2's own
tables when a page is not open, and reports rather than throws.

**Fixing that revealed the checks had only been finishing by accident.** The ten
seconds that gate spent timing out was what let the navigating checks finish before
`Shutdown`. With the gate no longer stalling, the app shut down mid-check and the
widget check printed *nothing at all* — a clean exit, no failures, and nothing
looked at. Checks started from the window opening now say so before it opens, and
the screenshot path waits for them.

**The Data tab's "from archives" box was never exercised.** Held only to "did not
add rows", it passed on a tree that went from 52 rows to 52. It is now held to the
number of rows actually served out of an archive, and descends into the folders
MO2 lists a BSA's contents under, since the Data root has none. On this instance
it reports honestly that it found none to take away — MO2 lets none of this
profile's 21 archives be ticked, so there is nothing for the box to remove. **This
widget is drawn and unexercised, and is not claimed as tested.**

**The screenshots were of windows the app never showed.** The gate passed on a
list's first row, so `audit-final.png` recorded a Plugins panel holding five of
eleven plugins. It now waits for the counts to stop moving, and the same capture
holds all eleven.

**The three list filters were passing on a result they could not distinguish from
a broken filter.** Each was typed with `zzzzzz` and checked only for the row count
going down, so "the Data filter narrowed 52 rows to 0" was a pass — and a filter
that threw away every row, matches included, produces exactly that line. They are
now typed with text taken off a row that is actually listed, and held to both
halves of what a filter does. Where the matching is the page's own, the surviving
count is predicted and compared: the Data filter keeps 3 of 52 rows for `Conf`,
the downloads filter 2 of 6 for `1_MC`, each checked against the rows MO2 holds.
Where it is the native search box's, the count is not predicted but the row the
text was taken from must still be drawn: 11 plugins to 1 for `Fallo`, with
`FalloutNV.esm` still there.

That was confirmed by breaking the product on purpose. With the Data filter made
to drop every row, the new assertion fails — `left 0 of 52 rows for "Conf", where
3 row(s) carry it` — while the old one passes it and prints the same words it
printed for the working filter. A second control, the filter made to do nothing at
all, is caught by both. The checks are falsifiable, and were confirmed so against
the live host rather than argued from the code.

**Preset fit was measuring whichever pages the panel happened to be holding.** MO2's
layout puts five tabs in the right-hand panel and the panel keeps the view built for
each, so walking the tabs measured all of them together. Nothing showed this until
the number of widgets examined was printed, and then it moved between runs of the
same build: Archives 24 one run and 3 the next, Plugins 15 and 26. Every one of
those runs passed — a page measured with three of its widgets present has no clipped
ones either. It now measures the view built for the selected tab's own page, and
waits for the page to stop changing shape first, since Data rebuilds its widgets
whenever it renders. Four consecutive runs report the same 43, 26, 22, 24, 3, 15.
Those counts are *higher* than the unstable ones, because what it used to measure
were pages only part-way built.

**It was only looking at one edge, and at half the things on the page.** The test
was `box.Right` and `box.Left` — nothing vertical — and MO2's layout is the narrow
one, where what a panel does to the row of widgets under a list is push it off the
bottom. The comment said it covered "the captions beside them" while the code listed
only controls that can be clicked, so MO2's `Profile`, `Active:` and `Filter` labels
were never measured. Both are fixed; captions inside a table stay out, because
trimming to an ellipsis is a cell fitting its column. Confirmed by pushing the mod
pane's filter bar 260px down: `ModsDisplayCategoriesButton sits at 770–794 of the
panel's 673`, along with the label beside it. The old check passed that — a whole row
of MO2's widgets off the bottom of the panel, reported as fitting.

**"Readable ink" was a restatement, not a measurement.** The separator colour check
compared the drawn foreground against the same `Mo2Density.Ink` the view calls, which
a rule that picked an unreadable colour satisfies just as well. The contrast is now
computed in the check, independently, and held to WCAG AA: `#b91c1c` carries its name
at 6.5:1 and `#e8d44d` at 14.0:1. Two colours are used rather than one, either side
of the luminance line, so a bar painted the same constant every time and an ink that
never follows the colour are both caught. With `Ink` forced to always return white,
the new assertions fail — `drawn at 1.5:1, under the 4.5:1 it must read at`, and
`both colours drew their name in the same White` — where the old one passes, because
breaking the rule breaks both sides of its own comparison.

**The bridge can now read MO2's menu for all six lists.** `readFileMenu` reaches
MO2's `downloadView`, `dataTree`, `bsaList` and `savegameList` through the same
selection and off-screen menu capture the mod and plugin menus already used —
`_list_menu` was generic, and only the row selection was not. Read live, MO2 offers
16 entries on a download, 5 on a Data row, `Extract...` on an archive and three on a
save. Two things had to be right for that to work, and neither was guessable from
the code: the four models are `DownloadList`, `FileTreeModel` and QTreeWidget's own
`QTreeModel` twice, and **MO2 fills `bsaList` and `savegameList` only when their tab
is shown** — both answered with nothing at all until the bridge brings their tab to
the front first, and puts back the tab that was there. An empty list has no menu, so
those two would have compared against nothing and agreed.

Each list is held to the model MO2 is expected to be using. Passing the model's own
name back through the check that validates it would have agreed with any list at
all, including one this was never written for.

**Comparing Downloads against MO2 found a menu entry with nothing behind it.** The
frontend offered a download **Visit the uploader's profile**. The hosted MO2 has no
such action: `ModOrganizer.exe` is 2.5.2 and carries **no occurrence** of
`issueVisitUploaderProfile` or of the wording, so the entry was drawn over a slot
that does not exist and could never have done anything. It came from
`src/downloadlistview.cpp`, where MO2 adds it beside `Visit on Nexus` in the same
branch — but that is the MO2 this repo's source builds, not the one running.

The fix is not to delete the entry, which would be wrong against a newer MO2, but to
stop guessing: the snapshot now carries **`downloadActions`**, the subset of MO2's
row slots this build actually has, read off the download view's own metaobject. The
hosted MO2 answers with eight — cancel, delete, hide, pause, queryInfo, resume,
unhide, visitOnNexus — and the menu offers what MO2 says it has. Downloads now
matches MO2's own menu entry for entry. An action unknown before MO2 has answered is
treated as offerable, so a menu built before the first snapshot is not empty.

**The Data menu was seven entries short of MO2's, and now matches it.** Four were
added, and each is MO2's own rather than a reimplementation:

- **Refresh** — the page already had the action and only the button to reach it by.
- **Open** — MO2 ends at a path and hands it to the handlers inside its prefix; this
  opens the same file with the handlers this desk has, which is the difference
  already recorded for a download's Open File. It reuses the `revealPath` the bridge
  already answered Reveal in Explorer with, opening the file where Reveal opens the
  folder.
- **Add as Executable** — triggered where MO2 built it, through `fileMenuAction`. MO2
  keeps the executables, and the Tools page lists what MO2 then holds.

Three are deliberately not carried, and the check names each with its reason rather
than dropping the comparison:

- **Expand All** and **Collapse All** have nothing to act on. MO2's tree expands in
  place; this page walks into a folder and shows one at a time.
- **Open with VFS** runs a Windows program inside the prefix from a file list.
- **Save Tree to Text File...** writes MO2's own tree rather than the folder this
  page is showing, so its output would not describe what is on screen.

Everything else still compares both ways, so an entry that stopped being offered, or
one MO2 gained, is still a difference. Downloads and Data now match MO2's own menus
entry for entry — 13 and 7.

Data has to be asked about a file. MO2 gives a folder `Expand All` and `Collapse
All` where a file carries the entries above, so a comparison that landed on a
folder — which the first one did, on `Config` — compares two different menus and
reports nonsense.

**The recorded entries and the running MO2 are not the same ground truth, which is
how the first difference turned up.** `src/downloadlistview.cpp` adds `Visit on
Nexus` and `Visit the uploader's profile` in the same branch, so a list written from
this repo's MO2 source carries both. The hosted build is older and offers only the
first. Entries taken from the source describe the MO2 that source builds; the check
now asks **the MO2 that is actually running**, which is the one the frontend has to
match.

**Archives and Saves are compared against MO2 too, and that is now all six.** They
were the last two held to entries written down in the check, which cannot notice MO2
gaining an entry nor this frontend offering one MO2 does not. Neither list names a
row the way its own first column reads: MO2's `bsaList` is a tree of mods with their
archives beneath, so an archive is a *child* row under the mod that supplies it; and
MO2 labels a save with a composite caption — character, slot and place — which **both
of this profile's saves carry, identically**, so a save cannot be named by its caption
at all. The file is in the list's second column, which is what the Saves page names a
row by and what MO2's own save actions already match on.

The bridge now walks into the archive tree and names a save by its file, opens the
branch a row sits in and scrolls to it — the menu is opened at the rectangle the view
draws that row at, and a row with no rectangle opens the list's menu on nothing.
`readFileRows` answers with those same names, so the frontend and MO2 are talking
about the same row.

**The check settles the row first and opens both menus on that one row.** It used to
read whichever row each side happened to land on: the frontend's menu from the first
row its table could select, MO2's from the first of the frontend's rows MO2 also
holds. Those are not necessarily the same row, and a menu belongs to the row it was
built for — MO2 offers a folder different entries from a file. It now picks the row
both sides hold, then asks each side for that row's menu, and reports nothing rather
than comparing a menu it could not build for the right row.

**Turning the comparison on found an entry MO2 does not have.** The Archives row menu
offered **Browse...** beside Extract, on the written-down grounds that MO2 opens an
archive when its row is double-clicked. MO2 does not: `bsaList` has no activation
handler at all — `mainwindow.h` declares `on_bsaList_customContextMenuRequested` and
`on_bsaList_itemChanged` and nothing else — so the entry rested on a behaviour MO2
does not have. Looking inside an archive is MO2's Data preview, which this page's own
Browse action reaches; the entry is off the menu and the action is unchanged. MO2's
archive menu is one entry, and so is this one.

The comparison is falsifiable in both directions on those two pages, confirmed against
the live host rather than argued from the code. The extra entry above is the one
direction, caught on the first run that compared Archives at all. For the other,
`Fix enabled mods...` was taken off the Saves menu on purpose: the check fails with
`Saves (mo2-integration-test.fos)'s menu is missing MO2's Fix enabled mods...`, and
passes again with it back.

It now reports all six against MO2 itself: the 33-entry mod menu, the 9-entry plugin
menu, 13 on a download, 7 on a Data file, 1 on an archive and 3 on a save.

### Widgets that are drawn but not exercised

This instance cannot exercise three of them, and none is claimed as tested. They
now say so in their own words rather than borrowing the wording of a widget that
was driven:

- **Data's "from archives" box** — MO2 lets none of this profile's 21 archives be
  ticked, so nothing in the Data tree is served out of one and the box has nothing
  to take away. The check descends into the folders MO2 lists a BSA's contents
  under before saying so.
- **An archive's tick** — with archive management off, MO2 offers none tickable,
  and the frontend drawing them all disabled is MO2's answer rather than a fault.
  The round trip through MO2 runs whenever one *is* tickable.
- **The hidden-downloads box** — MO2 holds no hidden download, so there is none to
  bring back. The count is still checked against MO2's.

Exercising these needs an instance with archive management on and a hidden
download, which is a change to MO2's own state and has not been made here.

MO2's widgets do what MO2 does with them. Finding them and drawing them whole is
not the same as carrying their behaviour, so `MO2_VERIFY_WIDGET_BEHAVIOUR` works
every one the way a user does — types in the filters, ticks the categories, picks
from the boxes, presses the keys, opens the menus — and reads back what the list
did. It drives the real thing where the behaviour is MO2's: it switches a mod in
MO2 and switches it back, and sends a mod to the top of MO2's order and returns
the order it found.

What that turned up, and what is fixed: Query Metadata did nothing (the single
button arrived after 2.5.2, so the hosted build is asked download by download for
the ones whose .meta names no mod); Edit... hid the filter list rather than
opening MO2's category editor; and the tick beside an archive moved without MO2
moving with it. The lists also do what MO2's do without a menu — a mod opens on
double-click, the space bar switches it, Delete takes it out through the same
confirmation, a download installs on double-click, and an archive dropped on the
mod list is installed. A mod's menu gains MO2's Send to top/bottom, Open in
Explorer and Rename; a plugin's gains Open origin in Explorer.

The lists are drawn at MO2's density. The borrowed theme is built for a handful
of large cards — 46px a row, 24px of padding in every cell, 14px text — so the
window that shows MO2's forty mods showed eleven, and no amount of matching
captions made a page read like the tab it stands in for. One set of numbers
(`Mo2Density`) now says how tall a list line is, and every table takes it. Three
of the four places a row's height was decided were doing it behind the others'
backs: the deferred placeholder pinned 40, the highlight pass wrote 40 onto every
row it touched as a local value, and the theme sets its own inside rules that
name the table as well as the row, which is why the file pages kept 32px rows
after the shared style was in place. `MO2_VERIFY_DENSITY` measures the rows each
page actually draws, in the panel it draws them in.

The mod pane is MO2's own (`src/mainwindow.ui`): the profile box and the buttons
above the list, the Filters group beside it with Clear and Edit, And/Or and what
to do with separators, and under the list the button that shows and hides that
group, what the list is narrowed to, the grouping box and the filter field.
Downloads gains the two widgets MO2 puts under its own list.
`MO2_VERIFY_QT_WIDGETS` covers them, and drives the filter button rather than
only looking it up.

Separator colours work. They never left MO2 — nothing in the snapshot carried
one — so every separator was the same grey bar. The bridge reads the colour off
the row MO2 draws, the bar is painted in it with ink that reads against it, and a
mod's Notes cell takes the colour MO2 gives that cell. Setting one goes through
MO2's own "Select Color...", so MO2 writes the file and refreshes its list.
`MO2_VERIFY_SEPARATOR_COLOR` drives that round trip and puts the separator back.

`MO2_VERIFY_PRESET_FIT` applies MO2's own panel layout — the mod list left, its
five tabs right — and fails on a widget that leaves its panel, is given less room
than it asked for, or leaves its list's name column too narrow to read. It found
Data spending its width on Mod, Type, Size and Date; those are dropped from the
right as the panel narrows, and the mod and plugin columns give way in turn until
the name has 180px.

Archives now forgets an explicitly cleared selection and a selected archive that
disappears on refresh. Filtering still remembers a temporarily hidden selection
by archive name **and owning mod**. Reappearing files do not silently regain
selection, and Browse/Extract follows the visible selection. The current live
file-page suite passed without changing actual archives or native activation.

My Mods and Plugins now draw their rows through the same shared metrics, and the
live check measures both tables in place rather than only comparing declarations:
both start 24px into their page, with the status column 16px and the name column
65px inside the table. My Mods no longer adds a selection group of its own beside
the native one — that second group was the source of the duplicated and unresponsive
buttons — and the native Deselect empties the table's own selection.

Three mismatches survived the first pass at sharing and are fixed: the overflow
dots were the same glyph at 24px on one page and 16 on the other, because one
button was built by hand and took the icon's default size; the search control was
32px tall on My Mods and 24 on Plugins, because the original markup wraps it in a
padded box that only that page kept; and a selected row was filled on Plugins but
not on My Mods, because the highlight pass wrote Transparent into a mod row's
background and a local value beats the shared `:selected` style. The checks now
measure the buttons, the toolbar layout and what a selected row is painted, and
cover a click on a row and a selection surviving a refresh.

My Mods and Plugins are built from the same parts rather than merely behaving
alike: one rail and scrollbar connection, one toolbar and pill, one selection
group, one highlight wiring. Matching them behaviour by behaviour had left two
implementations of each — Plugins carried its own thirty-line copy of the rail's
scroll connection that had lost the line hiding the table's own scrollbar, so that
list showed a scrollbar and a rail and the two pages scrolled differently.
`MO2_VERIFY_SHARED_LISTS=1` compares what the pages are made of, including what
each toolbar holds and in what order, and drives the rail in both directions.

Selecting rows behaves the same in My Mods and Plugins. Plugins had no selection
group at all — no count, no deselect, and no multi-row Enable/Disable since an
earlier tidy-up removed those buttons — and marked nothing in the other table,
while selecting mods has always marked the plugins they install. Both now show the
original app's group and mark each other. `MO2_VERIFY_SELECTION_PARITY=1` drives
both selection models and compares what the two pages do, including the colour a
selected, hovered and cross-marked row takes.

External Files, Data and Overwrite are built from one set of parts. Data drew an
arrow in front of folder names and styled its table for mod lists; Overwrite wrote
its own sizes in B and KB only, drew five labelled buttons in the body and offered
no column control or search. `MO2_VERIFY_FOLDER_PAGES=1` builds all three over the
same fixture folder and compares them as rendered, down to where the icon and the
name sit inside the shared cell. The header separator was NMA's `Divider`, which is
a 4×4 dot for status-bar bullets, so every page had been drawing a stray mark where
a rule belongs.

Dropping a tab on a panel's edge splits that panel, and the split is now checked
against what the workspace can hold — two columns and two rows. Splitting a panel
that is already half of something asked for a third of one or the other and drew
four panels across three columns, which cannot be saved; such a drop is refused
and the page joins the panel it was dropped on as a tab, which is also what the
preview under the pointer now shows. A move puts back a maximised panel first,
since maximising draws a panel over the whole canvas without changing its place in
the layout. `MO2_VERIFY_TAB_DRAG=1` builds the workspace up to four panels and
asserts after every drop — including eight driven 40ms apart, landing while the
previous reflow animates — that the panels still cover it exactly once. The
platform's own pointer path (press, move, release through the OS drag) is not
driven by any check: synthetic X input under this compositor lands on the root
window rather than the application. What is checked is the press that starts a
drag resolving to the right tab, and everything from the drop onwards.

Page headers give way in stages as a panel narrows: words, then the pictogram alone,
then nothing, and only after that do the actions take a second row. The header keeps
its title floor so its width cannot follow its own content (which fed back into the
measure that produced it and aborted Downloads with an infinite layout loop), and it
is pinned left so that floor cannot centre the pictogram off the left of the panel.
`MO2_VERIFY_HEADER_FIT=1` walks three panels through 13 sizes at three scroll
positions; `MO2_VERIFY_ROW_PADDING=1` and `MO2_VERIFY_MODS_SELECTION=1` cover the
tables against a live profile.

Tools now discards reads and errors from a closed activation and performs a fresh
read when reopened. Delayed success/failure cases pass both reopening sequences.
The normal native list and physical Refresh action also render correctly.

Keyboard search now retains focus when Escape or Ctrl+F closes the search box.
Repeated reopen cycles pass the routed regression check and physical keyboard
checks in both live panels, including after returning from Skyrim to FNV.
See [focus regression](artifacts/search-focus-fixed.log) and
[returned FNV screenshot](artifacts/keyboard-review-returned-fnv.png). This does
not complete the remaining keyboard or screen-by-screen review.

External Files now clears retained rows and disables folder actions when closed.
Delayed scan success/failure checks pass for reopening before and after completion.
Fresh Steam ownership scans still report 11 FNV and 2,239 Skyrim SE external entries;
this does not imply managed import/cleanup or modified-original detection.

The login popup fits verified 1280×750 and 480×320 owner sizes, with reachable
Copy/Cancel controls and repeat cancellation/reopening. This used injected waiting
authorization, not a fresh browser login; subsequent native readback confirmed all
three instances remained connected.

Mods presentations now bind to their original profile and ignore other-game
snapshots even while a subscription stays active. They reconcile current records
on return and skip unchanged content revisions. The [current live suite](artifacts/target-presentation-final-check.log)
passes retained-subscription identity, game switching, source/column identity,
filtering, row recycling, conflict highlights and unchanged native activation/order.
This stream-only check does not establish latency; see the bounded cache results below.

Plugins now uses the same profile-bound snapshot policy for the live MO2 order,
while retaining the existing inline component updates. [Provider and live checks](artifacts/plugin-target-check.log)
pass movement boundaries, both retained subscriptions, filtering, row recycling,
highlights and unchanged native order.

A new mounted-workspace experiment with these fixes reduced model construction
from 632 to 152 and measured cold/return/revisit pointer gaps of 436/104/118 ms
([measurement](artifacts/isolated-mounted-measurement.json)); its paired-list
[functional suite](artifacts/isolated-mounted-check.log) also passed. That unbounded prototype was **not enabled**
in the normal build: file/utility pages react to game changes while hidden,
and it lacked cache lifetime/eviction handling. The prototype is preserved as
[an exact patch](artifacts/isolated-mounted-workspace.patch). This led to the bounded implementation below. Cold first display remains open.

The normal build now retains at most three workspace views (active included),
using least-recently-used eviction. Only live profile workspaces whose tabs are
all Mods/Plugins qualify for inactive retention; other workspaces release their
view model and detach on departure. Cache release also runs when the window closes.
[Current checks](artifacts/bounded-cache-check.log) pass cache bounds, identity,
ineligible eviction, detach/rebuild, clear exactly once, the complete paired-list
suite, and actual Tools → Home → Tools eviction/reopening.
[Current measurement](artifacts/bounded-cache-measurement.json) reports
357/127/126 ms cold/return/revisit maximum pointer delivery gaps with a 100 ms
injection cadence. Only 152 mod models and one new Mods/Plugins view each were
created during sampling. This is one run, not a general latency guarantee; cold
first-display responsiveness and broader keyboard/UI review remain open.

Cached-workspace keyboard review: [focus regression](artifacts/cache-focus-check.log)
confirms hiding a retained text box removes its keyboard focus. Physical FNV →
Skyrim → FNV navigation preserved the [tribal query](artifacts/cache-keyboard-returned-tribal.png);
Escape/Ctrl+F then accepted [caravan](artifacts/cache-keyboard-reopened-caravan.png).
Initial physical typing was inconclusive until cycling Control restored text
events; that setup issue is recorded in the audit. Temporary input tracing was
removed. This covers the reviewed Mods search path, not all keyboard interactions.

The [cached resize suite](artifacts/cached-resize-check.log) now verifies returning
to hidden Skyrim after resizing at 900/640/1280 widths: retained body identity,
current canvas/panel bounds and divider hit testing all pass. On that returned
workspace the four-line 48→28 px header transition stays unclipped, hidden headers
retain tabs at 250 px window height and reappear at 750 px. Subsequent filters,
recycled rows, conflict highlights and native activation/order checks pass.
This verifies panel geometry and the tested header behavior, not every control’s
layout or every interaction at narrow widths. Normal workspace restored.

The [live MO2 preset check](artifacts/layout-preset-check.log) passes the actual
toolbar action callbacks: Mods left, five right-side tabs (Plugins, Data, Archives,
Saves, Downloads) each attach visibly, saved layout matches the model, and Undo
restores the original panels/tabs/search/selection both live and on disk. This
used a temporary layout and normal workspace was restored.

FNV launch preflight: the current isolated profile has 11 plugin lines, unlike
the historical 14-plugin gameplay record. Do not reuse old index expectations.
The existing Windows input helper is present, but no bDisable360Controller key
was found in the three profile game INIs; a keyboard-driven fresh run must handle
controller input and any temporary preference restoration explicitly. No new
game launch or INI change was made in this preflight.

Fresh FNV run on the current build: the compact sidebar picker selected NVSE,
the test save loaded, the game reported xNVSE 6.4.8 and MCM ESP index 0A (matching
the current profile), movement changed the view and the HUD appeared. Console
qqq exited normally with code 0. [Restoration record](artifacts/current-gameplay-restoration.json)
confirms only the temporary falloutprefs.ini changed, all profile bytes restored,
all four saved files unchanged and no new saves.

[Pause screenshot](artifacts/current-gameplay-pause.png) has no Mod Configuration
entry. The [current runtime evidence](artifacts/current-gameplay-mcm-evidence.json)
confirms MCM.dll loaded correctly and the MCM ESP is not physically in the game
folder; Author Examples is currently disabled. The missing menu is not yet
explained or marked verified; a bounded next check can temporarily enable the
existing Author Examples via the frontend, repeat the run and restore activation.
This fresh gameplay run remains separate from the combined account/NXM/install
workflow. Launch-verifier setup now supports Home startup and the collapsed
sidebar flyout, instead of requiring visible Mods/Plugins adapters.

MCM menu investigation: native Data readback identifies MCM as the sole origin
of start_menu.xml, includes_StartMenu.xml and MCM.dll. The installed ESP’s script
explicitly returns when ListGetCount MCMMods is zero. The current plugin list
contains only base game/DLCs plus MCM, and Author Examples is disabled
([source/state evidence](artifacts/current-mcm-menu-condition.json)). This makes
the missing entry consistent with having no enabled MCM consumers, as the earlier
acceptance notes also describe. It is not evidence that the menu files failed to
map. No mods/settings were changed in this investigation; opening MCM contents
with a registered consumer is still historical evidence, not a fresh observation.

NXM startup concurrency: `Mo2HostStartup.Connect` now holds an instance-specific
cross-process file lease through connection and checks existing processes after
acquiring it. The old semaphore only coordinated calls within one frontend;
separate browser protocol handlers could otherwise race to launch the same host.
The lock file remains in the frontend configuration directory, while ownership
is released on disposal or process exit. A contending request times out after
90 seconds without starting another host.
[Current subprocess check](artifacts/host-startup-lease-check.log) verifies
exclusion, bounded waiting, independent instances, recovery after killing the
holder, normal release, and trailing-slash normalization. Release build and NXM
routing checks pass. This tests the shared startup boundary, not a fresh pair of
concurrent live Nexus downloads. The normal frontend was restarted with the build;
combined login/download/install coverage and cold-switch latency remain open.

Row-size experiment (reverted): moved Mods/Plugins responsive column fitting
from per-layout ancestor walks to an attached panel SizeChanged subscription,
with detach cleanup and unchanged-width suppression. Live game switches,
900/640/1280 cached resizing, four-line headers, filtering/recycling and native
highlights passed ([check](artifacts/row-size-check.log)). However, the same
100ms-cadence native-pointer probe reported 570.8ms cold / 130.0ms return /
136.4ms revisit gaps ([measurement](artifacts/row-size-measurement.json)), compared
with the earlier 356.6/127.5/126.0ms run. This is not a controlled distribution
or proof of a regression, but provides no evidence of a cold-switch improvement.
The candidate was removed; investigate first-layout styling/measurement further
instead of treating reduced callback counts as a demonstrated latency fix.
In this run the largest gap spans 1043–1614ms after switch start and overlaps
the 80ms My Mods body attachment; the rest is outside that synchronous timing
scope. This points the next trace at the Mods body's subsequent layout rather
than assuming plugin-row construction is the sole cause.

Fresh first-layout trace: [sampled hotspots](artifacts/mods-first-layout-hotspots.json)
cover 6500–7500ms of `/tmp/mo2-mods-current.nettrace`, UI thread 365255. In that
window template application accounts for about 343ms inclusive, styling 322ms,
and native row-presenter measurement 139ms. These overlapping sampled thread
times are not additive CPU measurements or an uninstrumented latency benchmark.

Separator counting fix: removed the per-layout full-mod-list sort/recount.
Counts now refresh with the owning profile's changes; title width responds to
size changes. Cached separators also ignore another profile's data. The live
[interaction check](artifacts/separator-count-check.log) verifies the displayed
count after moving the temporary separator and restoring its priority, plus
Create/Cancel, collapse/expand, routed double-click Rename/Cancel, preserved
collapsed state, and Delete/Cancel. The temporary separators were removed and
all three native order files match their pre-test bytes
([restoration](artifacts/separator-count-restoration.json)). Release build passes.
This removes repeated work; it does not establish that cold-switch latency is
resolved. Normal user layout was restored with the updated build.

Combined account/download/install session: the real browser SSO page opened from
the NMA popup; Authorise returned a credential, and the popup applied it and
confirmed native readback in all three instances. `xdg-open` then handed the
unsigned Premium FNV NXM file link (mod 42507, file 1000001534) to the registered
frontend handler. MO2's duplicate-download confirmation preserved the old file
and created `1_MCM BugFix 2-42507-.7z` (18,141 bytes, SHA256 matching the earlier
transfer). The frontend Downloads page's Install selected button opened the
original installer. A unique `nxmworkflowcheck` name avoided replacing MCM.
The installed 97,874-byte ESP matches the archive exactly; native readback and
[My Mods screenshot](artifacts/current-nxm-installed-mod.png) confirm the mod.
The mod stayed disabled. Its row's delete action opened the frontend confirmation;
accepting removed it. Native order files and all prior downloads are byte-identical
to the pre-test state. The new downloaded archive remains available. This closes
fresh login-through-install coverage, but is not a gameplay test of that archive.

Downloads presentation: byte counts now use NMA's existing
`SizeToStringTypeConverter`, so the 18,141-byte archive displays `18 KB` instead
of `0.0 MB`. The Name column uses remaining panel space after the size/status
columns and native margins. A star-width attempt collapsed in the native table
and was replaced with explicit width updates on view resizing; source changes
clear the previous resize callback. Physical review passed at
[1280×750](artifacts/download-presentation-wide.png),
[640×750](artifacts/download-presentation-narrow.png), and
[restored 1280×750](artifacts/download-presentation-restored.png). Narrow names
truncate with their existing full-name tooltips, while size/status stay visible.
Release build passes. This was a presentation-only review, with no download,
installation, or mod-state actions invoked.

Downloads toolbar fidelity: archive install, selected install and Nexus link now
use native StandardButtons grouped inside NMA's Toolbar beside search, below the
header. The separate plain-button row above the header was removed. At narrow
widths the actions collapse to icons with tooltips. A shared StandardButton bug
was fixed: ShowLabel changes now update the rendered label, and switching
ShowIcon to/from IconOnly also refreshes label visibility. This is the only
StandardButton difference from the pinned FNV reference; the 14-file comparison
and exact patch were refreshed.
Physical review passed at [1280×750](artifacts/download-toolbar-wide.png),
[640×750](artifacts/download-toolbar-narrow.png), and
[restored 1280×750](artifacts/download-toolbar-restored.png), including native
pause/resume labels collapsing and returning. The compact Nexus-link action
opened its [flyout](artifacts/download-toolbar-link-flyout.png); the archive
action opened the KDE archive picker and Cancel returned to the frontend.
No download or install was submitted during this toolbar review. Full Release
build passes with 48 existing warnings. Normal user layout restored afterward.

Nexus link context and validation: the Downloads flyout captures its instance and
profile when opened, closes when that context changes or becomes busy, and clears
its target when closed/detached. Submitting carries the captured target into
`DownloadNexus`, which rejects stale profiles/instances before parsing or sending.
Malformed and wrong-game links now report local validation errors without
discarding the confirmed MO2 connection.
[Current live context check](artifacts/nexus-link-target-check.log) passes against
Frontend Test and Frontend Clone Test: real flyout closure on switching, stale
Nexus/profile/instance rejection, archive-picker continuation guards, local link
validation, busy controls, native missing-archive rejection without disconnecting,
table retention/refresh and unchanged native mod/plugin state in both profiles.
Malformed test links never reach a network download. The verifier was updated
for the current native table/flyout layout and command-error semantics; its older
startup and disconnection assumptions were obsolete. Release build passes and
normal user layout is restored.

Unknown download progress: MO2's public download-manager proxy exposes paths and
completion/pause/failure callbacks, but not its internal total-size/progress
counters; `createMetaFile` does not persist those counters. The frontend therefore
must not present its placeholder zero as a measured percentage. A host-scoped
style now keeps NMA's DownloadBar appearance but uses an indeterminate indicator
for Running status and labels unknown progress `Downloading` or `Paused`.
Completed downloads retain their native checkmark display and do not request an
indeterminate animation. Actual numeric progress telemetry remains unimplemented.
Two temporary file/metadata fixtures verified the
[rendered states](artifacts/download-progress-fixtures.png): the running indicator
changed between captures while the paused indicator stayed still
([comparison](artifacts/download-progress-presentation.json)). This is presentation
evidence, not a real transfer-progress measurement. Both fixtures were removed
and the entire pre-existing download file inventory matches its prior hashes
([restoration](artifacts/download-progress-restoration.json)).

Failed-download status readback: MO2 writes paused=true for both paused and
failed transfers. The bridge now reads the original DownloadList model's
translated Error status, unwrapping proxies and resolving download paths at
snapshot time. Only partial files are marked failed; a completed archive with
the same base name remains completed. The frontend maps this to Failed status
and label, retaining the existing native resume action.
The five download bridge tests pass, including translated error text, proxy
models, pending rows without download paths, and completed/partial name
collisions. Release build passes with one existing warning.
All three hosts exited through native WM_CLOSE and restarted exactly once.
[Live deployment readback](artifacts/download-failure-deployment-check.json)
confirms profile/mod/plugin state unchanged when compared by name, including
priorities and enabled states. All nine existing download records expose the
new field; the legacy instance has no downloads. Native enumeration order
changed after restart, but plugin state did not. The normal frontend was
restarted with this build. A real failed-transfer UI/retry test remains open;
this evidence covers mocked error classification and live healthy snapshots.

Download row retry follow-up: the shared StatusComponent has an optional
canRetryFailed argument (default false); the MO2 provider opts in so failed
rows expose their existing native resume command. Failure state remains Failed.
The status-transition check covers initial failure, running, pause, subsequent
failure and completion, plus unchanged default NMA behavior.
Source review also found an outstanding action mismatch: MO2's cancelDownload
only handles STATE_DOWNLOADING, while the frontend still offers cancel for
paused partial downloads. Native MO2 instead offers Delete for paused/error
rows. Failed-row cancellation was not added. Aligning those existing controls
and testing real failure/retry remain open.

Download row sizing: status now reserves room for the progress bar and row
actions. Below 460px panel width, the downloaded-size column is removed and
the bar shrinks to 60px (overriding the theme's 100px minimum locally).
Widening restores that same size column and the 120px bar. Physical review
passed with a paused file-only fixture at 640px window width with sidebar
expanded and after restoring 1280px: [compact](artifacts/download-retry-controls-narrow.png),
[restored](artifacts/download-retry-controls-restored.png). The fixture and its
metadata were removed; no transfer or download control was invoked.
[Status transition check](artifacts/download-status-check.log) passes and
Release builds pass. The source comparison now covers fifteen shared files,
six identical and nine intentional differences. Normal user layout restored.

Paused-download cancel correction: supersedes the mismatch noted above. The
MO2 StatusComponent opts out of cancellation for inactive jobs while retaining
the new failed-download resume support. Default NMA cancellation remains
unchanged. Toolbar visibility, enablement, selection flags and batch dispatch
now use one file action policy: pause/cancel for running partial files, resume
for paused or failed partial files, no actions for completed files. The
ControlDownload entry point rejects unsupported actions against its latest
known snapshot without discarding the connection. Native MO2 remains the
final authority if a transfer changes after that snapshot.
The status/policy check passes, including failure without a paused metadata
flag, invalid actions and unchanged NMA paused cancellation. Full Release build
passes with 48 existing warnings. A file-only paused fixture verified
[row controls](artifacts/download-actions-paused.png) and
[selected toolbar](artifacts/download-actions-paused-selected.png): resume is
visible, cancel absent. No transfer command was invoked. Fixture removed and
all pre-existing download files match their prior hashes
([restoration](artifacts/download-actions-restoration.json)). Normal workspace
restored with this build. Native Delete plus confirmation for paused/failed
downloads, real failure/retry coverage and numeric progress remain open.

Download deletion: Downloads now exposes a responsive Delete selected action
for completed, paused and failed files. It captures the target profile and
selected paths, rejects active-transfer deletion, and invokes the original
DownloadListView.issueDelete slot after resolving the current native row.
MO2 owns confirmation and Recycle Bin deletion of archive plus metadata.
The frontend marks the operation busy while the modal is open and allows
30 minutes for user input; closing the dialog refreshes the list. It reports
dialog closure rather than falsely reporting success after Cancel.
Six bridge tests pass, including native slot/path routing and missing-row
rejection; the file action/status check and Release build pass. All three
registered hosts received the module and restarted through normal WM_CLOSE;
[readback](artifacts/download-delete-deployment-check.json) confirms unchanged
profiles, mods and plugin state.
Physical tests through the actual frontend toolbar: [completed archive confirmation](artifacts/download-delete-confirmation.png)
Cancel retained both files; reopening and confirming removed archive and
metadata and refreshed the frontend. A separate paused fixture opened
[the native confirmation](artifacts/download-delete-paused-confirmation.png)
and confirming removed the .unfinished file and metadata. All pre-existing
download files retain their original hashes in both
[completed](artifacts/download-delete-restoration.json) and
[paused](artifacts/download-delete-paused-restoration.json) checks. No mod was
installed or enabled. Normal workspace restored. A real failed-transfer
retry/deletion test and numeric progress telemetry remain open.

Real HTTP transfer behavior — local server, no Nexus credentials or external
network: a resumable fixture was started with the actual frontend row Resume
button against an HTTP 404 endpoint. Native MO2 issued four requests, exhausted
its retries, and removed both partial archive and metadata
([evidence](artifacts/transfer-failure-exhaustion.json)). This matches the
current DownloadManager::finishDownload implementation. The Failed row state
is transient during automatic retries; prior mocked state tests must not be
read as proof of a persistent retryable error row.
A second fixture contained the first 65,536 bytes of a valid ZIP. Clicking
Resume sent Range: bytes=65536- to the local server; the completed file exactly
matched its source ([hash/range evidence](artifacts/transfer-range-resume-check.json))
and the frontend showed [Completed](artifacts/transfer-resume-completed.png).
This proves real partial-file resume/completion, not manual retry of a
retry-exhausted record (MO2 removes that record). Both fixtures and the local
HTTP server were removed. Existing downloads and profile/mod/plugin state
are unchanged ([restoration](artifacts/transfer-test-restoration.json)).

Important remaining notification gap: the current Notifications monitor reads
health diagnostics only. A real exhausted download disappears without a
retained failure notification in the frontend. Native onDownloadFailed callbacks
are available through IDownloadManager and should feed a bounded failure
history so this warning survives row removal. Account/health warnings alone
do not satisfy the user's request to include MO2 warnings.
Numeric progress remains unavailable via DownloadList data roles: COL_SIZE
provides localized size text, while DownloadProgressDelegate reads the manager
progress directly. A native bridge accessor is needed for measured progress.

Download failure notifications: the native Downloads bridge subscribes to
onDownloadFailed and onDownloadComplete. It retains up to 50 interrupted
downloads per MO2 host session, keyed by the native callback identifier,
deduplicating retries and removing recovered downloads on completion. It
captures only the filename before MO2 removes the row; URLs, metadata and
full paths are excluded. Frontend-initiated cancellation is suppressed.
Native cancellation outside the frontend may also emit onDownloadFailed, so
the warning accurately says Download interrupted rather than asserting a
network error. The retained warnings join native health reports, making them
available through both Notifications and Health Check. History is in memory
and clears when MO2 restarts; persistent history/dismissal is not implemented.

Eight download bridge tests and 36 bridge contract tests pass, covering
retry deduplication, row removal, completion, bounded retention, cancellation
suppression, filename-only details and health-report merging. All three native
hosts received the modules and restarted with unchanged profile/mod/plugin
state ([deployment](artifacts/download-notification-deployment-check.json)).
A real local HTTP 404 transfer, started through the frontend Resume button,
exhausted four attempts and disappeared from Downloads. One warning survived
in health readback and the actual [Notifications popup](artifacts/download-failure-notification.png),
including its filename and recovery guidance.
[Live check](artifacts/download-notification-live-check.json) confirms one
warning, four requests, removed fixture and unchanged original download hashes.
The local server stopped. A fresh Mark all read verification was inconclusive
because the foreground switched to the separately running reference NMA; no
reference-app settings were intentionally changed. Existing notification-state
tests cover read-state behavior, but this is not fresh physical evidence.
The FNV test host is restarted normally to clear only this session's synthetic
failure history, then the normal frontend workspace is restored.

Download lifecycle cleanup: Mo2DownloadProvider now drops ID mappings when a
file disappears and clears mappings when the profile target changes. Surviving
files retain their IDs across ordinary refreshes, preserving row identity.
The old dictionary accumulated every removed file path for the lifetime of
the page model. The [turnover check](artifacts/download-identities-check.log)
passes 200 file replacements with only the current two mappings retained,
plus profile isolation and complete cleanup on disconnect.
The shared DownloadComponents.StatusComponent now disposes IsPaused along
with its other reactive properties. A tracked observable verifies zero live
subscriptions after both default NMA and MO2 status components are disposed
([status/lifecycle check](artifacts/download-status-check.log)). The first
test draft used an unavailable Subject.HasObservers API; the final check
counts subscriptions directly and passes. Release build passes, and the
shared-source hashes/patch include this disposal fix. No native mod, plugin
or download files were changed. The normal frontend was relaunched. These
fixes bound retained state; they do not establish reduced cold-switch latency.

Lazy row menus (retained): Mods and Plugins no longer construct their context
and action-flyout MenuItems while building each row. Menus populate on first
Opening and reuse their items thereafter; current native state determines
action availability, and existing profile-change handlers update created
actions. Drag grips remain plain Borders using the native row gesture.
The [live menu check](artifacts/lazy-entry-menu-check.log) navigates both lists,
verifies unopened menus contain no items, opens context menus through the
normal ContextRequested route and action flyouts through ShowAt, and confirms
five actions plus item reuse on reopen. No mod/plugin action is executed.
An initial diagnostic used ContextMenu.Open directly, which bypasses Opening
in [Avalonia's source](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/11.3.0/src/Avalonia.Controls/ContextMenu.cs);
the verifier and existing installed-interaction check now use ContextRequested.
Release build passes.

A fresh paired input measurement used the same layout seed, 100ms evdev
pointer cadence and no concurrent builds. Baseline cold/return/revisit maximum
pointer gaps: 468.5/126.5/172.4ms; candidate: 334.0/111.1/172.0ms. Corresponding
maximum queued-input waits were 452.8ms and 316.7ms. Both constructed 11 mod
rows and 14 plugin rows during measurement; the candidate recorded only one
generic entry-menu action construction during sampling.
[Comparison](artifacts/lazy-menu-latency-comparison.json) retains the raw-report
references and scope. An earlier baseline overlapped compilation and is
excluded. This one clean pair is encouraging, not a controlled distribution
or proof that cold-switch fluidity is solved. The measured remaining 334ms
pause still fails the goal's responsiveness requirement. Normal workspace
restored with the lazy-menu build.

Lazy-menu repeat measurement: ran candidate then baseline (reverse of the
first clean pair), without builds alongside either measurement. Candidate
cold/return/revisit pointer gaps were 504.3/162.6/127.4ms; baseline was
602.1/205.9/128.2ms. The [comparison](artifacts/lazy-menu-latency-comparison.json)
now includes both pairs. Both favor lazy menu construction, but the wide
run-to-run variation means this does not establish a stable distribution or
solve fluidity. Candidate cold gaps remain 334.0–504.3ms.
For both candidate runs the maximum cold gap overlaps first My Mods body
attachment: offsets 872.9–1206.9ms and 1287.9–1792.2ms after switch start.
Synchronous body creation/attachment accounts for only 68.6ms and 101.6ms
respectively, pointing remaining work at the ensuing first layout/styles.
Source inspection also shows unused collection header/status controls are
hidden in RefreshMods after activation; investigate whether their initial
attachment contributes before changing layout scheduling again. No production
code changed during this repeat measurement. Normal workspace restored.

Unused collection-tree detachment experiment (reverted): detached the
WritableCollectionPageHeader and Statusbar from the MO2 Mods page before
attachment, keeping native name registrations for upstream bindings. The
[live check](artifacts/trimmed-mods-check.log) confirmed detached controls,
preserved names, visible real header/toolbar and working lazy menus on both
lists. The measured maximum cold pointer gap was 542.5ms versus 566.7ms for
the current lazy-menu baseline. Return/revisit gaps were 166.9/126.9ms versus
125.7/108.8ms. This does not establish a material gain given observed variation.
In this candidate the maximum gap overlapped Plugins body/layout at
1630–2172ms after switch start, while Mods row templates appeared at
1526.5–1613.2ms. Both list layouts remain relevant.
[Experiment evidence](artifacts/trimmed-mods-experiment.json) preserves timing
records. The older ACCEPTANCE_AUDIT also records an unsuccessful detachment
trial (before the current lazy-menu/cache changes); this run reproduces its
lack of a clear benefit on the current implementation. The detachment and
its optional verifier branch were removed. Release rebuilt and normal
workspace restored. Lazy menus and earlier validated changes remain.

Clock-aligned cold-layout diagnosis: the retained lazy-menu build was sampled
on UI thread 419535. EventPipe session UTC start aligns the largest injected
pointer gap to 3581.3718–4105.8517ms (524.4799ms). This instrumented gap is not
a new uninstrumented performance baseline. Evidence:
[trace interval](artifacts/lazy-layout-trace-window.json),
[hotspots](artifacts/lazy-layout-current-hotspots.json),
[leaf callers](artifacts/lazy-layout-leaf-callers.json), and
[style owners](artifacts/lazy-layout-style-owners.json).
Within that interval, inclusive style application samples cover 270.2ms and
measure 268.6ms; these overlap and must not be added. List.Insert leaf stacks
lead through ValueStore.InsertFrame and StyleBase.Attach, identifying style
value attachment rather than application collection insertion. Effective
viewport notification contributes 41.9ms of leaf samples. PublishNext has
multiple callers, including visual-parent changes and size notification;
there is no evidence here that a single subscriber or animation is the cause.
Animatable.OnPropertyChangedCore is ordinary property processing and its
presence must not be interpreted as animation playback cost.
Style-owner samples split between deferred body attachment (104.0ms) and
framework callbacks without an application frame (124.7ms); this trace cannot
identify concrete control instances. Next diagnosis should measure attachment
and template counts by control type, before changing scheduling or removing
styles. Previously rejected header detachment and row pacing remain reverted.
No production code changed in this diagnosis. The normal workspace was
restored with the retained Release build (PID 421489).

External Files revalidation: current Release classification tests passed and
both actual game folders were rescanned. FNV has 11 external entries against
464 owned paths from 17 depots; Skyrim SE has 2,239 against 46 owned paths from
3 depots. The refreshed current-steam-external-files-check.json contains only
counts. Detection remains read-only; NMA clean/import workflow is not complete.

Opt-in control census added to the latency diagnostic: MO2_UI_CONTROL_CENSUS=1
records control types, effective visibility and nonzero bounds immediately
following deferred-body assignment and two seconds later. Normal execution
returns before traversal or timer creation. The first capture has only the
wrapper because templating has not yet run. After layout, My Mods contains
704 controls (459 effectively visible), Plugins 661 (551 effectively visible).
Top types are ContentPresenter, UnifiedIcon, Border and TextBlock. Traversal
cost was 2.89ms and 1.59ms for those bodies. Outer panel counts include those
same bodies and must not be added. Evidence: control-census-summary.json and
ui-latency-menu-control-census.json. No latency benefit is claimed from this
instrumented run. This confirms substantial template expansion after creation;
it does not justify removing native styles or repeating the rejected direct-icon
replacement experiment.

Retained endorsement icon refresh fix: Mo2ModRow now changes its icon Value
only when the endorsement flag changes. IconValue is a reference class and
its ProjektankerIcon conversion creates a new instance; repeated assignment
previously caused UnifiedIcon to rebuild its inner control on every Changed
notification, including first attachment. Opacity and tooltip still update
for current metadata. The live endorsement-refresh-check.log verifies nine
attached icons retain their Content after a diagnostic Changed notification,
then verifies lazy context menus and action flyouts on both lists. The test
invokes existing subscribers without changing MO2 state. Initial attempts
awaiting Refresh notifications were invalid because identical snapshots
intentionally return without publishing Changed; those attempts are not
claimed as passes. Release build passes with the existing dependency warning.
Normal workspace restored. No new cold-latency improvement is claimed; the
full responsiveness requirement remains unfinished.

External Files folder presentation (retained): the Steam-owned path classifier
now feeds a hierarchical tree with folders first, native expander cells,
folder/file/link icons and relative-path tooltips. Folder selection opens that
folder; file/link selection reveals its parent. Search keeps ancestor folders
and explicitly expands the filtered source. Clearing search restores remembered
folder collapse state; switching game clears the previous game's expansion map.
Links remain leaf entries and are never traversed. This follows the reference
NMA ExternalChanges documentation's parent-folder presentation; clean/import
and modified-original-file handling are still missing.

Release build and --check-external-files pass. New tree cases verify exact leaf
preservation, ordering, links, case-insensitive search, ancestors, empty results
and expansion state. The live external-tree-lifecycle.log passes collapse/search/
restore plus all existing delayed success/failure and close/reopen cases. Tests
initially exposed a template overload mistake and stale collapsed state during
source replacement; named width and explicit search ExpandAll resolve those.
A test using only model IsExpanded mutation was corrected to use the tree's
Collapse operation. Final live evidence includes both actual game pages:
external-files-tree-fnv.png (11 entries) and external-files-tree-skyrim.png
(2,239 entries, folder tree in a narrow panel beside Plugins). Normal workspace
restored; no game files or native mod/plugin settings were changed by this work.

External Files rescan selection fix (retained): refreshing previously cleared
the table before the selected path could be restored. The view now keeps the
pending path for the same target while results are unavailable, restores it
only if present in the replacement tree, and clears pending selection on close
or target change. Repeated refresh requests can inherit the pending path;
existing cancellation/target guards still reject stale results. No row-index
fallback is used when a selected file disappears.
Release build passed. external-selection-lifecycle.log verifies same-file
selection and collapsed-folder retention after rescan, no selection after the
file disappears, search expansion/restoration, and delayed result/error handling
across close/reopen. Normal workspace restored with the fix. Native game files
and mod/plugin settings were not changed. The overall goal remains unfinished,
including cold-switch responsiveness and external-file clean/import actions.

External Files copy import (retained): a selected external Data file/folder can
now use Import copy as mod. The frontend rescans exact Steam manifests before
packaging only the selected extra files, strips the Data prefix for MO2's mod
layout, stages a ZIP privately, and publishes it into the active instance's
native Downloads directory. MO2's existing installArchive bridge opens its
installer plugins and dialogs. The archive remains available for reinstall,
including after installer cancellation. Original game files remain in place;
the toolbar tooltip and operation status explicitly say they may still override
the imported mod. This is copy import, not NMA's clean/move workflow. Root-level
files and external links are not eligible as ordinary MO2 Data mods.

The native downloads directory comes from the existing snapshot. Import is
disabled without a current selection/target/connected host, during another
import, and outside Data. Closing or changing target cancels preparation;
InstallArchive retains its own captured-target guard. A ZIP already published
before navigation remains in that original instance's Downloads. Cancellation,
missing files and link substitution discard unpublished staging files.

Release build and --check-external-files pass. Archive tests verify only extra
Data files are included, correct relative paths, byte preservation, originals
retained, root rejection, cancellation and link failure cleanup. Existing live
rescan/tree/lifecycle checks pass in external-import-lifecycle.log.
A physical frontend test selected an isolated Data/textures fixture, clicked
Import, completed native Quick Install as mo2externalimportcheck, and verified
both archive and installed bytes against the retained source. Evidence:
external-import-native-dialog.png and external-import-live-check.json. Native
MO2 remained hidden apart from its installer dialog. The test mod was removed
through MO2; its generated archive and source fixture were removed. All prior
mod name/priority/state and plugin name/priority/active-state fields compare
unchanged in external-import-live-restoration.json. Normal workspace restored.
Cold-switch responsiveness, clean/move handling and full acceptance remain open.

External import Windows path validation (retained): before creating staging or
reading any source bytes, the importer checks all selected Data-relative paths
using case-insensitive comparisons. Duplicate names and a file colliding with
an ancestor directory now fail with an actionable error instead of relying on
MO2 extraction to choose a winner. Both file/folder input orderings are covered.
The complete existing external-file/archive check passes alongside the new
collision cases in external-import-path-check.log; failed cases publish no ZIP
and leave no staging directory. Release build passed with the existing package
warning. Normal frontend relaunched with this guard. No native/game state changed
and no new end-to-end installer claim beyond the preceding verified live import.
The full goal remains active: clean/move support and fluid cold switches are
among the unfinished requirements.

External copy verification foundation (retained): CreateCopy returns the archive
path, game directory and a per-entry source path, ZIP path, actual copied byte
count and SHA-256. Hashes are accumulated from the same buffers written into
the ZIP, not from a later source read. The existing Create wrapper remains for
callers needing only the archive path. ChangedOriginals verifies current bytes,
rejects links through the existing parent-path check, reports missing/unreadable
sources and supports cancellation during hashing.
The External Files import flow now verifies originals asynchronously after the
native installer closes; changed/unavailable source counts are appended to the
operation status. Captured-target and cancellation guards prevent late status
updates after navigation. Originals are still never moved or deleted. This
provides a prerequisite for reviewed cleanup, not a completed clean/restore
workflow; installed-file matching and reversible backup remain to be built.
Release build passes. external-copy-verification-check.log includes all existing
archive/path checks plus exact copy-record verification, unchanged-source
acceptance, same-length edit detection, missing files and cancellation. Tests
use temporary fixtures only; no new live installer claim. Normal frontend
restored with this build. The full goal remains unfinished.

Installed-copy verification (retained and deployed): native installArchive now
returns the installed mod's actual absolutePath alongside its name. Cancellation
returns neither. Mo2LiveProfile.InstallArchive returns a typed optional result;
existing callers can continue ignoring it. Linux accepts the supported Proton
Z: mapping; other Windows drives report verification unavailable rather than
being guessed relative to the frontend directory. Prefix-specific C: mapping
remains unsupported for this check.
External Files compares every copied archive-relative path and SHA-256 against
the returned installed directory. Component matching follows Windows casing,
but ambiguous Linux names and linked paths fail verification. Modified/missing
files are reported; successful imports show the verified file count. Original
files remain untouched. This verifies bytes, not effective conflict precedence
or mod activation, and does not yet authorize/implement cleanup or restore.

Release build, 9 download bridge tests, 36 bridge contract tests and all external
file/archive checks pass. installed-copy-check.log covers matching bytes with
case differences, modified/missing installed files, links and ambiguous names.
Bridge changes were deployed to all three native hosts with normal WM_CLOSE
restarts and unchanged profile/mod/plugin readbacks. The second launch initially
returned already-active because its old launcher still held the prefix lock;
inspection confirmed no native MO2 process, then a nonblocking flock confirmed
release before a single retry. The retry and final host succeeded; no duplicate
native host was launched. Evidence: installed-copy-deployment-check.json and
/tmp/mo2-installed-copy-deploy checks.

A fresh physical External Files import of a unique Data/textures fixture opened
MO2 Quick Install, installed mo2installedcopycheck, and displayed verified-copy
success using the returned native path: installed-copy-live-verified.png.
Independent byte checks match archive, installed file and retained source in
installed-copy-live-check.json. Native removal and fixture/archive cleanup
restored the prior mod/plugin states (installed-copy-live-restoration.json).
Normal frontend restored. The overall goal remains active and incomplete.

Reversible cleanup of verified Data imports (retained): External Files now offers
Move originals to backup after a successful copy/installed-byte verification
and after the imported mod is enabled. The latest verified import is retained
on the live profile so navigating to My Mods and back does not discard it.
This pending import is session-only; backups themselves persist across restarts.
The NMA modal lists the files (first ten plus remaining count), names the enabled
mod, explains that the physical folder affects every profile, and offers Cancel.

Cleanup takes the frontend command lock, refreshes the native snapshot, checks
the same target and enabled mod, rescans Steam ownership and rechecks original
and installed bytes. A backup outside the game directory stores a flushed intent
manifest before any move. Each moved file is verified, installed files are checked
again, and failure/cancellation attempts rollback without overwriting a new game
file. If rollback cannot finish, the journal/remaining backup stays available.
Restore last cleanup discovers manifests from disk, checks backup bytes and game
identity, refuses existing destination files, and supports resuming a partial
restore based on files remaining in the backup. Housekeeping removes only empty
directories and its own manifest, preserving unrelated files added to a backup.
This is a verified-import cleanup workflow, not general root-file removal or a
proof that the imported mod wins all MO2 conflicts. The dialog describes its
cross-profile effect; it does not change any other profile's mod activation.

Release build, external-file/archive/backup tests and live panel lifecycle tests
pass. external-backup-check.log verifies movement, persistent discovery, no-
overwrite conflict/retry, byte-exact restore, modified-source rejection,
cancellation and preservation of unrelated backup content. Physical fixture
verification used mo2cleanupcheck and a unique Data/textures file. Cancel left
the source unchanged and created no backup (external-cleanup-cancel-check.json).
Confirmed cleanup moved only that file; after a full frontend restart, Restore
last cleanup returned identical bytes and retained the installed copy
(external-cleanup-restore-check.json). Screenshots: external-cleanup-confirm.png,
external-cleanup-moved.png, external-cleanup-restart.png and
external-cleanup-restore-confirm.png. The test mod was enabled through the bridge;
UI navigation away/back preserved cleanup eligibility. Native deletion and test
archive/source/empty-backup-root cleanup restored prior mod/plugin state in
external-cleanup-live-restoration.json. Normal frontend restored (PID 449071).

Two runtime observations remain to investigate before full acceptance: a native
launcher cmd.exe console surfaced after the installer and obscured clicks until
the frontend was raised; and injected keyboard text did not reach the My Mods
search box during this run (both evdev and xdotool attempts). This is not yet
proved to be a normal hardware-input failure. The test used bridge activation
of only its fixture to continue, and does not claim physical search/toggle
acceptance. Direct window captures sometimes differed from desktop composition;
Spectacle desktop captures were used for final dialog checks. Cold-switch
responsiveness and overall acceptance remain unfinished.

Native search-input diagnosis: opt-in MO2_VERIFY_SEARCH_INPUT now exercises the
real visible My Mods TextBox using externally injected keyboard events, with a
temporary-layout guard and restoration of search visibility/text. Logs contain
only counts/focus flags and aggregate modifiers, never entered text or key names.
Earlier runs received 13 key events with zero unmodified events and no text.
Disabling the IME for one diagnostic run did not help and was not retained.
X11 initially reported Control held; an XTEST release did not clear the issue.
A native Control press/release while the diagnostic frontend had focus followed
by native text injection passed with 13 unmodified events, 13 TextInput events,
13 changes, and the expected full text. Evidence: search-input-modifiers.log.
This resolves the observed injected search failure without a production input
path or IME change; it does not establish what originally latched Control, or
claim acceptance testing with the user's physical keyboard. The launcher-console
and cold-switch latency observations remain open. Release build passes with the
existing NU1902 warning. Normal workspace restored after the diagnostic.

Hidden Proton launcher (retained and deployed): start-mo2-proton.sh now copies
mo2-frontend-launch.vbs alongside its existing CMD file and invokes wscript.exe
in batch/no-logo mode. The script runs only that fixed CMD name with window style
0 and synchronous wait, then propagates its exit code. Windows-side Qt/environment
setup, native host supervision and prefix lock ownership remain intact. It does
not hide installer/tool windows or kill console/host processes after launch.
Wine's WshShell3_Run implementation supports the requested window style and wait:
https://github.com/wine-mirror/wine/blob/master/dlls/wshom.ocx/shell.c

A real GE-Proton10-4 fixture from a spaced directory observed an attached but
invisible console, the expected host environment, and propagated exit code 7
(hidden-launcher-proton-check.json). All seven launcher contract tests and bash
syntax validation pass (hidden-launcher-contract-check.log); argv tests also
verify both launcher files are copied byte-for-byte. All three native hosts were
closed normally via WM_CLOSE, observed exited, and relaunched only after the
prefix lock became available. Profile, mod states/priorities and plugin states/
priorities/load order were unchanged. Evidence: hidden-launcher-live-check.json,
hidden-launcher-live-check-0.json and hidden-launcher-live-check-1.json. The first
FNV process count was false with a broad cmdline substring matcher; a settled
exact argv[0]/non-zombie inspection confirmed one host. Subsequent deployment
checks use exact argv[0]. The false intermediate count is not used as proof.

The actual FNV Quick Install dialog remained visible under the hidden launcher.
Physical Cancel returned installed=false with unchanged mods and the native main
UI still hidden (hidden-launcher-dialog-check.json). Its temporary archive was
removed. Two old Skyrim consoles were still visible during that dialog check;
after their normal restarts, xdotool found zero visible cmd.exe windows across
the desktop. The normal Release frontend remains running. Cold workspace switch
latency and the wider completion audit remain unfinished.

Style-frame census (diagnostic only): extended the existing opt-in control census
with MO2_UI_STYLE_CENSUS=1. After layout, a read-only reflection probe against the
pinned Avalonia version counts ValueStore frames/entries by control type. It
never reads control text, model data or property values; unavailable internal
members produce an explicit diagnostic result. Both census flags and the latency
probe must be enabled; normal UI operation does no inspection. Release build
passed with the existing NU1902 warning. Real evdev FNV/Skyrim round trip completed
in a temporary workspace (ui-latency-menu-style-census.json), then the normal
workspace was restored.

The result materially narrows the cold-layout investigation: Mods has 93
UnifiedIcons with 15,624 value frames (max 171 per icon), Plugins 89 with 14,924
(max 170). Borders follow at 3,279/3,471 frames. The counts include inactive
selectors, not only effective setters. IconsStyles.axaml defines separate
class-based Value rules for icon aliases; these are the next optimization target,
preserving dynamic class changes and scoped/local value precedence. This is
different from the rejected direct-Projektanker wrapper replacement: the intended
change is to preserve UnifiedIcon and reduce redundant global alias-rule
attachment. style-census-summary.json records alias count and per-panel groups.
Inspection itself took about 10–22ms; this instrumented run is not a fresh
uninstrumented performance baseline. Nested panel roots overlap the list controls
and their counts must not be added. No production styling change or latency
improvement is claimed yet.

Icon alias rule consolidation (retained): the theme's 160 icon-name Value styles
are now one class resolver plus a dynamic style. IconClassAliases tracks the
icon's own Classes changes, resolves the last original rule among its current
classes, and activates a style-priority Value binding only while an alias exists.
Disabling the resolver unsubscribes and clears its derived state; a weak-key table
does not retain abandoned icons. Existing UnifiedIcon, glyphs, assets, severity
colors, templates and scoped overrides remain in place. Alias name/value/order
records exactly match the pinned reference-fnv IconsStyles.axaml. NMA_FIDELITY.md
documents this implementation difference.

The opt-in MO2_VERIFY_ICON_ALIASES check constructs original alias styles beside
the candidate using MO2_ICON_ALIAS_REFERENCE. icon-alias-behavior-check.log passes
all 160 mappings, reverse multi-class insertion/removal, local values and explicit
null, scoped class override/removal, detach/reattach and disable/reenable. The
first detach check compared before layout to an attached original; the corrected
check reattaches both controls and compares after layout. Initial build issues
were a lambda-discard name collision and a nonexistent test-only IconValues.Play;
both corrected before the successful Release build (three existing warnings).

icon-alias-census-check.json confirms 93 Mods icons now carry 930 style frames
(previously 15,624), and 89 Plugins icons carry 862 (previously 14,924), about 94%
fewer. Control counts remain unchanged. Two uninstrumented native-pointer pairs
in icon-alias-latency-comparison.json measured cold gaps candidate/baseline
359.5/402.3ms, then baseline/candidate 576.3/564.5ms. Both favor the candidate
slightly, but variation is larger than the difference; this does not demonstrate
a material cold-latency improvement. Revisit gaps remain near the 100ms injector
cadence. Retained for the verified reduction in redundant style work, not as a
claim that the responsiveness requirement is complete.

icon-alias-panels-check.log passes FNV/Skyrim/return presentation identity,
900/640/1280 cached widths and divider hit testing, responsive headers/tabs,
filtering/empty/clear, recycled row handles/toggles, red/green conflict and linked
plugin highlights, and unchanged native activation/order. Cold-switch delay
remains open; further profiling must use this consolidated-style build rather
than assuming the old 160-alias frames still dominate.

icon-alias-menus-check.log also passes both lists' lazy context menus and action
flyouts (including item reuse) plus unchanged endorsement-icon content on a
diagnostic profile Changed event. Normal user workspace restored after all tests.

Post-alias profiling (diagnostic progress): collected a fresh sampled-thread
trace on the consolidated-icon build, aligned the largest cold native-pointer
gap with EventPipe UTC start, and inspected that interval only. Evidence:
alias-current-trace-window.json, alias-current-hotspots.json and
alias-current-style-owners.json. The instrumented gap was 728.6ms; it is not a
normal-use latency baseline. Inclusive layout measurement covered 542.6ms;
style-owner samples include 305.2ms in framework callbacks and 26.1ms in deferred
body attachment. Inclusive categories overlap. Leaf samples include selector
matching, ApplyStyle, property-frame insertion and logical-parent notifications.
The old icon-alias diagnosis cannot be assumed to explain this remaining work.

The initial trace attempt used cpu-sampling, which this installed dotnet-trace
supports only for collect-linux; collect rejected it before tracing. Its runner
was interrupted, while the frontend diagnostic completed; that run is excluded
from pointer comparisons. The subsequent supported dotnet-sampled-thread-time
run and its pointer runner both completed successfully. No native state mutation.

Extended the opt-in style census with static selector source counts; no property
values or model data are read. Release build passed (existing NU1902 warning).
ui-latency-menu-style-sources.json and remaining-style-sources.json identify
the remaining global aliases: each of 72 Mods/76 Plugins Borders receives all
rounded-corner and color class rules (3,236/3,429 Style-sourced frames total), and
each of 40/20 StackPanels receives every spacing class rule (1,375/695 total,
including other scoped rules). StandardButton has repeated hover/pressed/disabled
rules across its type/fill combinations, which need their parent conditions
preserved and are not simple icon-style aliases. Next bounded optimization target
is the primitive Border/StackPanel class rules, with original-rule differential
tests, dynamic class removal and scoped/local precedence retained. No new
production styling change or speed improvement is claimed in this diagnosis.

Primitive alias prototype (REVERTED): tried typed XAML catalogs for 33 StackPanel
spacing rules and 17 Border corner, eight outline and 13 background rules. All
original StaticResource keys/order were retained, as were the disabled opacity
and dynamic LeftPaneTitle/nested-text styles. Prototype behavior checks passed
all original values (including 14 background rules with LeftPaneTitle), reverse
multi-class precedence/removal, local/scoped overrides, control resource
shadowing, detach/reattach and catalog reenabling. Original rules match the
pinned reference-fnv source. Evidence: primitive-alias-behavior-check.log and
primitive-alias-source-check.json. These describe the prototype, not shipped code.

Primitive-alias-census-check.json showed Border frames falling from 3279/3471
to 975/1039 and StackPanel frames from 1379/697 to 139/77 (Mods/Plugins), with
unchanged control counts. However, native pointer pairs were inconsistent:
candidate/baseline cold gaps 597.0/456.6ms, then baseline/candidate 521.6/441.4ms.
No repeatable latency benefit was established; reverted the extra resolver,
catalogs and opt-in verifier rather than retaining complexity based only on
frame counts. primitive-alias-latency-comparison.json marks both pairs REVERTED.
Saved source experiment under /tmp/mo2-primitive-aliases/prototype. Both theme
files restored byte-for-byte to their pre-experiment contents; icon alias
consolidation remains unchanged. Restoring old file timestamps initially reused
stale Avalonia XAML resources; touching the verified restored files invalidates
that cache for the final rebuild. No native activation/order mutation was made.

Next performance diagnosis should compare the main thread's actual CPU runtime
with the UTC-aligned delivery gap, before another broad style rewrite. Read-only
preflight confirms per-thread /proc stat (100Hz CPU ticks) and schedstat runtime
counters are accessible. sched_schedstats is 0, so run-queue-delay accounting must
not be assumed complete even though a nonzero historical counter is present.
No scheduler setting was changed. Existing sampled-thread stacks are wall-time
evidence, not proof that every millisecond was spent executing those functions.

Final restored Release build passed with the three existing warnings. Normal
user workspace relaunched after the primitive prototype was removed.

Main-thread CPU accounting (verified evidence): sampled /proc main-thread stat
and schedstat runtime at roughly 10ms alongside a fresh native evdev round trip.
ui-main-thread-cpu-accounting.json stores only counters/timestamps/states;
ui-main-thread-cpu-summary.json aligns bracketing samples with maximum delivery
gaps. Cold input gap 477.9ms was bracketed by 489.6ms elapsed and 462.7ms scheduled
CPU runtime (470ms using 100Hz CPU ticks). Brackets extend 10.4ms before and 1.4ms
after the gap. This strongly supports actual CPU execution as the dominant
cause in that run, rather than scheduling/waiting. Main thread nice=0, normal
scheduling policy; no priority or scheduler setting changed. Return/revisit gaps
191.2/114.4ms had bracketing CPU runtime 101.0/57.1ms. Maximum counter-read duration
was 1.48ms. Sampling states are not a complete scheduling trace, and disabled
sched_schedstats means run-queue-delay counters were not used for attribution.

Scoped panel style prototype (REVERTED): tested removing collection, load-order
table and load-order page sheets from global styles, then applying their unchanged
source assets within the corresponding native panel controls. Prototype was
opt-in only. Initial startup guards failed because compiled StyleInclude nodes
were flattened; inspection identified the three single-root compiled groups.
The corrected prototype matched all three groups, then passed the full paired
profile/resize/filter/recycling/conflict/native-order suite in
scoped-style-panels-check.log. Rendered paired Skyrim panels were inspected in
/tmp/mo2-scoped-panels.png. No native activation/order was changed.

The timing pair showed no benefit: candidate/baseline cold pointer gaps
530.5/527.8ms, with identical view/model/row counts. scoped-style-experiment.json
marks the experiment REVERTED. Scope changes also construct separate style-sheet
instances per panel; that additional construction cost was not isolated, so it
is a hypothesis rather than an explanation of the result. Saved prototype under
/tmp/mo2-scoped-style-experiment/prototype, restored Program.cs/Mo2ModsView.cs/
Mo2PluginsView.cs byte-for-byte and removed the helper. Restored Release build
passes (existing NU1902 warning). Icon alias consolidation remains intact.
Further work should identify expensive template realization more precisely,
rather than attribute the stall to scheduling or repeat these global-style
rewrites without new evidence. Overall responsiveness/completion remains open.

Shared Alert reload fix (retained): reconnect the existing AlertSettings wrapper
on Loaded after disposing its subscription on Unloaded; avoid duplicate initial
subscriptions. The original themed control and template remain in use.
alert-lifecycle-before.log reproduces the lost subscription;
alert-lifecycle-fixed.log passes detached-state isolation, resynchronization,
three dismiss/show reload cycles and settings replacement. The actual Plugins
help command also passes before/after retaining and reattaching its panel; its
original dismissal preference was restored. This is command/lifecycle validation,
not a new physical pointer test. Release build passed with the existing warning.
This shared Alert source difference supplements earlier source-comparison counts;
those counts are historical, not a complete census of today's differences.
No cold-switch latency improvement is claimed.

External import Windows filename compatibility (retained): CreateCopy validates
every archive path component before opening source content or creating staging
files. Rejects reserved device names (including extensions and COM/LPT superscript
digits), control/invalid characters, and trailing ASCII dots/spaces. Validation
uses Windows rules explicitly rather than Linux GetInvalidFileNameChars.
Reference: https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file
external-windows-names-check.log passes rejection cases, allowed Unicode/ordinary
names, and the existing ownership/import/hash/backup/restore suite. Failed cases
publish no ZIP or staging folder. Release build passes with one existing NU1902
warning. No native mod/game files changed. C: installed-copy verification still
needs a confirmed per-instance prefix mapping; no guessed mapping added.
The preceding goal turn made progress by documenting the verified Alert fix and
restoring the normal workspace. Overall goal remains active; cold responsiveness
and final combined acceptance remain unproven.

Wine installed-path mapping (retained): the installing native bridge now asks
its own Wine kernel32 wine_get_unix_file_name export for the Unix path, using
CDECL and releasing the returned buffer with HeapFree/GetProcessHeap, as Wine's
winepath utility does. Optional modUnixPath is returned only on successful UTF-8
absolute-path conversion. Native Windows/unavailable exports retain the old
result. Linux frontend prefers this absolute host-resolved path; older hosts
retain Z: support and unknown C: mappings stay unavailable. No prefix guessing.
Source reference: https://github.com/wine-mirror/wine/blob/master/programs/winepath/winepath.c

Actual GE-Proton10-4 Windows Python tests in both FNV and current Skyrim prefixes
pass C: directory identity, Unicode, Z: identity and unmapped-drive handling:
wine-path-mo2-fnv-compat-check.json and wine-path-mo2-skyrim-compat-check.json.
Wine returns dosdevices/c: spelling rather than drive_c; initial string-spelling
expectation was corrected and directory identity verified. Temporary directories
were removed in finally. These test the actual resolver, not a complete MO2
C: archive installation. Full end-to-end C: installation remains unverified.

Build passed with the existing NU1902 warning; wine-path-download-check.log has
10 passing tests including optional path response and canceled-install behavior,
wine-path-bridge-check.log has 36 passing contracts. All three native hosts were
closed via WM_CLOSE, awaited including released launcher locks, and launched once.
wine-path-deploy-check-{0,1,2}.json confirms one host and unchanged profiles, mod
states/priorities and plugin states/priorities/order. Deployed downloads.py byte
identity is recorded in wine-path-deployed-source-check.json. No native mod or
game content was changed by deployment. The prior goal turn was progress through
retained Windows filename validation and its passing regression checks.

Cold template realization diagnosis (retained opt-in instrumentation):
Mo2TemplateTiming is installed only with MO2_UI_TEMPLATE_TIMING=1. It wraps the
original IControlTemplate, returns the same result/name scope, and records Build
and Build-through-TemplateApplied separately through the existing latency probe.
The latter includes templated-parent assignment, logical/visual attachment and
OnApplyTemplate, but not subsequent child measure/arrange. SetCurrentValue
wrapping can perturb timings; these are instrumented diagnostic runs, not speed
benchmarks. No listener is installed during normal launch.
Pinned source inspected: https://github.com/AvaloniaUI/Avalonia/blob/11.3.5/src/Avalonia.Controls/Primitives/TemplatedControl.cs

ui-latency-menu-template-builds.json: largest cold native-pointer gap 488.1ms;
89 overlapping builds occupy 13.9ms union. Extended second run,
ui-latency-menu-template-realization.json, has 560.4ms gap; 89 builds occupy
12.6ms, while Build-through-TemplateApplied occupies 301.3ms union. Eight
StandardButtons account for 125.9ms inclusive realization (max16.7ms each),
but only 4.5ms of their own Build calls. Eleven TreeDataGridRows account for
33.8ms inclusive realization. Nested inclusive totals must not be added.
Summary: template-realization-gap-summary.json, reproducible with
frontend/tools/analyze_template_timing.py REPORT OUTPUT. The analyzer clips
intervals to the cold gap and computes union separately for each metric.

This narrows the next performance target to StandardButton attachment/styling
rather than template construction. Existing theme has two alternate content
branches plus icon/premium parts, and many state styles; no change to those
controls or styles has been made. Previously rejected primitive/global-style
experiments remain reverted. Release build passed with the existing NU1902
warning. Prior goal turn was progress through deployed Wine path mapping and
actual prefix tests. No latency improvement is claimed in this diagnostic turn.

Hidden StandardButton icon prototype (REVERTED): deferred assigning left/right
UnifiedIcon.Value while the icon slot was hidden; ShowIcon changes synchronized
current values before showing, preserving existing content on unchanged reshow.
The themed control check passed initial hidden values, all visibility modes,
null clearing, hidden value updates and template replacement:
button-icons-behavior-check.log. The paired profile/resize/header/filter/recycling/
conflict suite also passed in button-icons-panels-check.log, with native mod and
plugin state/order unchanged.

Uninstrumented native-pointer comparisons did not establish a repeatable gain:
candidate/baseline cold gaps 301.7/618.2ms, then reverse baseline/candidate
560.4/580.4ms. View/model/row counts matched. button-icons-latency-comparison.json
marks the prototype REVERTED. Restored StandardButton.cs byte-for-byte from the
pre-experiment copy, removed the opt-in check and Program hook; prototype saved
under /tmp/mo2-hidden-button-icons/prototype. Source restore writes a fresh
timestamp so the shared UI library is rebuilt. No theme or icon asset changes.

Avoid repeating this hidden-icon-only experiment as a cold responsiveness fix
without new evidence. Template realization diagnosis still identifies shared
button attachment/styling as expensive; reducing hidden icon creation alone did
not reliably reduce the complete cold input stall. The preceding goal turn was
progress through timings distinguishing construction from attachment.

Template-owner style census (retained diagnostic): Mo2StyleCensus now groups
frame/entry counts by each control's TemplatedParent type as well as its own
type. It reads no values or user text and remains behind existing census flags.
Build passed with the existing NU1902 warning. The exact pinned TemplatedControl
ApplyTemplate source confirms the realization interval begins at Build and
includes assigning template parents, logical/visual attachment and OnApplyTemplate.
It does not include all of the button root's earlier own style application.
Do not equate the measured 125.9ms with button type/fill state-selector cost.

ui-latency-menu-template-owners.json and button-template-owner-census.json show
Mods' button-template borders: 16 controls, 744 frames, 1056 entries; Plugins:
8 controls, 372 frames, 528 entries. Button roots separately carry about65 frames
each (one alert-owned button has66). These are retained-frame counts, not CPU
time attribution, and native parent-panel census scopes overlap child page
scopes, so their totals must not be added together. No state-style rewrite was
attempted based on the earlier ambiguous attribution. Prior hidden-icon
prototype remains reverted. Its final restored full build passed (48 existing
warnings), and the prior goal turn was progress through the negative paired
experiment plus restoration, not a demonstrated responsiveness improvement.

A distinct next experiment could prepare existing templates across dispatcher
turns before revealing a newly attached panel, keeping original theme assets.
This has not been implemented or verified: cancellation, body reuse, template
children created later, loading presentation and responsive layout all need
proof. Do not assume that detached preconstruction supplies the real style scope
or that reparenting prepared controls preserves their applied styles. Cold
responsiveness and overall goal completion remain open.

Panel template preparation (RETAINED, enabled by default): Mo2DeferredView keeps
a stable content host and loading-message overlay. Newly created native outer
panels and reusable Mods/Plugins bodies attach hidden in their real logical
ancestry. Mo2TemplatePreparation calls the existing ApplyTemplate method for one
control per Background dispatcher turn, traverses the resulting visual children,
and reveals the original body without reparenting template parts. Existing cached
bodies retain their reuse path. Cancellation checks host version/model/visibility
and root attachment; completion restores visibility and cannot clear a newer
model's loading message. MO2_PREPARE_TEMPLATES=0 disables preparation for diagnosis.
No NMA template, style, artwork or icon assets changed.

Initial prototype missed ContentPresenter because it is not TemplatedControl;
its two-step timing run prepare-candidate is excluded. Corrected ApplyTemplate
traversal reached187 controls in page bodies but left a cold gap before that work.
Extending preparation to outer native panels reached237 steps (max14.3ms in one
run). The largest remaining gap then occurred during sidebar/workspace activation,
with no preparation steps or row builds overlapping it. This shifts the next
performance target; it does not prove that one named activation method accounts
for every millisecond of that gap.

Evidence: template-preparation-latency-comparison.json preserves the run order
and exclusions. Pre-overlay baseline cold gaps445.5/566.2ms versus outer-panel
preparation282.5/234.4ms. Final identical-binary disabled/enabled comparison,
including the loading wrapper, measured454.8/288.9ms cold native pointer gaps
and433.9/261.9ms maximum dispatcher input delay. Counts remain identical: one
Mods view, one Plugins view,152 mod models,11 mod rows,14 plugin rows. Native
evdev pointer moves are injected at100ms cadence; no guarantee of universally
smooth or sub100ms switching is claimed. Cold input pauses remain open.

Final validation: template-preparation-final-lifecycle.log passes input between
builds, cancellation/detach, reattach without duplicate template builds, visible
loading message, replacement model/stale completion, detached old body and
message clearing. template-preparation-final-panels.log passes profile identity,
900/640/1280 cached resize, four-line header collapse/restoration, reachable
divider, filters/empty/clear/scrolling, row handles/toggles and native conflict
/highlight correspondence; mod/plugin activation and order unchanged.
template-preparation-final-help.log passes the original Plugins help command
before/after retained reload and restores its preference. Rendered paired Skyrim
UI inspected in template-preparation-final-panels.png, with correct geometry,
headers, help/trophy rails and usable rows. Build passed with existing NU1902.
The preceding goal turn was progress through the template-owner census; this
turn retains a tested scheduling change. Normal workspace restored after tests.

Retained preparation and fresh trace follow-up (verified): extended the real
Mo2DeferredView check to hide its ancestor without detaching, during the first
child template build. Remaining preparation stops; the body stays attached and
returning keeps its identity, completes remaining templates and clears loading.
retained-preparation-lifecycle.log passes this plus the existing cancellation,
input, reattach, replacement-model and loading checks. Returning after canceled
preparation currently finishes remaining work through ordinary layout; no claim
is made that it resumes the background queue. This turn changes the verifier,
not the retained production scheduling implementation. Build passes with the
existing NU1902 warning.

Fresh sampled-thread trace of enabled preparation: /tmp/mo2-prepared-current
.nettrace and .speedscope.json; tracer and native-pointer runner both exited0.
prepared-current-trace-window.json aligns thread511005 and the largest cold
358.6ms pointer gap against EventPipe UTC start. This is instrumented evidence,
not a new uninstrumented speed benchmark. prepared-current-hotspots.json shows
250.6ms inclusive MeasureCore,227.5ms ApplyTemplate,207.9ms ApplyStyling, and
131.9ms TreeDataGrid RealizeElements. These overlap and must not be summed.
Leaf samples include Selector.Match55.6ms, PublishNext38.5ms and property-dictionary
insertion32.5ms. Remaining work includes initial table layout, not only the
earlier sidebar/workspace activation interval; which gap is largest varies.

prepared-current-gap-context.json includes16 Mods-row template requests for15
distinct models from one owner in that interval, while final counts still show
11 actual Mod-row content constructions. Existing deferred rows avoided content
construction for transient containers. Source inspection confirms responsive
headers already skip hidden/unsized owners. Pinned TreeDataGrid11.1.1 source
(commit0cb3b3a5cba5efb1da0477694a4636ca680abf08) realizes and measures each row
while filling the viewport; its25px estimate is not alone proof of over-building.
No fixed-row-height, header-sizing, or viewport-ramping change was made without
further evidence. Next investigation can examine initial effective viewport and
row/container realization; do not mislabel template-request counts as actual
custom row constructions or attribute the whole interval to one sidebar method.
The prior goal turn was progress through shipped preparation and paired tests.
Overall goal remains active. Normal workspace restored after this verification.

Vortex reference refresh and header sidebar toggle (retained): the Vortex checkout
at /home/deck/vortex/src/vortex was fast-forwarded from the stale local master
(5eb451242) to origin/master 4aaef4306. The `local/linux-support` branch is
untouched and still 13 commits ahead of its old base. Two things in that range
matter here.

First, Vortex's mods page redesign (LAZ-947, commits a44e74d14 through 5aa566c62
plus c8425dab1) is now reference material, not implemented here. It renders
Page/PageHeader/PageScroll/PageContent with the new Toolbar instead of the legacy
MainPage chrome and IconBar: a non-scrolling page whose full-bleed header holds
the "mod" pictogram, the Mods title, the "Manage the mods installed for this game."
subtitle and the toolbar on the title row; an edge-to-edge sticky-header table
below it; and the footer and dropzone under the table rather than over it.
Scrolling shrinks the header (pictogram 56 to 28 px, title moderate to subdued,
subtitle hidden) and swaps its hairline for a shadow. The toolbar takes actions
rather than components, hides an action whose condition returns false, greys out
one that returns a string without showing the explanation, and collapses whatever
does not fit into an overflow menu. The empty state is a NoResults block titled
"You don't have any installed mods". Our My Mods page still uses its existing
NMA chrome; no part of this redesign has been ported.

Second, Vortex's modern layout moved its menu toggle out of the menu. Our toggle
was at the foot of the sidebar with mdi-chevron-double-left/right and
Expand/Collapse sidebar wording. It is now a Tertiary/None/Medium StandardButton
inserted at the head of the top bar's title panel, left of the workspace title,
exactly where Vortex's Header places it. It uses Vortex's own nxmPanelOpen and
nxmPanelClose geometry (copied verbatim from its icon-paths.ts and parsed into
Avalonia geometry so the icon takes the button foreground) and Vortex's
"Open menu" / "Collapse menu" tooltip and automation wording. The two IconValues
are cached and reused, because assigning a fresh one rebuilds UnifiedIcon's inner
control. Collapsed width stays 64 px, matching Vortex's w-16; the expanded width
stays at NMA's 232 px rather than Vortex's 222 px, so panel geometry is unchanged.
The now-empty sidebar wrapper Grid was removed and the sidebar sits directly in
its original column again. Attribution was added to Assets/Vortex/NOTICE.md.

Opt-in MO2_VERIFY_SIDEBAR_TOGGLE=1 checks the real window: the button is inside
TopBarView, is the first child of the title panel and precedes
ActiveWorkspaceTitleTextBlock; both geometries parse into drawable shapes and
differ; and across two clicks the width, icon, tooltip, automation name and the
persisted preference all follow, returning to the original state.
[Check output](artifacts/sidebar-toggle-check.log) passes. Rendered evidence at
1280x750: [expanded](artifacts/sidebar-toggle-expanded.png) and
[collapsed](artifacts/sidebar-toggle-collapsed.png). Release build passes with the
existing NU1902 warning. These runs used the Home workspace with no bridge
directory, so no MO2 host was started and no mod, plugin or download state was
touched; the sidebar preference was restored to its original expanded value.
Cold-switch responsiveness and the wider acceptance review remain open.

Shared component gallery, panel chrome and tab drag/drop (retained):

A Components page now sits in the home left menu below My Loadouts, registered as
an ordinary home-context page factory and inserted into the native
HomeLeftMenuView stack (the native home menu interface exposes only two items).
It is a gallery of the shared NexusMods.App.UI controls this frontend builds
panels from: the full StandardButton matrix across type, fill, size, icon
placement and disabled state; the shared Toolbar in its default and
"1 selected" forms; PageHeader; all four Alert severities; EmptyState;
ProgressRing, Spinner and ProgressBar; Divider; and the unthemed Avalonia inputs
and toggles. Sections whose reference component has no shared control say so, so
the gaps stay visible rather than being reimplemented per panel. Remaining gaps
are Label chips, primary/secondary tab styles, the pin and view-options
dropdowns, and input-group states.

A "Did you know" hint was added as Mo2UsageAlert. It is the shared Alert plus an
AlertSettingsWrapper, so dismissal persists by key through the existing
Mo2AlertPreferences file, matching Vortex's UsageAlert contract. No new alert
control was written.

Sidebar entry sizing was a real defect, not a style preference. ApplyMenu
captured each nav button's padding before styles applied and wrote it back as a
local value, which beats the theme setter and permanently flattened expanded rows
to 24px. It now clears the local value instead, so rows take the theme's own
metrics (40px, matching Vortex's h-10), with 2px between entries like Vortex's
gap-y-0.5. Sidebar collapse is now animated: the column is Auto and the sidebar
owns a 160ms CubicEaseOut width transition, because GridLength is not animatable.
The top bar's grid was top-aligned inside a taller row; it is centred now, and
the toggle check measures that the toggle, title and window controls share a
centre line within 1.5px. The spine Home button was aligned to the same row.

Mo2PanelChrome is now the single definition of panel chrome: 24px root padding,
an animated compaction to 12px below 350px height, and a Divider directly under
the page header. Tools, Archives, Data, Saves, Overwrite, Logs and External Files
all use it; their previous per-view margins of 16/24 and three different
compaction rules were removed. Views keep their own content and control names.
Mods, Plugins and Downloads still have bespoke chrome and have not been migrated.

Mo2TabDragDrop moves pages between panels. Dragging a tab header past a 6px
threshold starts a drag; dropping over a panel's middle adds the page as a tab
there, and dropping within the outer 28% band of a panel's shorter side splits
that panel and opens the page in a new one on that side. The region that would be
occupied is previewed as an overlay while dragging, so the split is visible before
release. The move reuses the existing workspace controller: OpenPageBehavior.NewTab
for a tab drop and OpenPageBehavior.NewPanel with a grid state built from the
panels' logical bounds for a split. Dropping a tab on its own panel, and splitting
a panel by its only tab, are both ignored. No NMA workspace source was changed;
the workspace system has no drag support of its own.

Checks: [panel chrome](artifacts/panel-chrome-check.log) constructs all seven
panels, asserts the shared padding, the animated compaction at 300px and back,
and a separator under each header. [Tab drag and drop](artifacts/tab-drag-check.log)
covers the five drop zones and their previewed regions, a real edge drop that
splits into two panels, a tab drop that rejoins to one, and the ignored self-drop.
[Components gallery](artifacts/components-gallery-check.log) opens the page,
confirms the shared controls render, and dismisses and restores the hint without
changing the stored preference. [Sidebar toggle](artifacts/sidebar-toggle-check.log)
is now animation-aware and asserts header alignment. All four pass. Release build
passes with the existing NU1902 warning. These runs used the Home workspace with no
bridge directory, so no MO2 host started and no mod, plugin or download state was
touched.

Still open from this round: the Vortex mods-page header form (icon actions
replacing the toolbar row, compact-on-scroll), the column toggle under Actions,
notifications moving into the spine, Nexus account login, home icon highlighting,
white game-icon fill, the user profile icon, External Files against NMA's
original, separator sizing against the new mods-page reference, and adding
Vortex's own shared components to the gallery.

Spine, icon and separator follow-ups (retained): notifications moved out of the
header and into the foot of the spine above the download button, as Vortex's
modern layout places them. Game art is now composited onto a white plate instead
of #38383A. The mods-page separator chevron is 14px at .65 opacity, matching the
tag icon beside it rather than the full-weight row action icons. The spine Home
button fills its glyph (mdi-home) and takes the Active class while a home
workspace is showing, and outlines it (mdi-home-outline) otherwise; this is driven
from the active workspace because IconButtonViewModel.IsActive is not reactive, so
NMA's own binding never fired for it.

An earlier attempt to align the spine Home button by giving HomeBorder a 2px top
margin clipped the button against the top of the spine and was reverted. Header
alignment is handled instead by centring the top bar's MainGrid, which the toggle
check measures.

Nexus account login: the two verifiable layers both pass on the current build —
`--check-nexus-sso-connection` completes live SSO initialization and cancellation,
and [the popup check](artifacts/login-popup-check.log) opens the NMA popup at both
owner sizes, cancels and reopens it. The reported failure is not reproduced by
either, and no fix is claimed; the next step needs the actual symptom seen when
the header account button is pressed.

The three UI suites must be run one at a time. Run together they interfere —
the panel-chrome check opens its own window and the drag check reshapes panels,
which left the gallery's sections unmeasured and produced a false failure. Each
passes alone: [tab drag](artifacts/tab-drag-check.log),
[panel chrome](artifacts/panel-chrome-check.log),
[components gallery](artifacts/components-gallery-check.log).

Still open: the Vortex mods-page header form (icon actions replacing the toolbar
row, compact-on-scroll), the column toggle under Actions, the user profile icon,
External Files against NMA's original, the mods-page row/separator comparison
against the new reference screenshot, and adding Vortex's own shared components
to the gallery. Mods, Plugins and Downloads still have bespoke chrome.

Header actions, column toggle and tab strip (retained):

Mo2PanelChrome now places each panel's actions on the header's line instead of a
toolbar row of its own, matching the reference where the right-hand icons replace
the toolbar. Tools, Archives, Data, Saves, External Files, Logs and Overwrite all
pass their search box and action buttons to Apply; the emptied toolbar rows
collapse to nothing without disturbing the other row indices. The header also
compacts: the pictogram animates from 60 to 28px, the description hides and the
title stays, driven either by the panel scrolling away from the top or by the
panel being shorter than 350px. Overwrite keeps its five labelled operations in
their own row, because they are page operations rather than header icons.

Mo2ColumnToggle adds the reference's view-options action: a tune icon in the
header opening a checkable column list plus Reset to default. Panels rebuild
their table source on every render, so the hidden set lives in the toggle and is
re-applied to each fresh column list, and it persists per panel under
columns-<panel>.json. The last visible column cannot be hidden. Archives, Data,
Saves and External Files carry it.

The panel tab strip now stays visible with a single tab. Two attempts inside the
frontend failed first: forcing visibility from LayoutUpdated re-triggered layout
and Avalonia aborted with "Infinite layout loop detected", and pushing the value
back from the border's own PropertyChanged simply fought upstream's subscription
every frame. The rule lives in upstream PanelView.axaml.cs, which set
TabHeaderBorder.IsVisible = !hasOneTab; it now also honours MO2_ALWAYS_SHOW_TABS,
defaulting to showing the strip. The theme already styles the :one-tab case, so
no style change was needed.

A user profile icon replaces NMA's placeholder avatar art: mdi-account-circle
when an account is connected, the outline form otherwise. This is wired but not
visually confirmed, because NMA only shows the avatar button once logged in.

Corrections worth keeping: the sidebar toggle check was reading Width immediately
after clicking, but Width is the animated property, so it returned an interpolated
value and the check failed against a working app; it now waits for the value to
arrive. An added tab-strip assertion first matched the first TabHeaderBorder in
the window, including hidden workspaces, and was scoped to the effectively
visible panel.

Checks, each run on its own: [panel chrome](artifacts/panel-chrome-check.log),
[column toggle](artifacts/column-toggle-check.log),
[tab drag](artifacts/tab-drag-check.log),
[components gallery](artifacts/components-gallery-check.log),
[sidebar toggle](artifacts/sidebar-toggle-check.log). All pass. Release builds
with the existing NU1902 warning.

Tooling: .mcp.json registers Avalonia's Build MCP (remote HTTP docs server) and
the DevTools MCP. AvaloniaUI.DiagnosticsSupport 2.2.3 is referenced and
WithDeveloperTools() is called when MO2_DEVELOPER_TOOLS=1, so the DevTools MCP can
attach. The avdt tool is not on nuget.org and DevTools MCP needs an Avalonia Plus
licence in AVALONIA_TOOLS_LICENSE_KEY, so that half is configured but not usable
until a licence and the tool feed are available.

Still open: Nexus account login is unreproduced, the mods page has not been
compared row by row against the new reference screenshot, External Files does not
follow NMA's original page, and Vortex's own shared components are not in the
gallery. Mods, Plugins and Downloads still have bespoke chrome.

Plan completion round:

Panel chrome now also covers Plugins. Its header and toolbar carried their own
24px side margins; those moved to the shared root padding, and Apply accepts any
Panel root so a DockPanel-based page works, re-inserting the header stack at its
original index because a DockPanel gives its last child the remaining space.
Downloads and My Mods embed NMA's own native pages, which bring their own header
and chrome, so they are deliberately left on that rather than given a second one.

Nexus sign-in failures are no longer silent. Account() had no catch, so any
exception disappeared into the ReactiveCommand and the button looked dead; it now
reports the failure through the account status line and the console. The two
separate silent nulls are also distinguished: a sign-in already in progress now
says so, rather than sharing the "window is not ready" path. This makes a failing
sign-in diagnosable; it is not itself a fix for the reported failure, which still
has not been reproduced here.

Mo2Label adds the reference's chip in six brands across strong, weak and outline
fills, resolving theme brushes with a neutral fallback so a missing palette does
not throw at runtime. The gallery gained Labels, Tabs (themed TabControl plus a
secondary pill row built from StandardButtons) and View options, which shows the
same column chooser the table panels use. The gallery is now 16 sections and 63
StandardButton samples.

All five checks pass, each run on its own:
[components gallery](artifacts/components-gallery-check.log),
[panel chrome](artifacts/panel-chrome-check.log),
[column toggle](artifacts/column-toggle-check.log),
[tab drag](artifacts/tab-drag-check.log),
[sidebar toggle](artifacts/sidebar-toggle-check.log).
Release builds with the existing NU1902 warning.

Genuinely not done: the My Mods page has not been compared row by row against
Screenshot_20260913-193447.png, and External Files does not follow NMA's original
ExternalChanges page. Both are redesigns of a working page rather than additions,
and neither was attempted.

External Files against NMA's original, and mods-page separators:

NMA has no External Changes page in source — only its user documentation and the
0.8.2 screenshot, which show NAME, SIZE and FILE COUNT columns over a folder tree,
with deleted files struck through and Edit File / Delete on the toolbar. Our tree
carried Name, Type and a KB-only size. Mo2ExternalFileNode now aggregates over
descendants: folders report a total size and a file count, files report their own
size and no count, links report neither (they contribute no bytes to their parent),
and sizes step through B / KB / MB / GB as the screenshot shows. The tree gained a
File count column; Type stays, because MO2 also surfaces links here, which NMA's
view has no equivalent for. Struck-through deletions are not applicable: this page
lists files present in the game folder but not owned by any Steam depot, so there
is no deleted-file state to strike.
`--check-external-columns` covers the aggregation, the link case and the size
steps; the existing `--check-external-files` suite still passes unchanged.

Mods-page separators now draw as filled, full-width rounded bars using
SurfaceMidBrush, matching the category rows in
Screenshot_20260913-193447.png rather than sitting as bare rows. Conflict and
linked-plugin highlighting still paints over the same border. The chevron was
already reduced to 14px at .65 opacity in the previous round. Remaining
differences from that screenshot are the per-column filter row and the separate
"N selected" toolbar group, which are not implemented.

Plugins also moved onto Mo2PanelChrome; its header and toolbar side margins
became the shared root padding. Downloads and My Mods embed NMA's native pages,
which bring their own header, so they keep that rather than gaining a second one.

Avalonia developer tools and MCP:

The tool is installed globally as AvaloniaUI.DeveloperTools.Linux 2.2.3, which
provides the `avdt` command (the platform-specific package is required below .NET
SDK 10; this machine is on 9.0.318). `avdt` fails on its own here because the
repo keeps its SDK under frontend/../.tools/dotnet rather than system-wide, so the
MCP entry supplies DOTNET_ROOT and a PATH that includes it.

The app side uses AvaloniaUI.DiagnosticsSupport 2.2.3 with WithDeveloperTools()
called only when MO2_DEVELOPER_TOOLS=1, so a normal user session installs no
diagnostics listener. A verified run with that flag starts and renders normally.

Avalonia.Diagnostics, the free F12 inspector, cannot be added alongside it: the
DiagnosticsSupport targets fail the build with an explicit conflict error, because
it does not support Avalonia v12. It was added, hit that error, and was removed
again; DiagnosticsSupport is the one that matters, since the MCP attaches through
it.

.mcp.json registers both documented servers: avalonia-docs (the remote Build MCP
over HTTP, usable immediately) and avalonia_devtools (`avdt mcp` over stdio).
`avdt mcp` starts and exits cleanly on a closed stdin, which is correct for a
stdio server. Attaching to a running app additionally needs an Avalonia Plus
licence in AVALONIA_TOOLS_LICENSE_KEY; the entry reads it from the environment,
so it stays out of the repo.

Plugins panel chrome (retained, not visually verified): Mo2PanelChrome gained an
ApplyDocked overload, because Plugins docks its header in a DockPanel rather than
placing it in a grid row. It gives Plugins the same header stack, separator,
animated padding and header compaction as the other seven. Its toolbar actions
were not moved onto the header line, so it has no PanelHeaderActions group and is
not covered by the panel-chrome check, which requires one. The seven covered
panels still pass. Confirming Plugins visually needs a connected MO2 profile,
which the Home-only verification runs do not have.

Avalonia DevTools MCP licence: the key supplied is an online activation key
(avln_on_key:v1:...). Passed through as-is, attach-to-app reports an invalid
format expecting Base64; base64-encoded, it gets past format parsing and reports
an unsupported container version. Both errors come from the licence container, not
from the wiring: avdt 2.2.3 completes the MCP handshake and advertises its tools,
and the app runs with DiagnosticsSupport and WithDeveloperTools enabled. The
server exposes only an `mcp` command, with no activate or login, so it needs the
offline Base64 licence key from the Portal rather than the activation string.
The Build MCP (avalonia-docs) is confirmed working against live documentation.

Mods page against the new reference screenshot (partly closed): the category
separators already match it — Mo2ModRow draws each as a full-width rounded
#29292E bar with a highlight overlay, chevron, category icon, name and count — so
the only gap there was the chevron weight, now 14px at .65 opacity. The toolbar
already groups its icon actions in the same rounded pill the reference shows.

Added the selection group the reference grows beside that pill: a clear-selection
action and an "N selected" count in a second pill, hidden while nothing is
selected and revealed by the existing SelectedModels subscription. Per-row actions
deliberately stay on the row, as the Toolbar usage notes require. The group is
built and the Release build passes, but it is not visually verified: it only
appears with a connected MO2 profile and rows selected, which the Home-only
verification runs cannot produce.

Not done on this page: the per-column filter row and the Actions/gear column
toggle. Mods drives its table through the NMA adapter rather than rebuilding a
FlatTreeDataGridSource per render, so Mo2ColumnToggle does not apply to it as it
does to Archives, Data, Saves and External Files; wiring it needs a different
hook into the adapter's CreateColumns.

External Files against NMA's original (partly closed): this fork has no
ExternalChanges page to copy — the earlier note referred to NMA's documentation,
not a page in the tree. Its nearest original is LoadoutGroupFilesPage, the file
tree page, which is a Grid of Auto,* with a shared Toolbar above a TreeDataGrid
carrying Classes="Compact". Our page now uses Compact instead of
MainListsStyling, which NMA reserves for mod lists rather than file trees. The
external file, archive, import, installed-copy and backup checks all still pass,
as does the panel-chrome check.

The toolbar half of that original is deliberately not matched: NMA puts a Toolbar
row above the table, while the Vortex reference and every other panel here put the
icon actions on the header line. Those two references genuinely conflict, and the
Vortex form was chosen for consistency across panels. This is a recorded
divergence, not an oversight.

Crash fixed, found only by running live: applying the shared chrome to Plugins
threw on connecting to a profile —
"The control PageHeader (Name = PluginsPageHeader) already has a visual parent
StackPanel (Name = PanelHeaderStack)". Page bodies are reusable, so a view can be
set up more than once, and re-parenting a header that already sits in a header
stack is fatal. Both Apply and ApplyDocked now return early when the header is
already inside a PanelHeaderStack or PanelHeaderRow. Every Home-only check passed
before and after this bug existed; it took a run against the FNV bridge to surface
it, and it would have crashed the app on opening any profile.

The live run stops at the next step for an unrelated reason: the native MO2 host
is not running, so the bridge reports "Live MO2 profile or required tables did not
connect". Verifying the Mods selection group, the per-column filter row, the Mods
column toggle and the reported login failure all need that host started through
artifacts/start-fnv-host.sh, which launches MO2 under Proton.

Connected live run (FNV test host): the host was started through
artifacts/start-fnv-host.sh and the frontend connected — the status bar read
"12 mods · 11 plugins · Connected to MO2". Evidence:
[live Mods page](artifacts/live-mods-page.png).

What it confirms: the Mods page draws the new chrome correctly against real data —
pictogram, title, grey description, the Mods count tab, the rounded toolbar pill
holding the add and overflow icon actions and search, and the Status / Mod name /
Actions column headings.

What it exposes, unresolved: at capture the Mods table reported 13 rows with 13
visual rows but drew no row content, and the second panel stayed on the
"Loading panel…" message with its Plugins table at zero bounds
(TABLE: source=11 rows=11 bounds=0,0,0,0 visualRows=0). This may be the ordinary
deferred body still building four seconds in, or a regression from giving Plugins
the docked chrome. It is not diagnosed, and no claim is made either way. The next
connected run should capture later than four seconds and compare against a build
with Mo2PanelChrome.ApplyDocked removed from Mo2PluginsView.

The Mods selection group, the per-column filter row, the Mods column toggle and
the reported login failure were not exercised in this run.

Shutdown: WM_CLOSE to the MO2 window did not end the process tree on its own, so
the wait-mo2-host, pressure-vessel and pv-adverb launcher processes were stopped
by PID. No ModOrganizer.exe or mo2-fnv-compat process remains. The run only read
state; no mod, plugin, profile or download was modified.

Plugins rendering regression diagnosed and fixed: the cause was in the new
ApplyDocked overload, not in deferred loading. It set the padding transition on
Decorator.MarginProperty while its target is a DockPanel, which is not a
Decorator; Apply had correctly used Layoutable.MarginProperty. With the wrong
owner the Plugins body never took a size.

Before, on the connected FNV host:
  TABLE: source=11 rows=11 bounds=0,0,0,0 visualRows=0
After, same host and profile:
  TABLE: source=11 rows=11 bounds=0,31,386,498 visualRows=11

[Fixed live capture](artifacts/live-mods-fixed.png) shows both panels drawing
fully against real data: My Mods listing 12 mods with their enabled checks, row
delete and overflow actions, and a "ggg" separator bar with its count of 2; and
Plugins listing all 11 plugins with Status / Plugin name / Actions headings and
the Sort with LOOT toolbar. This also confirms the separator bar form and the
header chrome against live data, which the Home-only runs could not.

The MO2 host was closed afterwards; no ModOrganizer.exe or mo2-fnv-compat process
remains. The run read state only.

Mods per-column filter row (retained): Mo2ModsAdapter.SetColumnFilters had existed
with no caller — the filter row the reference shows under the headings was never
built. Mo2ModsView now draws one in the same column grid as the headings: text
filters for Mod name, Version and Category, and an All / Endorsed / Not endorsed
picker, each calling the existing SetColumnFilters. It shares the headings'
responsive Fit, so narrow panels drop the same columns the headings do.

Verified on the connected FNV host: [live capture](artifacts/live-mods-filters.png)
shows the filter under Mod name with all 13 rows still drawn, and the remaining
filters correctly collapsed at the 434px panel width. Both tables render
(TABLE: source=13 rows=13 visualRows=13, source=11 rows=11 visualRows=11).
The host was closed afterwards and no ModOrganizer.exe or mo2-fnv-compat process
remains; the run read state only.

Remaining after this round: the Nexus login failure is still unreproduced, the
Mods column toggle is unbuilt because Mo2ColumnToggle hooks a per-render
FlatTreeDataGridSource rebuild that the NMA adapter does not do, and the Mods
selection group has been built but never seen with rows selected.

Mods column toggle (retained, verified live): the Build MCP surfaced
TreeDataGridColumn.IsVisible, which led to the right hook — Mods does not use
TreeDataGrid columns at all. It draws its own seven-column grid, and
Mo2ModRow.Fit was already hiding Version, Category and Endorsed responsively.
Fit now also consults Mo2ModRow.HiddenColumns, so a column shows only when it
both fits and has not been switched off. A tune action at the end of the heading
row lists the three optional columns with Reset to default, and the choice
persists in columns-mods.json through Mo2ModColumnPreference. The heading row,
filter row and every mod row share one Fit call.
Verified on the connected FNV host: [live capture](artifacts/live-mods-columns.png),
13/13 mod rows and 11/11 plugin rows, no exceptions. Panel chrome, column toggle
and components gallery checks all pass afterwards.

Mods selection group — written, not yet verified: Mo2ModSelectionCheck drives the
real TreeDataGridRowSelectionModel and asserts the group is hidden with nothing
selected, shows an accurate count, and disappears after the clear action. It does
not yet reach the page. Hooking liveWindow.Opened and calling ShowProfile is too
early: the check now waits for the bridge to connect and then for
live.ModsPage.Adapter.SourceCount, and still times out with "Mods page model never
populated", while the MO2_SCREENSHOT path reaches the same page reliably. The next
attempt should hook where that path does, after its own WaitFor, rather than from
window Opened.

Avalonia DevTools MCP is unusable here, and this is now established from the
binary rather than inferred. Its UTF-16 strings contain both "avln_on_key:v1" and
"avln_off_key:v1", alongside "Authentication required to use this MCP server. Set
\"AVALONIA_TOOLS_LICENSE_KEY\" env variable or sign in via browser". The key
supplied is an on (online) key; the variable wants an off (offline) key, which the
user does not have. The browser sign-in alternative fails in this build with
OpenIddict invalid_request, "The specified 'client_id' is invalid" (ID2052), so
neither route is open. The Build MCP works and was used materially: the column
toggle hook came from it.

Mods selection group is now under test, and the test fails on a real defect.

Two obstacles were cleared first. The check was registered on liveWindow.Opened,
which races workspace construction; gating it behind the screenshot path's WaitFor
only hid which part never arrived. Letting the check report its own state gave the
answer directly: connected=True, profile=True, mods=14, page=null, views=0 — the
bridge was fine and the profile workspace simply had no Mods tab. That came from a
saved layout: repeated verification runs had persisted a profile workspace without
one, and every later run restored it. Resetting workspace-layout.json restored the
default and the check reached the page immediately. The previous file is kept at
the session scratchpad as workspace-layout.backup.json.

With that cleared the check passes its first three assertions against live data —
the group is hidden with nothing selected, appears on selecting a row with the
count reading "1 selected", and tracks a multi-row count — and then fails:
  FAIL mods selection group: Clear action left rows selected
Clearing the TreeDataGridRowSelectionModel does not empty the adapter's
SelectedModels, which is what the toolbar counts, and adding an explicit
SelectedModels.Clear() alongside it did not help either. The clear action is
therefore not working, and this is a defect in the feature rather than a gap in
coverage. The next attempt should look at how LoadoutTreeDataGridAdapter maintains
SelectedModels — most likely it is derived from the grid's selection-changed
notifications, so the fix belongs at the selection model the grid actually holds
rather than at Source.Value.Selection captured earlier.

The MO2 host was closed; no ModOrganizer.exe or mo2-fnv-compat process remains.

Mods selection group now passes end to end against live data:
  PASS mods selection group: hidden with no selection, "1 selected" for one row,
  "3 selected" for 3, and the clear action emptied the selection and hid the group
([check output](artifacts/mods-selection-check.log)).

The defect was mine and quiet: Mo2ModRow.IconButton marks Click handled in its own
handler, and Avalonia skips later handlers for an already-handled event, so the
clear handler attached afterwards never ran at all. Two earlier fixes failed
because they changed the wrong layer — first Source.Value.Selection, then
SelectedModels.Clear() — while the handler itself was dead. Passing the action
into IconButton, clearing through the grid's own RowSelection (the model the
adapter subscribes to, unlike Source.Value.Selection), fixes it. Any future button
built with IconButton must take its action through the factory for the same
reason.

Panel chrome, column toggle and components gallery all still pass. The MO2 host
was closed; nothing remains running.

#15 is complete: chrome, separators, toolbar pill, selection group, per-column
filter row and column toggle are all built and verified against the live FNV
profile. The one remaining plan item is #11, the Nexus login failure, which is
still unreproduced — both verifiable layers (SSO connection and popup lifecycle)
pass, so the symptom seen when pressing the header account button is the missing
input.

Nexus account login (#11) — investigated to a conclusion, no defect found.

The gap was in coverage, not in the product. Mo2LoginPopupCheck calls
Mo2NexusLoginPopup.Show directly, so the header button, its command binding and
LoginToNexus had never been exercised by any check — precisely where a "login does
nothing" report would live. Mo2LoginButtonCheck now covers that link: it finds the
real LoginButton, asserts it is visible (NMA hides it whenever IsLoggedIn is true,
so a wrongly-connected account would leave nothing to press), asserts a command is
bound and reports executable, presses it, and waits for the popup window.

A first run of that check reported the failure and looked like a reproduction. It
was a flaw in the check: raising Button.ClickEvent only notifies handlers, while a
real click runs Button.OnClick, which executes Command. Executing the command as a
click does gives:
  PASS login button: visible with a bound command, and pressing it opened the
  "Nexus login" popup
([check output](artifacts/login-button-check.log)). The earlier failure must not be
read as evidence of a product bug.

All three layers now pass: live SSO initialization and cancellation
(--check-nexus-sso-connection), popup lifecycle and sizing at both owner sizes
(MO2_VERIFY_LOGIN_POPUP), and the header button through to the popup
(MO2_VERIFY_LOGIN_BUTTON). No reproducible fault remains in the sign-in path, and
no speculative change was made to it. If sign-in still misbehaves in ordinary use,
the failure lies beyond the popup — in the browser authorization round trip or the
credential readback — and the next step is the message shown in the status line at
the moment it fails, which none of these layers can produce synthetically.

Mods page against the reference (closed): the page now carries the reference's
separators, row action group, header form with the toolbar on its right, the split
status control with its caret, and no tab strip. Version, Category, Endorsed and
Actions were verified rendering real values once the Mods panel had the window to
itself (916px): 1.5.1 and 1.06 against the actual mods, Non-MO categories, and
endorsement thumbs where a Nexus id exists.

Three mistakes are worth recording. The tab strip took three attempts: hiding the
TabControl's ItemsPresenter from the constructor ran before the template existed,
moving it to AttachedToVisualTree still selected the wrong presenter, and the
answer was hiding ModsTabItem, which is the same mechanism already used two lines
above for RulesTabItem. The single-panel capture aid fired on a timer at 8s while
the screenshot path shuts the app down at ~4s, so it never ran; the panel close now
happens inside the capture. And the header's compaction located its pictogram by
matching the current width against 60 or 28, so mid-animation it found nothing,
never committed the state flag, and retriggered on every scroll event - that was
the per-line fading. The parts are resolved once and kept.

The update pill needed a bridge change, not a UI one. Mo2LiveMod had no newest
version and mod_actions.py read versions from the mod list model, which does not
carry one. MO2 exposes it on the mod itself, so the payload now includes
newestVersion from mods.getMod(name).newestVersion(), guarded so builds that do not
expose it report an empty string rather than failing. Mo2LiveMod gained
NewestVersion and a HasUpdate that only claims an update when the newest version
actually differs from what is installed. The row hides the plain version text and
shows a filled pill with a download glyph in its place.

Verified live end to end: the restarted FNV host reports newestVersion for all 14
mods. None of them currently has an update, so no pill renders in this profile -
the plumbing is confirmed, the populated appearance is not. The bridge module was
deployed to the FNV host only; the Skyrim and legacy hosts still run the previous
module and will report no newest version until they are updated too.

Mods and Plugins finished: the two pages now share one set of table entry pieces.
Mo2TableRow owns Add, IconButton and one responsive Fit driven by a
(column, width, threshold) spec; both pages call into it and Mo2ModRow keeps thin
forwarders. Previously both reached into Mo2ModRow, which made the mod row look
like the owner of something both depended on, and each carried its own copy of the
hide/show loop. The Plugins metrics are the shared basis, as preferred, and the
leading grip gutter went from 24px to 16px on both tables.

Row hover and selection are now a darkened rounded bar instead of the default
accent fill: Mo2TableRow.InstallRowStyles adds pointerover and selected styles
using SurfaceMid and SurfaceHigh with no border and an 8px radius, installed on
both tables so the two pages cannot drift. The rows themselves also lost the
square corners and outline they had been pinned to.

Both pages carry the same chrome: header with pictogram, grey description, the
toolbar on the header's right rather than in a row of its own, a separator beneath,
and the animated compaction. Mods additionally lost its single remaining tab strip
and gained the split status control.

Verified live against the FNV host: 13/13 mod rows and 11/11 plugin rows render
with no exceptions, and the offline panel chrome check passes for all seven other
panels. Hover and selection colours are applied through styles; a static capture
cannot show them, so that part is confirmed by construction rather than by picture.

## Session — panel chrome consolidation, physicality and bridge latency (2026-09-15)

Every page the frontend can reach is now audited in one pass by
`MO2_VERIFY_PAGE_AUDIT`, which navigates to each page in turn, captures it to
`artifacts/audit/` and reads its chrome. It reports one row per page and fails on
any page that departs from the shared design, so "every page audited" is a run
rather than a claim. All 15 pages pass: 24px padding, 28px header actions, a
pictogram, a description and a header separator; the 12 pages inside a game
workspace also carry the maximise action, and no game icon carries a badge strip.

Building that audit found real defects rather than confirming a design:

- **Downloads, Profiles, Health Check and Connections had no shared chrome.** The
  first three are drawn by native NMA views with no frontend constructor to call
  `Apply` from; `Mo2PanelChrome.Adopt` brings them in from the view locator.
  Connections drew its own title and subtitle as two text blocks and now carries a
  real `PageHeader`. Home's own pages were adopted too, so Home is no longer the
  one place a page has no separator.
- **Two header-collapse implementations were fighting.** `Mo2ResponsiveHeaders`
  blends the pictogram between 48 and 28 against scroll position and available
  space; `Mo2PanelChrome.Compact` was a second, binary version keyed on a 60px
  pictogram. The responsive one runs from the window's layout and resizes the
  pictogram first, so the binary one never matched its plate and never ran at all.
  `Compact` is gone and the sizes now live with their one owner.
- **`Mo2PanelChromeCheck` had silently stopped checking anything.** It matched the
  header row by type, the row changed from a `DockPanel` to a `Grid`, and the
  type-specific guard stopped matching. It now matches by name, and also asserts
  every visible header action is the shared size — which caught Overwrite and Logs
  still drawing a 62px labelled Refresh where every other panel has a 28px icon.
- **The External Files header drew its search box over its own description.** The
  actions sat in an `Auto` grid column, which is never told how much room it has,
  so they measured at full width and painted over the title. The group is now
  capped at whatever is left after the title's floor, and a page that hands over
  one wide control (Downloads gives its whole toolbar) is told the width itself.
  The maximise action has a column of its own so a wide toolbar cannot push it off.
- **Game icons carried a badge strip and Skyrim's sat on black.** The badge is a
  filled strip across the top right of the icon carrying a profile number that
  means nothing here; it was hidden on the spine only. Skyrim's Steam icon is
  opaque artwork with the black baked in, so a white plate behind it was never
  visible, and every piece of Steam art for that game is dark (its cover averages
  31 of 255). Opaque artwork is now inset on the plate instead.

Panels gained a maximise/restore action that animates to the full canvas and back
without touching the saved layout, and closing a panel now runs an exit animation
before the workspace removes it. The action lives on the page header: a button
inserted into the panel's tab strip laid out correctly, reported visible, and
never painted a pixel. That is why `Mo2PhysicalityCheck` counts the pixels a
control actually changes rather than trusting its bounds.

Physical responses were added where the workspace previously just stopped:
wheeling past either end of a list pulls up to 28px with diminishing notches and
springs back without touching the scroll offset, and a divider pushed past the
workspace's 30% clamp gives against the push and springs back on release.

### Measured latency

| | Before | After |
| --- | --- | --- |
| Action reaching MO2 and returning | 115ms median | **16ms median** |
| Full snapshot | 115ms | 65ms (48ms of it MO2's own work) |
| Periodic poll while nothing changes | a 65ms snapshot every 2s | **no request at all** |
| First snapshot during startup | 2009ms | 1439ms |
| MO2 connected after the window opens | behind first display | 2.8s, ~80ms after the window |

Both sides polled on a fixed tick: the MO2 plugin looked for requests every 100ms
and the frontend looked for replies every 50ms, so every action paid up to 150ms
before any work happened. The host now ticks at 16ms for 0.6s after each request
and idles at 100ms; the frontend's first look is 1ms with a backoff. Bridge I/O
also runs off the UI thread — Avalonia's synchronization context was putting the
wait-for-reply loop back on the dispatcher, which is why a request issued while
the window was building took two seconds to do sixty milliseconds of work.

The two-second poll that exists to notice changes made in MO2 itself now checks
the profile, mods and downloads folder timestamps first and only asks MO2 when one
has moved, with a full read every tenth quiet poll regardless. Verified by
`MO2_VERIFY_REFRESH_CADENCE`.

### Startup, measured properly, and what is still slow

**The earlier figure in this section was wrong.** `MO2_VERIFY_STARTUP` polled with
`Task.Delay`, whose continuations queue on the same dispatcher startup saturates,
so every number it produced was "when the dispatcher went idle" — all three within
a millisecond of each other, and 1.5 seconds after the screenshot already showed
both panels full of rows. It now stamps from the layout pass that first satisfies
each condition, and reports the dispatcher-idle moment separately as its own
figure. On this machine, over three runs:

| | |
| --- | --- |
| First window | 2.66 – 2.73s |
| MO2 connected | 2.75 – 2.82s |
| Mod list built | 4.05 – 4.09s |
| **Rows on screen** | **4.90 – 4.94s** |
| Dispatcher first has room to answer | 6.68 – 6.74s |

Phases before the window: 0.85s of Avalonia and theme initialization before any
frontend code runs, 0.83s constructing the workspace, 0.57s creating the window.
The check refuses to run alongside the rest of the suite, which drives the same
dispatcher it is timing.

Five changes were measured against time-to-first-rows and **four of them did
nothing**, which is worth recording so they are not tried again:

| Change | Effect on rows on screen |
| --- | --- |
| Template preparation in 4ms slices instead of one control per dispatcher turn | none |
| Slices raised to 24ms at Normal priority | none (4.89s vs 4.80s — if anything worse) |
| Panel body posted at Input / Loaded / Normal instead of Background | none; all within noise |
| Panel bodies built one after another instead of together | none (4.88s vs 4.90s) |
| Starting the first MO2 read at construction instead of on window open | connected at 2.8s instead of behind first display |

The slice and priority change to `Mo2TemplatePreparation` is **kept** even though
it does not move first display, because it takes preparation of a panel from
1118ms of wall-clock for 119ms of work down to 78ms, and because at the old
settings the page bodies' preparation bailed out having applied **zero** templates
— the panel was hidden by its own preparation, so `current()` was false the moment
their turn came. It is less waste, not a speed-up, and it should not be described
as one.

What is left between the profile's data arriving (2.8s) and its rows appearing
(4.9s) is Avalonia laying out and rendering the two tables. Roughly 740ms of that
is instrumented (building and preparing the two panels and their two bodies); the
rest is layout and render. It is work rather than waiting, which is why none of
the scheduling changes touched it. **Reducing it means reducing what is laid out —
the control count per row, or row virtualisation — and that is not attempted here.
This remains the main open performance item.**

### Still open from the requested scope

- MO2's own process startup (the Proton host) has not been worked on; only the
  frontend's.
- External Files has not been compared against the FNV-enabled NMA fork in this
  session.
- Missing MO2 functionality has not been surveyed in this session.

### Two more defects the audit work turned up

**External Files headed its columns in capitals** — `NAME`, `SIZE`, `FILE COUNT` —
where every other table in the frontend, and NMA itself, uses title case. The
headers now come from NMA's own column definitions
(`SharedColumns.NameWithFileIcon`, `ItemSizeOverGamePath`, `FileCount`), which
MockHost already references, so they read `Name`, `Size`, `File Count` and cannot
drift from the app this page is meant to match. `--check-external-columns`
compares them against those definitions rather than against typed-out strings.

**Dragging a tab out to split a panel was unreachable.** A panel holding one tab
hides its tab strip, and `Mo2TabDragDrop` drags by the tab header inside that
strip — so with the default Mods/Plugins layout, where each panel holds one tab,
there was nothing to drag. The strip now returns when the pointer reaches the top
44px of a panel and goes away again when it leaves, so the page stays clean at
rest and the handle is there when it is reached for. `MO2_VERIFY_TAB_DRAG` passes
again: five drop zones and their previewed regions, an edge drop splitting into
two panels, a tab drop rejoining to one, and a self-drop ignored.

That same hidden strip is why an earlier attempt to put the maximise action in the
panel's tab header laid out correctly, reported visible, and never painted: its
whole container was `IsVisible=false`. The action lives on the page header
instead, and `Mo2PhysicalityCheck` now counts the pixels a control actually
changes in the rendered window rather than trusting its bounds.

### Running the checks

These are opt-in and some of them rearrange the workspace, so order matters. The
set below passes together on the FNV host:

```
MO2_VERIFY_PAGE_AUDIT=1 MO2_VERIFY_PANEL_CHROME=1 MO2_VERIFY_PHYSICALITY=1 \
MO2_VERIFY_MAXIMISE=1 MO2_VERIFY_REFRESH_CADENCE=1 MO2_VERIFY_COLUMN_TOGGLE=1 \
MO2_VERIFY_TAB_DRAG=1 MO2_PAGE_AUDIT_DIRECTORY=frontend/artifacts/audit \
MO2_SCREENSHOT=frontend/artifacts/audit-final.png frontend/run-live.sh <bridge>
```

`MO2_VERIFY_TAB_DRAG` runs last because it navigates to Home and splits panels;
`MO2_VERIFY_STARTUP` refuses to run alongside any of them.

`MO2_VERIFY_ROW_MENUS` needs `MO2_SCREENSHOT` set, like the other checks in that
group, and asks MO2 to open its own menus, so run it with the live host connected
and not alongside `MO2_VERIFY_QT_WIDGETS`, which navigates the same panel.

`MO2_VERIFY_ENTRY_MENUS` and `MO2_VERIFY_SHARED_LISTS` both require a temporary
`MO2_FRONTEND_LAYOUT`, and under the single narrow panel a fresh layout gives them
they report the responsive columns as missing and find no attached mod row. That is
the layout, not the pages: both fail identically before and after the row-menu work.
Run them against a layout that holds the two lists side by side.

### First missing MO2 functionality found and added

Comparing `src/mainwindow.ui`'s 24 main-window actions against what the frontend
can reach: Install Mod, Profiles, Executables, Tool Plugins, Browse Mod Page,
Notifications, Manage Instances, Log and Refresh all have routes. **Settings had
none at all** — the frontend's own gear opens its Connections page, and MO2's
settings dialog (game paths, Nexus integration, plugin settings, the downloads
directory) was unreachable from the frontend.

A new `openOriginal` bridge action triggers a named MO2 main-window action from an
allowlist held in the plugin (`settings`, `notifications`, `endorse`, `nexus`,
`help`) rather than taking a name from the request, and it goes through the same
profile guard as every other editing action. Connections now offers **MO2
settings…** and **MO2 notifications…**; MO2 owns the dialog and everything it
writes. `--check-original-actions` passes against the live host: the action
exists, a name off the allowlist is refused, and a request with no name is
refused. Opening the dialog itself is not exercised — it is modal and waits for
whoever opened it.

Connections now carries **MO2 settings…** as its own button, and a **More MO2
actions** menu holding notifications, Check for MO2 updates, Endorse Mod
Organizer, Visit Nexus and MO2 help — the remaining main-window entries, behind
one control because they are used rarely. Every one of MO2's 24 main-window
actions now either has a route from the frontend or is MO2's own window chrome
(toolbar/menu/status-bar toggles and Exit), which the frontend does not have.

MO2 owns each dialog and everything it writes; nothing is reimplemented here.

## Every widget MO2 puts beside a list, worked rather than found

The widget check reported all five of MO2's tabs carrying their furniture, and the
behaviour check drove some of it. Between them, **eleven controls were drawn and
never touched**: Plugins' Sort, Restore, Save and its active count, the mod pane's
Restore, Save and its And/Or pair, Data's Conflicts only, Show hidden files and
Refresh, and Downloads' Refresh. Each is now worked the way a user works it and
read back against what MO2 holds, and two of them turned out to be doing nothing.

**Plugins' Sort, Restore and Save stood drawn live whatever MO2 said.** MO2 greys
its own sortButton out when the managed game has no sorting, and greys the whole
pane out while one of its dialogs is up; the action behind each of these three
returns without a word when MO2 cannot take it, so pressing one went nowhere and
said nothing. The mod pane's pair of backup buttons already followed MO2 this way
and the toolbar's own Sort already carried MO2's reason — only the three on MO2's
espTab row did not. They now take the same state, and Sort shows MO2's own reason
when it is off. On this FNV host that reason is MO2's:

> LOOT sorting is disabled by the Fallout NV Support Plugin. Its default follows
> the FNV modding community recommendation. To opt in, open MO2 Settings > Plugins
> > Fallout NV Support Plugin and enable enable_loot_sorting.

**Downloads' Refresh refreshed nothing.** MO2's btnRefreshDownloads runs
`DownloadsTab::refresh`, which is `downloadManager()->refreshList()` — the folder is
read again and the `.meta` beside each archive with it. This one recomputed which
of the page's own controls were live and stopped there, which looks identical from
the outside, so a download added or removed outside MO2 never appeared. A new
`refreshDownloads` bridge action presses MO2's own button, waking its lazy download
tab first as the Query Metadata route already does, and the page then applies the
snapshot that comes back.

What the two Save buttons do is checked against the files MO2 writes rather than
against anything the frontend holds: MO2's slot flushes the list and copies each
file beside itself under the second it was taken, so the check presses the button,
waits for `modlist.txt.<stamp>` (and for plugins, all three of `plugins.txt`,
`loadorder.txt`, `lockedorder.txt`) to appear in MO2's own profile folder, and then
removes the copies it caused. All 24 profile files hash identically before and
after every run in this session.

Restore is not pressed. MO2's picker is modal and waits for whoever opened it, so
what is checked is that the button is live exactly when MO2 will take it — over the
same `orderBackup` route the Save beside it was driven through. Sort is not pressed
either: it is MO2's LOOT run over the real load order. Both say so in the check's
own output rather than being reported as exercised.

Four controls had nothing to exercise them with on this host and say so by name
rather than passing quietly: the And/Or pair (no two of MO2's categories here would
answer differently), Data's hidden files (MO2 hides none), its archive-served rows
under the Data root, and the hidden-downloads box.

### Two checks were reporting a design the app no longer has

`MO2_VERIFY_PANEL_CHROME` and `MO2_VERIFY_COLUMN_TOGGLE` both failed, and had been
failing since the tab toolbars were removed — neither failure is a defect in the
app. Panel chrome demanded every page draw actions on its header line; the header
actions group is deliberately empty, because MO2 carries no toolbar on a tab.
Column toggle looked its button up on the Archives page; MO2 offers a column
chooser on one list only — the downloads header's right-click menu — and its
bsaTab, dataTab and savesTab have none.

Both now assert what the app actually does, including the negative: a page that
starts drawing a toolbar, or Archives drawing a chooser MO2 has not got, fails.
Panel chrome failing on its first panel had been taking the other six down with it,
so seven panels' padding, compaction and separators were going unchecked as well.
Verified as pre-existing by building the unchanged sources and reproducing both
failures before the fix.

**Found and not yet done:** MO2's downloads header carries a column chooser and
this frontend's Downloads has none at all. That is the next gap on this list.

### Running these two

Both navigate the selected panel, so run each on its own:

```
MO2_VERIFY_WIDGET_BEHAVIOUR=1 MO2_SCREENSHOT=frontend/artifacts/widget-behaviour-full.png \
frontend/run-live.sh <bridge>
MO2_VERIFY_QT_WIDGETS=1 MO2_SCREENSHOT=frontend/artifacts/qt-widgets-full.png \
frontend/run-live.sh <bridge>
```

Evidence: [widget-behaviour-full.log](artifacts/widget-behaviour-full.log),
[qt-widgets-full.log](artifacts/qt-widgets-full.log). The audit group now passes
nine for nine including `MO2_VERIFY_SEPARATOR_COLOR` and `MO2_VERIFY_DENSITY`, and
`MO2_VERIFY_ROW_MENUS` and `MO2_VERIFY_TAB_DRAG` pass after the bridge change.

`refreshDownloads` is a plugin-side action, so MO2 must be restarted after copying
`frontend/mo2-plugin/nexus_frontend_bridge` over the host's `plugins/` copy. MO2
2.5.2 hung after its window closed on this host and had to be terminated; it had
already removed its bridge endpoint and written its state, and the profile files
were unchanged across the restart.

## The chooser MO2 has, and the two borders it draws

### Downloads' column chooser

MO2 offers a column chooser on exactly one of its lists: right-clicking the
downloads header lists every column but the name as a checkbox
(`DownloadListView::onHeaderCustomContextMenu`, which counts from column 1), and
its list starts with **Mod name, Version, Nexus ID and Source Game hidden**
(`setManager`). This page drew all eight columns and offered no way to choose —
both more than MO2 shows and less than MO2 offers.

The chooser is now on the heading strip, where MO2 puts it, over the same
`Mo2ColumnToggle` the folder pages use. Three things were added to that class for
it: a default hidden set, so a page that has never been opened starts where MO2
starts and **Reset to default** returns there rather than to everything-shown; and
a pinned set, so the name is left off the menu entirely as MO2 leaves it off.
Panels that name neither keep exactly what they had.

The page's column widths used to be written out by position — `(2, 90, 430)` and
so on — which would have set the wrong column's width the moment one was hidden.
They are keyed by MO2's own column names now and read off whatever is drawn.

### A filtered list says so

MO2 rings its mod list in red while a filter is on and rings the active-mod count
with it, and rings the list in green while it is grouped and not filtered
(`ModListView::onModFilterActive`). Without it a filter left on looks exactly like
mods that have gone missing, which is why MO2 draws it. The frontend drew neither.

MO2's own `#f00` and `#337733` at 2px. Its border is a Qt ridge, which Avalonia has
no equivalent for, so this is solid — and it is drawn **over** the list rather than
around it: wrapping the list moved the mod table 2px off the plugin table beside
it, which `MO2_VERIFY_ROW_PADDING` caught immediately.

`ModsClearFiltersButton` and `ModsCurrentCategoryLabel` — MO2's `clearFiltersButton`
and `currentCategoryLabel` — were already implemented and already followed the
filter, but nothing had ever driven them. They are driven now: typed into, read for
what they say the list is narrowed to, and pressed to clear it. Neither is in the
Qt widget check's list, because MO2 shows the Clear only while there is something
to clear and the label is empty until there is; a check that demanded them at rest
would be demanding what MO2 does not draw.

### Painted, not merely assigned

The ring is checked by rendering the window, fading the border out, rendering again
and counting the pixels that changed — 3,296 of them on this host. A brush on a
control that never reaches the screen reads correct from every property on it,
which is the fault this whole check exists for. Same technique as
`Mo2PhysicalityCheck`, for the same reason.

### Where MO2's widgets were read from this time

`src/mainwindow.ui` parsed for every named widget under each tab, rather than a
list typed out by hand:

| MO2 | Widgets |
| --- | --- |
| mod pane | categoriesGroup, filtersClear, filtersEdit, filtersAnd, filtersOr, filtersSeparators, profileBox, listOptionsBtn, openFolderMenu, restoreModsButton, saveModsButton, activeModslabel, displayCategoriesBtn, currentCategoryLabel, clearFiltersButton, groupCombo and two labels |
| espTab | sortButton, restoreButton, saveButton, activePluginsLabel |
| dataTab | dataTabRefresh, dataTabShowOnlyConflicts, dataTabShowFromArchives, dataTabShowHiddenFiles, dataTabFilter |
| downloadTab | btnRefreshDownloads, btnQueryDownloadsInfo, showHiddenBox, downloadFilterEdit |
| bsaTab, savesTab | none — both are the list alone |

Every one of them now has a counterpart that is driven by
`MO2_VERIFY_WIDGET_BEHAVIOUR`, which reports 56 statements on this host.

**Still open:** `executablesListBox`, `startButton` and `linkButton` — MO2's run
row — have not been compared against the frontend's launch panel. The plugin list's
own filter field is the frontend's own; MO2's `espFilterEdit` is a
`MOBase::LineEditClear`, which has a clear affordance this has not been compared
against.

## Every page's own actions had been gone, and nothing said so

`MO2_VERIFY_REACHABLE_ACTIONS` is new, and the first thing it did was fail on all
seven pages it covers:

```
FAIL reachable actions: Tools cannot reach ManageToolsButton, RefreshToolsButton;
Archives cannot reach BrowseArchive, ExtractArchive, RefreshArchives;
Data cannot reach Button, Button, RevealDataFile, Button, Button;
Saves cannot reach Button, Button, Button, Button;
Overwrite cannot reach OpenOverwriteFiles, Overwrite_create, Overwrite_move,
  Overwrite_sync, Overwrite_clear, RefreshOverwrite;
Logs cannot reach Button;
External Files cannot reach ImportExternalFiles, CleanupExternalFiles,
  RestoreExternalFiles, RevealExternalFiles, RefreshExternalFiles
```

Twenty-six actions. Every page hands its actions to `Mo2PanelChrome`, which
detaches each one from the row the page had built for it — and the group it hands
them to has never drawn anything, because MO2 carries no toolbar on a tab. So
**Overwrite's create, move, sync and clear**, External Files' import, cleanup and
restore, Tools' add-or-edit-programs, Archives' Browse and Extract, Data's parent
folder and every one of those pages' Refreshes existed, were wired, and could not
be reached from the window at all.

Nothing about the pages said so. Each control was still constructed, still had its
handler, still reported the right size and the right enabled state — it simply was
not in the tree. This is the same class of fault as the two dead buttons found
earlier, one level up: the check that would have caught it was itself asserting
against the header line and had been failing, uninvestigated, since the toolbars
went.

They are drawn now in **the page's own row under the separator**, which is where
MO2 puts a tab's own controls — dataTab's refresh and its three boxes,
downloadTab's two buttons. The title's line stays empty, because MO2 puts nothing
there, so `MO2_VERIFY_PANEL_CHROME` still holds.

### The check

`Mo2PanelChrome` records what each page handed it. The check builds each page, lays
it out, and requires every handed action to be in that page's tree, effectively
visible, with a size. Its list of deliberate exceptions is **empty and meant to
stay that way**: a page that builds an action and wires it has said the action is
worth having, and the only honest answers are to draw it or to stop building it.

```
MO2_VERIFY_REACHABLE_ACTIONS=1 MO2_SCREENSHOT=… frontend/run-live.sh <bridge>
```

### Two checks were asserting around the hole

`MO2_VERIFY_FOLDER_PAGES` wanted the actions on the header line and had been
failing on every page since the toolbars went. It looks in the page's own row now
and reports 7, 7 and 8 icon actions on External Files, Data and Overwrite.

`MO2_VERIFY_COLUMN_TOGGLE` is corrected a second time, and the correction last time
was wrong. It first wanted the chooser on the header line; the previous turn
changed it to require Archives **not** to draw one, reasoning from MO2's bsaTab
having none — but Archives was not drawing it because it could not draw any of its
actions, which was a bug, not a decision. MO2's own chooser is on its downloads
header, which is where this frontend's is; the chooser on the folder pages is the
frontend's own, for columns MO2's tabs do not carry, and it belongs in the action
row with the rest. The check now requires it there and drawn.

**Recording this plainly because it is the more important finding:** a check that
has been failing is not evidence of anything, and two of them here were quietly
covering a real hole. A failing check has to be investigated on the turn it starts
failing, not reasoned about later from its output.

### Still out of that check's reach

Mods and Plugins hand over their column choosers and their own toolbars — the
search magnifier, Sort with LOOT, the overflow menu — and those are still not
drawn, because both pages' header stacks are not shown in the panel layout (their
tab strip carries the title instead). Neither page is in the check's list yet.
MO2's own mod-pane and espTab furniture is reproduced separately as their Qt bars
and is verified, so nothing MO2 has is missing there; what is unreachable is the
frontend's own. That is the next thing to settle.

Also still open: MO2's `<Edit...>` entry, which is the first row of its
`executablesListBox` and opens its Edit Executables dialog before restoring the
previous selection. The frontend reaches that dialog from Tools instead. And
`linkButton`'s menu — Toolbar and Menu, Desktop, Start Menu, each showing add or
remove by whether the shortcut exists — has no counterpart beside the launch panel.

## In the layout this frontend is meant to be used in, no page had its actions

The previous turn put every page's actions into a row of its own and the check
passed. It passed because the check built each page on its own. Adding the two live
list pages to it, read in the open window, failed at once:

```
FAIL reachable actions: My Mods cannot reach ColumnsButton, Toolbar;
                        Plugins cannot reach ColumnsButton, PluginsToolbar
```

`MO2_REACHABLE_TRACE=1` prints the chain from a lost control up to its page with
what each link says about itself, and named the link in one line:

```
TRACE My Mods ColumnsButton: ColumnsButton[vis=True,eff=False,w=24]
  < PanelActionRow[vis=True,eff=False,w=120]
  < PanelHeaderStack[vis=False,eff=False,w=506]   <-- here
  < Panel[vis=True,eff=True,w=506] < ...
```

`Mo2ResponsiveHeaders` hides a page's whole header stack when its panel is not
alone in the workspace: a shared panel is named by its tab strip, so its page
header would say the same thing twice. That is right for the title, the pictogram,
the description and the rule under them. It is not right for the page's own
actions, which the tab strip does not carry and nothing else offers — and **the
default layout is two panels**, so in the layout this frontend exists to
reproduce, every page's actions were off screen. The action row stays now; the
rest of the header still stands down.

This is the second time the same hole has been found one layer further out. Worth
stating as a rule: **a check that builds a page beside the window is not checking
the page the user has.** The live pass is what caught it, and it is the pass to
extend first next time.

## MO2's `<Edit...>`

MO2's `executablesListBox` opens with its own `<Edit...>` row
(`MainWindow::refreshExecutablesList`); choosing it opens the Edit Executables
dialog and puts the previous choice back
(`on_executablesListBox_currentIndexChanged`). The frontend's box listed
executables only, and that dialog was reachable from the Tools page and nowhere
else — not from where the executable about to be run is chosen.

The entry is MO2's own wording and MO2's own position, and choosing it restores the
previous selection **before** handing over rather than after: MO2's dialog is modal
and the bridge call waits on it, and a box reading `<Edit...>` for as long as that
dialog is open is not what MO2 shows.

The check reads it where it sits and does not choose it — that would open a modal
MO2 dialog and wedge the host for the rest of the run, the same reason Restore,
Sort and the category editor are not driven. It reads the run row off
`Mo2LiveWorkspace.LaunchPanel` rather than out of the window, because a collapsed
sidebar — which is this host's saved state — moves the whole row into its tool
flyout, where nothing in the window's tree can find it.

**Still open:** `linkButton`'s menu — Toolbar and Menu, Desktop, Start Menu, each
drawn with an add or a remove icon by whether that shortcut already exists — has no
counterpart beside the frontend's run row. It needs a bridge action and therefore
an MO2 restart, which is why it is not in this turn.

## MO2's shortcut menu

MO2's `linkButton` sits beside its executables box and offers three places to put a
shortcut to the chosen executable: **Toolbar and Menu**, **Desktop**, **Start
Menu**. It decides which way round each entry reads only when the button is
pressed — `on_linkButton_pressed` sets the remove icon where the shortcut already
exists and the add icon where it does not. The frontend had no counterpart at all.

The new `shortcutMenu` bridge action presses MO2's own button, which is what makes
those icons current for the executable asked about, and compares each against MO2's
own `:/MO/gui/remove` resource. The answer is MO2's rather than a second guess at
where MO2 keeps its shortcuts; nothing here looks for `.lnk` files or reimplements
`env::Shortcut`. With no entry named it reports the three; with one it triggers
MO2's own action, so the toggling, the toolbar update and the shortcut files are all
MO2's.

The frontend draws MO2's three under MO2's own captions, worded by what MO2 just
reported — **Add to Toolbar and Menu** where MO2 would draw its add icon, **Remove
from …** where it would draw remove. Read when the menu opens rather than held: MO2
is the only thing that knows whether a shortcut is still there, and one can be
removed outside this window.

The check drives **Toolbar and Menu** through to MO2 and back, because that one is
MO2's own executable setting rather than a file in its Windows prefix and MO2
reports it back from the icon it draws. On this host it went on and back off for
NVSE, and all three of MO2's `toolbar=` flags in `ModOrganizer.ini` read `false`
afterwards, as they did before. Desktop and Start Menu are offered and reported but
not driven: both would write into the Proton prefix's own shell folders, which is
not this desktop's.

One thing the route needed that was not obvious: every bridge action past `snapshot`
goes through a guard that refuses anything not naming the profile it is for, so the
first version answered nothing and said **"Active MO2 profile changed; refresh
before editing"** — which the check only surfaced because it was changed to report
the reason MO2 gave rather than just the emptiness. A route that answers nothing and
says nothing is the same shape of fault as a button that draws and does nothing.

`refreshDownloads` and `shortcutMenu` are both plugin-side, so MO2 must be restarted
after copying `frontend/mo2-plugin/nexus_frontend_bridge` over the host's `plugins/`
copy. MO2 2.5.2 hung after its window closed again on this host and was terminated
once its window was gone; the profile files were unchanged across the restart.

**Every widget in `src/mainwindow.ui` now has a counterpart that is driven live.**
The mod pane's eighteen, espTab's four, dataTab's five, downloadTab's four, and the
run row's three — the executables box with MO2's `<Edit...>`, Run, and the shortcut
menu. bsaTab and savesTab are the list alone in MO2 and here.

## Reachability under MO2's own layout, with a negative control

Every reachability result so far was read in whatever layout happened to be saved.
The layout this frontend exists to reproduce is MO2's own preset — the mod list on
the left, its five tabs on the right — and **in that layout every panel is shared**,
which is the exact condition that had been hiding every page's action row. Nothing
had checked it there: `MO2_VERIFY_REACHABLE_ACTIONS` builds its pages on their own,
where a page is always the whole panel.

`MO2_VERIFY_PRESET_FIT` already applies the preset and walks all six tabs, so the
assertion went there. It now reports, per tab, how many actions the page handed over
as well as how many widgets were examined — so a run where it found nothing to judge
reads as nothing rather than as agreement:

```
PASS MO2 preset fit: … My Mods (47, 2 handed), Plugins (30, 2 handed),
Data (29, 7 handed), Archives (29, 5 handed), Saves (9, 6 handed),
Downloads (21, 1 handed) is drawn whole inside its panel
```

**Negative control.** The fix from the previous turn was reverted, the host rebuilt,
and the same check run again:

```
FAIL MO2 preset fit: My Mods: it cannot reach ColumnsButton, Toolbar |
Plugins: it cannot reach ColumnsButton, PluginsToolbar |
Data: it cannot reach Button, SearchToggleButton, Button, RevealDataFile, Button,
  ColumnToggleButton, Button |
Archives: it cannot reach SearchToggleButton, BrowseArchive, ExtractArchive,
  ColumnToggleButton, RefreshArchives |
Saves: it cannot reach SearchToggleButton, Button, Button, Button,
  ColumnToggleButton, Button |
Downloads: it cannot reach Toolbar
```

All twenty-three, on all six tabs. The source was then restored byte for byte from
the copy taken before the revert, rebuilt, and the check passes again. An assertion
that has never been seen to fail is not evidence that the thing it asserts is true,
and this one had only ever been seen to pass.

## One pin list instead of two

The row of shortcuts beside Run was drawn out of a file of the frontend's own, and
MO2 keeps that list itself — the executables it shows on its toolbar and in its Run
menu (`MainWindow::updatePinnedExecutables`). The same idea was held in two places
and could disagree: an executable pinned in MO2 did not appear beside Run, and one
pinned here was unknown to MO2.

MO2's toolbar is the answer for executables now. The snapshot reports
`pinnedExecutables`, read off MO2's own toolbar — the actions it names `custom__…`
— rather than out of its settings file, so it is what MO2 actually decided to show
and it moves as soon as MO2 is told to move it. Pinning an executable, from this row
or from the Tools page, goes through MO2's own **Toolbar and Menu**. Tool plugins
stay in the frontend's file, because MO2 pins no tool plugin.

The check reads the row against MO2's toolbar and then moves one through MO2 and
back: on this host New Vegas went onto the row and came off again, the row came back
to exactly what it read before, and all three `toolbar=` flags in `ModOrganizer.ini`
are `false` as they were.

## Where the goal stands

| Requirement | Evidence |
| --- | --- |
| All pages reproduce MO2's Qt widgets | `MO2_VERIFY_QT_WIDGETS` passes over all 73 widgets **parsed out of `src/mainwindow.ui` at run time**, each accounted for both ways round; shown to fail when a counterpart's name is taken away |
| All MO2's menu and toolbar actions | `MO2_VERIFY_QT_ACTIONS` passes over all 24 actions parsed out of the same file — 15 found live, 9 named as Qt's own furniture; shown to fail when an entry is reworded |
| As visible in the window as in MO2 | `MO2_VERIFY_PRESET_FIT` passes in MO2's own layout, every widget on all six tabs drawn whole and none squeezed to nothing; `MO2_VERIFY_REACHABLE_ACTIONS` and the preset check's own reachability pass, the latter shown to fail when the fix is reverted |
| Under MO2's matching layout preset | The preset check applies it and walks all six tabs; `MO2_VERIFY_LAYOUT_PRESET` covers the preset and its Undo |
| Separator colours work | `MO2_VERIFY_SEPARATOR_COLOR` passes — MO2 coloured the separator through its own action, read back both ways, each name legible against it |
| All content fits | `MO2_VERIFY_HEADER_FIT` at 13 sizes, `MO2_VERIFY_PRESET_FIT` at MO2's own panel widths, now including a control given no width at all |
| As dense as MO2 | `MO2_VERIFY_DENSITY`: every page within 4px of MO2's 22px line |
| All MO2 behaviour | `MO2_VERIFY_ROW_MENUS` compares all six of MO2's own row menus both ways round; `MO2_VERIFY_QT_ACTIONS` drives MO2's own window actions |
| Every widget tested live | `MO2_VERIFY_WIDGET_BEHAVIOUR` works 27 of the 30 widgets MO2's file declares workable, against the live host and never a fixture, and names the other three and why each cannot be driven; the count comes from MO2's file and from what the check actually reached for, so a widget it stops working fails it |

What each check cannot say is said in its own output: the entries with nothing on
this host to exercise them name themselves rather than passing quietly, and the
three MO2 dialogs that are modal — Restore, the category editor and Edit
Executables — are reached but deliberately not opened.

Still open and unchanged by this work: cold first-display latency, and the combined
end-to-end login/download/install/gameplay run.

## A full sweep of the checks, and a correction to how they were being verified

Running the widget/page/layout checks that had not been exercised this session
turned up nine failures. **None of the nine was a defect in the app.** Three were
checks looking for something the app deliberately no longer has, three were
missing a precondition, two were looking in the wrong place after a control
changed shape, and one was run against a host that had died. Each is recorded
below with what it actually was, because "the check failed" and "the app is wrong"
are different claims and this sweep produced only the first.

| Check | Why it failed | What was done |
| --- | --- | --- |
| `SHARED_LISTS` | Read a column's heading only if it was a word, so the four MO2 draws as a glyph read as missing | Reads the name off whichever heading a column has; both lists confirm MO2's 13 and 8 |
| `ENTRY_MENUS` | Wanted a five-item `ContextMenu` on the row, replaced by MO2's own menu on a `ContextFlyout` | Checks what still matters — nothing built before opening, same objects on reopen |
| `TOPBAR` | Required all six native Nexus actions dead; two are deliberately live since the account work | Four held to disabled, two held to their own conditions |
| `CONTROLS` | Looked for a plugin button by an old caption and pressed a `MoveModEarlierButton` that no longer exists, MO2 having no such button either | Uses the name, and the move the frontend actually offers |
| `PAGE_REUSE` | Read the body off `Content`, which became the surface when the loading overlay went in; also ran while the dispatcher was still building first rows | Reads the view's own body and its loaded model, and takes a settled turn |
| `ICON_ALIASES` | Needs the original icon style file, which the alias work replaced | Recovered from upstream's own git history; passes all 160 rules |
| `SORTED_ROOTS_UI`, `PLUGIN_ROW` | Need an isolated layout and the Plugins page open | Run against a paired layout built for it |
| `SEPARATOR_INTERACTIONS` | Ungated, so the screenshot path shut the app down mid-check and left its fixture in MO2 | Takes a turn now, so the run lasts until it has cleaned up |
| `FILTERED_ROWS` | MO2 had died during the run | Re-run against a live host |

### The correction

The profile-restoration evidence quoted in the turns above — "all 24 profile files
hash identically" — **was not comparing hashes.** The command behind it was

```
find . -type f -printf '%P ' -exec sha256sum {} \; | awk '{print $1, $2}'
```

and every profile path here contains a space, so `$1` and `$2` took two halves of
the *filename* and the digest never entered the comparison at all. It compared file
names to file names and could not have failed. Use this instead, which is what the
saved baseline `artifacts/fnv-profile-baseline.sha256` was taken with:

```
find . -type f -print0 | sort -z | xargs -0 sha256sum
```

Checked properly, one thing had in fact been left changed: **`The Mod Configuration
Menu.esp` was disabled**, by one of the runs that was killed by a timeout part-way
through its restore. `plugins.txt` held ten plugins where the window had shown
eleven all session. It was put back with MO2 stopped — MO2 writes that file from
its own state, so editing it offline is the supported way — and MO2 restarted:
the plugin is enabled again, the count reads 11, and the window matches the
screenshots taken earlier in the session. A stray `__Separator interaction check`
separator left by the same class of interruption was removed the same way.

The lesson is the one this audit keeps finding one layer out: a check that cannot
fail is not evidence, and that applies to the commands used to verify the checks
just as much as to the checks themselves.

## The widget list MO2 writes, not the one we remembered

The line in the table above used to say that every widget named in
`src/mainwindow.ui` was checked, "parsed from it, not typed out". **Nothing parsed
it.** `Mo2QtWidgetCheck` carried a hand-written array of 38 names over five pages,
and no code anywhere in `frontend/` opened `mainwindow.ui`. A list like that agrees
with MO2 on the day it is written and says nothing afterwards, which is the same
fault as an assertion that cannot fail.

`Mo2QtWidgetSource` now reads the file at run time — every `<widget>` with a name,
the tab it sits in, and MO2's own `visible` property, which is how MO2's
`clearFiltersButton` is known to start hidden rather than that being decided here.
The check accounts for all **73** of them, both ways round: a widget MO2 has that
nothing answers for fails, and an answer for a widget MO2 has removed fails too.

MO2's file holds 73 where the typed list held 38. What the missing 35 hid:

* **MO2's readout of what the mod list is narrowed to was drawn 0px wide.**
  `currentCategoryLabel` sits in the stretching column of the row under the list,
  and in a panel beside another panel that row had 216px for the 305px its six
  widgets ask for — so the stretching column went to nothing and MO2's readout, and
  its Clear all Filters button with it, were squeezed out of existence.
* Neither the mod list nor the plugin list, the Saves tab, the run row, the status
  line, MO2's menu bar or its toolbar had ever been looked for at all.

The row now takes two lines when one will not hold it, in MO2's own order read left
to right and then down, rather than one of the six taking none. What it needs is
measured from the theme's own metrics rather than compared against a number typed
into the check.

`MO2_VERIFY_PRESET_FIT` could not have caught that: it skipped any control with no
width, because what it looks for is a control that *leaves* its panel, and one
squeezed out of existence is not there to be found leaving anything. It now reports
those too — excluding a caption with nothing to say, which is MO2's own behaviour
for that label, and a scrollbar's own template parts, which are zero-sized by
design.

**Negative control.** The names were taken off the two "Active:" captions and the
check named exactly those two, on exactly the two pages MO2 puts them on. Restored
byte for byte (`sha256` compared before and after) and it passes again.

## MO2's menus, which held no widget and so were never checked

MO2's menu bar and toolbar carry `QAction`s, not widgets, so walking widgets said
nothing about them. An earlier turn compared those 24 actions against the frontend
**by hand** and found Settings unreachable; nothing kept that comparison true
afterwards.

`MO2_VERIFY_QT_ACTIONS` parses the same file's action list and requires each of the
24 to be answered: 15 are found live through the control that offers them, and 9
are named as Qt's own furniture — toolbar icon sizes, icons-or-text, and the
toggles for Qt's own menu bar, toolbar and status bar, none of which this frontend
draws. The menu widgets in the widget check are answered by requiring every action
MO2 puts on each of them to be one this check offers, so `menuFile` is tied to its
five entries rather than to a sentence.

Driving them turned up one thing worth keeping: **Browse Mod Page is offered by 2
of 12 mod rows.** MO2 gates it on a mod it knows on Nexus. Asking the first row and
reporting "this host has none" — which is what the check did first — would have
read as agreement on a host where it was simply the wrong row.

**Negative control.** One entry was reworded from "MO2 help…" to "MO2 assistance…"
and the check named `actionHelp` and the control it was looking in. Restored byte
for byte and it passes again.

## Checks that said nothing, and read as checks that passed

`MO2_VERIFY_PANEL_CHROME` and `MO2_VERIFY_COLUMN_TOGGLE` printed no verdict at all.
Neither registers a turn with `Mo2CheckTurn`, and `MO2_SCREENSHOT` — which is what
ends a run — waits only for the checks that do, so both were shut down part-way
through and printed nothing. In a summary that greps for `FAIL`, silence is
indistinguishable from a clean run. Run without that path, both pass.

`frontend/tools/verify.sh` is the runner, so this cannot be got wrong by hand
again. It reports **NO VERDICT** as a failure, gives the checks that need one a
freshly built isolated paired layout (`tools/paired_layout.py`), recovers the
original icon styles from upstream's own history for the alias check, and ends the
unregistered checks itself once they have printed a verdict and gone quiet —
rather than waiting out a seven-minute timeout that told us nothing more.

## Three checks that were describing a shape the app no longer has

`MO2_VERIFY_FILTERED_ROWS` had never run in this audit: it refuses to start without
an isolated paired layout, and every previous run had reported that refusal rather
than a result. Given one, it failed — and each failure was one layer of the same
kind of staleness:

| What it said | What it was |
| --- | --- |
| "filtered rows did not settle" | Three different waits shared one message. Each now says which it is. |
| Row 0 "has no drag handle" | Only separators carry a grip since MO2's own menu went onto the row's `ContextFlyout`. The check now asks for the menu that is there. |
| "Not every native linked/conflict row was observed" | MO2 named `data` — the game's own Data folder, which MO2 draws no row for either. Named in the pass line rather than demanded as a row. |

Neither of the first two was a defect in the app; the third was a check asking for
something MO2 itself does not draw. All three failure messages now name what they
found, because "the check failed" without "here is what it saw" cost three runs
each to narrow down by hand.

**Negative control.** The mod row's own `ContextFlyout` was deleted and the check
**passed** — the first version of the menu assertion accepted a flyout anywhere
under the row, and a mod's Notes cell carries a colour menu of its own that
answered for it. Tightened to the row's own entry, the same deletion fails it,
naming the row. Restored byte for byte and it passes again.

## Host state left behind by an interrupted run

`MO2_VERIFY_CONTROLS` threw `Sequence contains no matching element`. Read against
MO2's own snapshot, the cause was that **`The Mod Configuration Menu` and `MCM
Author Examples` were both disabled** — so the plugin the check works with was
being served by MO2's `Unmanaged:` backdrop instead of by the mod, and no plugin
had that mod's name. Compared against `artifacts/fnv-profile-baseline.sha256`
properly, `modlist.txt` and `plugins.txt` were the two files that differed.

Both mods were enabled again through MO2's own `setModActive`, and the check
passes. Two things to record honestly rather than round off:

* MO2 wrote three more plugins into `plugins.txt` as a consequence of enabling
  `MCM Author Examples`, which is MO2 following the mod rather than anything this
  frontend did.
* The profile still does not hash identically to that baseline, and cannot: the
  baseline predates the removal of the stray `__Separator interaction check`
  separator recorded in the previous section. The baseline is stale, not the
  profile. It is left in place rather than silently re-taken, because a baseline
  rewritten to match whatever is there now is the same shape of fault as an
  assertion that cannot fail.

## Which of MO2's widgets were never worked

`MO2_VERIFY_WIDGET_BEHAVIOUR` says what each widget did when it was worked. It said
nothing about which of MO2's widgets were never worked at all, so "every widget is
driven" rested on reading the check rather than on the check.

The controls it reaches for are now recorded as it reaches for them, and MO2's own
file says which there are to reach for. Every widget MO2 declares that a user can
work — button, box, tick, radio, field: **30** of them — has to have been reached,
or to be named in the check with the reason it cannot be. It reports the count:

```
PASS MO2 widget behaviour: all 27 of MO2's 30 workable widgets were worked — …
Not worked, and why: filtersEdit opens MO2's category editor, which is modal;
startButton runs the game; btnQueryDownloadsInfo asks Nexus about every download,
which needs an account and changes MO2's metadata
```

**The assertion found one on its first run:** `displayCategoriesBtn`, the button
that shows and hides the filter list beside the mod list, was driven only by the
widget check — which is about what is drawn. Its behaviour now has a case of its
own here, and the same run that named it passes with it. That is the negative
control for this assertion as well: it named exactly the widget that was not being
worked, and stopped naming it when the widget was worked.

The three that are not driven are the three that cannot be driven without taking
the host somewhere a check must not take it — a modal dialog that waits for
whoever opened it, a game launch, and a Nexus round trip that rewrites MO2's
download metadata. They are named in the output of every run rather than left out
of the count.

## A filter list that stayed hidden, found by the new check

Running the widget check repeatedly turned up a fault on one run in several:

```
FAIL MO2 widgets: my-mods is missing categoriesGroup (ModCategoriesGroup) is built
but not drawn — visible=False bounds=200x352 in Grid# 506 holding ModsQtBar 506,
ModCategoriesGroup 200, Grid 300, …
```

The mod pane decides whether MO2's filter list fits beside the mod list, and it
recomputed that **only when the pane's width changed or the button was pressed**.
A moment when the pane was briefly under that width — the sidebar opening, a page
still being laid out — hid the filter list, and once the width settled at a value
the pane had already recorded, nothing brought the decision back. The list stayed
hidden until the window was resized or the button was pressed twice.

It now also recomputes whenever what is drawn disagrees with what should be drawn,
and writes nothing while the two agree, so it still asks for no layout pass of its
own — which is the constraint that made it conditional in the first place.

**A fault found once in several runs is not a fault a passing run disproves.** So
it is reproduced on purpose rather than waited for: `MO2_VERIFY_WIDGET_BEHAVIOUR`
now hides the filter list behind its button's back and requires the pane to put it
back. Reverting the fix and rebuilding, that case fails —

```
FAIL MO2 widget behaviour: the filter list stayed hidden after something other
than its button hid it
```

— and with the fix restored byte for byte it passes again.

This is what the derived widget check was for. The hand-written list did not name
`categoriesGroup` on any page, so nothing would have looked for it, and the filter
list beside MO2's mod list could have stayed hidden without a single check having
anything to say.

## A check that failed on a race and left the host changed

`MO2_VERIFY_CONTROLS` ended a full sweep with no verdict at all — an unhandled
`Host plugin activation disagrees`, which kills the process before anything is
printed. The runner reported that as **NO VERDICT** rather than passing over it,
which is what that reporting rule exists for.

It read MO2's snapshot **once**, the instant the frontend's action returned, and
compared. MO2 applies and writes on its own schedule, so a read taken that early
can disagree with a frontend that is right. Worse, the throw ends the process: the
restore in the check's `finally` had asked MO2 to put `The Mod Configuration
Menu.esp` back, but MO2 had not written its profile yet, so the plugin was left
disabled on the host — the same class of leftover state this audit had already had
to clean up once.

Both halves are fixed. MO2 is now asked again until it agrees, up to fifteen
seconds, and a run that gives up says what MO2 had against what the frontend had
rather than "disagrees". And the restore is waited for **inside** the `finally`,
because a failing run is exactly the run whose restore has to land before the
process ends; it was previously verified after the `try`, where a failure skipped
it.

Three consecutive runs pass, and the profile still carries the plugin enabled and
both mods enabled afterwards.

## Controls that were drawn and could never be used

A frontend built on another application's views inherits that application's
controls. What could be re-pointed was; what could not was left where it stood,
still drawn. Nothing looked for that, so it was found by looking:
`MO2_VERIFY_DEAD_CONTROLS` walks the window and every page, asks every command a
**drawn** control is bound to whether it can run, and fails on one that cannot
unless it is named in the check with the condition that would let it.

Two things that first run got wrong, and both mattered:

* **In the tree is not drawn.** The first version judged everything under the
  window and reported the account role badges — Supporter, Free — which the theme
  keeps built and never shows. Judging only what is effectively visible dropped
  them, which is right: nobody can read them either way.
* **A menu's entries are in no tree until it opens.** The entries this check was
  written for are in the top bar's two menus, and a walk of the window found
  every control except them. They are opened, judged and closed again.

### What it found

Five entries, drawn greyed in the top bar's menus on every run for every user:

| Entry | Bound to |
| --- | --- |
| Nexus profile | a command constructed disabled |
| Get Premium | a command constructed disabled |
| Nexus forums | a command constructed disabled |
| Changelog | a command constructed disabled |
| Welcome message | a command constructed disabled |

A greyed entry reads as "not now". These meant "never". They are taken out, along
with the rules left stranded between them, rather than greyed — which is the rule
MO2's own widgets are already held to: a control that draws and does nothing is
the fault, not the remedy. What remains disabled is named with what would enable
it: sign-out needs an account, the two history arrows need somewhere to go.

**Negative control.** One entry was put back and the check named it —
`ViewChangelogMenuItem is drawn bound to a command that cannot run` — and with the
removal restored byte for byte it passes over all 262 commanded controls again.

## One action, drawn twice

Three controls were doing a job another control on the same page already did.

**Plugins had two LOOT sorts.** An icon on the header line and MO2's own labelled
Sort in the row of MO2 widgets under it called the same method, were greyed by the
same line, and explained themselves with the same tooltip. MO2 has one
`sortButton`. The header's copy is gone — and that header is the one that had to
give up its page title to fit its actions.

**My Mods had two column choosers.** The chooser on its header line and a tick per
optional column at the foot of MO2's list-options menu ran over the same columns,
the same hidden set and the same preference file. MO2 puts none there at all: its
`listOptionsBtn` carries the mod list's global actions and its columns are chosen
from the list's own header, which is what this frontend's Downloads does. The
menu's copy is gone, and `MO2_VERIFY_WIDGET_BEHAVIOUR` now fails if it comes back
— it used to *require* it.

## The toolbar that had been empty for months

Every page's header line carried a `PanelHeaderActions` group, built, measured and
arranged on every layout pass of all fifteen pages. It had held nothing since a
page's actions moved into a row of their own under the separator — MO2 puts no
toolbar on a tab — and three checks had grown around that emptiness:

* `MO2_VERIFY_PANEL_CHROME` threw if it held anything;
* `MO2_VERIFY_SHARED_LISTS` faulted if it held anything;
* `MO2_VERIFY_HEADER_FIT` measured how its contents wrapped across 13 window
  sizes — over an empty group, so not one of those comparisons could ever fail;
* and `MO2_VERIFY_PANEL_CHROME` checked the size of the buttons in it, and that no
  text box had got onto that line, against the same nothing.

A box that must exist and must be empty is a box with no use. It is gone, and
`Mo2HeaderLine` lays out the header alone — which is what it was doing. The
checks now assert what the group stood for rather than its emptiness: the header
line carries the header and nothing beside it, and the two assertions that were
being made against an empty group are made against `PanelActionRow`, where a
page's actions actually are.

## What this turn could not establish about MO2_VERIFY_WIDGET_BEHAVIOUR

That check failed repeatedly while the cleanup above was being confirmed, and it
is recorded here as it stands rather than rounded off.

**It is not the cleanup.** The committed revision was built and run against the
same host and failed too — differently, but failed. Two of the runs reported the
mod list as empty (`0 of 0 rows` from every widget over it), and `MO2_VERIFY_DENSITY`
at the committed revision reported `my-mods has no rows to measure` on the same
host in the same state. MO2 itself answered a snapshot in 0.4s with 14 mods
throughout, so the frontend was reading a host that was serving.

**What was fixed, and each removed a class of false failure:**

* The check read the mod list, the plugin list and the Data tree the frame after
  navigating to them, with a fixed 800ms pause. On a profile large enough that
  the rows had not arrived, that produced five faults in a row about widgets —
  "the filter left 0 of 0 rows", "the separators box changed the list by 0 rows",
  "the grouping left the list in the same order" — none of them about a widget.
  Each section now waits for its rows and says so when there are none.
* It found its Mods and Plugins views by walking the window for the first of that
  type. The workspace keeps the view a panel was showing beside the one it is
  showing now, so a two-panel window holds more than one `Mo2ModsView` and the
  retained one has an empty adapter. Found by the page it belongs to now.
* Its Archives box read the tree once after ticking. Data is re-read from MO2 when
  a box is ticked, so the tree is rebuilt rather than refiltered — the single read
  was long enough while the instance served nothing out of an archive, and not
  once it did.

**What remains, unfixed and named.** The check drives MO2 faster than MO2 answers.
One run reported `MO2 offered no shortcut entry for NVSE — Asking MO2 to read its
downloads again`: the profile's own status, quoted back, from an action issued
while MO2 was still finishing the one before it. Gating every action on the
profile being idle is a rework of a thousand-line check and was not attempted
here. Its verdict is not evidence of anything on this host until that is done.

**Host state.** Runs that fail part-way leave MO2 changed. `The Mod Configuration
Menu` was left disabled by four separate runs and was re-enabled through MO2 each
time; the profile carries it enabled, and all three `toolbar=` flags in
`ModOrganizer.ini` are `false`, as they were. `MCM Author Examples` was put back
to disabled — it was enabled earlier in this session to fix an unrelated check's
precondition, which only needed the other mod, and enlarging the profile is what
first pushed these waits over their budgets.

**MO2 wedges under this load.** The bridge stopped answering twice, and the
frontend's mod list came up empty on a host whose snapshot was healthy. Both
cleared on restarting MO2. The IPC directory had 2,044 orphaned response files
and 55 unconsumed requests — litter from runs killed part-way, which is what the
runner does by design to the checks that cannot end themselves.

### What the cleanup was confirmed against

Fourteen checks, on the live host, after the removals:

```
DEAD_CONTROLS QT_WIDGETS QT_ACTIONS PRESET_FIT PAGE_AUDIT REACHABLE_ACTIONS
HEADER_FIT PANEL_CHROME SHARED_LISTS FOLDER_PAGES COLUMN_TOGGLE DENSITY
ROW_STYLE ROW_PADDING
```

All fourteen pass. Two of them — `ROW_STYLE` and `ROW_PADDING` — failed on the
run where MO2 had wedged, both alongside `FAIL live gate` and with `ROW_PADDING`
saying outright that neither table could be measured, and both pass on a restarted
host. `ROW_PADDING` also failed once on a heading measured mid-layout and passed on
the next run; that is a marginal reading rather than a result, and it is recorded
as one.

`QT_WIDGETS` still accounts for all 73 of MO2's widgets and `QT_ACTIONS` for all
24 of its actions with the header-line group gone, so nothing MO2 draws was
removed along with it.

## The mod pane, cut back to what MO2 has and what this frontend adds

Four removals from My Mods, each because something else already does the job.

**The toolbar above the list.** It carried an add-mod button, an overflow menu, the
original app's search box and the selection group it grows when rows are picked.
MO2 puts no toolbar on a tab, and everything on that row that MO2 also offers, MO2
offers from a widget this page already draws:

| Was on the toolbar | Is now |
| --- | --- |
| Add mod from archive… | the list-options button — MO2's `listOptionsBtn`, whose own global menu opens with Install mod… |
| Add separator… | the same menu, where MO2's own has Create separator |
| Search box | MO2's filter field under the list (`modFilterEdit`) |
| Move earlier / Move later | the row's own menu, which carries MO2's Send to… |
| Order history | the list-options menu |
| Selection group | nothing. MO2 has no equivalent — it works a selection from the row menu, and so does this list. That is a loss of the "3 selected" readout and its deselect action, and it is stated rather than glossed. |

**MO2's profile box and its "Profile" caption.** This frontend has a page for the
instances it is connected to and the profiles in each; a second chooser above the
mod list offered the same switch from a place that showed none of what it would
switch to. `MO2_VERIFY_QT_WIDGETS` gained a kind for this — a widget MO2 draws
beside its list that this frontend deliberately draws elsewhere — and still has to
**find it there**, on the Connections page, or fail. It reads:

```
… my-mods carries all 20, … connections carries the 2 MO2 puts beside its mod list
— label_3 as Mo2ChooseProfile, profileBox as Mo2ChooseProfile
```

**Five columns: Author, Nexus ID, Source Game, Installation and Priority.** The
first four say where a mod came from rather than what it does in this profile, and
MO2's priority is the order the list is already in — the row's place in it is the
column. Eight remain, and what the five held is still MO2's and still reported
through a mod's Information… entry. `MO2_VERIFY_SHARED_LISTS` takes its expectation
from the column table itself, so it followed without being told.

### Checks this moved, and why each had to move

None of these was deleted; each now asserts what the page actually does.

* `MO2_VERIFY_QT_ACTIONS` — MO2's Install Mod is answered by the list-options
  button, which is where MO2 keeps it, rather than by a toolbar overflow.
* `MO2_VERIFY_SELECTION_PARITY` — compared the two lists' selection groups. They
  differ on purpose now, so the group is **asserted** in both directions instead:
  one appearing on My Mods fails, and Plugins losing its own fails too.
* `MO2_VERIFY_TRADITIONAL_UI` — looked for the archive and separator entries in the
  overflow; looks for them where they are.
* `MO2_VERIFY_FILTERED_ROWS` — set the window to 520px and then required both lists
  to be too long for it. That held while the mod pane drew a toolbar and stopped
  the moment the row came off: thirteen rows fit. It now **finds** a viewport the
  lists overflow rather than assuming one, and reports which it used (460px here).
* `MO2_VERIFY_WIDGET_BEHAVIOUR` — its workable-widget count is now 29 rather than
  30, because the profile box is worked where it is drawn.

Thirteen checks were run against the live host after the removals and all thirteen
pass: dead controls, preset fit, reachable actions, page audit, density, panel
chrome, shared lists, selection parity, row style, row padding, column toggle,
filtered rows and widget behaviour.

### One thing found along the way that was not a UI fault

The machine had run out of memory, and that is why MO2 kept wedging and why a build
was killed mid-compile. The cause was this session's own runner: it kills a check by
killing the job it started, and the app is a *grandchild* of that job — timeout,
then `run-live.sh`, then `dotnet run`, then the host — so every killed run left a
host behind at about 120MB. `frontend/tools/verify.sh` now sweeps them after every
run, however it ended.

## The toolbars on MO2's pages that were not MO2's

MO2 puts no toolbar on a tab. Two of the six pages still drew one, and neither was
MO2's: Plugins built one of this frontend's own, and Downloads used the original
app's. Both are gone, and everything on them is accounted for.

**Plugins.** Its toolbar carried a search box, an overflow menu and a selection
group. Each has a home already:

| Was on the toolbar | Is now |
| --- | --- |
| Search box | MO2's filter field under the list (`espFilterEdit`) |
| Enable / Disable for a selection | the row's own menu, which has carried MO2's *Enable selected* and *Disable selected* all along — the buttons were duplicates of entries beside them |
| Refresh plugins | MO2's list-options button on the mod pane, which is MO2's own Refresh over the whole profile |
| Order-history menu | MO2's own Restore and Save beside Sort |

**Downloads.** Its toolbar was the original app's, with the frontend's own actions
inserted into it. MO2's download row menu — the one this frontend asks MO2 to build
— already carries Install, Delete…, Hide, Cancel, Pause and Resume, so every
transfer button on that row was a duplicate of an entry MO2 offers. *Install
archive…* is the same install MO2's mod list offers from its list-options button.
The search box is replaced by the filter field MO2 draws under the list.

One action on it was offered nowhere else: **Nexus link…**, which downloads a file
from a pasted link. It moves into the row of MO2's own widgets beside Query
Metadata, the other action on that page that asks Nexus about a download. Nothing
was dropped silently.

MO2's double-click-to-install on a download stays: that gesture is MO2's own, and
it shared its code with the button rather than the other way round.

### Checks that were pointed at the removed controls

Four, all re-pointed at what replaced their subject rather than deleted:

* `MO2_VERIFY_CONTROLS` and the pointer plugin checks drove `DisableSelectedPlugins`
  and `EnableSelectedPlugins`. They go through MO2's own menu entries now — **on the
  row of the plugin in question**, which is the part that had to be got right: MO2
  gates those entries on the plugin the menu was opened over, so opening the first
  row's menu asked about the wrong plugin and reported *Disable selected* greyed out
  on a list where it was live.
* `MO2_VERIFY_SELECTION_PARITY` asserted that My Mods had no selection group and
  Plugins did. Neither has one now, so it asserts the absence on both and fails if
  either grows one back.
* `MO2_VERIFY_FILTERED_ROWS` typed into the two pages' search controls. The plugin
  one is in no visual tree at all any more, so it types into MO2's own filter fields
  — `ModsQtFilter` and `PluginsQtFilter` — which is what a user types in.
* `MO2_VERIFY_INSTALL_ARCHIVE` watched `InstallArchiveButton` across a profile
  switch; it watches the Nexus link action, which is the one still on that page.

Seventeen checks were run against the live host: fourteen passed on the first pass
and the three that did not — selection parity, controls and filtered rows — pass
after the re-pointing above. Two of the four re-pointed checks could not be run on
this host: `MO2_VERIFY_PLUGIN_MULTI` needs two enabled MCM example plugins and that
mod is disabled here, and `MO2_VERIFY_INSTALL_ARCHIVE` needs an archive path passed
in. They are changed but unverified, and that is not the same as changed and
working.

## Every button on MO2's pages, checked against MO2 — the other direction

`MO2_VERIFY_QT_WIDGETS` asks whether everything MO2 draws is here. Nothing asked
the reverse, and a page drifts by growing buttons as easily as by losing them.
`MO2_VERIFY_EXTRA_BUTTONS` walks MO2's six tabs and requires every button drawn on
one to be either the counterpart of a widget in `src/mainwindow.ui` — which
`Mo2QtWidgetCheck.Answers` already records, so the two checks read the same map
from opposite ends — or named in the check with what MO2 does instead.

It found **nine**, and all nine came off:

| Page | Button | Why it went |
| --- | --- | --- |
| My Mods | help | MO2 explains its lists nowhere on the tab |
| Plugins | the original app's load-order banner and the help button that reopens it | MO2's espTab is Sort, Restore, Save, a count and the list |
| Data | a second Refresh | MO2's `dataTabRefresh` is already drawn in the row of MO2's own widgets |
| Data | Open, Reveal in Explorer, Hide | all three are on the menu MO2 builds for a Data row |
| Archives | Browse, Extract, Refresh | MO2's bsaTab is a note above a list — no buttons at all |
| Saves | Details, Fix mods, Delete, Refresh | MO2's savesTab is its list alone; the three actions are on its row menu |
| Downloads | Nexus link… | MO2 has no widget for it either, and a download still arrives through the nxm handler MO2 registers itself for |

That last one had been moved into MO2's own widget row an hour earlier to keep it
reachable. Held to this rule it goes too, which is the rule doing its job rather
than being argued with.

### What is kept, and named

Eight buttons are recorded in the check as this frontend's own, each for something
MO2 does from somewhere other than a widget on the tab:

* the column choosers — MO2 opens the same chooser by right-clicking a list header;
* the search toggles — they show the filter row MO2 draws permanently under its list;
* the clear inside each filter field — MO2's `LineEditClear` carries it inside the box;
* Data's parent-folder button — this page navigates folders where MO2's tree expands
  in place, so without it there is no way back up.

Adding a line there is how this check is silenced, which is why each line says what
MO2 does instead rather than that the button is wanted.

**Negative control.** Data's Reveal was put back and the check named it —
`data draws 1 button(s) MO2 has no widget for: RevealDataFile` — and with the
removal restored byte for byte it passes again.

### Checks that were watching the removed buttons

* `MO2_VERIFY_ARCHIVES` asserted Browse and Extract tracked the selection. Its
  subject is the selection, which it still asserts; it now fails if either button
  comes back.
* `MO2_VERIFY_ALERT_LIFECYCLE` drove the load-order banner through a dismiss/show
  lifecycle across a retained panel. That lifecycle is not something this page has
  any more, so it asserts that neither the banner nor its button is drawn.
* `MO2_VERIFY_CONTROLS` failed once more on its restore — ten seconds was not
  enough on a host the check before it had just worked hard, and it left the plugin
  disabled again. The one wait that has to land now gets thirty.

Nineteen checks run live; all nineteen pass.

## The six pages MO2's layout was never measured with

`MO2_VERIFY_PRESET_FIT` is the check that answers *does everything fit*. It puts
the window into MO2's own panel layout — the mod list on the left, MO2's five tabs
on the right — and fails on a widget clipped by the panel holding it or squeezed
below the size it asked for. It walked the preset's own tabs and nothing else, so
it answered that question for MO2's six tabs and for no other page.

The window opens twelve. **Tools, Logs, Overwrite, External Files, Health Check and
Profiles** come off the same sidebar into the same half-width panel, and no check
had measured one there. `MO2_VERIFY_REACHABLE_ACTIONS` builds each of them alone,
where a page is the whole panel; `MO2_VERIFY_HEADER_FIT` measures headers and not
bodies; `MO2_VERIFY_PANEL_CHROME` measures padding. A panel to itself is the width
at which a control that does not fit still does, and it was the only width these
six had ever been seen at.

They are opened now through their own sidebar entries into MO2's right panel — the
same panel, the same 470px — and measured by the same code, with the run refusing
to measure a page that landed anywhere else. The check reports `Tools (40, 3
handed), Logs (11, 2 handed), Overwrite (10, 7 handed), External Files (16, 6
handed), Health Check (2, 0 handed), Profiles (21, 0 handed)` beside MO2's six, and
every widget and caption on all twelve is drawn whole inside its panel.

### That it passed first time is not the evidence

A fit check that has never failed cannot be told apart from one that is not
looking. The live window's size is the platform's to give and cannot be narrowed
from inside the check, so `MO2_PRESET_FIT_SENSITIVITY=1` brings in the edge each
control is measured against instead — 60% of the panel — driving the same
comparison over the same pages. With it on, all twelve fail by name, the six new
ones among them: `Tools: Button ("Pin") runs to 498 of 336`, `Health Check:
TextBlock runs to 510 of 336`, `Profiles: RenameMo2ProfileButton runs to 405 of
336`. The suite never sets it, and a run with it on is not evidence of fit — it is
evidence that the pass is.

## A default read off the wrong MO2 file

The mod pane's filter list starts closed, and the reason written beside it was that
the list takes a third of the pane before a single category has been picked. That
is a judgement about this frontend, not a fact about MO2, and
`MO2_VERIFY_QT_WIDGETS` carried the same reasoning inverted — that MO2 declares the
list visible where this frontend does not, so the check opens it to measure it.

MO2's `mainwindow.ui` does declare `categoriesGroup` visible. MO2 overrides it on
every start: `restoreVisibility(ui->categoriesGroup, false)` in `mainwindow.cpp`,
whose second argument is what a profile with nothing saved gets, with
`displayCategoriesBtn` then set to match. **Closed is MO2's own default**, so the
behaviour was already right and only the reason was wrong. Both comments cite that
line now. The `.ui` says which widgets MO2 has; it does not say how MO2 starts
them, and every widget check here reads the `.ui`.

## Two sweeps at once, and the logs that could not tell you

`verify.sh` ends every run with `pkill -KILL -f bin/Release/net9.0/MockHost`, which
it has to: the app is a grandchild of the job, so killing the job's own children
left hosts running until the machine had no memory left. But that pattern matches
**any** host, not only the one this sweep started. Two sweeps therefore shoot each
other's checks part-way through.

That is not hypothetical — it happened here. A sweep was still going when a second
one started, and `MO2_VERIFY_WIDGET_BEHAVIOUR` was killed mid-run, leaving a log
holding build output and no verdict. The script reports that as NO VERDICT, which
is right, and is exactly why it does. What it cannot report is *why*: a check shot
by a neighbour and a check that has stopped saying anything leave the same log. Nor
can the checks that ran alongside it be told apart from clean runs, because two
frontends were driving the same live MO2 host at once.

A sweep now writes its pid and a second one is **refused**, with the reason, rather
than queued — a wait that takes half an hour to begin is indistinguishable from a
hang. The three checks that fell inside that window were re-run with nothing else
on the host.

`flock` on a descriptor was the first attempt, and it failed in a way worth
recording because it looked like it worked. The fd is inherited by everything the
sweep starts; `dotnet run` leaves MSBuild node-reuse daemons behind on purpose; and
those daemons held the lock after the sweep had finished, so every later sweep was
refused by a build server. A pid that is checked for life cannot outlive the thing
it stands for, and a sweep killed part-way leaves nothing to clean up by hand. Both
directions were exercised: a live holder is refused with exit 3, a pid that is gone
is ignored, and the file is removed on exit.

The same defect then caught a run a second time, and how it did is the point: the
lock was tested by starting a throwaway check, whose own cleanup killed the sweep
already in flight. A guard added while the thing it guards is running does not
guard that run — and neither does editing the script that run is executing, which
bash reads by offset as it goes.

## Two checks that had never finished a run

`MO2_VERIFY_ROW_MENUS` and `MO2_VERIFY_ROW_STYLE` were both sitting on logs holding
build output and nothing else, and `MO2_VERIFY_CONTROLS` on one that had never
carried a verdict at all. None of them was broken: each had been cut off part-way
by an interrupted sweep, which is the failure mode `verify.sh` reports as NO
VERDICT rather than passing over. Run to the end against the live FNV host, all
three pass — row style on both tables, MO2's own menus on all six lists compared
entry for entry and both ways round, and the plugin-disable and mod-priority round
trip through MO2 with the host's original state restored.

## The keyboard, which no check had ever looked at

`MO2_VERIFY_QT_ACTIONS` requires every one of the twenty-four actions in MO2's
`mainwindow.ui` to be offered from something on screen, and all twenty-four are. It
says nothing about *how* they are reached, and eight of them MO2 also gives a key:
**Ctrl+M, Ctrl+P, Ctrl+E, Ctrl+I, Ctrl+S, Ctrl+N, Ctrl+H and F5**.

This window bound none of them. Not one — a grep for `KeyGesture`, `HotKey` or
`KeyBinding` across the whole frontend found nothing, and no check named a gesture.
"MO2's actions are all reachable here" was true of the pointer, and had never been
asked of the keyboard at all.

`Mo2QtShortcuts` binds all eight. A press opens the page the action is offered on
and works the control that offers it — the same control `MO2_VERIFY_QT_ACTIONS`
requires to be there, read from the same table. That table moved out of the check
into `Mo2QtActions`, so the keys and the check read one list rather than two that
could disagree without either being wrong on its own.

### What the check found on its first run

**Ctrl+I and F5 reached nothing from Connections.** Both actions are offered from
the profile sidebar and Connections is on the home workspace; the two sidebars
swap, so navigating to a profile page while the home workspace was showing put the
page nowhere and left the control it is offered from out of the window entirely.
MO2's keys act wherever its window happens to be. These two did not, and the fault
was in the frontend rather than in the check.

### And what the check was doing by accident

It found that only because the gestures happen to be tried in MO2's own order, and
Connections happens to come before Tool Plugins and Refresh in it. Reordering the
list would have hidden it again — which is not a check, it is a coincidence that
held. Every gesture is now pressed from **both** of this window's workspaces, the
profile one and Connections, and has to arrive from each.

### The negative control

Three faults were planted in the bound list at once, and the run named all three:
a gesture changed to one MO2 does not give (`actionInstallMod is bound to Ctrl+J
where MO2 gives it Ctrl+M`), a binding for an action MO2 has no key for (`Ctrl+9 is
bound to actionNotAThing, which MO2 no longer gives a key`), and F5 left unbound
(`MO2 gives action_Refresh ("Refresh") F5 and nothing here binds it`). They were
put back, and the run is green.

### What this does not establish

Six of the eight end in a modal MO2 dialog or a browser window: Settings,
Executables, Install Mod and Profiles each open a dialog that owns the thread until
a person closes it, and Visit Nexus and Help hand a URL to the desk. Those six are
followed to the control and the entry MO2 would run, and then not pressed — which
the verdict states rather than omits. What is established for them is that the key
is bound and arrives at the right control, not that MO2 then did the work. Ctrl+I
and F5 are pressed for real.

## Twelve checks nothing had ever run, and the eight that had rotted

The sweeps so far named the checks that were already known to matter. Listing every
`MO2_VERIFY_*` in `Program.cs` against the logs showed **eighty-nine that had never
been run in a sweep at all**. Twelve of those are about widgets, pointer input,
narrow layouts and panel combinations, which is what this page is for, so they were
run. Seven passed. **Eight had rotted, and not one of them was a product fault.**

They had rotted for one reason: nothing ran them. A check nobody runs stops being
evidence and quietly becomes a description of a program that no longer exists.

| Check | What was actually wrong |
| --- | --- |
| `DEFERRED_PANELS`, `PAIRED_PANELS`, `INSTALLED_INTERACTIONS` | missing from `verify.sh`'s `PAIRED` list, though each says plainly it needs an isolated layout |
| `TAB_RESTORE`, `INSTALLED_INTERACTIONS` | never registered a turn, so the screenshot path shut them down before they could speak |
| `TAB_RESTORE` | then given a two-panel fixture when it asserts on `Panels.Single()` with two tabs |
| `MODS_SELECTION` | required a selection group MO2 has not got and that was deliberately removed |
| `SEARCH_INPUT` | drove a `SearchControl` whose toolbar is detached from the page, and needs a person besides |
| `PAIRED_PANELS` | required two visible page headers, which a shared panel deliberately stands down |
| `INSTALLED_INTERACTIONS` | read a row's `ContextMenu` after the row menus became MO2's own on `ContextFlyout` |

### The one that left something behind

`INSTALLED_INTERACTIONS` creates a separator in MO2 and removes it in a `finally`.
Unregistered, it was shut down before that `finally` ran, and
`__Drag modal check_separator` stayed in the live FNV profile — in `modlist.txt` and
as a mod directory. Its guard against a stale fixture then sat *outside* the `try`,
so it threw "Fixture already exists" without running the removal it was complaining
about, and stayed wedged for every run after. A check that cannot get past its own
litter is a trap rather than a guard. It now takes a stray out through MO2's own
`RemoveCollection` — the same path its cleanup uses — and the stray is gone.

**A check that changes the host has to be waited for, or it changes the host and
does not change it back.** That is the general rule the registration was missing.

### Attended checks are not unattended results

`SEARCH_INPUT` waits thirty seconds for a person to type a string; `NEXUS_ACCOUNT`
and `OPEN_MOD_DETAILS` each wait three minutes for someone to close a modal MO2
dialog. In a sweep they fail on their timeout and read as three broken features.
They are skipped with the reason now unless `MO2_VERIFY_ATTENDED=1` says someone is
watching. Counting an attended check among unattended results misleads in both
directions.

### Six wrong diagnoses, and what stopped them

Working through these, the cause was guessed wrong six times — that the pages had
stopped drawing a `PageHeader`, that the window never resized, that the responsive
header had stood down on width, that a geometry mismatch was transient, that a
shared panel must draw a visible action row, and that the row drag grips had been
deleted. Every one was disproved by looking, two of them only after reaching the
code and being caught by the next live run.

The grip is the one worth keeping: a `grep` for `ModDragHandle` across `.cs` found
it only in checks, so it looked removed. `Mo2EntryMenu` composes that name as
`kind + "DragHandle"`, and the grip is there. A literal search for a composed name
proves nothing, and the only reason the wrong assertion did not survive is that
everything here is run against the live app rather than reasoned about.

What survives from all of it is smaller and more useful than any of the guesses:
these checks report what they saw. `PAIRED_PANELS` names the header count, what was
built, the panel count, the size and which ancestor hid each one — and that turned
the last of them from three wrong guesses into one run.

## Every check that had never been run, run

Listing every `MO2_VERIFY_*` in `Program.cs` against the logs found **eighty-nine
that no sweep had ever named**. The ones about widgets, pointer input, layout and
panels were run — **thirty-three of them across three runners**. Eleven passed
first time. **Twenty-two had rotted, and not one was a fault in the frontend.**

They rotted for a single reason: nothing ran them. A check nobody runs stops being
evidence and becomes a description of a program that no longer exists — and it
fails in a way that reads exactly like the program being broken.

### The two that looked like defects, and were not

**"History restored an incorrect game page."** One message served six call sites,
including the first, which runs before any history navigation. Made to say which
step and what it saw, it answered at once: context New Vegas, headings *New Vegas
profiles*, view unbound. It wanted *New Vegas Loadouts* — `ScenarioData`'s fixture
wording — where `Mo2HomePages` heads a live section `"<game> profiles"`, because
**profiles is MO2's word and loadouts is NMA's**. Against MO2's wording it passes
whole.

**"Panel interaction changed MO2 state"** — that dragging a divider wrote to the
host. Thrown without naming anything. Made to print the difference, it printed
none: 11 of 11 plugins and 14 of 14 mods identical, rebuilt in another enumeration
order after a game switch reconnected. It was `SequenceEqual` over a collection
whose order is not the state under test; `SortIndex` and `Priority` carry that, and
both sit inside the tuples being compared.

Both would have been reported as frontend defects on the strength of their own
messages.

### Six runners, not one

`verify.sh` knew one way a check could need special handling. It knows six now, and
every check it cannot run says why and names the runner that can:

| Class | Why |
| --- | --- |
| paired layout | needs the two lists side by side |
| tabbed layout | needs one panel with two tabs, which the paired fixture cannot hold |
| attended | waits for a person at the keyboard |
| pointer-driven | needs `check_plugin_mouse.py` and its virtual evdev devices |
| parameterised | takes a value, and was being handed `1` |
| fixtures-only | wired into the fixtures window, never reached against a bridge |

### The pointer checks had not built since Debug broke

`PANEL_DRAG`, `PLUGIN_DRAG` and `PLUGIN_MULTI` are the only checks that press a
real pointer, and their driver launched `bin/Debug/net9.0/MockHost.dll`. Debug has
not built since MockHost took `AvaloniaUI.DiagnosticsSupport`: upstream references
`Avalonia.Diagnostics` in Debug alone and the two conflict under Avalonia v12.
Release was untouched, so nothing noticed. The driver defaults to Release now.

`PANEL_DRAG` then passes every phase under genuine kernel-level input — dividers
resize, survive a game switch, and leave MO2's mods and plugins untouched.
`PLUGIN_DRAG`, `PLUGIN_MULTI` and `PLUGIN_DOWN` need `MCM Author Examples` enabled
in the Frontend Test profile; it is disabled, so there are no movable MCM plugins.
Enabling it writes extra entries into `plugins.txt` that do not cleanly revert, so
it is left as a decision rather than made here.

### What the checks now do that they did not

* A wait says which wait it was. `Mo2InstalledInteractionCheck` had twenty-nine
  throwing one sentence; the line comes from `[CallerLineNumber]` now, so a wait
  cannot be written without saying where it is.
* A failure says what it saw. `PAIRED_PANELS` names the header count, what was
  built, the panel count, the size and which ancestor hid each one — which turned
  its diagnosis from three wrong guesses into one run.
* Sixty-one live checks and the fixtures path are guarded, so a throw is a named
  FAIL rather than a crash with no verdict.
* A sweep takes a lock, because this script kills any MockHost after each run —
  including another sweep's.
* Three checks that capture MO2 state **to restore it** wait for that state to
  arrive first. An early capture did not only misread the host; it decided what the
  host was put back to.
