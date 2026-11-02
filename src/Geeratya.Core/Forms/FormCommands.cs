using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Forms;

/// <summary>A command handled by <see cref="FormReducer"/>.</summary>
public abstract record FormCommand : Command;

/// <summary>Open a form on a tab; its metadata is fetched and the form fills when the reply arrives.</summary>
public sealed record OpenFormRequested(TabId Tab, FormTarget Target) : FormCommand;

/// <summary>Open the labels pilot form on the issue the active tab shows or has selected (<c>:labels</c>).</summary>
public sealed record OpenLabelsFormRequested : FormCommand;

/// <summary>Open the link form on the issue the active tab shows or has selected (<c>issue.link</c>).</summary>
public sealed record OpenLinkFormRequested : FormCommand;

/// <summary>
/// The metadata arrived; applied only while the tab's form waits for that request id. <paramref name="Target"/>
/// replaces the form's target when the load refined it (the transitions Jira offers); <paramref name="At"/> is the
/// time of the reply, used when the target submits at once.
/// </summary>
public sealed record FormMetadataLoaded(
    TabId Tab,
    long RequestId,
    IReadOnlyList<FormField> Fields,
    FormTarget? Target = null,
    DateTimeOffset At = default) : FormCommand;

/// <summary>The metadata could not be fetched; the form shows the message and can only be closed.</summary>
public sealed record FormMetadataFailed(TabId Tab, long RequestId, string Message) : FormCommand;

/// <summary>
/// The people (or other options) found for the text typed in a searchable field; applied only while the form waits
/// for that search.
/// </summary>
public sealed record FieldOptionsLoaded(TabId Tab, long RequestId, IReadOnlyList<FieldOption> Options) : FormCommand;

/// <summary>The search failed; the message goes to the form's top line and the old options stay.</summary>
public sealed record FieldOptionsFailed(TabId Tab, long RequestId, string Message) : FormCommand;
