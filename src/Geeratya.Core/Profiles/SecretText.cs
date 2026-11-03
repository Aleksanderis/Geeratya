using System.Globalization;
using Geeratya.Core.Text;

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

    /// <summary>
    /// The secret after an edit with the caret <paramref name="back"/> units from the end, and the caret after it. The
    /// editor sees the text inside this method only; nothing leaves it but the new secret and a number.
    /// </summary>
    public (SecretText Secret, int Back) Edit(int back, Func<PromptText, PromptText> change)
    {
        var edited = change(new PromptText(_value, back));

        return (edited.Text == _value ? this : new SecretText(edited.Text), edited.Back);
    }

    /// <summary>
    /// How many text elements lie after the caret that is <paramref name="back"/> units from the end, so a masked
    /// display can put its caret in the same place without the text.
    /// </summary>
    public int ElementsAfter(int back)
    {
        var tail = Math.Clamp(back, 0, _value.Length);

        return new StringInfo(_value[^tail..]).LengthInTextElements;
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
