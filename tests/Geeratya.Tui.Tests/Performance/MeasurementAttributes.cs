namespace Geeratya.Tui.Tests.Performance;

/// <summary>A fact that is skipped unless GEERATYA_BENCH=1.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MeasurementFactAttribute : FactAttribute
{
    public MeasurementFactAttribute()
    {
        Skip = HotPathMeasurements.Enabled ? null : "Set GEERATYA_BENCH=1 to run measurements.";
    }
}

/// <summary>A theory that is skipped unless GEERATYA_BENCH=1.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MeasurementTheoryAttribute : TheoryAttribute
{
    public MeasurementTheoryAttribute()
    {
        Skip = HotPathMeasurements.Enabled ? null : "Set GEERATYA_BENCH=1 to run measurements.";
    }
}
