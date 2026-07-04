using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MpqInstaller.Core;

public sealed class InstallConfig
{
    public string MpqEditorPath { get; set; } = "MPQEditor.exe";
    public string FilesBaseDir { get; set; } = "Files";
    public int HashTableSize { get; set; } = 128;
    public string[] MapExtensions { get; set; } = { "*.w3x", "*.w3m" };
    public string[]? UninstallFiles { get; set; }
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new();
    public Dictionary<string, string> WarningTexts { get; set; } = new();
}

public sealed class ProfileConfig
{
    public Dictionary<string, string>? DisplayName { get; set; }
    public string? SubDir { get; set; }
    public WriteActionConfig[]? BaseActions { get; set; }
    public Dictionary<string, ModeConfig> Modes { get; set; } = new();
}

public sealed class ModeConfig
{
    public Dictionary<string, string>? DisplayName { get; set; }
    public WriteActionConfig[]? ExtraActions { get; set; }
}

public sealed class WriteActionConfig
{
    public string Source { get; set; } = string.Empty;
    public string Dest { get; set; } = string.Empty;
}

public static class ConfigLoader
{
    public const string DefaultFileName = "config.json";
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, DefaultFileName);

    public static bool TryLoad(string path, out InstallConfig? config, out string errorMessage)
    {
        config = null;
        errorMessage = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                errorMessage = LanguageManager.GetFormat("err_config_not_found", path);
                return false;
            }
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            config = JsonSerializer.Deserialize<InstallConfig>(json, opts);
            if (config == null)
            {
                errorMessage = LanguageManager.Get("err_config_empty");
                return false;
            }
            if (string.IsNullOrWhiteSpace(config.MpqEditorPath))
                config.MpqEditorPath = "MPQEditor.exe";
            if (string.IsNullOrWhiteSpace(config.FilesBaseDir))
                config.FilesBaseDir = "Files";
            if (config.MapExtensions == null || config.MapExtensions.Length == 0)
                config.MapExtensions = new[] { "*.w3x", "*.w3m" };
            if (config.HashTableSize <= 0)
                config.HashTableSize = 128;
            if (config.Profiles == null)
                config.Profiles = new();
            if (config.WarningTexts == null)
                config.WarningTexts = new();
            return true;
        }
        catch (JsonException jex)
        {
            errorMessage = LanguageManager.GetFormat("err_config_parse", jex.Message);
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = LanguageManager.GetFormat("err_config_generic", ex.Message);
            return false;
        }
    }

    public static string GetDisplayName(Dictionary<string, string>? dict, string fallback)
    {
        if (dict == null || dict.Count == 0) return fallback;
        var cur = LanguageManager.CurrentLanguage;
        if (dict.TryGetValue(cur, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (dict.TryGetValue(LanguageManager.English, out var en) && !string.IsNullOrEmpty(en)) return en;
        foreach (var kv in dict)
            if (!string.IsNullOrEmpty(kv.Value)) return kv.Value;
        return fallback;
    }

    public static string? GetWarningText(InstallConfig config)
    {
        if (config.WarningTexts == null || config.WarningTexts.Count == 0) return null;
        var cur = LanguageManager.CurrentLanguage;
        if (config.WarningTexts.TryGetValue(cur, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (config.WarningTexts.TryGetValue(LanguageManager.English, out var en) && !string.IsNullOrEmpty(en)) return en;
        foreach (var kv in config.WarningTexts)
            if (!string.IsNullOrEmpty(kv.Value)) return kv.Value;
        return null;
    }
}
