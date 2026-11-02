namespace Geeratya.FakeJira;

/// <summary>One add or remove of a label from an <c>update</c> edit.</summary>
public sealed record LabelOperation(bool IsAdd, string Label);
