using Geeratya.Core.Views;

namespace Geeratya.Core.Writes;

/// <summary>
/// One user action that writes, from the moment it is sent until it is finished. Kept in
/// <c>SiteSession.Operations</c>, not in a tab: the same issue can be on screen in several tabs, and a tab closed
/// after the write was sent must not forget it.
/// </summary>
/// <param name="Id">Drawn from <c>AppState.Sequence</c>, never reused; also the request id of its replies.</param>
/// <param name="Request">What is being written.</param>
/// <param name="Origin">The tab that started it; it may be closed by now.</param>
/// <param name="Phase">Where the operation is.</param>
/// <param name="Undo">What the optimistic update changed; empty when nothing was patched.</param>
/// <param name="StartedAt">The time of the command that started it.</param>
public sealed record Operation(
    long Id,
    WriteRequest Request,
    TabId Origin,
    OperationPhase Phase,
    IReadOnlyList<RowChange> Undo,
    DateTimeOffset StartedAt)
{
    public OperationKind Kind => Request.Kind;

    public IReadOnlyList<string> Keys => Request.Keys;
}
