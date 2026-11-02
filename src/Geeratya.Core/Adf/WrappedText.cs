using System.Globalization;
using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Core.Adf;

/// <summary>
/// Collects styled text and breaks it into lines of at most the given width. Prose mode wraps at spaces and
/// hard-wraps words longer than a line; preformatted mode keeps every space and only breaks at newlines and the
/// width. A character is never split: wide characters and grapheme clusters move as one cell, and a cell wider than
/// the width still gets a line of its own, so the layout always makes progress.
/// </summary>
internal sealed class WrappedText
{
    private const int LastAscii = 0x7F;
    private const int TabWidth = 4;

    private static readonly string[] _asciiStrings = CreateAsciiStrings();

    private readonly int _width;
    private readonly bool _preformatted;
    private readonly List<StyledLine> _lines = [];
    private readonly List<StyledSpan> _spans = [];
    private readonly StringBuilder _run = new();
    private readonly List<Cell> _word = [];

    private TextRole _runRole;
    private int _lineWidth;
    private bool _lineEmpty = true;
    private int _wordWidth;
    private bool _pendingSpace;
    private TextRole _spaceRole;

    public WrappedText(int width, bool preformatted)
    {
        _width = Math.Max(1, width);
        _preformatted = preformatted;
    }

    /// <summary>Adds text with one role. Whitespace breaks lines in prose and is kept in preformatted text.</summary>
    public void Append(string text, TextRole role)
    {
        var index = 0;

        while (index < text.Length)
        {
            var character = text[index];

            if (char.IsWhiteSpace(character))
            {
                index++;
                AppendWhitespace(character, role);
            }
            else if (char.IsControl(character))
            {
                index++;
            }
            else if (character <= LastAscii)
            {
                index++;
                AddCell(new Cell(_asciiStrings[character], 1, role));
            }
            else
            {
                var length = StringInfo.GetNextTextElementLength(text.AsSpan(index));
                var cluster = text.Substring(index, length);

                index += length;
                AddCell(new Cell(cluster, DisplayWidth.OfCluster(cluster), role));
            }
        }
    }

    /// <summary>Ends the current line, even when it is empty.</summary>
    public void LineBreak()
    {
        FlushWord();
        EndLine();
        _pendingSpace = false;
    }

    /// <summary>Writes the finished lines to <paramref name="output" />; a trailing line break adds no line.</summary>
    public void Finish(List<StyledLine> output)
    {
        FlushWord();

        if (!_lineEmpty)
        {
            EndLine();
        }

        output.AddRange(_lines);
        _lines.Clear();
    }

    private static string[] CreateAsciiStrings()
    {
        var strings = new string[LastAscii + 1];

        for (var code = 0; code < strings.Length; code++)
        {
            strings[code] = ((char)code).ToString();
        }

        return strings;
    }

    private void AppendWhitespace(char character, TextRole role)
    {
        if (!_preformatted)
        {
            FlushWord();
            _pendingSpace = true;
            _spaceRole = role;

            return;
        }

        switch (character)
        {
            case '\n':
                LineBreak();
                break;
            case '\r':
                break;
            case '\t':
                for (var count = 0; count < TabWidth; count++)
                {
                    PlaceCell(new Cell(" ", 1, role));
                }

                break;
            default:
                PlaceCell(new Cell(" ", 1, role));
                break;
        }
    }

    private void AddCell(Cell cell)
    {
        if (_preformatted)
        {
            PlaceCell(cell);

            return;
        }

        _word.Add(cell);
        _wordWidth += cell.Width;
    }

    private void FlushWord()
    {
        if (_word.Count == 0)
        {
            return;
        }

        var gap = _pendingSpace && !_lineEmpty ? 1 : 0;

        if (!_lineEmpty && _lineWidth + gap + _wordWidth > _width)
        {
            EndLine();
            gap = 0;
        }

        if (gap == 1)
        {
            PlaceCell(new Cell(" ", 1, _spaceRole));
        }

        for (var index = 0; index < _word.Count; index++)
        {
            PlaceCell(_word[index]);
        }

        _word.Clear();
        _wordWidth = 0;
        _pendingSpace = false;
    }

    private void PlaceCell(Cell cell)
    {
        if (!_lineEmpty && _lineWidth + cell.Width > _width)
        {
            EndLine();
        }

        if (_run.Length > 0 && _runRole != cell.Role)
        {
            FlushRun();
        }

        _runRole = cell.Role;
        _run.Append(cell.Text);
        _lineWidth += cell.Width;
        _lineEmpty = false;
    }

    private void FlushRun()
    {
        if (_run.Length == 0)
        {
            return;
        }

        _spans.Add(new StyledSpan(_run.ToString(), _runRole));
        _run.Clear();
    }

    private void EndLine()
    {
        FlushRun();
        _lines.Add(_spans.Count == 0 ? StyledLine.Empty : new StyledLine(_spans.ToArray()));
        _spans.Clear();
        _lineWidth = 0;
        _lineEmpty = true;
    }

    private readonly record struct Cell(string Text, int Width, TextRole Role);
}
