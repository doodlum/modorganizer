Tools pictogram from Nexus-Mods/Vortex, assets/pictograms/tools.svg. GPL-3.0; see LICENSE.md copied here. The CSS currentColor fill is resolved to the primary orange for Avalonia.
Source: https://github.com/Nexus-Mods/Vortex/blob/master/assets/pictograms/tools.svg

Reference commit: 089b1c59958e9c9c52d9ba0ae47fbdcfd17ba493. Primary colour: #fb923c. Sidebar icon: mdi-wrench-outline, matching Vortex iconMap.ts.

Plugins header artwork (`plugins.svg`) is Vortex's `assets/pictograms/puzzle-piece.svg`, extracted from the installed Vortex app. Original path geometry and white/blue gradients preserved; CSS currentColor resolved to orange #fb923c. Same Vortex GPL-3.0 license. Replaces the small plus-circle navigation symbol.

Sidebar and panel-tab Plugins icon: `mdi-power-plug-outline`, matching the installed Vortex iconMap mapping `plugins: mdiPowerPlugOutline`. The layered puzzle pictogram is used only for the page header.

Sidebar toggle geometry: `Mo2CollapsibleSidebar` embeds Vortex's `nxmPanelOpen` and
`nxmPanelClose` path data verbatim from `src/renderer/src/ui/icon-paths.ts` (Vortex master
4aaef4306), where they are described as the Material panel open/close icons. Same Vortex
GPL-3.0 license; the underlying Material Symbols shapes are Apache-2.0. The paths are parsed
into Avalonia geometry so the icon takes the button foreground, as Vortex's does.

`../power-plug-3d.svg` adapts the same MDI `mdiPowerPlugOutline` path used by Vortex into layered header artwork. Material Design Icons are Apache-2.0 (https://github.com/Templarian/MaterialDesign); the original path geometry is preserved.
