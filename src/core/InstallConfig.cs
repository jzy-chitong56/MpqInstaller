using System.Collections.Generic;

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
