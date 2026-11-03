using System.Globalization;

namespace Geeratya.Core.Detail;

/// <summary>Formats an estimate (story points) for display: whole numbers without a decimal part.</summary>
public static class EstimateText
{
    /// <summary>The estimate as text, such as <c>5</c> or <c>0.5</c>, in the invariant culture.</summary>
    public static string Format(double estimate)
    {
        return estimate.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
