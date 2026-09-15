using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using NexusMods.UI.Sdk.Icons;
using NexusMods.Themes.NexusFluentDark.Extensions;
using System.Xml.Linq;

namespace Mo2.Frontend;

internal static class Mo2IconAliasesCheck
{
    internal static async Task Run(Window owner)
    {
        var reference = Environment.GetEnvironmentVariable("MO2_ICON_ALIAS_REFERENCE")
            ?? throw new Exception("Original icon style reference required");
        var ns = XNamespace.Get("https://github.com/avaloniaui");
        var rules = XDocument.Load(reference).Root!.Elements(ns + "Style")
            .Where(style => style.Elements(ns + "Setter").Count() == 1 &&
                (string?)style.Element(ns + "Setter")!.Attribute("Property") == "Value")
            .Select(style => (Name: ((string)style.Attribute("Selector")!).Replace("icons|UnifiedIcon.", ""),
                Value: (IconValue)typeof(IconValues).GetField(((string)style.Element(ns + "Setter")!.Attribute("Value")!)
                    .Replace("{x:Static icons:IconValues.", "").TrimEnd('}'))!.GetValue(null)!)).ToArray();
        if (rules.Length != 160) throw new Exception("Unexpected reference rule count");
        var legacy = new UnifiedIcon();
        IconClassAliases.SetEnabled(legacy, false);
        var candidate = new UnifiedIcon();
        var oldRoot = new StackPanel { Children = { legacy } };
        var newRoot = new StackPanel { Children = { candidate } };
        foreach (var rule in rules)
            oldRoot.Styles.Add(new Style(selector => selector.OfType<UnifiedIcon>().Class(rule.Name)) {
                Setters = { new Setter(UnifiedIcon.ValueProperty, rule.Value) }
            });
        var root = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { oldRoot, newRoot } };
        var window = new Window { Width = 220, Height = 140, Content = root, Title = "Icon alias verification" };
        void Same(string stage) {
            if (!ReferenceEquals(legacy.Value, candidate.Value)) throw new Exception("Alias mismatch: " + stage);
        }
        try {
            window.Show(owner); await Task.Delay(100);
            foreach (var rule in rules) {
                legacy.Classes.Clear(); candidate.Classes.Clear();
                legacy.Classes.Add(rule.Name); candidate.Classes.Add(rule.Name);
                Same(rule.Name);
            }
            legacy.Classes.Clear(); candidate.Classes.Clear();
            // Reverse addition order must still follow the theme's rule order.
            foreach (var rule in rules.Reverse()) { legacy.Classes.Add(rule.Name); candidate.Classes.Add(rule.Name); Same("multiple classes"); }
            foreach (var rule in rules.Reverse()) { legacy.Classes.Remove(rule.Name); candidate.Classes.Remove(rule.Name); Same("remove class"); }
            if (candidate.Value is not null) throw new Exception("Empty aliases retain a value");
            legacy.Classes.Add(rules[0].Name); candidate.Classes.Add(rules[0].Name);
            legacy.Value = IconValues.Cog; candidate.Value = IconValues.Cog; Same("local value");
            legacy.Value = null; candidate.Value = null; Same("explicit local null");
            legacy.ClearValue(UnifiedIcon.ValueProperty); candidate.ClearValue(UnifiedIcon.ValueProperty); Same("clear local value");
            foreach (var scope in new[] { oldRoot, newRoot })
                scope.Styles.Add(new Style(selector => selector.OfType<UnifiedIcon>().Class("test-override")) {
                    Setters = { new Setter(UnifiedIcon.ValueProperty, IconValues.CogOutline) }
                });
            legacy.Classes.Add("test-override"); candidate.Classes.Add("test-override"); Same("scoped override");
            legacy.Classes.Remove("test-override"); candidate.Classes.Remove("test-override"); Same("remove scoped override");
            oldRoot.Children.Remove(legacy); newRoot.Children.Remove(candidate);
            oldRoot.Children.Add(legacy); newRoot.Children.Add(candidate);
            window.UpdateLayout(); await Task.Delay(50); Same("detach/reattach");
            IconClassAliases.SetEnabled(candidate, false);
            if (candidate.Value is not null) throw new Exception("Disabled resolver retained alias");
            candidate.ClearValue(IconClassAliases.EnabledProperty); Same("reenable resolver");
            Console.WriteLine("PASS icon aliases: all 160 original rules, reverse multi-class precedence/removal, local value/null, scoped override, detach/reattach and disable/reenable");
        } finally { window.Close(); }
    }
}
