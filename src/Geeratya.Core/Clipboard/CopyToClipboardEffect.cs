using Geeratya.Core.State;

namespace Geeratya.Core.Clipboard;

/// <summary>
/// Put text on the system clipboard. The host answers with <see cref="ClipboardCopied"/>. The text is user content
/// (an issue's Description, a comment); it is never logged.
/// </summary>
/// <param name="Text">The text to copy.</param>
/// <param name="Description">What it is, for the status line ("3 lines", "the Description").</param>
public sealed record CopyToClipboardEffect(string Text, string Description) : Effect;
