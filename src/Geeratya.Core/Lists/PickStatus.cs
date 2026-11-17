namespace Geeratya.Core.Lists;

/// <summary>
/// Where the rows of a pick list come from. The rows shown are always what is known so far; the status only says
/// whether more may still arrive. An empty list that is <see cref="Ready"/> reads "no match".
/// </summary>
public enum PickStatus
{
    /// <summary>Everything the list will show is there.</summary>
    Ready,

    /// <summary>The query changed and a lookup is about to run or runs quickly; nothing is said yet.</summary>
    Waiting,

    /// <summary>A lookup takes longer than a moment: the list says it is searching.</summary>
    Loading,

    /// <summary>The lookup failed; the list shows what is known and says why.</summary>
    Failed,
}
