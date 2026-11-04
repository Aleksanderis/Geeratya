using Geeratya.Core.Adf;
using Geeratya.Core.Panes;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// Collects the lines of a detail document together with what each line opens and where each panel starts, so the
/// builders cannot let the three lists drift apart.
/// </summary>
internal sealed class DocumentSink
{
    private const string ZoomedText = " · zoomed";

    private readonly List<DetailTarget?> _targets = [];
    private readonly List<string> _ids = [];
    private readonly List<int> _sections = [];
    private readonly List<int> _starts = [];
    private readonly List<int> _edges = [];
    private readonly List<IReadOnlyList<StyledSpan>> _titles = [];
    private readonly List<IReadOnlyList<StyledSpan>> _rightTitles = [];
    private readonly List<PaneScroll?> _scrolls = [];
    private readonly List<LineBlock> _blocks = [];
    private readonly HashSet<int> _skipped = [];

    public List<StyledLine> Lines { get; } = [];

    /// <summary>
    /// When set, only the panel with this id is laid out (the zoomed section); the others are only recorded in
    /// <see cref="Strip"/>.
    /// </summary>
    public string? Only { get; set; }

    /// <summary>
    /// The inner scroll of the zoomed pane: when set and the zoomed pane's content is longer than
    /// <see cref="PaneWindow.Rows"/>, only that window of it is laid out, with a scrollbar and a position chip (#740).
    /// Null lays the pane out in full.
    /// </summary>
    public PaneWindow? Window { get; set; }

    /// <summary>The panels offered to the section strip, in order, with the title text each has.</summary>
    public List<StripSection> Strip { get; } = [];

    /// <summary>Appends a pane; <paramref name="content"/> carries the lines and the target of each.</summary>
    /// <param name="id">The pane id (a <see cref="PaneIds"/> member).</param>
    /// <param name="title">The title chip.</param>
    /// <param name="content">The body lines.</param>
    /// <param name="width">The pane width.</param>
    /// <param name="right">The dim chip at the right of the top edge, null for none.</param>
    /// <param name="scroll">The inner scroll shown as a scrollbar, null for a pane that fits.</param>
    public void AddPanel(
        string id,
        IReadOnlyList<StyledSpan> title,
        PanelContent content,
        int width,
        IReadOnlyList<StyledSpan>? right = null,
        PaneScroll? scroll = null)
    {
        if (id is not (PaneIds.Title or PaneIds.Error))
        {
            Strip.Add(new StripSection(id, string.Concat(title.Select(static span => span.Text))));
        }

        if (Only is not null && id != Only)
        {
            return;
        }

        if (Only is not null)
        {
            title = [.. title, new StyledSpan(ZoomedText, TextRole.Muted)];
        }

        var start = Lines.Count;
        var framed = width >= Panel.MinFramedWidth;

        if (Only is not null && Window is { } window && scroll is null && framed && content.Lines.Count > window.Rows)
        {
            scroll = new PaneScroll(window.Offset, content.Lines.Count, window.Rows);
            content = content.Slice(scroll.ClampedOffset, window.Rows);
            right = [new StyledSpan(scroll.Chip(), TextRole.Muted)];
        }

        _ids.Add(id);
        Panel.Add(Lines, title, content.Lines, width, right);

        // The title line (the top edge when framed) comes first, so the content starts one line later.
        var first = start + 1;

        _starts.Add(start);
        _sections.Add(first);
        _titles.Add(title);
        _rightTitles.Add(right ?? []);
        _scrolls.Add(framed ? scroll : null);
        _edges.Add(start);

        if (framed)
        {
            _edges.Add(Lines.Count - 1);
        }

        Pad();

        for (var index = 0; index < content.Targets.Count; index++)
        {
            _targets[first + index] = content.Targets[index];
        }

        _blocks.AddRange(content.Blocks.Select(block => new LineBlock(first + block.Start, first + block.End)));
        _skipped.UnionWith(content.Skipped.Select(line => first + line));
    }

    public DetailDocument ToDocument(int width)
    {
        Pad();

        var blocked = new bool[Lines.Count];

        foreach (var block in _blocks)
        {
            for (var line = block.Start + 1; line < block.End; line++)
            {
                blocked[line] = true;
            }
        }

        var edges = _edges.ToHashSet();
        var stops = Enumerable.Range(0, Lines.Count)
            .Where(line => !edges.Contains(line) && !_skipped.Contains(line) && !blocked[line])
            .ToArray();

        return new DetailDocument(width, Lines, _targets, _sections, stops)
        {
            PaneStarts = _starts,
            PaneTitles = _titles,
            PaneRightTitles = _rightTitles,
            PaneScrolls = _scrolls,
            SectionIds = _ids,
            Blocks = _blocks,
            Strip = Only is null ? [] : Strip,
        };
    }

    private void Pad()
    {
        while (_targets.Count < Lines.Count)
        {
            _targets.Add(null);
        }
    }
}

/// <summary>The content lines of one panel, each with the target Enter opens on it.</summary>
internal sealed class PanelContent
{
    public List<StyledLine> Lines { get; } = [];

    public List<DetailTarget?> Targets { get; } = [];

    /// <summary>Line ranges (content line numbers) that are one cursor stop each, such as a wrapped field.</summary>
    public List<LineBlock> Blocks { get; } = [];

    /// <summary>Content lines the cursor never rests on (the gap between fields).</summary>
    public HashSet<int> Skipped { get; } = [];

    /// <summary>Appends the lines as one cursor stop that opens <paramref name="target"/>.</summary>
    public void AddBlock(IReadOnlyList<StyledLine> lines, DetailTarget target)
    {
        var start = Lines.Count;

        foreach (var line in lines)
        {
            Add(line, target);
        }

        Blocks.Add(new LineBlock(start, Lines.Count));
    }

    /// <summary>
    /// The <paramref name="count"/> content lines from <paramref name="first"/>, with their targets; a block or a
    /// skipped line is cut to the window.
    /// </summary>
    public PanelContent Slice(int first, int count)
    {
        var end = Math.Min(Lines.Count, first + count);
        var window = new PanelContent();

        for (var line = first; line < end; line++)
        {
            window.Add(Lines[line], Targets[line]);

            if (Skipped.Contains(line))
            {
                window.Skipped.Add(line - first);
            }
        }

        foreach (var block in Blocks)
        {
            var start = Math.Max(block.Start, first);
            var stop = Math.Min(block.End, end);

            if (start < stop)
            {
                window.Blocks.Add(new LineBlock(start - first, stop - first));
            }
        }

        return window;
    }

    /// <summary>Appends a line the cursor skips.</summary>
    public void AddGap(StyledLine line)
    {
        Skipped.Add(Lines.Count);
        Add(line);
    }

    public void Add(StyledLine line, DetailTarget? target = null)
    {
        Lines.Add(line);
        Targets.Add(target);
    }

    public void AddRange(IEnumerable<StyledLine> lines)
    {
        foreach (var line in lines)
        {
            Add(line);
        }
    }

    /// <summary>Finishes the wrapped text; every line it takes opens <paramref name="target"/>.</summary>
    public void Finish(WrappedText text, DetailTarget? target = null)
    {
        var rows = new List<StyledLine>();

        text.Finish(rows);

        foreach (var row in rows)
        {
            Add(row, target);
        }
    }
}
