namespace Mo2.Frontend;

// Where this frontend offers each of MO2's actions.
//
// One list, read from both ends. `MO2_VERIFY_QT_ACTIONS` uses it to require that
// every <action> in MO2's mainwindow.ui is answered for and that no answer names
// an action MO2 has dropped; `Mo2QtShortcuts` uses the same entries to decide what
// the key MO2 gives an action should reach. It lived inside the check until the
// shortcuts needed it, and a second copy beside them could have disagreed with
// this one without either being wrong on its own.
internal static class Mo2QtActions
{
    internal enum Kind
    {
        // Offered here. Found live, through the control named.
        Offered,
        // Qt's own window furniture: how big the toolbar icons are, whether the
        // menu bar is showing. This frontend draws no Qt toolbar, so there is
        // nothing for these to act on.
        Chrome,
    }

    // Page is the page to open first, or "" for something the window carries
    // whatever page is open. Entry is the menu entry to look for inside Control's
    // flyout, or "" when the control is the answer itself.
    internal sealed record Answer(Kind Kind, string Page, string Control, string Entry, string Note);

    private static Answer On(string page, string control, string entry = "", string note = "") =>
        new(Kind.Offered, page, control, entry, note);
    private static Answer Qt(string note) => new(Kind.Chrome, "", "", "", note);

    // MO2's action, and what answers for it here. Everything in src/mainwindow.ui's
    // action list appears exactly once.
    internal static readonly Dictionary<string, Answer> Answers = new() {
        // MO2 opens its own global mod-list menu with Install mod..., which is the
        // button this offers it from; the page's own toolbar, which used to carry a
        // copy, is gone.
        ["actionInstallMod"] = On("my-mods", "ModsListOptionsButton", "Add mod from archive…"),
        ["actionAdd_Profile"] = On("connections","Mo2ManageProfiles"),
        ["actionModify_Executables"] = On("", "ExecutablesListBox", Mo2LaunchPanel.EditEntry,
            "MO2's own first row of the executables box, which opens its Edit Executables dialog"),
        ["actionTool"] = On("tools", "Mo2ToolsMenuItem", "", "MO2's tool plugins, as a page of their own"),
        ["actionSettings"] = On("connections","OriginalMo2Settings", "", "opens MO2's own settings dialog"),
        ["actionNexus"] = On("connections","OriginalMo2More", "Visit Nexus…"),
        ["actionModPage"] = On("my-mods", "ModRedesignRow", "Visit on Nexus",
            "MO2 offers this for a mod it knows on Nexus, and so does the row menu here"),
        ["actionUpdate"] = On("connections","OriginalMo2More", "Check for MO2 updates…"),
        ["actionNotifications"] = On("connections","OriginalMo2More", "MO2 notifications…"),
        ["actionHelp"] = On("connections","OriginalMo2More", "MO2 help…"),
        ["actionEndorseMO"] = On("connections","OriginalMo2More", "Endorse Mod Organizer…"),
        ["actionChange_Game"] = On("connections","Mo2AddInstance"),
        ["actionExit"] = On("", "CloseButton", "", "the window's own close button"),
        ["actionViewLog"] = On("", "Mo2LogsMenuItem", "", "MO2's log, which MO2 docks and this frontend opens as a page"),
        ["action_Refresh"] = On("my-mods", "ModsListOptionsButton", "Refresh"),

        ["actionMainMenuToggle"] = Qt("shows and hides Qt's menu bar"),
        ["actionStatusBarToggle"] = Qt("shows and hides Qt's status bar"),
        ["actionToolBarMainToggle"] = Qt("shows and hides Qt's toolbar"),
        ["actionToolBarSmallIcons"] = Qt("sizes Qt's toolbar icons"),
        ["actionToolBarMediumIcons"] = Qt("sizes Qt's toolbar icons"),
        ["actionToolBarLargeIcons"] = Qt("sizes Qt's toolbar icons"),
        ["actionToolBarIconsOnly"] = Qt("captions Qt's toolbar buttons"),
        ["actionToolBarTextOnly"] = Qt("captions Qt's toolbar buttons"),
        ["actionToolBarIconsAndText"] = Qt("captions Qt's toolbar buttons"),
    };

}
