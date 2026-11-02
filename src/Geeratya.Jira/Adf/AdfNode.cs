namespace Geeratya.Jira.Adf;

/// <summary>
/// A node of an ADF tree. Media nodes are kept as nodes and never resolved to attachments. Records with child lists
/// compare the lists by reference, so tests assert through a tree walk, not on whole-tree equality.
/// </summary>
public abstract record AdfNode;

/// <summary>A node that holds child nodes.</summary>
public abstract record AdfContainerNode(IReadOnlyList<AdfNode> Children) : AdfNode;

public sealed record AdfParagraph(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfHeading(int Level, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfBulletList(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary><see cref="Order" /> is the number of the first item (<c>attrs.order</c>, 1 when absent).</summary>
public sealed record AdfOrderedList(int Order, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfListItem(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfTaskList(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfTaskItem(AdfTaskState State, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfCodeBlock(string? Language, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfBlockquote(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary><see cref="PanelType" /> is <c>info</c>, <c>note</c>, <c>warning</c> and so on, as sent.</summary>
public sealed record AdfPanel(string PanelType, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfRule : AdfNode;

public sealed record AdfTable(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfTableRow(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary>
/// A header cell. <see cref="RowSpan" /> and <see cref="ColumnWidths" /> are optional attributes: a span below 1 or
/// missing is 1, and the widths (<c>attrs.colwidth</c>, pixels in the web editor, one per spanned column) are empty
/// when absent. Lists compare by reference like the children do.
/// </summary>
public sealed record AdfTableHeader(int ColumnSpan, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children)
{
    public int RowSpan { get; init; } = 1;

    public IReadOnlyList<int> ColumnWidths { get; init; } = [];
}

/// <summary>A data cell; see <see cref="AdfTableHeader" /> for <see cref="RowSpan" /> and <see cref="ColumnWidths" />.</summary>
public sealed record AdfTableCell(int ColumnSpan, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children)
{
    public int RowSpan { get; init; } = 1;

    public IReadOnlyList<int> ColumnWidths { get; init; } = [];
}

public sealed record AdfExpand(string? Title, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary>An expand inside a table cell or another expand (<c>nestedExpand</c>); same attributes as <see cref="AdfExpand" />.</summary>
public sealed record AdfNestedExpand(string? Title, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary>A decision list; its children are <see cref="AdfDecisionItem" />.</summary>
public sealed record AdfDecisionList(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary><see cref="State" /> is <c>DECIDED</c> as sent, null when absent.</summary>
public sealed record AdfDecisionItem(string? State, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary>The caption under a <c>mediaSingle</c>; its children are inline text.</summary>
public sealed record AdfCaption(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfMediaSingle(string? Layout, IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

public sealed record AdfMediaGroup(IReadOnlyList<AdfNode> Children) : AdfContainerNode(Children);

/// <summary>
/// A block media reference. The id is a media-service id that does not match the attachment id, so it cannot be
/// resolved to an attachment (<c>docs/jira-layer.md</c>).
/// </summary>
public record AdfMedia(
    string? Id,
    string? MediaType,
    string? Collection,
    string? Alt,
    int? Width,
    int? Height) : AdfNode;

/// <summary>
/// A media reference inside a line of text (<c>mediaInline</c>). It is an <see cref="AdfMedia" />, so consumers that
/// show media show it as media; check the type to tell it from a block one.
/// </summary>
public sealed record AdfMediaInline(
    string? Id,
    string? MediaType,
    string? Collection,
    string? Alt,
    int? Width,
    int? Height) : AdfMedia(Id, MediaType, Collection, Alt, Width, Height);

public sealed record AdfText(string Text, IReadOnlyList<AdfMark> Marks) : AdfNode;

public sealed record AdfHardBreak : AdfNode;

public sealed record AdfInlineCard(string? Url) : AdfNode;

/// <summary>An emoji: the short name (<c>:rocket:</c>) and the glyph in <see cref="Text" /> when sent.</summary>
public sealed record AdfEmoji(string? ShortName, string? Text) : AdfNode;

/// <summary>
/// <see cref="TimestampMilliseconds" /> is epoch milliseconds (sent as a string), null if unreadable.
/// </summary>
public sealed record AdfDate(long? TimestampMilliseconds) : AdfNode;

public sealed record AdfStatus(string Text, string? Color) : AdfNode;

/// <summary><see cref="Id" /> is an account id, <see cref="Text" /> the visible <c>@Name</c>.</summary>
public sealed record AdfMention(string? Id, string? Text) : AdfNode;

/// <summary>
/// A node this model does not know; its type name, own text (<c>text</c>, else <c>attrs.text</c>) and children are
/// kept, other attributes are dropped.
/// </summary>
public sealed record AdfUnknownNode(string TypeName, string? Text, IReadOnlyList<AdfNode> Children)
    : AdfContainerNode(Children);
