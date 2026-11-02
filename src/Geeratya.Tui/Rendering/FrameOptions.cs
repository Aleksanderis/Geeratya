using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>Reads the <c>--print-frame</c> options and builds the sample state a snapshot shows.</summary>
public static class FrameOptions
{
    private const char SizeSeparator = 'x';
    private const int DefaultWidth = 80;
    private const int DefaultHeight = 24;
    private const int MaximumSize = 1000;

    /// <summary>Parses <c>WIDTHxHEIGHT</c>; null input gives 80x24, invalid input gives null.</summary>
    public static FrameSize? ParseSize(string? text)
    {
        if (text is null)
        {
            return new FrameSize(DefaultWidth, DefaultHeight);
        }

        var parts = text.Split(SizeSeparator);

        if (parts.Length != 2
            || !int.TryParse(parts[0], out var width)
            || !int.TryParse(parts[1], out var height)
            || width < 0
            || height < 0
            || width > MaximumSize
            || height > MaximumSize)
        {
            return null;
        }

        return new FrameSize(width, height);
    }

    /// <summary>Parses a mode name; null input gives Normal, an unknown name gives null.</summary>
    public static InputMode? ParseMode(string? text)
    {
        return text switch
        {
            null or "normal" => InputMode.Normal,
            "help" => InputMode.Help,
            "command" => InputMode.Command,
            "confirm" => InputMode.Confirm,
            "insert" => InputMode.Insert,
            _ => null,
        };
    }

    /// <summary>Parses the first visible help line; null input gives 0, a negative or invalid number gives null.</summary>
    public static int? ParseScroll(string? text)
    {
        if (text is null)
        {
            return 0;
        }

        return int.TryParse(text, out var value) && value >= 0 ? value : null;
    }

    public static AppState SampleState(InputMode mode)
    {
        return mode switch
        {
            InputMode.Command => AppState.Initial with { Mode = mode, PaletteText = "search" },
            InputMode.Confirm => AppState.Initial with
            {
                Mode = mode,
                Confirm = new ConfirmRequest("Delete PROJ-1? Jira cannot undo this", "issue.delete"),
            },
            InputMode.Insert => AppState.Initial with
            {
                Mode = mode,
                Form = ProfileForm.Start with
                {
                    Field = ProfileFormField.Token,
                    Token = new SecretText("sample-token"),
                },
            },
            _ => AppState.Initial with { Mode = mode },
        };
    }
}
