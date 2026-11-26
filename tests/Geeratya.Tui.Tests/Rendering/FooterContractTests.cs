using Geeratya.Core.Forms;
using Geeratya.Core.Links;
using Geeratya.Core.Sprints;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// The rendered-frame contract of every footer and status-line hint list (issue #1030,
/// <c>docs/internal/ui-consistency.md</c>, component 6, findings H1 to H5, H8 and L3). Each surface is reached through
/// the reducer or its builder, drawn by <see cref="FrameBuilder"/>, and the key chips are read back from the drawn
/// cells: the in-box footer (key role, muted label) and the status line (bar key role, bar muted label). Both must
/// equal the expected list of that surface cell for cell, so one word per action holds across surfaces and the two
/// places cannot disagree. A new surface that carries hints is added to the surface table, not given its own test.
/// </summary>
[Trait("Os", "Independent")]
public class FooterContractTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly FieldChoiceContractTests _fields = new();
    private static readonly TextEditorContractTests _editor = new();
    private static readonly FormPresentationContractTests _forms = new();

    // The surfaces and, at 120x36, the chips both places show (key|label, left to right), the badge and the prompt.
    private static readonly Surface[] _surfaces =
    [
        new("jump", "INSERT", "Jump to an issue", ["Up/Down|move", "Enter|open", "Esc|close"]),
        new("picker", "INSERT", "Open a view", ["Up/Down|move", "Enter|open in a new tab", "Esc|close"]),
        new("tab list", "INSERT", "Open tabs", ["Up/Down|move", "Enter|switch", "Esc|close"]),
        new("sprint switcher", "INSERT", "Switch sprint",
            ["Up/Down|move", "Enter|open", "Ctrl-t|new tab", "Esc|close"]),
        new("sprint move", "INSERT", "Move to sprint: A-1", ["Up/Down|move", "Enter|move here", "Esc|close"]),
        new("actions menu", "NORMAL", "Actions", ["Up/Down|move", "Enter|run", "Esc|close"]),
        new("palette", "COMMAND", ":", ["Up/Down|move", "Enter|run", "Esc|close"]),
        new("help", "HELP", string.Empty, ["j/k|move", "Tab|page", "/|filter", "J/K|scroll", "Esc|close"]),
        new("help filter", "HELP", string.Empty, ["Enter|keep filter", "Tab|page", "Esc|clear filter"]),
        new("messages", "MESSAGES", string.Empty, ["j/k|scroll", "Esc|close"]),
        new("comment", "INSERT", "Commenting on A-1", ["Ctrl-s|send", "Enter|new line", "Esc|cancel"]),
        new("profile form name", "INSERT", "Profile name",
            ["Enter|next", "Tab|field", "Ctrl-s|save", "Alt-t|test", "Esc|cancel"]),
        new("profile form token", "INSERT", "API token (hidden)",
            ["Enter|save", "Tab|field", "Alt-t|test", "Esc|cancel"]),
        new("profile list", "NORMAL", "Active: work",
            ["Up/Down|move", "Enter|switch", "Ctrl-a|add", "Ctrl-e|edit", "Ctrl-x|remove", "Esc|close"], false),
        new("settings", "NORMAL", "Settings: a change applies at once and is saved",
            ["j/k|move", "Enter|change", "Alt-r|reset", "Esc|close"]),
        new("which-key", "NORMAL", "<space>i", ["Esc|cancel"]),
        new("type chooser", "INSERT", "Choose the issue type", ["Up/Down|move", "Enter|choose", "Esc|close"]),
        new("status picker", "INSERT", "Change status of A-1 · Move to", ["Up/Down|move", "Enter|choose", "Esc|close"]),
        new("box assignee", "INSERT", "Assign GCM-12 · Assignee",
            ["Up/Down|move", "Enter|choose", "Tab/Shift-Tab|field", "Ctrl-s|save", "Esc|stop editing"]),
        new("create assignee", "INSERT", "Create GCM · Task · Assignee",
            ["Up/Down|move", "Enter|choose", "Tab/Shift-Tab|field", "Ctrl-s|create", "Esc|stop editing"], false),
        new("subtask assignee", "INSERT", "Create GCM · Sub · Assignee",
            ["Up/Down|move", "Enter|choose", "Tab/Shift-Tab|field", "Ctrl-s|create", "Esc|stop editing"], false),
        new("dropdown assignee", "INSERT", "Edit GCM-12 · Assignee",
            ["Up/Down|move", "Enter|choose", "Esc|close"], false),
        new("box reporter", "INSERT", "Assign GCM-12 · Reporter",
            ["Tab/Shift-Tab|field", "Ctrl-s|save", "Esc|stop editing"]),
        new("create reporter", "INSERT", "Create GCM · Task · Reporter",
            ["Tab/Shift-Tab|field", "Ctrl-g|open after: off", "Ctrl-s|create", "Esc|stop editing"], false, true, 2),
        new("subtask reporter", "INSERT", "Create GCM · Sub · Reporter",
            ["Tab/Shift-Tab|field", "Ctrl-g|open after: off", "Ctrl-s|create", "Esc|stop editing"], false, true, 2),
        new("dropdown reporter", "INSERT", "Edit GCM-12 · Reporter", ["Esc|close"], false),
        new("link form", "INSERT", "Link A-1 · First", ["Tab/Shift-Tab|field", "Ctrl-s|save", "Esc|cancel"]),
        new("sprint form", "INSERT", "Create sprint · First", ["Tab/Shift-Tab|field", "Ctrl-s|save", "Esc|cancel"]),
        new("create navigating", "NORMAL", "Create GCM · Task · Summary",
            ["Tab/Shift-Tab|field", "Enter|edit", "Ctrl-s|create", "Esc|cancel"], false, false, 2),
        new("create due date", "INSERT", "Create GCM · Task · Due date",
            ["Tab/Shift-Tab|field", "Ctrl-g|open after: off", "Ctrl-s|create", "Esc|stop editing"], false, false, 2),
        new("dropdown due date", "INSERT", "Edit GCM-12 · Due date", ["Enter|save", "Esc|close"], false),
        new("create editing", "INSERT", "Create GCM · Task · Summary",
            ["Tab/Shift-Tab|field", "Ctrl-g|open after: off", "Ctrl-s|create", "Esc|stop editing"], false, true, 2),
    ];

    public static TheoryData<string, int, int> Matrix()
    {
        var data = new TheoryData<string, int, int>();

        foreach (var surface in _surfaces)
        {
            data.Add(surface.Name, 120, 36);
            data.Add(surface.Name, 80, 24);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void GivenASurface_WhenDrawn_ThenTheStatusLineAndTheFooterShowTheSameChipsInTheKeyAndMutedRoles(
        string name,
        int width,
        int height)
    {
        var surface = _surfaces.Single(candidate => candidate.Name == name);
        var frame = FrameBuilder.BuildStyled(StateOf(name), width, height, _keymap)!;
        var status = StatusChips(frame);

        if (width == 120 && surface.Fits)
        {
            Assert.Equal(surface.Chips, status);
        }
        else
        {
            AssertNarrowed(surface, status);
        }

        if (FooterChips(frame) is not { } footer)
        {
            Assert.False(surface.HasFooter, $"{name}: the box shows no footer at {width}x{height}");

            return;
        }

        if (surface.HasFooter && width == 120)
        {
            Assert.Equal(surface.Chips, footer);
        }
        else
        {
            AssertNarrowed(surface, footer);
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void GivenASurface_WhenDrawn_ThenTheBadgeAndThePromptAreTheOnesOfItsRule(string name, int width, int height)
    {
        var surface = _surfaces.Single(candidate => candidate.Name == name);
        var frame = FrameBuilder.BuildStyled(StateOf(name), width, height, _keymap)!;
        var line = frame.StatusStyled!;
        var badge = line.Spans[0].Text.Trim();

        // The prompt is the span after the badge and its space; a line without one has none.
        var rest = line.Spans.Count > 2 ? line.Spans[2].Text.Trim() : string.Empty;

        Assert.Equal(surface.Badge, badge);

        if (width == 120 && surface.Prompt.Length > 0)
        {
            Assert.StartsWith(surface.Prompt, rest, StringComparison.Ordinal);
            Assert.True(char.IsUpper(surface.Prompt[0]) || surface.Prompt[0] is ':' or '<', name);
        }
    }

    [Theory]
    [InlineData("quit", "CONFIRM", TextRole.BadgeConfirmWarn)]
    [InlineData("edit form", "CONFIRM", TextRole.BadgeConfirmWarn)]
    [InlineData("delete issue", "CONFIRM", TextRole.BadgeConfirm)]
    [InlineData("complete sprint", "CONFIRM", TextRole.BadgeConfirm)]
    public void GivenAQuestion_WhenDrawn_ThenTheConfirmBadgeFollowsTheFrameColour(
        string place,
        string badge,
        TextRole role)
    {
        var frame = FrameBuilder.BuildStyled(ConfirmContractTests.Asked(place), 120, 36, _keymap)!;
        var span = frame.StatusStyled!.Spans[0];

        Assert.Equal(badge, span.Text.Trim());
        Assert.Equal(role, span.Role);
    }

    [Fact]
    public void GivenAnInformationOnlyNotice_WhenDrawn_ThenTheBadgeIsHelpAndTheKeyClosesIt()
    {
        var frame = FrameBuilder.BuildStyled(ConfirmNoticeTests.Open("Ship it"), 120, 36, _keymap)!;
        var span = frame.StatusStyled!.Spans[0];

        Assert.Equal("HELP", span.Text.Trim());
        Assert.Equal(TextRole.BadgeHelp, span.Role);
        Assert.Equal(["y|close"], StatusChips(frame));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(80)]
    public void GivenALongPaletteQuery_WhenDrawn_ThenTheTypedEchoIsNeverCutForTheHints(int width)
    {
        var query = "jql project = GEN and status = Done order by created desc";
        var state = TextEditorContractTests.Open("palette") with { PaletteText = query };
        var frame = FrameBuilder.BuildStyled(state, width, 24, _keymap)!;

        Assert.Contains($":{query}", frame.StatusLine, StringComparison.Ordinal);
        Assert.DoesNotContain("…", frame.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenEveryFormPopup_WhenCompared_ThenThePickerKeysAreTheSameInEveryPresentation()
    {
        var assignee = new[] { "box assignee", "create assignee", "subtask assignee", "dropdown assignee" };
        var reporter = new[] { "box reporter", "create reporter", "subtask reporter", "dropdown reporter" };

        foreach (var group in new[] { assignee, reporter })
        {
            var keys = group
                .Select(static name => _surfaces.Single(surface => surface.Name == name).Chips
                    .Where(static chip => chip.StartsWith("Up/Down|", StringComparison.Ordinal)
                        || chip.StartsWith("Enter|", StringComparison.Ordinal)))
                .ToList();

            Assert.All(keys, row => Assert.Equal(keys[0], row));
        }
    }

    [Fact]
    public void GivenAnyOverlayHint_WhenRead_ThenItUsesTheWordsOfOneTable()
    {
        var labels = _surfaces
            .SelectMany(static surface => surface.Chips)
            .Select(static chip => chip.Split('|')[1])
            .Distinct();
        var table = typeof(HintLabels).GetFields()
            .Select(static field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.All(labels, label => Assert.True(
            table.Contains(label, StringComparer.Ordinal) || label.StartsWith("open after", StringComparison.Ordinal),
            $"'{label}' is not a HintLabels word"));
    }

    // On a narrow line the chips before the kept ones (the way out, in the create panes the write too) drop from the
    // end; the kept ones stay last.
    private static void AssertNarrowed(Surface surface, List<string> shown)
    {
        var kept = surface.Chips[^surface.Kept..];
        var rest = surface.Chips[..^surface.Kept];
        var expected = $"[{string.Join(", ", surface.Chips)}]";
        var tail = shown.Count >= surface.Kept ? shown[^surface.Kept..] : [];
        var head = shown.Count >= surface.Kept ? shown[..^surface.Kept] : shown;

        Assert.True(
            tail.SequenceEqual(kept) && head.SequenceEqual(rest.Take(head.Count)),
            $"{surface.Name}: [{string.Join(", ", shown)}] is not {expected} narrowed");
    }

    // The chips of the status line: a bar key span followed by its bar muted label span.
    private static List<string> StatusChips(StyledFrame frame)
    {
        var spans = frame.StatusStyled!.Spans;
        var chips = new List<string>();

        for (var index = 0; index < spans.Count - 1; index++)
        {
            if (spans[index].Role == TextRole.BarKey)
            {
                Assert.Equal(TextRole.BarMuted, spans[index + 1].Role);
                chips.Add($"{spans[index].Text}|{spans[index + 1].Text.Trim()}");
            }
        }

        return chips;
    }

    // The last line of the box that is made of key and muted chips only: the in-box footer. Null when none is drawn.
    private static List<string>? FooterChips(StyledFrame frame)
    {
        List<string>? found = null;

        foreach (var line in frame.Body)
        {
            var spans = line.Spans
                .Where(static span => span.Text.Trim().Length > 0 && !IsWall(span.Text))
                .ToList();

            if (spans.Count < 2 || spans.Count % 2 != 0)
            {
                continue;
            }

            var chips = new List<string>();

            for (var index = 0; index < spans.Count; index += 2)
            {
                if (spans[index].Role != TextRole.Key || spans[index + 1].Role != TextRole.Muted
                    || !spans[index + 1].Text.StartsWith(' '))
                {
                    chips = [];

                    break;
                }

                chips.Add($"{spans[index].Text}|{spans[index + 1].Text.Trim()}");
            }

            if (chips.Count > 0)
            {
                found = chips;
            }
        }

        return found;
    }

    private static bool IsWall(string text)
    {
        return text.Trim().All(static character => "│╭╮╰╯─┌┐└┘|+-".Contains(character));
    }

    private static AppState StateOf(string name)
    {
        return name switch
        {
            "jump" => TextEditorContractTests.Open("jump"),
            "picker" => TextEditorContractTests.Open("picker"),
            "tab list" => TabListOverlayTests.Opened(3),
            "sprint switcher" => SprintPickerSnapshotTests.Sample(2),
            "actions menu" => ContextMenuOverlayTests.Sample(),
            "palette" => TextEditorContractTests.Open("palette"),
            "help" => AppState.Initial with { Mode = InputMode.Help },
            "help filter" => AppState.Initial with { Mode = InputMode.Help, HelpFiltering = true },
            "messages" => StatusMessages.OpenHistory(AppState.Initial with { Mode = InputMode.Help }),
            "comment" => CommentEditorOverlayTests.StateWith(CommentEditorOverlayTests.Editor("hello")),
            "profile form name" => AppState.Initial with { Mode = InputMode.Insert, Form = ProfileForm.Start },
            "profile form token" => AppState.Initial with
            {
                Mode = InputMode.Insert,
                Form = ProfileForm.Start with { Field = ProfileFormField.Token },
            },
            "profile list" => ProfileManagerScreenTests.Sample(),
            "settings" => SettingsScreenTests.Sample(),
            "which-key" => WhichKeyOverlayTests.Opened("Space i"),
            "sprint move" => SprintMove(),
            "link form" => TwoFields(new LinkFormTarget("A-1")),
            "sprint form" => TwoFields(new SprintFormTarget(1, null)),
            "type chooser" => TypeChooser(),
            "status picker" => StatusPicker(),
            "create navigating" => _editor.Keys(CreatePanes(), "Esc"),
            "create editing" => CreatePanes(),
            "create due date" => CreatePanes(DueDate(), 1),
            "dropdown due date" => _forms.Open("details-due", DueDate()),
            _ => FormPopup(name),
        };
    }

    // "box assignee", "create reporter", "dropdown assignee": the presentation, then the field.
    private static AppState FormPopup(string name)
    {
        var parts = name.Split(' ');
        var field = FieldChoiceContractTests.FieldOf(parts[1]);

        // A reporter who is already the chosen one has no rows to offer.
        field = parts[1] == "reporter" ? field.WithInitial(FormValue.OfChosen([FieldChoiceContractTests.Me])) : field;

        // The popup is open as the "open" scenario of FieldChoiceContractTests leaves it.
        return _fields.Open(parts[0], field);
    }

    private static FormField DueDate()
    {
        return FormField.Create(FormFieldIds.DueDate, "Due date", FormFieldKind.Date, false, []);
    }

    private static AppState SprintMove()
    {
        var state = SprintPickerSnapshotTests.Sample(2);
        var picker = state.Picker!;

        return state with { Picker = picker with { Sprint = picker.Sprint! with { MoveKeys = ["A-1"] } } };
    }

    // A form of two text fields in its edit state, as the link and the sprint form open.
    private static AppState TwoFields(FormTarget target)
    {
        var first = FormField.Create("first", "First", FormFieldKind.Text, false, []);
        var second = FormField.Create("second", "Second", FormFieldKind.Text, false, []);
        var form = FormState.Opened(target, 3, null) with { Fields = [first, second], IsLoading = false };

        return FormOverlaySnapshotTests.WithForm(form);
    }

    private static AppState TypeChooser()
    {
        var target = new CreateFormTarget("A", null, false, null);
        var types = new[] { new CreateIssueType("1", "Task", string.Empty, 0, false) };
        var form = FormState.Opened(target, 3, null) with { TypeChoices = types, IsLoading = false };

        return CreatePaneOverlaySnapshotTests.Open(form) with { Mode = InputMode.Insert };
    }

    private static AppState StatusPicker()
    {
        var target = new TransitionPickTarget("A-1", [], null, [])
            .With([new Transition("21", "In Progress", new IssueStatus("21", "In Progress", "new"), false, true, [])]);
        var form = FormState.Opened(target, 3, null) with { Fields = target.Fields(), IsLoading = false };
        var shown = form with { Suggest = ChoicePopup.For(form.Fields[0], 0, null) };

        return FormOverlaySnapshotTests.WithForm(shown);
    }

    private static AppState CreatePanes(FormField? second = null, int focus = 0)
    {
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);
        var estimate = second ?? FormField.Create("estimate", "Estimate", FormFieldKind.Text, false, []);
        var type = new CreateIssueType("10", "Task", string.Empty, 0, false);
        var target = new CreateFormTarget("GCM", null, false, type);
        var form = FormState.Opened(target, 3, null) with
        {
            Fields = [summary, estimate],
            IsLoading = false,
            Navigable = true,
            Focus = focus,
        };

        return CreatePaneOverlaySnapshotTests.Open(form);
    }

    // One surface: its name, the badge and prompt of its status line, its chips at 120x36 and whether its box has
    // a footer (the panes, the Details dropdown and the profile list are narrower than their hints, or have none).
    private sealed record Surface(
        string Name,
        string Badge,
        string Prompt,
        string[] Chips,
        bool HasFooter = true,
        bool Fits = true,
        int Kept = 1);
}
