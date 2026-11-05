using Geeratya.Core.Adf;
using Geeratya.Core.Forms;
using Geeratya.Core.Panes;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Detail;

/// <summary>
/// Lays an opened issue out as styled lines for a width: header, description, comments (<c>docs/ux-screens.md</c>,
/// "Issue detail"). Pure: the same state and width give the same lines, and relative times are taken against
/// <see cref="DetailState.LoadedAt"/> rather than a clock. Only the screen's window of the lines is drawn, so the
/// result is built once per change of data or width, not per frame.
/// </summary>
public static class IssueDocumentBuilder
{
    private const string DescriptionTitle = "Description";
    private const int StatusGap = 2;
    private const string CommentsTitle = "Comments";
    private const string NewestFirstText = "newest first";
    private const int MinCommentRows = 6;
    private const string ErrorTitle = "Error";
    private const string LoadingText = "Loading…";
    private const string NoDescriptionText = "No description.";
    private const string BackText = "Esc to go back.";
    private const string ChipSeparator = " · ";
    private const string EditInBrowserText = "(edit in browser)";

    /// <summary>The not-found text, shared with the status and the tests.</summary>
    public static string NotFoundText(string key)
    {
        return $"Not found or no access: {key}";
    }

    /// <param name="detail">The issue to lay out.</param>
    /// <param name="width">The columns of the document.</param>
    /// <param name="viewportRows">
    /// The rows the screen shows; the Comments pane takes what the panes above leave and scrolls inside it. Null lays
    /// every comment out.
    /// </param>
    /// <param name="commentRows">
    /// The rows of the Comments pane when the caller has just built them for this <paramref name="detail"/> and
    /// <paramref name="width"/> (<see cref="DetailZoom.CommentRowsOf"/>), so a key builds them once. Null builds them.
    /// </param>
    /// <param name="form">
    /// The open form of the tab. When it edits the title or the Description of this issue in place, that pane is laid
    /// out as its editor (the caret, the notices) instead of its content.
    /// </param>
    public static DetailDocument Build(
        DetailState detail,
        int width,
        int? viewportRows = null,
        IReadOnlyList<CommentRow>? commentRows = null,
        FormState? form = null)
    {
        var edit = PaneEdit.Of(form, detail.Key);
        var total = Math.Max(1, width);
        var zoomed = detail.Zoomed is not null;
        var sidebar = DetailLayout.HasSidebar(total) && !zoomed;
        var columns = sidebar ? DetailLayout.MainWidth(total) : total;
        var inner = Panel.InnerWidth(columns);
        var sink = new DocumentSink { Only = detail.Zoomed };

        // An editor in a zoomed pane sizes itself to the pane; the scroll window of #740 is for read-only text.
        if (detail.Zoomed is not (null or PaneIds.Comments) && edit is null && viewportRows is { } shown)
        {
            sink.Window = new PaneWindow(detail.ZoomOffset, Math.Max(1, shown - Panel.EdgeRows));
        }

        AddTitle(sink, detail, columns, inner, edit);

        if (detail.Issue is null && detail.IssueLoad is Failed failed)
        {
            sink.Only = null;

            var error = new PanelContent();

            error.Add(StyledLine.Of(FailureText(detail.Key, failed), TextRole.Error));
            error.Add(StyledLine.Of(BackText, TextRole.Muted));
            sink.AddPanel(PaneIds.Error, [new StyledSpan(ErrorTitle, TextRole.Error)], error, columns);

            return sink.ToDocument(total);
        }

        AddDescription(sink, detail, columns, inner, edit, viewportRows);
        IssueRelationsBuilder.Add(sink, detail, columns, inner);
        var commentIds = AddComments(sink, detail, columns, inner, viewportRows, commentRows);

        if (!sidebar)
        {
            DetailsPane.Add(sink, detail, columns, inner);

            // A zoomed pane that is gone (its data went away) shows the normal layout again.
            if (zoomed && sink.Lines.Count == 0)
            {
                return Build(detail with { Zoomed = null }, width, viewportRows, null, form);
            }

            return sink.ToDocument(total) with { ViewportRows = viewportRows, CommentRowIds = commentIds };
        }

        var side = BuildSide(detail, total);

        return sink.ToDocument(total) with
        {
            Side = side.Lines.Count == 0 ? null : side,
            ViewportRows = viewportRows,
            CommentRowIds = commentIds,
        };
    }

    /// <summary>The Details sidebar: a document of its own, laid out for the sidebar width.</summary>
    private static DetailDocument BuildSide(DetailState detail, int total)
    {
        var side = new DocumentSink();
        var sidebarWidth = DetailLayout.SidebarWidthFor(total);

        DetailsPane.Add(side, detail, sidebarWidth, Panel.InnerWidth(sidebarWidth));

        return side.ToDocument(sidebarWidth);
    }

