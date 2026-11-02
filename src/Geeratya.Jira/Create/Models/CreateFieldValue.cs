using System.Globalization;

namespace Geeratya.Jira.Create.Models;

/// <summary>
/// A typed value for one create field. Built with the factories and written by the create client in the shape the
/// field's kind needs, so callers never assemble JSON.
/// </summary>
public sealed record CreateFieldValue
{
    private CreateFieldValue(CreateFieldValueKind kind, IReadOnlyList<string> values)
    {
        Kind = kind;
        Values = values;
    }

    public CreateFieldValueKind Kind { get; }

    /// <summary>The text, ids or labels; exactly one entry for the single-value kinds.</summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>A string; also dates (<c>2026-11-30</c>) and date-times in the format Jira shows.</summary>
    public static CreateFieldValue Text(string text) => new(CreateFieldValueKind.Text, [text]);

    /// <summary>Plain text written as an ADF document, one paragraph per line.</summary>
    public static CreateFieldValue RichText(string text) => new(CreateFieldValueKind.RichText, [text]);

    /// <summary>One allowed value, by id.</summary>
    public static CreateFieldValue Choice(string id) => new(CreateFieldValueKind.ChoiceId, [id]);

    public static CreateFieldValue Choices(IEnumerable<string> ids) => new(CreateFieldValueKind.ChoiceIds, [.. ids]);

    public static CreateFieldValue Labels(IEnumerable<string> labels) => new(CreateFieldValueKind.Strings, [.. labels]);

    public static CreateFieldValue User(string accountId) => new(CreateFieldValueKind.AccountId, [accountId]);

    public static CreateFieldValue Number(decimal number) =>
        new(CreateFieldValueKind.Number, [number.ToString(CultureInfo.InvariantCulture)]);
}
