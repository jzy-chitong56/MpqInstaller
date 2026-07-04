using System;
using System.Globalization;
using System.Windows.Forms;
using MpqInstaller.UI;
using MpqInstaller.i18n;

namespace MpqInstaller;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        LanguageManager.Initialize(DetectDefaultLanguage());
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    private static string DetectDefaultLanguage()
    {
        try
        {
            var cult = CultureInfo.CurrentUICulture;
            if (cult.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return LanguageManager.ChineseSimplified;
            return LanguageManager.English;
        }
        catch
        {
            return LanguageManager.English;
        }
    }
}
