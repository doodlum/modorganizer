using System.Xml.Linq;

namespace Mo2.Frontend;

// MO2's own window description, read rather than remembered.
//
// The widget check used to carry a list of what MO2 draws, typed out by hand. A
// list like that agrees with MO2 only on the day it is written: a widget MO2 has
// and the list forgot is a widget nothing looks for, and the check passes with it
// missing from both. So the expectation comes from src/mainwindow.ui itself — the
// file Qt builds MO2's window from — and the mapping below has to account for
// every name in it.
internal static class Mo2QtWidgetSource
{
    // Starts is MO2's own <property name="visible"> — its clearFiltersButton is
    // declared hidden and appears when a filter goes on, so whether a widget has to
    // be on screen the moment its tab opens is read from MO2 rather than decided
    // here.
    internal sealed record Widget(string Name, string Class, string Tab, bool Starts);

    // Walking up from the built binary rather than from the working directory: a
    // check is started by run-live.sh from wherever the shell happens to be.
    internal static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            var candidate = Path.Combine(directory.FullName, "src", "mainwindow.ui");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("src/mainwindow.ui was not found above " + AppContext.BaseDirectory);
    }

    // Every named widget, with the tab it is inside. Qt nests the tabs of its
    // QTabWidget as plain widgets, so the tab a widget belongs to is the nearest
    // ancestor named for one; anything outside them belongs to the window.
    internal static Widget[] Read(string? path = null)
    {
        var document = XDocument.Load(path ?? Locate());
        string[] tabs = ["espTab", "bsaTab", "dataTab", "savesTab", "downloadTab"];
        return document.Descendants("widget")
            .Where(x => (string?)x.Attribute("name") is { Length: > 0 })
            .Select(x => new Widget(
                (string)x.Attribute("name")!,
                (string?)x.Attribute("class") ?? "",
                x.Ancestors("widget").Select(a => (string?)a.Attribute("name") ?? "")
                    .FirstOrDefault(tabs.Contains) ?? "window",
                x.Elements("property").FirstOrDefault(p => (string?)p.Attribute("name") == "visible")
                    ?.Element("bool")?.Value != "false"))
            .ToArray();
    }

    // MO2's actions: what its menus and its toolbar are made of. A QMenu carries no
    // widget of its own, so a check that only walks widgets says nothing about
    // whether the things MO2 offers from its menu bar are offered here at all.
    internal sealed record Action(string Name, string Text);

    internal static Action[] Actions(string? path = null) =>
        XDocument.Load(path ?? Locate()).Descendants("action")
            .Where(x => (string?)x.Attribute("name") is { Length: > 0 })
            .Select(x => new Action((string)x.Attribute("name")!, Caption(x)))
            .ToArray();

    // Which actions MO2 puts on a given menu or on its toolbar, in its own order.
    // Qt writes a run of separators as addaction name="separator"; those are not
    // actions and are left out.
    internal static string[] Carries(string host, string? path = null) =>
        XDocument.Load(path ?? Locate()).Descendants("widget")
            .Where(x => (string?)x.Attribute("name") == host)
            .SelectMany(x => x.Elements("addaction"))
            .Select(x => (string?)x.Attribute("name") ?? "")
            .Where(x => x.Length > 0 && x != "separator")
            .ToArray();

    // MO2's caption for an action, with the Qt accelerator ampersand taken out.
    private static string Caption(XElement action) =>
        (action.Elements("property").FirstOrDefault(p => (string?)p.Attribute("name") == "text")
            ?.Element("string")?.Value ?? "").Replace("&", "");
}
