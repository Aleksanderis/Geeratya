using Geeratya.Core.Settings;
namespace Geeratya.Core.Sources;

/// <summary>
/// The palette's command table: the one place that names the commands, their usage and what they do. The help
/// overlay and the generated key reference list it, so no list is written by hand.
/// </summary>
public static class PaletteCommands
{
    public const string Jql = "jql";
    public const string Filter = "filter";
    public const string Board = "board";
    public const string Backlog = "backlog";
    public const string History = "history";
    public const string View = "view";
    public const string Tab = "tab";
    public const string DoneCutoff = "donecutoff";
    public const string Labels = "labels";
    public const string Create = "create";
    public const string CreateSubtask = "create subtask";
    public const string Edit = "edit";
    public const string Priority = "priority";
    public const string Estimate = "estimate";
    public const string Assign = "assign";
    public const string AssignMe = "assign-me";
    public const string Status = "status";
    public const string Profile = "profile";
    public const string Set = "set";
    public const string Settings = "settings";
    public const string Theme = "theme";
    public const string Keys = "keys";
    public const string Help = "help";
    public const string Quit = "quit";

    public static IReadOnlyList<PaletteCommand> All { get; } =
    [
        new(Jql, "jql [query]", "Open a JQL query in a new tab (no query: pick from recent ones)"),
        new(Filter, "filter [name]", "Open a saved filter in a new tab"),
        new(Board, "board [name]", "Open a board as columns in a new tab"),
        new(Backlog, "backlog [name]", "Open a board's backlog in a new tab"),
        new(History, "history clear", "Forget the recent JQL queries and issues of this profile"),
        new(
            View,
            "view [name]|save <name>|rename <old> <new>|delete <name>",
            "Open, save, rename or delete a saved view (source and layout)"),
        new(Tab, "tab new|rename [name]|close|next|prev|<n>", "Open the source picker, close or switch tabs"),
        new(
            DoneCutoff,
            "donecutoff [auto|1w|2w|3w|4w|all]",
            "Choose how long a Kanban board keeps Done issues for this tab (auto follows Jira)"),
        new(Edit, "edit", "Edit the fields of the selected or shown issue (field form)"),
        new(Status, "status [next|prev]", "Change the status of the selected or shown issue; next or prev moves the card a column"),
        new(Labels, "labels", "Edit the labels of the selected or shown issue (field form)"),
        new(Create, "create [subtask]", "Create an issue, or a sub-task of the selected issue (field form)"),
        new(Priority, "priority", "Change the priority of the selected or shown issue (field form)"),
        new(Estimate, "estimate", "Change the estimate (story points) of the selected or shown issue (field form)"),
        new(Assign, "assign", "Assign the selected or shown issue: search people, or unassign"),
        new(AssignMe, "assign-me", "Assign the selected or shown issue to me"),
        new(Settings, "settings", "Open the settings screen: change the theme and the options"),
        new(Profile, "profile [list]|add|edit [name]|switch <name>|remove <name>|check", "Open the profile manager, or add, edit, switch, remove or check a profile"),
        new(Set, SetUsage(), "List the settings, show one, or change one (saved)"),
        new(Theme, "theme [name]", "Switch the colour theme (no name: list the families); saved"),
        new(Keys, "keys reload", "Read keymap.json again and apply it"),
        new(Help, "help", "Show or hide help"),
        new(Quit, "quit", "Quit"),
    ];

    /// <summary>The usage text of the command called <paramref name="name"/> (the name itself when unknown).</summary>
    public static string UsageOf(string name)
    {
        return All.FirstOrDefault(command => command.Name == name)?.Usage ?? name;
    }

    private static string SetUsage()
    {
        return $"set [{string.Join('|', SettingsRegistry.All.Select(setting => setting.Name))}] [value]";
    }
}
