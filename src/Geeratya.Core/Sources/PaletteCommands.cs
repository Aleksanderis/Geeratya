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
    public const string Tab = "tab";
    public const string Labels = "labels";
    public const string Profile = "profile";
    public const string Help = "help";
    public const string Quit = "quit";

    public static IReadOnlyList<PaletteCommand> All { get; } =
    [
        new(Jql, "jql [query]", "Open a JQL query in a new tab (no query: pick from recent ones)"),
        new(Filter, "filter [name]", "Open a saved filter in a new tab"),
        new(Board, "board [name]", "Open a board as columns in a new tab"),
        new(Backlog, "backlog [name]", "Open a board's backlog in a new tab"),
        new(History, "history clear", "Forget the recent JQL queries and issues of this profile"),
        new(Tab, "tab new|close|next|prev|<n>", "Open the source picker, close or switch tabs"),
        new(Labels, "labels", "Edit the labels of the selected or shown issue (field form)"),
        new(Profile, "profile add|list|switch <name>|remove <name>|check", "Manage Jira profiles"),
        new(Help, "help", "Show or hide help"),
        new(Quit, "quit", "Quit"),
    ];

    /// <summary>The usage text of the command called <paramref name="name"/> (the name itself when unknown).</summary>
    public static string UsageOf(string name)
    {
        return All.FirstOrDefault(command => command.Name == name)?.Usage ?? name;
    }
}
