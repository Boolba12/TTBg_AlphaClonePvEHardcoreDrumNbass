using System.Globalization;

public enum UIStatFormat
{
    Integer,
    Percentage,
    Multiplier,
    CurrentMaximum,
    SignedModifier
}

public static class UIStatFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string FormatInteger(float value)
    {
        return System.Math.Round(value).ToString("0", Invariant);
    }

    public static string FormatPercentage(float normalizedValue, int decimalPlaces = 0)
    {
        int decimals = System.Math.Max(0, decimalPlaces);
        return (normalizedValue * 100f).ToString($"F{decimals}", Invariant) + "%";
    }

    public static string FormatMultiplier(float value, int decimalPlaces = 2)
    {
        int decimals = System.Math.Max(0, decimalPlaces);
        return "x" + value.ToString($"F{decimals}", Invariant);
    }

    public static string FormatCurrentMaximum(float current, float maximum)
    {
        return $"{FormatInteger(current)} / {FormatInteger(maximum)}";
    }

    public static string FormatModifier(float value, int decimalPlaces = 0)
    {
        int decimals = System.Math.Max(0, decimalPlaces);
        string fractional = decimals > 0 ? "." + new string('0', decimals) : string.Empty;
        return value.ToString($"+0{fractional};-0{fractional};0", Invariant);
    }

    public static string Format(
        UIStatFormat format,
        float value,
        float secondaryValue = 0f,
        int decimalPlaces = 0)
    {
        switch (format)
        {
            case UIStatFormat.Percentage:
                return FormatPercentage(value, decimalPlaces);
            case UIStatFormat.Multiplier:
                return FormatMultiplier(value, decimalPlaces);
            case UIStatFormat.CurrentMaximum:
                return FormatCurrentMaximum(value, secondaryValue);
            case UIStatFormat.SignedModifier:
                return FormatModifier(value, decimalPlaces);
            default:
                return FormatInteger(value);
        }
    }
}
