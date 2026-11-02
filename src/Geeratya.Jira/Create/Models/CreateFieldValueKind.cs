namespace Geeratya.Jira.Create.Models;

/// <summary>The JSON shape a <see cref="CreateFieldValue"/> is written as.</summary>
public enum CreateFieldValueKind
{
    Text,
    RichText,
    ChoiceId,
    ChoiceIds,
    Strings,
    AccountId,
    Number,
}
