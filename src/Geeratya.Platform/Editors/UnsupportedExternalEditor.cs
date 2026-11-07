namespace Geeratya.Platform.Editors;

/// <summary>The explicit path for a system that is not supported: always a failed result.</summary>
public sealed class UnsupportedExternalEditor : IExternalEditor
{
    public Task<ExternalEditResult> EditAsync(string text, CancellationToken cancellationToken)
    {
        return Task.FromResult(ExternalEditResult.Failed("an outside editor is not supported on this system"));
    }
}
