using Avalonia;
using Avalonia.Controls;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Mo2.Frontend;

// Opt-in, read-only inspection of the pinned Avalonia version. Never reads
// property values, user text, or model data; unavailable internals are reported.
internal static class Mo2StyleCensus
{
    internal static object Capture(string panel, Control[] controls)
    {
        var clock = Stopwatch.StartNew();
        try {
            var getter = typeof(AvaloniaObject).GetMethod("GetValueStore", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMemberException("AvaloniaObject.GetValueStore");
            var framesProperty = getter.ReturnType.GetProperty("Frames")
                ?? throw new MissingMemberException("ValueStore.Frames");
            var sources = new Dictionary<(string? Type, string Selector), int>();
            var rows = controls.Select(control => {
                var frames = ((IEnumerable)framesProperty.GetValue(getter.Invoke(control, null))!).Cast<object>().ToArray();
                foreach (var frame in frames) {
                    var source = frame.GetType().GetProperty("Source")?.GetValue(frame);
                    if (source is not Avalonia.Styling.Style style) continue;
                    // Selectors describe static theme rules, never property values.
                    var key = (control.GetType().FullName, style.Selector?.ToString() ?? "(unconditional)");
                    sources[key] = sources.GetValueOrDefault(key) + 1;
                }
                return new { Type = control.GetType().FullName, TemplateOwner = control.TemplatedParent?.GetType().FullName,
                    Frames = frames.Length,
                    Entries = frames.Sum(frame => Convert.ToInt32(frame.GetType().GetProperty("EntryCount")!.GetValue(frame))) };
            }).ToArray();
            var groups = rows.GroupBy(row => row.Type).Select(group => new {
                Type = group.Key, Controls = group.Count(), Frames = group.Sum(row => row.Frames),
                MaximumFrames = group.Max(row => row.Frames), Entries = group.Sum(row => row.Entries)
            }).OrderByDescending(group => group.Frames).ToArray();
            var owners = rows.GroupBy(row => (row.TemplateOwner, row.Type)).Select(group => new {
                TemplateOwner = group.Key.TemplateOwner, Type = group.Key.Type, Controls = group.Count(),
                Frames = group.Sum(row => row.Frames), Entries = group.Sum(row => row.Entries)
            }).OrderByDescending(group => group.Frames).ToArray();
            return new { Panel = panel, Stage = "Style census", DurationMs = clock.Elapsed.TotalMilliseconds,
                Controls = controls.Length, Groups = groups, TemplateOwners = owners,
                Sources = sources.Select(pair => new { Type = pair.Key.Type, Selector = pair.Key.Selector, Frames = pair.Value })
                    .OrderByDescending(row => row.Frames).ToArray() };
        } catch (Exception error) {
            return new { Panel = panel, Stage = "Style census unavailable", Error = error.GetType().Name,
                DurationMs = clock.Elapsed.TotalMilliseconds };
        }
    }
}
