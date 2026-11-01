using System.Globalization;

namespace Geeratya.Core.Profiles;

/// <summary>
/// Text that must never be shown or logged, such as an API token being typed. <see cref="ToString"/> and the
/// record-style printing of anything that holds it show only a mask.
/// </summary>
public sealed class SecretText
{
    private const string Mask = "***";

    private readonly string _value;

    public SecretText(string value = "")
    {
        _value = value;
    }

    public static SecretText Empty { get; } = new();

    public bool IsEmpty => _value.Length == 0;

    /// <summary>The number of text elements typed, for drawing one mask character each.</summary>
    public int Length => new StringInfo(_value).LengthInTextElements;

    public SecretText Append(string text)
    {
        return new SecretText($"{_value}{text}");
    }

    public SecretText RemoveLast()
    {
        if (_value.Length == 0)
        {
            return this;
        }

        var starts = StringInfo.ParseCombiningCharacters(_value);

        return new SecretText(_value[..starts[^1]]);
    }

    /// <summary>The secret itself; only the code that hands it to the secret store calls this.</summary>
    public string Reveal()
    {
        return _value;
    }

    public override string ToString()
    {
        return Mask;
    }
}
