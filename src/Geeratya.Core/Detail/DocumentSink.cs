using Geeratya.Core.Adf;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// Collects the lines of a detail document together with what each line opens and where each panel starts, so the
/// builders cannot let the three lists drift apart.
/// </summary>
internal sealed class DocumentSink
{
    private const string FoldedText = " (folded)";

    private readonly List<DetailTarget?> _targets = [];
    private readonly IReadOnlyList<string> _folded;
    private readonly List<string> _ids = [];
    private readonly List<int> _sections = [];
    private readonly List<int> _edges = [];
    private readonly List<IReadOnlyList<StyledSpan>> _titles = [];
    private readonly List<IReadOnlyList<StyledSpan>> _rightTitles = [];
    private readonly List<LineBlock> _blocks = [];
    private readonly HashSet<int> _skipped = [];

    public DocumentSink(IReadOnlyList<string> folded)
    {
        _folded = folded;
    }

    public List<StyledLine> Lines { get; } = [];

    /// <summary>Appends a panel; <paramref name="content"/> carries the lines and the target of each.</summary>
    public void AddPanel(IReadOnlyList<StyledSpan> title, PanelContent content, int width, IReadOnlyList<StyledSpan>? right = null)
    {
        var start = Lines.Count;
        var isFirst = _sections.Count == 0;
        var id = Panel.IdOf(title);

        if (!isFirst && _folded.Contains(id))
        {
            title = [.. title, new StyledSpan(FoldedText, TextRole.Muted)];
            content = Preview(content, Panel.InnerWidth(width));
        }

        _ids.Add(id);
        Panel.Add(Lines, title, content.Lines, width, isFirst);

        // The first framed panel has no title line (the frame's top edge carries it); every other panel starts with
        // its title line (the shared edge when framed), so its content starts one line later.
        var first = width >= Panel.MinFramedWidth && isFirst ? start : start + 1;

        _sections.Add(first);
        _titles.Add(title);
        _rightTitles.Add(right ?? []);

        if (first > start)
        {
            _edges.Add(start);
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
            PaneTitles = _titles,
            PaneRightTitles = _rightTitles,
            SectionIds = _ids,
            Blocks = _blocks,
        };
    }

    /// <summary>A folded section: its first line and how many more lines it hides.</summary>
    private static PanelContent Preview(PanelContent content, int inner)
    {
        var preview = new PanelContent();

        if (content.Lines.Count == 0)
        {
            return preview;
        }

        preview.Add(content.Lines[0]);

        if (content.Lines.Count > 1)
        {
            var note = new WrappedText(inner, false);

            note.Append($"… {content.Lines.Count - 1} more lines hidden (z a)", TextRole.Muted);
            preview.Finish(note);
        }

        return preview;
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
