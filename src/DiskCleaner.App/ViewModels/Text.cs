using System.Globalization;
using DiskCleaner.Core.Rules;

namespace DiskCleaner.App.ViewModels;

public static class Text
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Number(long n) => n.ToString("N0", PtBr);

    public static string Percent(double fraction) => fraction.ToString("P1", PtBr);

    public static string Category(CleanupCategory c) => c switch
    {
        CleanupCategory.Temp => "Temporários",
        CleanupCategory.BrowserCache => "Cache de navegadores",
        CleanupCategory.AppCache => "Cache de aplicativos",
        CleanupCategory.LogsDumps => "Logs e despejos de erro",
        CleanupCategory.RecycleBin => "Lixeira",
        CleanupCategory.LargeFiles => "Arquivos grandes",
        CleanupCategory.Duplicates => "Duplicados",
        CleanupCategory.OldDownloads => "Downloads antigos",
        CleanupCategory.DevFolders => "Pastas de desenvolvimento",
        _ => c.ToString(),
    };

    public static string Risk(Risk r) => r == Core.Rules.Risk.Safe ? "Seguro" : "Revisar";

    public static string Age(DateTime utc, DateTime nowUtc)
    {
        if (utc == DateTime.MinValue) return "";
        var d = nowUtc - utc;
        if (d.TotalDays >= 365) return $"há {(int)(d.TotalDays / 365)} ano(s)";
        if (d.TotalDays >= 30) return $"há {(int)(d.TotalDays / 30)} mês(es)";
        if (d.TotalDays >= 1) return $"há {(int)d.TotalDays} dia(s)";
        return "hoje";
    }
}
