using System.Globalization;

namespace DiskCleaner.Core.Formatting;

public static class SizeFormatter
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Units = { "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes < 1024) return bytes.ToString(PtBr) + " B";
        double value = bytes;
        int unit = -1;
        while (value >= 1024 && unit < Units.Length - 1) { value /= 1024; unit++; }
        return value.ToString("0.0", PtBr) + " " + Units[unit];
    }
}
