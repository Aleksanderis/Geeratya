using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Secrets;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The profile manager (<c>:profile</c>, and the first screen when there is no profile): a full-body frame with two
/// panes, the profiles on the left (the active one marked) and the details of the highlighted one on the right (site,
/// account, where the token comes from, the connection status). Enter switches, <c>Ctrl-a</c> adds, <c>Ctrl-e</c>
/// edits, <c>Ctrl-x</c> or a right-click asks to remove. The token itself is never shown, only its source. Under
/// <see cref="DetailsMinimumWidth"/> columns the details pane is left out. Pure.
/// </summary>
public static class ProfileManagerScreen
{
    /// <summary>The narrowest body that shows the details pane next to the list.</summary>
    public const int DetailsMinimumWidth = 60;

    private const string ListTitle = "Profiles";
    private const string DetailsTitle = "Details";
    private const string AddLabel = "Add";
    private const string EditLabel = "Edit";
    private const int ButtonGap = 3;
    private const int NameColumn = 14;
    private const int LabelColumn = 9;
    private const int MinListWidth = 26;
    private const int MaxListWidth = 44;
    private const int ButtonRows = 1;
    private const string ActiveMark = "●";
    private const string ActiveMarkAscii = "*";
    private const string CheckingText = "looking up...";
    private const string NotSetText = "not set";
    private const string StoreText = "OS secret store";
    private const string NotCheckedText = "not checked (Enter switches to it)";

    /// <summary>
    /// The body lines for <paramref name="state"/> (exactly <paramref name="height"/> lines); the clickable rows and
    /// buttons are added to <paramref name="regions"/> (body-relative) when it is not null.
    /// </summary>
    public static List<StyledLine> Render(
        AppState state,
        BodyContext context,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var profiles = state.Profiles.Profiles;
        var selected = Math.Clamp(state.ProfileList?.Selected ?? 0, 0, Math.Max(0, profiles.Count - 1));
        var withDetails = width >= DetailsMinimumWidth;
        var listWidth = withDetails ? Math.Clamp(width * 2 / 5, MinListWidth, MaxListWidth) : width;
        var framed = FrameGrid.CanFrame(width, height);
        var inner = framed ? Math.Max(0, height - 2) : height;
        var listRoom = Math.Max(0, listWidth - (framed ? 3 : 0));
        var area = Math.Max(0, inner - ButtonRows);
        var first = FirstVisible(selected, profiles.Count, area);
        var left = ListLines(state, context, selected, first, area, listRoom, inner);
        var right = withDetails ? DetailLines(state, selected, width - listWidth - 1) : [];

        if (regions is not null)
        {
            MarkRegions(regions, framed, width, height, profiles.Count, first, area, listWidth);
        }

        if (!framed)
        {
            return [.. left.Take(height), .. Enumerable.Repeat(StyledLine.Empty, Math.Max(0, height - left.Count))];
        }

        return Frame(profiles.Count, width, height, listWidth, withDetails, left, right);
    }

    /// <summary>The text of the token source row (never a token).</summary>
    public static string TokenText(AppState state, Profile profile)
    {
        if (!state.TokenSources.TryGetValue(profile.CredentialKey, out var source))
        {
            return CheckingText;
        }

        return source switch
        {
            TokenSourceKind.SecretStore => StoreText,
            TokenSourceKind.EnvironmentVariable => $"env {TokenProvider.EnvironmentVariable}",
            TokenSourceKind.Unavailable => "secret store unavailable",
            _ => NotSetText,
        };
    }

    private static List<StyledLine> Frame(
        int count,
        int width,
        int height,
        int listWidth,
        bool withDetails,
        List<StyledLine> left,
        List<StyledLine> right)
    {
        var listPane = new PaneRect(0, 0, height, listWidth);
        var detailsPane = new PaneRect(0, listWidth - 1, height, width - listWidth + 1);
        var panes = withDetails ? new List<PaneRect> { listPane, detailsPane } : [listPane];
        var grid = new FrameGrid(width, height, panes);
        var title = new List<StyledSpan> { new($"{ListTitle} {count}", TextRole.Heading) };
        var chips = new List<EdgeChip>(FrameGrid.Chips(listPane, title));

        if (withDetails)
        {
            chips.AddRange(FrameGrid.Chips(detailsPane, [new StyledSpan(DetailsTitle, TextRole.Muted)]));
        }

        var lines = new List<StyledLine>(height) { new(grid.Row(0, static (_, _) => [], chips)) };

        for (var row = 1; row < height - 1; row++)
        {
            var index = row - 1;
            var spans = grid.Row(
                row,
                (start, length) => start < listWidth - 1
                    ? Line(left, index, length)
                    : Line(right, index, length));

            lines.Add(new StyledLine(spans));
        }

        lines.Add(new StyledLine(grid.Row(height - 1, static (_, _) => [])));

        return lines;
    }

    private static IReadOnlyList<StyledSpan> Line(List<StyledLine> lines, int index, int length)
    {
        var line = index < lines.Count ? lines[index] : StyledLine.Empty;

        if (length < 2)
        {
            return StyledLines.Fit(line, length).Spans;
        }

        return [new StyledSpan(" ", TextRole.Body), .. StyledLines.Fit(line, length - 1).Spans];
    }

