using Geeratya.Core.Adf;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// Collects the lines of a detail document together with what each line opens and where each panel starts, so the
/// builders cannot let the three lists drift apart.
/// </summary>
internal sealed class DocumentSink
{
    private readonly List<DetailTarget?> _targets = [];
    private readonly List<int> _sections = [];
    private readonly List<int> _edges = [];
    private readonly List<IReadOnlyList<StyledSpan>> _titles = [];
    private readonly List<IReadOnlyList<StyledSpan>> _rightTitles = [];
    private readonly List<LineBlock> _blocks = [];
    private readonly HashSet<int> _skipped = [];

    public List<StyledLine> Lines { get; } = [];

    /// <summary>Appends a panel; <paramref name="content"/> carries the lines and the target of each.</summary>
    public void AddPanel(IReadOnlyList<StyledSpan> title, PanelContent content, int width, IReadOnlyList<StyledSpan>? right = null)
    {
        var start = Lines.Count;
        var isFirst = _sections.Count == 0;

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

        var stops = Enumerable.Range(0, Lines.Count).Where(line => !_edges.Contains(line) && !_skipped.Contains(line) && !InsideBlock(line)).ToArray();

        return new DetailDocument(width, Lines, _targets, _sections, stops) { PaneTitles = _titles, PaneRightTitles = _rightTitles, Blocks = _blocks };
    }

    private bool InsideBlock(int line)
    {
        return _blocks.Any(block => line > block.Start && line < block.End);
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
