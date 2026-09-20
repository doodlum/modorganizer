using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.VisualTree;
using NexusMods.Abstractions.Downloads;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages.Downloads;
using NexusMods.Paths;
using NexusMods.Sdk.Jobs;
using R3;

namespace Mo2.Frontend;

internal static class Mo2DownloadTransferFitCheck
{
    internal static async Task Run()
    {
        using var turn = await Mo2CheckTurn.Take();
        var statuses = new List<DownloadComponents.StatusComponent>();
        var rows = new[] { JobStatus.Running, JobStatus.Paused, JobStatus.Failed, JobStatus.Completed }.Select(state => {
            var row = new CompositeItemModel<DownloadId>(DownloadId.From(Guid.NewGuid()));
            row.Add(DownloadColumns.Name.NameComponentKey, new NameComponent("Example " + state + ".zip"));
            var status = new DownloadComponents.StatusComponent(Percent.Zero, state,
                Observable.Return(Percent.Zero), Observable.Return(state), canRetryFailed: true, canCancelInactive: false);
            statuses.Add(status); row.Add(DownloadColumns.Status.ComponentKey, status); return row;
        }).ToArray();
        using var source = new FlatTreeDataGridSource<CompositeItemModel<DownloadId>>(rows);
        source.Columns.Add(new TextColumn<CompositeItemModel<DownloadId>, string>("Name",
            row => row.Get<NameComponent>(DownloadColumns.Name.NameComponentKey).Value.Value));
        source.Columns.Add(ColumnCreator.Create<DownloadId, DownloadColumns.Status>(width: new GridLength(224)));
        var table = new TreeDataGrid { Source = source };
        Mo2DownloadsView.InstallProgressStyle(table);
        Mo2TableRow.InstallRowStyles(table);
        var view = new Mo2DownloadsView { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Content = new Grid { Margin = new Thickness(24), Children = { table } } };
        var window = new Window { Width = 650, Height = 360, Content = view, ShowInTaskbar = false };
        window.Show();
        try {
            foreach (var width in new[] { 650d, 470d, 340d, 280d, 240d }) {
                view.Width = width;
                source.Columns.SetColumnWidth(1, new GridLength(Mo2DownloadsView.StatusWidth(width, true)));
                source.Columns.SetColumnWidth(0, new GridLength(Math.Max(60, width - 48 - Mo2DownloadsView.StatusWidth(width, true))));
                await Task.Delay(400);
                if (Math.Abs(view.Bounds.Width - width) > 1)
                    throw new Exception($"Requested {width}px but rendered {view.Bounds.Width}px; transfer fit was not exercised");
                if (Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { } screenshot) {
                    using var shot = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                    shot.Render(window); shot.Save(Path.ChangeExtension(screenshot, $"transfers-{width:0}.png"));
                }
                var actions = table.GetVisualDescendants().OfType<StandardButton>().Where(x => x.IsEffectivelyVisible).ToArray();
                var progress = table.GetVisualDescendants().OfType<ProgressBar>().Where(x => x.IsEffectivelyVisible).ToArray();
                if (actions.Length != 4 || progress.Length != 3) throw new Exception("Transfer fixture did not render pause/cancel, resume and retry controls");
                foreach (var control in actions.Cast<Control>().Concat(progress)) {
                    var at = control.TranslatePoint(default, view)!.Value;
                    if (at.X < 24 || at.X + control.Bounds.Width > view.Bounds.Width - 23)
                        throw new Exception($"Transfer control clips at {width}px: {control.GetType().Name}, right {at.X + control.Bounds.Width:F0}, inner edge {view.Bounds.Width - 24:F0}");
                }
                Console.WriteLine($"PASS download transfer fit: {width}px; running/paused/failed/completed controls visible");
            }
        } finally { window.Close(); foreach (var status in statuses) status.Dispose(); }
    }
}