    private static string FailureText(string key, Failed failed)
    {
        return failed.Kind is JiraErrorKind.NotFound or JiraErrorKind.Forbidden ? NotFoundText(key) : failed.Message;
    }

    /// <summary>
    /// The title pane: the key and type in the top edge, the summary on the left of one row and the status in its
    /// category colour on the right. Two rows only when the summary does not fit beside the status.
    /// </summary>
    private static void AddTitle(DocumentSink sink, DetailState detail, int width, int inner, PaneEdit? edit)
    {
        var header = HeaderFields.Of(detail);
        var content = new PanelContent();
        var rows = SummaryRows(header, inner);

        if (edit is { Pane: PaneIds.Title })
        {
            content.AddRange(edit.TitleRows(inner));
        }
        else if (header.Known && rows.Count > 0)
        {
            content.AddBlock(rows, new DetailTarget(DetailTargetKind.Field, DetailFieldIds.Summary));
        }
        else
        {
            content.AddRange(rows);
        }

        var title = new List<StyledSpan> { new(Clean(detail.Key), TextRole.Key) };

        if (header.TypeName.Length > 0)
        {
            title.Add(new StyledSpan($" · {Clean(header.TypeName)}", TextRole.Muted));
        }

        var right = edit is { Pane: PaneIds.Title } ? PaneEdit.Chip() : null;

        sink.AddPanel(PaneIds.Title, title, content, width, right);
    }

    private static List<StyledLine> SummaryRows(HeaderFields header, int inner)
    {
        var rows = new List<StyledLine>();

        if (header.Status is not { Name.Length: > 0 } current)
        {
            return Wrapped(header.Summary, inner);
        }

        var status = new StyledSpan(DisplayText.Cut(Clean(current.Name), inner), StatusRoles.Of(current));
        var statusWidth = DisplayWidth.Of(status.Text) + StatusGap;
        var wrapped = new WrappedText(Math.Max(1, inner - statusWidth), false);

        wrapped.Append(header.Summary, TextRole.Strong);
        wrapped.Finish(rows);

        if (rows.Count > 1)
        {
            rows = Wrapped(header.Summary, inner);
        }

        if (rows.Count == 0)
        {
            rows.Add(StyledLine.Empty);
        }

        var last = rows[^1];
        var room = inner - DisplayWidth.Of(last.PlainText) - DisplayWidth.Of(status.Text);

        if (room < StatusGap && last.Spans.Count > 0)
        {
            rows.Add(StyledLine.Empty);
            last = rows[^1];
            room = inner - DisplayWidth.Of(status.Text);
        }

        var gap = new StyledSpan(new string(' ', Math.Max(0, room)), TextRole.Body);

        rows[^1] = new StyledLine([.. last.Spans, gap, status]);

        return rows;
    }

    private static List<StyledLine> Wrapped(string summary, int inner)
    {
        var rows = new List<StyledLine>();
        var wrapped = new WrappedText(inner, false);

        wrapped.Append(summary, TextRole.Strong);
        wrapped.Finish(rows);

        return rows;
    }

    private static void AddDescription(
        DocumentSink sink,
        DetailState detail,
        int width,
        int inner,
        PaneEdit? edit,
        int? viewportRows)
    {
        if (edit is { Pane: PaneIds.Description })
        {
            AddDescriptionEditor(sink, detail, width, inner, edit, viewportRows);

            return;
        }

        var content = new List<StyledLine>();

        if (detail.Issue is null)
        {
            content.Add(StyledLine.Of(LoadingText, TextRole.Loading));
        }
        else if (detail.Issue.Description is null)
        {
            content.Add(StyledLine.Of(NoDescriptionText, TextRole.Muted));
        }
        else
        {
            content.AddRange(AdfRenderer.Render(detail.Issue.Description, inner).Lines);
        }

        var panel = new PanelContent();
        var target = detail.Issue is null ? null : new DetailTarget(DetailTargetKind.Field, DetailFieldIds.Description);
        var expanded = detail.Expanded.Contains(DetailFieldIds.Description);
        var rows = detail.Zoomed == PaneIds.Description ? int.MaxValue : LongText.DescriptionRows;
        var cut = target is not null && !expanded && content.Count > rows;

        var textRows = LongText.DescriptionRows - 1;
        var scroll = new PaneScroll(detail.DescriptionOffset, content.Count, textRows);

        LongText.Add(panel, content, target, expanded, new LongTextLimit(rows, inner, true, First: scroll.ClampedOffset, KeyInFrame: true));

        var title = new StyledSpan(DescriptionTitle, TextRole.Heading);

        var browser = BrowserNote(detail);

        var toggleHint = ToggleHint(expanded ? "collapse" : "expand");

        if (!cut)
        {
            var spans = target is not null && content.Count > LongText.DescriptionRows && detail.Zoomed is null
                ? toggleHint
                : null;

            sink.AddPanel(PaneIds.Description, [title], panel, width, Joined(spans, browser));

            return;
        }

        var chip = new StyledSpan(scroll.Chip(), TextRole.Muted);

        sink.AddPanel(
            PaneIds.Description,
            [title],
            panel,
            width,
            Joined([chip, new StyledSpan(ChipSeparator, TextRole.Muted), .. toggleHint], browser),
            scroll);
    }

