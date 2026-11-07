namespace Geeratya.Platform.Editors;

/// <summary>Edits a text in the user's own editor; the caller has already released the terminal.</summary>
public interface IExternalEditor
{
    /// <summary>
    /// Runs the editor on a private temporary file holding <paramref name="text"/>, waits for it and returns the
    /// outcome. Never throws for an editor problem (not found, non-zero exit, unreadable file): that is a failed
    /// result and the caller keeps its text.
    /// </summary>
    Task<ExternalEditResult> EditAsync(string text, CancellationToken cancellationToken);
}