    private static List<StyledLine> ListLines(
        AppState state,
        BodyContext context,
        int selected,
        int first,
        int area,
        int room,
        int inner)
    {
        var profiles = state.Profiles.Profiles;
        var lines = new List<StyledLine>(inner);
        var mark = context.Glyphs == GlyphSet.Ascii ? ActiveMarkAscii : ActiveMark;

        if (profiles.Count == 0)
        {
            var add = context.Keymap.FindKeys(InputMode.Insert, ActionIds.ProfileAdd);

            lines.Add(StyledLine.Of("No profile yet."));
            lines.Add(StyledLine.Of($"{add} adds one.", TextRole.Muted));
        }

        for (var index = first; index < profiles.Count && lines.Count < area; index++)
        {
            lines.Add(RowLine(profiles[index], state.Profiles.ActiveName, index == selected, mark, room));
        }

        while (lines.Count < inner - ButtonRows)
        {
            lines.Add(StyledLine.Empty);
        }

        if (inner > 0)
        {
            lines.Add(ButtonLine());
        }

        return lines;
    }

    private static StyledLine RowLine(Profile profile, string? activeName, bool selected, string mark, int room)
    {
        var active = profile.Name == activeName;
        var name = TextWidth.Pad(TextWidth.Fit(profile.Name, NameColumn - 1), NameColumn);
        var cursor = selected ? ">" : " ";
        var dot = active ? mark : " ";
        var marker = $"{cursor}{dot} ";

        if (selected)
        {
            return StyledLine.Of(TextWidth.Pad($"{marker}{name}{profile.Email}", room), TextRole.Selected);
        }

        return new StyledLine(
        [
            new StyledSpan(marker[..1], TextRole.Muted),
            new StyledSpan(marker[1..], active ? TextRole.StatusDone : TextRole.Muted),
            new StyledSpan(name, TextRole.Strong),
            new StyledSpan(profile.Email, TextRole.Muted),
        ]);
    }

    private static StyledLine ButtonLine()
    {
        var gap = new string(' ', ButtonGap);

        return new StyledLine(
        [
            new StyledSpan(AddLabel, TextRole.Key),
            new StyledSpan(gap, TextRole.Body),
            new StyledSpan(EditLabel, TextRole.Key),
        ]);
    }

    private static List<StyledLine> DetailLines(AppState state, int selected, int room)
    {
        var profiles = state.Profiles.Profiles;

        if (profiles.Count == 0)
        {
            return EmptyDetails(room);
        }

        var profile = profiles[selected];
        var lines = new List<StyledLine>
        {
            Detail("Name", profile.Name, TextRole.Strong),
            Detail("Site", profile.SiteUrl, TextRole.Body),
            Detail("Account", profile.Email, TextRole.Body),
            Detail("Token", TokenText(state, profile), TokenRole(state, profile)),
        };

        var status = profile.Name == state.Profiles.ActiveName ? ConnectionText.Line(state.Session) : NotCheckedText;

        if (status.Length == 0)
        {
            status = "active";
        }

        var wrapped = ConfirmDialog.Wrap(status, Math.Max(1, room - LabelColumn - 1));

        for (var index = 0; index < wrapped.Count; index++)
        {
            lines.Add(Detail(index == 0 ? "Status" : string.Empty, wrapped[index], StatusRole(state, profile)));
        }

        return lines;
    }

    private static List<StyledLine> EmptyDetails(int room)
    {
        var lines = new List<StyledLine>();

        foreach (var text in ConfirmDialog.Wrap("Add a Jira Cloud site, the account and an API token to start.", room - 2))
        {
            lines.Add(StyledLine.Of(text));
        }

        lines.Add(StyledLine.Empty);

        foreach (var text in ConfirmDialog.Wrap("The token is kept in the OS secret store and never shown.", room - 2))
        {
            lines.Add(StyledLine.Of(text, TextRole.Muted));
        }

        return lines;
    }

    private static TextRole TokenRole(AppState state, Profile profile)
    {
        var text = TokenText(state, profile);

        return text == NotSetText ? TextRole.Warning : TextRole.Body;
    }

    private static TextRole StatusRole(AppState state, Profile profile)
    {
        if (profile.Name != state.Profiles.ActiveName)
        {
            return TextRole.Muted;
        }

        return state.Session.Connection is ConnectionCheckFailure ? TextRole.Error : TextRole.Body;
    }

    private static StyledLine Detail(string label, string value, TextRole role)
    {
        return new StyledLine(
        [
            new StyledSpan(TextWidth.Pad(label, LabelColumn), TextRole.Muted),
            new StyledSpan(value, role),
        ]);
    }

    private static int FirstVisible(int selected, int count, int area)
    {
        return area <= 0 || selected < area ? 0 : Math.Min(selected - area + 1, Math.Max(0, count - area));
    }

    private static void MarkRegions(
        List<OverlayRegion> regions,
        bool framed,
        int width,
        int height,
        int count,
        int first,
        int area,
        int listWidth)
    {
        var offset = framed ? 1 : 0;
        var left = framed ? 1 : 0;
        var shown = Math.Max(0, Math.Min(area, count - first));

        // The whole body is the screen: a click on a part of it that is no row or button does nothing.
        OverlayBox.Cover(regions, 0, 0, width, height);
        OverlayBox.Mark(
            regions,
            MouseTarget.ProfileRow,
            0,
            left - 1,
            listWidth + (framed ? 0 : 2),
            offset,
            Enumerable.Range(first, shown));

        var buttonRow = offset + Math.Max(0, (framed ? height - 2 : height) - ButtonRows);

        regions.Add(new OverlayRegion(MouseTarget.ProfileAddButton, buttonRow, left, AddLabel.Length, 1, 0));
        regions.Add(
            new OverlayRegion(
                MouseTarget.ProfileEditButton,
                buttonRow,
                left + AddLabel.Length + ButtonGap,
                EditLabel.Length,
                1,
                0));
    }
}