    // The key that expands or cuts the long Description, shown in the frame instead of a row of the text.
    private static StyledSpan[] ToggleHint(string action)
    {
        return [new StyledSpan("za", TextRole.Key), new StyledSpan($" {action}", TextRole.Muted)];
    }

    private static StyledSpan[]? Joined(StyledSpan[]? first, StyledSpan[]? browser)
    {
        if (first is null || browser is null)
        {
            return first ?? browser;
        }

        return [.. first, new StyledSpan(ChipSeparator, TextRole.Muted), .. browser];
    }

    // The note of a description that cannot be edited here, null when it can.
    private static StyledSpan[]? BrowserNote(DetailState detail)
    {
        if (detail.Issue?.Description is not { } document || DescriptionEditability.CanEdit(document))
        {
            return null;
        }

        return [new StyledSpan(EditInBrowserText, TextRole.Muted)];
    }

    // The Description pane as its editor: a window of the text that keeps the caret in view, the whole pane height
    // when zoomed.
    private static void AddDescriptionEditor(
        DocumentSink sink,
        DetailState detail,
        int width,
        int inner,
        PaneEdit edit,
        int? viewportRows)
    {
        var zoomed = detail.Zoomed == PaneIds.Description;
        var rows = PaneEdit.EditorRows;

        if (viewportRows is { } shown)
        {
            // Zoomed, the editor takes the pane's height; unzoomed, at most the screen minus the frame.
            rows = zoomed ? Math.Max(1, shown - Panel.EdgeRows) : Math.Clamp(shown - Panel.EdgeRows, 1, rows);
        }

        var content = new PanelContent();

        content.AddRange(edit.DescriptionRows(inner, rows, out var scroll));

        var title = new StyledSpan(DescriptionTitle, TextRole.Heading);
        var chip = scroll is null ? PaneEdit.Chip() : [new StyledSpan(scroll.Chip(), TextRole.Muted)];

        sink.AddPanel(PaneIds.Description, [title], content, width, chip, scroll);
    }

    private static List<string?> AddComments(
        DocumentSink sink,
        DetailState detail,
        int width,
        int inner,
        int? viewport,
        IReadOnlyList<CommentRow>? known)
    {
        var rows = known ?? CommentRows.Build(detail, inner, DetailZoom.BodyLines(detail));
        var ids = rows.Select(row => row.Target is { Kind: DetailTargetKind.Comment } target ? target.Value : null).ToList();
        var content = new PanelContent();
        var heading = new StyledSpan(CommentsHeading(detail), TextRole.Heading);
        var order = new StyledSpan(NewestFirstText, TextRole.Muted);
        var rowsLeft = viewport is { } shown ? shown - sink.Lines.Count - Panel.EdgeRows : rows.Count;
        var height = Math.Max(MinCommentRows, rowsLeft);

        if (rows.Count <= height)
        {
            AddRows(content, rows, 0, rows.Count);
            sink.AddPanel(PaneIds.Comments, [heading], content, width, detail.Comments.Count > 0 ? [order] : null);

            return ids;
        }

        var scroll = new PaneScroll(detail.CommentsOffset, rows.Count, height);
        var first = scroll.ClampedOffset;

        AddRows(content, rows, first, height);
        sink.AddPanel(PaneIds.Comments, [heading], content, width, [order], scroll);

        return ids;
    }

    private static void AddRows(PanelContent content, IReadOnlyList<CommentRow> rows, int first, int count)
    {
        for (var index = first; index < first + count && index < rows.Count; index++)
        {
            var row = rows[index];

            if (!row.IsStop)
            {
                content.Skipped.Add(content.Lines.Count);
            }

            content.Add(row.Line, row.Target);
        }
    }

    private static string CommentsHeading(DetailState detail)
    {
        var loaded = detail.Comments.Count;

        if (loaded == 0)
        {
            return CommentsTitle;
        }

        return detail.CommentTotal is { } total && total > loaded
            ? $"{CommentsTitle} {loaded} of {total}"
            : $"{CommentsTitle} {loaded}";
    }

    private static string Clean(string text)
    {
        return TextClean.Of(text);
    }
}
