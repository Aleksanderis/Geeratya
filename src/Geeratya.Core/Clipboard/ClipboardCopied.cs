using Geeratya.Core.State;

namespace Geeratya.Core.Clipboard;

/// <summary>The host tried to copy text: <paramref name="Copied"/> says whether a clipboard took it.</summary>
/// <param name="Description">What was copied, as in <see cref="CopyToClipboardEffect.Description"/>.</param>
/// <param name="Copied">True when a clipboard accepted the text.</param>
/// <param name="Message">How it was copied (the tool), or why it could not be.</param>
public sealed record ClipboardCopied(string Description, bool Copied, string Message) : Command;
