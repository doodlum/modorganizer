using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using NexusMods.App.UI.Dialog;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.UI.Sdk.Dialog.Enums;

namespace Mo2.Frontend;

internal static class Mo2SeparatorInteractionCheck
{
    public static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        const string original = "__Separator interaction check", renamed = "__Separator renamed check";
        var profile = shell.Profile;
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!ready()) { if (DateTime.UtcNow > until) throw new TimeoutException("Separator interaction check timed out"); await Task.Delay(50); }
        }
        await Wait(() => profile.CanChangeOriginalUi && profile.Mods.Count > 0);
        if (profile.Mods.Any(m => m.Name == original + "_separator" || m.Name == renamed + "_separator"))
            throw new InvalidOperationException("Separator test fixture already exists; refusing to reuse it");
        var target = profile.CurrentTarget;
        await shell.ProfileMenu.LeftMenuItemLoadout.NavigateCommand.Execute(NexusMods.App.UI.Controls.Navigation.NavigationInformation.From(NexusMods.App.UI.WorkspaceSystem.NavigationInput.Default));
        await Wait(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(v => v.IsEffectivelyVisible && v.Bounds.Width > 0));
        var view = window.GetVisualDescendants().OfType<Mo2ModsView>().First(v => v.IsEffectivelyVisible);
        var page = view.ViewModel!;
        var adapter = (Mo2ModsAdapter)page.Adapter;
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        async Task Answer(string? text, ButtonDefinitionId button, string? expected = null) {
            await Wait(() => desktop.Windows.OfType<DialogWindow>().Any(d => d.IsVisible));
            var dialog = desktop.Windows.OfType<DialogWindow>().Single(d => d.IsVisible);
            if (dialog.Owner != window) throw new InvalidOperationException("Dialog is not owned by the frontend");
            var content = (IDialogStandardContentViewModel)dialog.ViewModel!.ContentViewModel!;
            if (expected is not null && content.InputText != expected) throw new InvalidOperationException("Rename modal did not prefill current name");
            if (text is not null) content.InputText = text;
            dialog.ViewModel.ButtonPressCommand.Execute(button);
            await Wait(() => !desktop.Windows.OfType<DialogWindow>().Any(d => d.IsVisible));
        }
        async Task<TextBlock> Title(string name) {
            var rail = view.GetVisualDescendants().OfType<ScrollBar>().Single(b => b.Name == "ModsRailScrollBar");
            rail.Value = rail.Maximum;
            await Wait(() => view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "SeparatorTitle" && Equals(t.Tag, name)));
            return view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "SeparatorTitle" && Equals(t.Tag, name));
        }
        void DoubleTap(TextBlock title) {
            // Exercise the real routed-event handler, without moving the user's pointer.
            var args = (TappedEventArgs)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TappedEventArgs));
            args.RoutedEvent = InputElement.DoubleTappedEvent;
            title.RaiseEvent(args);
        }
        try {
            var above = profile.Mods.Where(m => m.CanManage && !m.IsSeparator && !m.IsOverwrite).OrderBy(m => m.Priority).First().Id;
            Console.WriteLine("Separator check: opening Create modal");
            var create = page.CreateSeparatorDialog(above);
            await Answer(original, ButtonDefinitionId.Cancel); await create;
            if (profile.Mods.Any(m => m.Name == original + "_separator")) throw new InvalidOperationException("Create Cancel changed native state");
            create = page.CreateSeparatorDialog(above);
            await Answer(original, ButtonDefinitionId.Accept); await create;
            await Wait(() => profile.Mods.Any(m => m.Name == original + "_separator"));
            Console.WriteLine("Separator check: native separator created");
            var title = await Title(original + "_separator");
            var separator = profile.Mods.Single(m => m.Name == original + "_separator");
            var members = adapter.SeparatorCount(separator);
            if (members == 0) throw new InvalidOperationException("Fixture has no members to test collapse");
            async Task CheckCount(int expected) {
                var label = await Title(separator.Name);
                var count = label.GetVisualAncestors().OfType<Grid>().First().Children.OfType<TextBlock>().Single(t => t.Name == "SeparatorModCount");
                await Wait(() => count.Text == expected.ToString());
            }
            await CheckCount(members);
            await profile.MoveMod(separator.Id, 1);
            await CheckCount(members - 1);
            await profile.MoveMod(separator.Id, -1);
            await CheckCount(members);
            Console.WriteLine("PASS separator displayed count updates after native priority changes and restoration");
            var table = view.NativeView.FindControl<TreeDataGrid>("TreeDataGrid")!;
            var before = table.Rows!.Count;
            var expand = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SeparatorExpandButton" && Equals(b.Tag, separator.Name));
            expand.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Wait(() => table.Rows!.Count == before - members);
            Console.WriteLine("Separator check: collapse passed");
            title = await Title(separator.Name);
            DoubleTap(title); await Answer("Cancelled rename", ButtonDefinitionId.Cancel, original);
            if (!profile.Mods.Any(m => m.Name == separator.Name)) throw new InvalidOperationException("Rename Cancel changed native state");
            DoubleTap(title); await Answer(renamed, ButtonDefinitionId.Accept, original);
            await Wait(() => profile.Mods.Any(m => m.Name == renamed + "_separator") && profile.CanChangeOriginalUi);
            if (!adapter.IsCollapsed(renamed + "_separator")) throw new InvalidOperationException("Rename lost collapsed state");
            title = await Title(renamed + "_separator");
            expand = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SeparatorExpandButton" && Equals(b.Tag, renamed + "_separator"));
            expand.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Wait(() => table.Rows!.Count == before);
            var renamedMod = profile.Mods.Single(m => m.Name == renamed + "_separator");
            var ids = new[] { NexusMods.Abstractions.Loadouts.LoadoutItemId.From(renamedMod.Id) };
            profile.Remove(ids); await Answer(null, ButtonDefinitionId.Cancel);
            if (!profile.Mods.Any(m => m.Name == renamedMod.Name)) throw new InvalidOperationException("Delete Cancel removed separator");
            await Task.Delay(100);
            profile.Remove(ids); await Answer(null, ButtonDefinitionId.Accept);
            await Wait(() => !profile.Mods.Any(m => m.Name == renamedMod.Name) && profile.CanChangeOriginalUi);
            Console.WriteLine("PASS separator modal Create/Cancel, collapse count, routed double-click Rename/Cancel, prefilled name, collapsed-state preservation, Delete/Cancel and native deletion");
        } finally {
            if (target == profile.CurrentTarget) {
                foreach (var mod in profile.Mods.Where(m => m.Name == original + "_separator" || m.Name == renamed + "_separator").ToArray())
                    await profile.RemoveCollection(mod.Name, target);
            }
        }
    }
}
