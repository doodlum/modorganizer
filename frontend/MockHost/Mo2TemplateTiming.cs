using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using System.Reactive.Linq;

namespace Mo2.Frontend;

// Diagnostic only: delegates to the original template and returns its exact
// result/name scope. Build and Build-through-TemplateApplied are recorded
// separately; neither measures the subsequent child measure/arrange passes.
internal static class Mo2TemplateTiming
{
    private static IDisposable? _observation;

    internal static void Install()
    {
        _observation ??= TemplatedControl.TemplateProperty.Changed.Subscribe(change => {
            if (change.Sender is TemplatedControl control && control.Template is { } template && template is not TimedTemplate)
                control.SetCurrentValue(TemplatedControl.TemplateProperty, new TimedTemplate(template));
        });
    }

    private sealed class TimedTemplate(IControlTemplate inner) : IControlTemplate
    {
        public TemplateResult<Control>? Build(TemplatedControl control)
        {
            var realization = Mo2UiLatencyProbe.Measure("Template realization: " + control.GetType().FullName, always: true);
            EventHandler<TemplateAppliedEventArgs>? applied = null;
            void Finish()
            {
                if (applied is not null) control.TemplateApplied -= applied;
                realization?.Dispose();
                realization = null;
            }
            if (realization is not null) {
                applied = (_, args) => { if (ReferenceEquals(args.Source, control)) Finish(); };
                control.TemplateApplied += applied;
            }
            using var timing = Mo2UiLatencyProbe.Measure("Template build: " + control.GetType().FullName, always: true);
            try {
                var result = inner.Build(control);
                if (result is null) Finish();
                return result;
            } catch { Finish(); throw; }
        }
    }
}
