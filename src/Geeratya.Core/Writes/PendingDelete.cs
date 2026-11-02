using Geeratya.Core.Views;

namespace Geeratya.Core.Writes;

/// <summary>
/// The delete key was pressed and the sub-tasks are being counted before the confirm opens. Kept in
/// <c>SiteSession</c>, so a profile change drops it.
/// </summary>
/// <param name="RequestId">The only reply that is accepted.</param>
/// <param name="Tab">The tab the key was pressed in; the write belongs to it.</param>
/// <param name="Key">The issue to delete.</param>
/// <param name="Summary">Its title.</param>
public sealed record PendingDelete(long RequestId, TabId Tab, string Key, string Summary);
