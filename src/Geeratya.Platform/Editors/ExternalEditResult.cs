namespace Geeratya.Platform.Editors;

/// <summary>
/// The outcome of an outside edit: <see cref="Text"/> is the edited text, or null when nothing changed or the editor
/// failed; <see cref="Error"/> says why it failed, null otherwise.
/// </summary>
public sealed record ExternalEditResult(string? Text, string? Error)
{
    /// <summary>The editor ended and the text is the same.</summary>
    public static ExternalEditResult Unchanged { get; } = new(null, null);

    public static ExternalEditResult Edited(string text)
    {
        return new ExternalEditResult(text, null);
    }

    public static ExternalEditResult Failed(string error)
    {
        return new ExternalEditResult(null, error);
    }
}
