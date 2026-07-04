using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace MpqInstaller.i18n;

public static class LanguageManager
{
    public const string ChineseSimplified = "zh-CN";
    public const string English = "en-US";

    public static readonly IReadOnlyList<(string Code, string DisplayName)> AvailableLanguages =
        new[]
        {
            (ChineseSimplified, "简体中文"),
            (English, "English"),
        };

    private static readonly Dictionary<string, string> Fallback = new();
    private static Dictionary<string, string> _strings = new();
    private static string _current = English;

    public static string CurrentLanguage => _current;

    public static void Initialize(string defaultCode)
    {
        Load(English);
        var code = AvailableLanguages.Any(l => l.Code == defaultCode) ? defaultCode : English;
        SetLanguage(code);
    }

    public static bool SetLanguage(string code)
    {
        var loaded = Load(code);
        if (loaded == null)
            return false;
        _strings = loaded;
        _current = code;
        try
        {
            var cult = code == ChineseSimplified
                ? CultureInfo.GetCultureInfo("zh-CN")
                : CultureInfo.GetCultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = cult;
            Thread.CurrentThread.CurrentCulture = cult;
        }
        catch
        {
        }
        return true;
    }

    public static string Get(string key)
    {
        if (_strings.TryGetValue(key, out var v)) return v;
        if (Fallback.TryGetValue(key, out var fb)) return fb;
        return key;
    }

    public static string GetFormat(string key, params object?[] args)
    {
        var fmt = Get(key);
        try
        {
            return args == null || args.Length == 0 ? fmt : string.Format(CultureInfo.CurrentCulture, fmt, args);
        }
        catch (FormatException)
        {
            return fmt;
        }
    }

    private static Dictionary<string, string>? Load(string code)
    {
        var fileName = code switch
        {
            ChineseSimplified => "zh-CN.json",
            English => "en-US.json",
            _ => null,
        };
        if (fileName == null) return null;

        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.Ordinal));
        if (resName == null) return null;

        using var stream = asm.GetManifestResourceStream(resName);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict == null) return null;

        if (code == English)
        {
            Fallback.Clear();
            foreach (var kv in dict) Fallback[kv.Key] = kv.Value;
        }
        return dict;
    }
}
