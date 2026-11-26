using Geeratya.Core.Detail;
using Geeratya.Core.Editing;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The panes of the create form placed in an overlay of a given size: the main column (title, Description, "Not
/// editable here"; with the Details pane under them in one column) and the Details sidebar from 100 columns. When the
/// overlay is short the pane that holds the focus gets the room. Pure; the mouse rows it draws go into the region
/// list it was given, relative to the overlay.
/// </summary>
internal sealed class CreatePaneLayout
{
    private const string Separator = " · ";
    private const string TitleText = "Create";
    private const string DescriptionTitle = "Description";
    private const string DescriptionHint = "markdown, Enter adds a line";
    private const string NotEditableTitle = "Not editable here";
    private const string NotEditableText = "Attachments and comments are added after the issue exists.";
    private const string OpenAfterText = "opens after create";
    private const string TitlePlaceholder = "What needs to be done?";
    private const string DescriptionPlaceholder = "Describe it.";
    private const int NotEditableMinimum = 6;

    private readonly FormState _form;
    private readonly KeymapTable _keymap;
    private readonly bool _ascii;
    private readonly CreatePaneFrames _frames;

    public CreatePaneLayout(FormState form, KeymapTable keymap, bool ascii, List<OverlayRegion> regions)
    {
        _form = form;
        _keymap = keymap;
        _ascii = ascii;
        _frames = new CreatePaneFrames(ascii, regions);
    }

    /// <summary>Exactly <paramref name="height"/> lines for an overlay <paramref name="width"/> columns wide.</summary>
    public List<StyledLine> Build(int width, int height)
    {
        var wide = DetailLayout.HasSidebar(width);
        var mainWidth = wide ? DetailLayout.MainWidth(width) : width;
        var main = MainColumn(mainWidth, height, wide);
        var column = mainWidth + DetailLayout.SidebarGap;
        var side = wide ? Details(DetailLayout.SidebarWidthFor(width), height, column, 0) : [];
        var lines = new List<StyledLine>(height);

        for (var row = 0; row < height; row++)
        {
            var left = row < main.Count ? main[row] : StyledLine.Empty;

            if (!wide)
            {
                lines.Add(left);

                continue;
            }

            var spans = StyledLines.Fit(left, column).Spans.ToList();

            spans.AddRange(row < side.Count ? side[row].Spans : []);
            lines.Add(new StyledLine(spans));
        }

        return lines;
    }

    private List<StyledLine> MainColumn(int width, int height, bool wide)
    {
        var lines = new List<StyledLine>();
        var focused = CreatePanes.FocusedPane(_form);
        var title = TitlePane(width, focused == PaneIds.Title);
        var notEditable = NotEditablePane(width);
        var notHeight = notEditable.Rows.Count + Panel.EdgeRows;
        var rest = height - title.Rows.Count - Panel.EdgeRows;
        var showNot = rest - notHeight >= (wide ? NotEditableMinimum : CreateDetailsFit.StackedNotEditableMinimum);

        lines.AddRange(_frames.Build(title, width, title.Rows.Count, 0, 0));

        rest -= showNot ? notHeight : 0;

        if (wide)
        {
            AddDescription(lines, width, rest, focused);
        }
        else
        {
            AddStacked(lines, width, rest, focused);
        }

        if (showNot)
        {
            lines.AddRange(_frames.Build(notEditable, width, notEditable.Rows.Count, lines.Count, 0));
        }

        return lines;
    }

    private void AddDescription(List<StyledLine> lines, int width, int rows, string? focused)
    {
        if (CreatePanes.Shown(_form, PaneIds.Description).Count == 0)
        {
            return;
        }

        var description = DescriptionPane(width, focused == PaneIds.Description);

        lines.AddRange(_frames.Build(description, width, rows - Panel.EdgeRows, lines.Count, 0));
    }

    // One column: Description and Details share the rows below the title pane, or only the focused one when short.
    private void AddStacked(List<StyledLine> lines, int width, int rest, string? focused)
    {
        var hasDetails = CreatePanes.Shown(_form, PaneIds.Details).Count > 0;
        var hasDescription = CreatePanes.Shown(_form, PaneIds.Description).Count > 0;

        if (!hasDetails)
        {
            AddDescription(lines, width, rest, focused);

            return;
        }

        if (!hasDescription || (rest < CreateDetailsFit.StackedMinimum && focused == PaneIds.Details))
        {
            lines.AddRange(Details(width, rest, 0, lines.Count));

            return;
        }

        if (rest < CreateDetailsFit.StackedMinimum)
        {
            AddDescription(lines, width, rest, focused);

            return;
        }

        var upper = Math.Clamp(rest / 3, CreateDetailsFit.DescriptionShortest, CreateDetailsFit.DescriptionTallest);

        AddDescription(lines, width, upper, focused);
        lines.AddRange(Details(width, rest - upper, 0, lines.Count));
    }

    private List<StyledLine> Details(int width, int height, int left, int top)
    {
        var focused = CreatePanes.FocusedPane(_form) == PaneIds.Details;
        var spec = new CreateDetailsRows(_form, _keymap, _ascii).Pane(Panel.InnerWidth(width), focused);

        return _frames.Build(spec, width, height - Panel.EdgeRows, top, left);
    }

    private CreatePaneSpec TitlePane(int width, bool focused)
    {
        var index = CreatePanes.Shown(_form, PaneIds.Title) is [var first, ..] ? first : -1;
        var create = (CreateFormTarget)_form.Target;
        var required = index >= 0 && _form.Fields[index].Required;
        var heading = $"{TitleText}{Separator}{create.ProjectKey}{Separator}{create.IssueType?.Name}";
        var named = FieldLabels.With(heading, required);
        var title = new StyledSpan(named, TextRole.Heading);
        var rows = new List<CreatePaneRow>();

        if (index >= 0)
        {
            rows.Add(new CreatePaneRow(SummaryLine(index, Panel.InnerWidth(width)), MouseTarget.FormField, index));

            if (_form.Fields[index].Error is { } error)
            {
                rows.AddRange(ErrorRows(error, index, Panel.InnerWidth(width)));
            }
        }

        rows.AddRange(Notices(Panel.InnerWidth(width)).Select(static line => new CreatePaneRow(line)));

        var openAfter = create.OpenAfter ? new[] { new StyledSpan(OpenAfterText, TextRole.Muted) } : null;

        return new CreatePaneSpec([title], openAfter, rows, focused);
    }

    private StyledLine SummaryLine(int index, int inner)
    {
        var field = _form.Fields[index];
        var editing = index == _form.Focus && !_form.Navigating && !_form.IsSaving;
        var look = new FieldLook(Math.Max(1, inner)) { Editing = editing, Focused = index == _form.Focus, Placeholder = TitlePlaceholder };

        return FieldRows.Value(field, look)[0];
    }

    private static IEnumerable<CreatePaneRow> ErrorRows(string error, int index, int inner)
    {
        return FieldRows.Error(error, inner).Select(line => new CreatePaneRow(line, MouseTarget.FormField, index));
    }

    private List<StyledLine> Notices(int inner)
    {
        var notices = new List<StyledLine>();

        if (_form.TopError is not null)
        {
            notices.AddRange(FieldRows.Error(_form.TopError, inner));
        }

        return notices;
    }

    private CreatePaneSpec DescriptionPane(int width, bool focused)
    {
        var index = CreatePanes.Shown(_form, PaneIds.Description) is [var first, ..] ? first : -1;
        var inner = Panel.InnerWidth(width);
        var rows = new List<CreatePaneRow>();
        var caretRow = -1;

        if (index >= 0)
        {
            var field = _form.Fields[index];
            var editing = index == _form.Focus && !_form.Navigating && !_form.IsSaving;
            var look = new FieldLook(inner)
            {
                Editing = editing,
                Focused = index == _form.Focus,
                Expanded = true,
                Placeholder = DescriptionPlaceholder,
            };

            caretRow = editing ? TextBuffer.FromBack(field.Value.Text, field.Back).Line : -1;
            rows.AddRange(FieldRows.Value(field, look).Select(line => new CreatePaneRow(line, MouseTarget.FormField, index)));

            if (field.Error is { } error)
            {
                rows.AddRange(ErrorRows(error, index, inner));
            }
        }

        return new CreatePaneSpec(
            [new StyledSpan(DescriptionTitle, TextRole.Heading)],
            [new StyledSpan(DescriptionHint, TextRole.Muted)],
            rows,
            focused)
        {
            FocusStart = Math.Max(0, caretRow),
            FocusLength = 1,
            FillIndex = index,
        };
    }

    private CreatePaneSpec NotEditablePane(int width)
    {
        var inner = Panel.InnerWidth(width);
        var rows = new List<CreatePaneRow>
        {
            new(StyledLine.Of(DisplayText.Cut(NotEditableText, inner), TextRole.Muted)),
        };
        var names = _form.Fields.Where(static field => !field.IsEditable).Select(FormFieldKinds.Named).ToList();

        if (names.Count > 0)
        {
            var text = DisplayText.Cut(string.Join(Separator, names), inner);

            rows.Add(new CreatePaneRow(StyledLine.Of(text, TextRole.Muted)));
        }

        return new CreatePaneSpec([new StyledSpan(NotEditableTitle, TextRole.Heading)], null, rows, false);
    }
}
