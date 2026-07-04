using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MpqInstaller.Core;

// ============================================================================
//  语言管理
// ============================================================================

public static class LanguageManager
{
    public const string ChineseSimplified = "zh-CN";
    public const string English = "en-US";
    public static readonly IReadOnlyList<(string Code, string DisplayName)> AvailableLanguages = new[] { (ChineseSimplified, "简体中文"), (English, "English") };
    private static readonly Dictionary<string, string> Fallback = new();
    private static Dictionary<string, string> _strings = new();
    private static string _current = English;
    public static string CurrentLanguage => _current;

    public static void Initialize(string defaultCode) { Load(English); var code = AvailableLanguages.Any(l => l.Code == defaultCode) ? defaultCode : English; SetLanguage(code); }

    public static bool SetLanguage(string code)
    {
        var loaded = Load(code); if (loaded == null) return false; _strings = loaded; _current = code;
        try { var cult = code == ChineseSimplified ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US"); Thread.CurrentThread.CurrentUICulture = cult; Thread.CurrentThread.CurrentCulture = cult; } catch { }
        return true;
    }

    public static string Get(string key) { if (_strings.TryGetValue(key, out var v)) return v; if (Fallback.TryGetValue(key, out var fb)) return fb; return key; }
    public static string GetFormat(string key, params object?[] args) { var fmt = Get(key); try { return args == null || args.Length == 0 ? fmt : string.Format(CultureInfo.CurrentCulture, fmt, args); } catch (FormatException) { return fmt; } }

    private static Dictionary<string, string>? Load(string code)
    {
        var fileName = code switch { ChineseSimplified => "zh-CN.json", English => "en-US.json", _ => null };
        if (fileName == null) return null;
        var asm = Assembly.GetExecutingAssembly(); var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName, StringComparison.Ordinal)); if (resName == null) return null;
        using var stream = asm.GetManifestResourceStream(resName); if (stream == null) return null;
        using var reader = new StreamReader(stream); var json = reader.ReadToEnd();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json); if (dict == null) return null;
        if (code == English) { Fallback.Clear(); foreach (var kv in dict) Fallback[kv.Key] = kv.Value; }
        return dict;
    }
}

// ============================================================================
//  配置加载
// ============================================================================

public static class ConfigLoader
{
    public const string DefaultFileName = "config.json";
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, DefaultFileName);

    public static bool TryLoad(string path, out InstallConfig? config, out string errorMessage)
    {
        config = null; errorMessage = string.Empty;
        try
        {
            if (!File.Exists(path)) { errorMessage = LanguageManager.GetFormat("err_config_not_found", path); return false; }
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            config = JsonSerializer.Deserialize<InstallConfig>(json, opts);
            if (config == null) { errorMessage = LanguageManager.Get("err_config_empty"); return false; }
            if (string.IsNullOrWhiteSpace(config.MpqEditorPath)) config.MpqEditorPath = "MPQEditor.exe";
            if (string.IsNullOrWhiteSpace(config.FilesBaseDir)) config.FilesBaseDir = "Files";
            if (config.MapExtensions == null || config.MapExtensions.Length == 0) config.MapExtensions = new[] { "*.w3x", "*.w3m" };
            if (config.HashTableSize <= 0) config.HashTableSize = 128;
            if (config.Profiles == null) config.Profiles = new();
            if (config.WarningTexts == null) config.WarningTexts = new();
            return true;
        }
        catch (JsonException jex) { errorMessage = LanguageManager.GetFormat("err_config_parse", jex.Message); return false; }
        catch (Exception ex) { errorMessage = LanguageManager.GetFormat("err_config_generic", ex.Message); return false; }
    }

    public static string GetDisplayName(Dictionary<string, string>? dict, string fallback)
    { if (dict == null || dict.Count == 0) return fallback; var cur = LanguageManager.CurrentLanguage; if (dict.TryGetValue(cur, out var v) && !string.IsNullOrEmpty(v)) return v; if (dict.TryGetValue(LanguageManager.English, out var en) && !string.IsNullOrEmpty(en)) return en; foreach (var kv in dict) if (!string.IsNullOrEmpty(kv.Value)) return kv.Value; return fallback; }

    public static string? GetWarningText(InstallConfig config)
    { if (config.WarningTexts == null || config.WarningTexts.Count == 0) return null; var cur = LanguageManager.CurrentLanguage; if (config.WarningTexts.TryGetValue(cur, out var v) && !string.IsNullOrEmpty(v)) return v; if (config.WarningTexts.TryGetValue(LanguageManager.English, out var en) && !string.IsNullOrEmpty(en)) return en; foreach (var kv in config.WarningTexts) if (!string.IsNullOrEmpty(kv.Value)) return kv.Value; return null; }
}

// ============================================================================
//  MPQ 调用 + 安装计划 + 批量执行
// ============================================================================

public readonly record struct MpqCommandResult(int ExitCode, string Output)
{
    public MpqExitCode Kind => ExitCode switch { 0 => MpqExitCode.Success, 5 => MpqExitCode.PermissionOrUac, _ => MpqExitCode.OtherError };
    public bool IsSuccess => ExitCode == 0;
}

public sealed class MpqEditor
{
    private readonly string _baseDir;
    public string ExePath { get; }

    public MpqEditor(string exePath, string baseDir) { ExePath = exePath; _baseDir = baseDir; }

    public static (string exePath, string baseDir) ResolvePaths(InstallConfig config)
    {
        var exeDir = AppContext.BaseDirectory;
        var exe = Path.IsPathRooted(config.MpqEditorPath) ? config.MpqEditorPath : Path.Combine(exeDir, config.MpqEditorPath);
        var baseDir = Path.IsPathRooted(config.FilesBaseDir) ? config.FilesBaseDir : Path.Combine(exeDir, config.FilesBaseDir);
        return (Path.GetFullPath(exe), Path.GetFullPath(baseDir));
    }

    public bool Exists() => File.Exists(ExePath);

    public string ResolveSource(string? profileSubDir, string relativeSource)
    {
        var segs = new List<string> { _baseDir };
        if (!string.IsNullOrEmpty(profileSubDir)) segs.Add(profileSubDir);
        segs.Add(relativeSource);
        for (var i = 0; i < segs.Count; i++) segs[i] = segs[i].Replace('/', '\\');
        return string.Join('\\', segs);
    }

    public MpqCommandResult HtSize(string map, int size) => Run("htsize", map, size.ToString());
    public MpqCommandResult Add(string map, string source, string dest) => Run("a", map, source, dest);
    public MpqCommandResult Delete(string map, string pathInMpq) => Run("d", map, pathInMpq);
    public MpqCommandResult Flush(string map) => Run("f", map);

    private MpqCommandResult Run(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.Default,
            StandardErrorEncoding = System.Text.Encoding.Default,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = new Process { StartInfo = psi };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        var output = string.IsNullOrEmpty(stderr) ? stdout : stdout + Environment.NewLine + stderr;
        return new MpqCommandResult(p.ExitCode, output?.Trim() ?? string.Empty);
    }
}

public enum ActionType { HtSize, Add, Delete, Flush }

public readonly record struct InstallAction(ActionType Type, string Map, string Source, string Dest, int Size, string LogMessage);

public static class InstallPlanBuilder
{
    public static List<InstallAction> BuildInstall(MpqEditor mpq, InstallConfig config, ProfileConfig profile, ModeConfig mode, string map)
    {
        var plan = new List<InstallAction>();
        plan.Add(new(ActionType.HtSize, map, string.Empty, string.Empty, config.HashTableSize, LanguageManager.GetFormat("step_htsize", config.HashTableSize)));
        if (profile.BaseActions != null) foreach (var a in profile.BaseActions) { var src = mpq.ResolveSource(profile.SubDir, a.Source); plan.Add(new(ActionType.Add, map, src, a.Dest, 0, LanguageManager.GetFormat("step_add", a.Source, a.Dest))); }
        if (mode.ExtraActions != null) foreach (var a in mode.ExtraActions) { var src = mpq.ResolveSource(profile.SubDir, a.Source); plan.Add(new(ActionType.Add, map, src, a.Dest, 0, LanguageManager.GetFormat("step_add", a.Source, a.Dest))); }
        plan.Add(new(ActionType.Flush, map, string.Empty, string.Empty, 0, LanguageManager.Get("step_flush")));
        return plan;
    }

    public static List<InstallAction> BuildUninstall(InstallConfig config, string map)
    {
        var plan = new List<InstallAction>();
        if (config.UninstallFiles != null) foreach (var f in config.UninstallFiles) plan.Add(new(ActionType.Delete, map, string.Empty, f, 0, LanguageManager.GetFormat("step_delete", f)));
        plan.Add(new(ActionType.Flush, map, string.Empty, string.Empty, 0, LanguageManager.Get("step_flush")));
        return plan;
    }
}

public enum ProgressKind { Started, Step, MapOk, MapFail, MapPermissionFail, MpqMissing, Completed, Cancelled }

public readonly record struct ProgressReport(ProgressKind Kind, int Index, int Total, string MapName, string StatusMessage, string LogLine);

public sealed class BatchSummary
{
    public int Total { get; set; } public int Success { get; set; } public int Failed { get; set; }
    public bool Cancelled { get; set; } public bool Aborted { get; set; }
    public List<string> Failures { get; } = new();
}

public static class BatchProcessor
{
    public static async Task<BatchSummary> RunInstallAsync(IReadOnlyList<string> maps, InstallConfig config, ProfileConfig profile, ModeConfig mode, MpqEditor mpq, IProgress<ProgressReport>? progress, CancellationToken ct)
    {
        var summary = new BatchSummary { Total = maps.Count };
        if (!mpq.Exists()) { Report(progress, ProgressKind.MpqMissing, 0, maps.Count, string.Empty, LanguageManager.GetFormat("err_mpq_missing", mpq.ExePath), string.Empty); summary.Aborted = true; return summary; }
        Report(progress, ProgressKind.Started, 0, maps.Count, string.Empty, LanguageManager.GetFormat("status_started", maps.Count), LanguageManager.GetFormat("log_started", maps.Count));
        try
        {
            for (var i = 0; i < maps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var map = maps[i]; var mapName = Path.GetFileName(map); var idx = i + 1;
                var plan = InstallPlanBuilder.BuildInstall(mpq, config, profile, mode, map);
                bool permFail = false; int lastNonZero = 0; string failOut = string.Empty;
                foreach (var action in plan) { ct.ThrowIfCancellationRequested(); Report(progress, ProgressKind.Step, idx, maps.Count, mapName, LanguageManager.GetFormat("status_step", idx, maps.Count, mapName, action.LogMessage), string.Empty); var r = ExecuteAction(mpq, action); if (!r.IsSuccess) { if (r.Kind == MpqExitCode.PermissionOrUac) permFail = true; lastNonZero = r.ExitCode; if (!string.IsNullOrEmpty(r.Output)) failOut = r.Output; } }
                if (permFail) { summary.Failed++; var line = LanguageManager.GetFormat("log_map_permission_fail", idx, maps.Count, mapName); summary.Failures.Add(line); Report(progress, ProgressKind.MapPermissionFail, idx, maps.Count, mapName, LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName), line + "  " + LanguageManager.Get("reason_permission")); }
                else if (lastNonZero != 0) { summary.Failed++; var line = LanguageManager.GetFormat("log_map_fail", idx, maps.Count, mapName, lastNonZero); summary.Failures.Add(line); Report(progress, ProgressKind.MapFail, idx, maps.Count, mapName, LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName), line + (string.IsNullOrEmpty(failOut) ? string.Empty : "  " + failOut)); }
                else { summary.Success++; Report(progress, ProgressKind.MapOk, idx, maps.Count, mapName, LanguageManager.GetFormat("status_map_ok", idx, maps.Count, mapName), LanguageManager.GetFormat("log_map_ok", idx, maps.Count, mapName)); }
                await Task.Yield();
            }
            Report(progress, ProgressKind.Completed, maps.Count, maps.Count, string.Empty, LanguageManager.GetFormat("status_completed", summary.Success, summary.Failed), LanguageManager.GetFormat("log_completed", summary.Success, summary.Failed));
        }
        catch (OperationCanceledException) { summary.Cancelled = true; Report(progress, ProgressKind.Cancelled, summary.Success + summary.Failed, maps.Count, string.Empty, LanguageManager.GetFormat("status_cancelled", summary.Success, summary.Failed), LanguageManager.GetFormat("log_cancelled", summary.Success, summary.Failed)); }
        return summary;
    }

    public static async Task<BatchSummary> RunUninstallAsync(IReadOnlyList<string> maps, InstallConfig config, MpqEditor mpq, IProgress<ProgressReport>? progress, CancellationToken ct)
    {
        var summary = new BatchSummary { Total = maps.Count };
        if (!mpq.Exists()) { Report(progress, ProgressKind.MpqMissing, 0, maps.Count, string.Empty, LanguageManager.GetFormat("err_mpq_missing", mpq.ExePath), string.Empty); summary.Aborted = true; return summary; }
        Report(progress, ProgressKind.Started, 0, maps.Count, string.Empty, LanguageManager.GetFormat("status_started", maps.Count), LanguageManager.GetFormat("log_started", maps.Count));
        try
        {
            for (var i = 0; i < maps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var map = maps[i]; var mapName = Path.GetFileName(map); var idx = i + 1;
                var plan = InstallPlanBuilder.BuildUninstall(config, map);
                bool permFail = false; int lastNonZero = 0; string failOut = string.Empty;
                foreach (var action in plan) { ct.ThrowIfCancellationRequested(); Report(progress, ProgressKind.Step, idx, maps.Count, mapName, LanguageManager.GetFormat("status_step", idx, maps.Count, mapName, action.LogMessage), string.Empty); var r = ExecuteAction(mpq, action); if (!r.IsSuccess) { if (r.Kind == MpqExitCode.PermissionOrUac) permFail = true; lastNonZero = r.ExitCode; if (!string.IsNullOrEmpty(r.Output)) failOut = r.Output; } }
                if (permFail) { summary.Failed++; var line = LanguageManager.GetFormat("log_map_permission_fail", idx, maps.Count, mapName); summary.Failures.Add(line); Report(progress, ProgressKind.MapPermissionFail, idx, maps.Count, mapName, LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName), line + "  " + LanguageManager.Get("reason_permission")); }
                else if (lastNonZero != 0) { summary.Failed++; var line = LanguageManager.GetFormat("log_map_fail", idx, maps.Count, mapName, lastNonZero); summary.Failures.Add(line); Report(progress, ProgressKind.MapFail, idx, maps.Count, mapName, LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName), line + (string.IsNullOrEmpty(failOut) ? string.Empty : "  " + failOut)); }
                else { summary.Success++; Report(progress, ProgressKind.MapOk, idx, maps.Count, mapName, LanguageManager.GetFormat("status_map_ok", idx, maps.Count, mapName), LanguageManager.GetFormat("log_map_ok", idx, maps.Count, mapName)); }
                await Task.Yield();
            }
            Report(progress, ProgressKind.Completed, maps.Count, maps.Count, string.Empty, LanguageManager.GetFormat("status_completed", summary.Success, summary.Failed), LanguageManager.GetFormat("log_completed", summary.Success, summary.Failed));
        }
        catch (OperationCanceledException) { summary.Cancelled = true; Report(progress, ProgressKind.Cancelled, summary.Success + summary.Failed, maps.Count, string.Empty, LanguageManager.GetFormat("status_cancelled", summary.Success, summary.Failed), LanguageManager.GetFormat("log_cancelled", summary.Success, summary.Failed)); }
        return summary;
    }

    private static void Report(IProgress<ProgressReport>? progress, ProgressKind kind, int idx, int total, string mapName, string status, string log) => progress?.Report(new(kind, idx, total, mapName, status, log));
    private static MpqCommandResult ExecuteAction(MpqEditor mpq, in InstallAction a) => a.Type switch { ActionType.HtSize => mpq.HtSize(a.Map, a.Size), ActionType.Add => mpq.Add(a.Map, a.Source, a.Dest), ActionType.Delete => mpq.Delete(a.Map, a.Dest), ActionType.Flush => mpq.Flush(a.Map), _ => new(0, string.Empty) };
}

// ============================================================================
//  主窗体
// ============================================================================

public sealed partial class MainForm : Form
{
    private System.ComponentModel.IContainer? components = null;
    private Panel pnlTitle = null!; private Label lblTitle = null!; private Label lblTitleVersion = null!; private ComboBox cboLanguage = null!;
    private Label lblConfigStatus = null!; private Button btnReloadConfig = null!;
    private Label lblOperation = null!; private RadioButton rbInstall = null!; private RadioButton rbUninstall = null!;
    private Panel pnlSep1 = null!;
    private Label lblProfile = null!; private RadioButton rbProfileA = null!; private RadioButton rbProfileB = null!;
    private Panel pnlSep2 = null!;
    private Label lblMode = null!; private CheckedListBox clbModes = null!;
    private Panel pnlSep3 = null!;
    private Button btnSingleMap = null!; private Button btnFolder = null!;
    private Button btnCancel = null!; private Button btnShowLog = null!;
    private ProgressBar bottomProgress = null!; private Label lblBottomProgress = null!;

    private bool _initializing = true; private bool _running; private CancellationTokenSource? _cts;
    private InstallConfig? _config; private string _configError = string.Empty;
    private List<ProfileItem> _profileItems = new(); private List<ModeItem> _modeItems = new();
    private LogForm? _logForm;

    public MainForm()
    {
        InitializeComponent(); InitLanguageCombo(); LoadConfig(); ApplyLanguage();
        _initializing = false; WireEvents(); UpdateUiEnabledState(); ResetBottomProgress();
    }

    protected override void Dispose(bool disposing) { if (disposing && components != null) components.Dispose(); base.Dispose(disposing); }

    private void InitializeComponent()
    {
        pnlTitle = new(); lblTitle = new(); lblTitleVersion = new(); cboLanguage = new();
        lblConfigStatus = new(); btnReloadConfig = new();
        lblOperation = new(); rbInstall = new(); rbUninstall = new();
        pnlSep1 = new(); lblProfile = new(); rbProfileA = new(); rbProfileB = new();
        pnlSep2 = new(); lblMode = new(); clbModes = new(); pnlSep3 = new();
        btnSingleMap = new(); btnFolder = new(); btnCancel = new(); btnShowLog = new();
        bottomProgress = new(); lblBottomProgress = new();
        SuspendLayout();

        pnlTitle.BackColor = System.Drawing.Color.FromArgb(0, 120, 215); pnlTitle.Dock = DockStyle.Top; pnlTitle.Size = new(420, 60);
        lblTitle.Font = new("微软雅黑", 14F, System.Drawing.FontStyle.Bold); lblTitle.ForeColor = System.Drawing.Color.White; lblTitle.Location = new(16, 10); lblTitle.AutoSize = true;
        lblTitleVersion.Font = new("微软雅黑", 9F); lblTitleVersion.ForeColor = System.Drawing.Color.FromArgb(220, 230, 245); lblTitleVersion.Location = new(16, 36); lblTitleVersion.AutoSize = true;
        cboLanguage.DropDownStyle = ComboBoxStyle.DropDownList; cboLanguage.FlatStyle = FlatStyle.Flat; cboLanguage.Font = new("微软雅黑", 9F); cboLanguage.Location = new(316, 12); cboLanguage.Size = new(90, 25);
        pnlTitle.Controls.Add(lblTitle); pnlTitle.Controls.Add(lblTitleVersion); pnlTitle.Controls.Add(cboLanguage);

        lblConfigStatus.AutoSize = true; lblConfigStatus.ForeColor = System.Drawing.Color.Gray; lblConfigStatus.Font = new("微软雅黑", 8F); lblConfigStatus.Location = new(12, 68);
        btnReloadConfig.FlatStyle = FlatStyle.Flat; btnReloadConfig.Font = new("微软雅黑", 8F); btnReloadConfig.ForeColor = System.Drawing.Color.FromArgb(0, 120, 215); btnReloadConfig.Location = new(316, 64); btnReloadConfig.Size = new(92, 22);

        lblOperation.Font = new("微软雅黑", 10F, System.Drawing.FontStyle.Bold); lblOperation.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50); lblOperation.Location = new(12, 98); lblOperation.Size = new(120, 20);
        rbInstall.Font = new("微软雅黑", 9F); rbInstall.Location = new(24, 125); rbInstall.AutoSize = true; rbInstall.Checked = true;
        rbUninstall.Font = new("微软雅黑", 9F); rbUninstall.Location = new(230, 125); rbUninstall.AutoSize = true;
        pnlSep1.BackColor = System.Drawing.Color.FromArgb(225, 225, 225); pnlSep1.Location = new(12, 158); pnlSep1.Size = new(396, 1);

        lblProfile.Font = new("微软雅黑", 10F, System.Drawing.FontStyle.Bold); lblProfile.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50); lblProfile.Location = new(12, 168); lblProfile.Size = new(120, 20);
        rbProfileA.Font = new("微软雅黑", 9F); rbProfileA.Location = new(24, 195); rbProfileA.AutoSize = true; rbProfileA.Checked = true;
        rbProfileB.Font = new("微软雅黑", 9F); rbProfileB.Location = new(230, 195); rbProfileB.AutoSize = true;
        pnlSep2.BackColor = System.Drawing.Color.FromArgb(225, 225, 225); pnlSep2.Location = new(12, 228); pnlSep2.Size = new(396, 1);

        lblMode.Font = new("微软雅黑", 10F, System.Drawing.FontStyle.Bold); lblMode.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50); lblMode.Location = new(12, 238); lblMode.Size = new(120, 20);
        clbModes.BorderStyle = BorderStyle.None; clbModes.CheckOnClick = true; clbModes.ColumnWidth = 190; clbModes.Font = new("微软雅黑", 9F); clbModes.ItemHeight = 20; clbModes.Location = new(24, 265); clbModes.MultiColumn = true; clbModes.Size = new(380, 70); clbModes.BackColor = SystemColors.Window;
        pnlSep3.BackColor = System.Drawing.Color.FromArgb(225, 225, 225); pnlSep3.Location = new(12, 340); pnlSep3.Size = new(396, 1);

        btnSingleMap.Font = new("微软雅黑", 11F); btnSingleMap.Location = new(12, 358); btnSingleMap.Size = new(195, 48); btnSingleMap.FlatStyle = FlatStyle.System;
        btnFolder.Font = new("微软雅黑", 11F); btnFolder.Location = new(213, 358); btnFolder.Size = new(195, 48); btnFolder.FlatStyle = FlatStyle.System;
        btnCancel.Font = new("微软雅黑", 9F); btnCancel.Location = new(12, 420); btnCancel.Size = new(80, 26); btnCancel.Visible = false;
        btnShowLog.Font = new("微软雅黑", 9F); btnShowLog.ForeColor = System.Drawing.Color.FromArgb(0, 120, 215); btnShowLog.FlatStyle = FlatStyle.Flat; btnShowLog.Location = new(320, 420); btnShowLog.Size = new(88, 26);

        bottomProgress.Dock = DockStyle.Bottom; bottomProgress.Size = new(420, 22); bottomProgress.Maximum = 100; bottomProgress.Value = 0; bottomProgress.Style = ProgressBarStyle.Continuous;
        lblBottomProgress.Dock = DockStyle.Bottom; lblBottomProgress.TextAlign = System.Drawing.ContentAlignment.MiddleRight; lblBottomProgress.Font = new("微软雅黑", 9F); lblBottomProgress.ForeColor = System.Drawing.Color.DimGray; lblBottomProgress.Size = new(420, 22); lblBottomProgress.Text = "0/0"; lblBottomProgress.Padding = new(0, 0, 14, 0); lblBottomProgress.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);

        AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new(420, 510);
        Controls.Add(pnlTitle); Controls.Add(lblConfigStatus); Controls.Add(btnReloadConfig);
        Controls.Add(lblOperation); Controls.Add(rbInstall); Controls.Add(rbUninstall); Controls.Add(pnlSep1);
        Controls.Add(lblProfile); Controls.Add(rbProfileA); Controls.Add(rbProfileB); Controls.Add(pnlSep2);
        Controls.Add(lblMode); Controls.Add(clbModes); Controls.Add(pnlSep3);
        Controls.Add(btnSingleMap); Controls.Add(btnFolder); Controls.Add(btnCancel); Controls.Add(btnShowLog);
        Controls.Add(bottomProgress); Controls.Add(lblBottomProgress);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen; MinimumSize = new(420, 510); MaximumSize = new(420, 510);
        ResumeLayout(false); PerformLayout();
    }

    private void InitLanguageCombo() { cboLanguage.Items.Clear(); foreach (var (code, display) in LanguageManager.AvailableLanguages) cboLanguage.Items.Add(new LangItem(code, display)); SelectLanguageCombo(LanguageManager.CurrentLanguage); }
    private void SelectLanguageCombo(string code) { for (var i = 0; i < cboLanguage.Items.Count; i++) { if (((LangItem)cboLanguage.Items[i]!).Code == code) { cboLanguage.SelectedIndex = i; return; } } if (cboLanguage.Items.Count > 0) cboLanguage.SelectedIndex = 0; }

    private void ApplyLanguage()
    {
        Text = L("app_title"); lblTitle.Text = L("app_title"); lblTitleVersion.Text = L("title_version");
        lblConfigStatus.Text = L("lbl_config_status") + (_config != null ? L("config_loaded_ok") : L("config_load_failed"));
        btnReloadConfig.Text = L("btn_reload_config"); lblOperation.Text = L("lbl_operation");
        rbInstall.Text = L("op_install"); rbUninstall.Text = L("op_uninstall");
        lblProfile.Text = L("lbl_profile"); lblMode.Text = L("lbl_mode");
        btnSingleMap.Text = L("btn_single_map"); btnFolder.Text = L("btn_folder");
        btnShowLog.Text = L("btn_show_log"); btnCancel.Text = L("btn_cancel");
    }

    private void WireEvents()
    {
        cboLanguage.SelectedIndexChanged += (_, _) => { if (_initializing) return; if (cboLanguage.SelectedItem is LangItem item) { LanguageManager.SetLanguage(item.Code); ApplyLanguage(); RefreshProfileAndModeDisplay(); } };
        btnReloadConfig.Click += (_, _) => { LoadConfig(); RefreshProfileAndModeDisplay(); UpdateUiEnabledState(); lblConfigStatus.Text = L("lbl_config_status") + (_config != null ? L("config_loaded_ok") : L("config_load_failed")); };
        rbInstall.CheckedChanged += (_, _) => { if (rbInstall.Checked) UpdateUiEnabledState(); };
        rbUninstall.CheckedChanged += (_, _) => { if (rbUninstall.Checked) UpdateUiEnabledState(); };
        rbProfileA.CheckedChanged += (_, _) => { if (_initializing || !rbProfileA.Checked) return; RefreshModesFromCurrentProfile(); };
        rbProfileB.CheckedChanged += (_, _) => { if (_initializing || !rbProfileB.Checked) return; RefreshModesFromCurrentProfile(); };
        btnSingleMap.Click += (_, _) => RunInstall(false); btnFolder.Click += (_, _) => RunInstall(true);
        btnCancel.Click += (_, _) => CancelRun(); btnShowLog.Click += (_, _) => ShowLogWindow();
    }

    private void UpdateUiEnabledState() { var installMode = rbInstall.Checked; var configOk = _config != null && _profileItems.Count >= 2; rbProfileA.Enabled = installMode && configOk; rbProfileB.Enabled = installMode && configOk; clbModes.Enabled = installMode && configOk; btnSingleMap.Enabled = !_running && configOk; btnFolder.Enabled = !_running && configOk; btnCancel.Visible = _running; }

    private void LoadConfig()
    {
        _profileItems.Clear(); _modeItems.Clear();
        if (ConfigLoader.TryLoad(ConfigLoader.DefaultPath, out var cfg, out var err)) { _config = cfg; _configError = string.Empty; } else { _config = null; _configError = err; return; }
        foreach (var kv in cfg!.Profiles) _profileItems.Add(new(kv.Key, ConfigLoader.GetDisplayName(kv.Value.DisplayName, kv.Key)));
        if (_profileItems.Count < 2) { _config = null; return; }
        if (!_initializing) RefreshProfileAndModeDisplay();
    }

    private void RefreshProfileAndModeDisplay()
    {
        if (_config == null || _profileItems.Count < 2) { rbProfileA.Text = "—"; rbProfileB.Text = "—"; clbModes.Items.Clear(); return; }
        for (var i = 0; i < _profileItems.Count; i++) { var kv = _config.Profiles[_profileItems[i].Key]; var display = ConfigLoader.GetDisplayName(kv.DisplayName, _profileItems[i].Key); _profileItems[i] = _profileItems[i] with { DisplayName = display }; }
        rbProfileA.Text = _profileItems[0].DisplayName; rbProfileB.Text = _profileItems[1].DisplayName;
        if (!rbProfileA.Checked && !rbProfileB.Checked) rbProfileA.Checked = true;
        RefreshModesFromCurrentProfile();
    }

    private void RefreshModesFromCurrentProfile()
    {
        clbModes.Items.Clear(); _modeItems.Clear();
        if (_config == null || _profileItems.Count < 2) return;
        var profileKey = rbProfileA.Checked ? _profileItems[0].Key : _profileItems[1].Key;
        if (!_config.Profiles.TryGetValue(profileKey, out var profile)) return;
        foreach (var kv in profile.Modes) { var display = ConfigLoader.GetDisplayName(kv.Value.DisplayName, kv.Key); _modeItems.Add(new(kv.Key, display)); clbModes.Items.Add(display); }
        for (var i = 0; i < clbModes.Items.Count; i++) clbModes.SetItemChecked(i, true);
    }

    private void RunInstall(bool batchMode)
    {
        if (_running || _config == null) return;
        var isInstall = rbInstall.Checked;
        List<string>? maps;
        if (batchMode) { using var fbd = new FolderBrowserDialog { Description = L("dlg_select_folder"), ShowNewFolderButton = false }; if (fbd.ShowDialog(this) != DialogResult.OK) return; maps = CollectMapsInFolder(fbd.SelectedPath); if (maps.Count == 0) { Warn(L("err_no_maps_in_folder")); return; } }
        else { using var ofd = new OpenFileDialog { Title = L("dlg_select_map"), Filter = L("filter_maps"), CheckFileExists = true }; if (ofd.ShowDialog(this) != DialogResult.OK) return; maps = new List<string> { ofd.FileName }; }

        var (exePath, baseDir) = MpqEditor.ResolvePaths(_config);
        if (!File.Exists(exePath)) { Warn(LF("err_mpq_missing", exePath)); return; }

        ProfileConfig? profile = null; List<ModeConfig>? selectedModes = null;
        if (isInstall)
        {
            var profileKey = rbProfileA.Checked ? _profileItems[0].Key : _profileItems[1].Key;
            if (!_config.Profiles.TryGetValue(profileKey, out profile)) { Warn(L("err_profile_invalid")); return; }
            selectedModes = new(); for (var i = 0; i < clbModes.CheckedItems.Count; i++) { var idx = clbModes.Items.IndexOf(clbModes.CheckedItems[i]!); if (idx >= 0 && idx < _modeItems.Count) { if (profile.Modes.TryGetValue(_modeItems[idx].Key, out var m)) selectedModes.Add(m); } }
            if (selectedModes.Count == 0) { Warn(L("err_no_modes_checked")); return; }
        }
        else { if (_config.UninstallFiles == null || _config.UninstallFiles.Length == 0) { Warn(L("err_config_empty") + " UninstallFiles"); return; } }

        var warningText = ConfigLoader.GetWarningText(_config);
        if (string.IsNullOrEmpty(warningText)) warningText = isInstall ? L("warn_default_install") : L("warn_default_uninstall");
        if (MessageBox.Show(this, warningText, L("warn_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        SetRunning(true); InitBottomProgress(maps.Count); OpenLogWindow();
        _cts = new CancellationTokenSource(); var mpq = new MpqEditor(exePath, baseDir); var progress = new Progress<ProgressReport>(ReportProgress);
        Task.Run(async () =>
        {
            BatchSummary totalSummary = new() { Total = maps.Count };
            if (isInstall) { foreach (var mode in selectedModes!) { var result = await BatchProcessor.RunInstallAsync(maps, _config!, profile!, mode, mpq, progress, _cts.Token); if (result.Aborted) { totalSummary.Aborted = true; break; } totalSummary.Success = Math.Max(totalSummary.Success, result.Success); totalSummary.Failed = Math.Max(totalSummary.Failed, result.Failed); if (result.Cancelled) { totalSummary.Cancelled = true; break; } } }
            else { var result = await BatchProcessor.RunUninstallAsync(maps, _config!, mpq, progress, _cts.Token); totalSummary = result; }
            return totalSummary;
        }).ContinueWith(t => { if (t.IsFaulted) Invoke(() => { AppendLog(t.Exception?.InnerException?.Message ?? t.Exception?.Message ?? "Unknown error"); SetRunning(false); }); else Invoke(() => OnFinished(t.Result)); }, TaskScheduler.Default);
    }

    private void CancelRun() { if (!_running) return; _cts?.Cancel(); }

    private void OnFinished(BatchSummary summary)
    {
        SetRunning(false); if (summary.Aborted) return;
        var total = summary.Total; var msg = summary.Cancelled ? LF("summary_cancelled", summary.Success, summary.Failed, total) : LF("summary_done", summary.Success, summary.Failed, total);
        AppendLog(msg); var icon = summary.Cancelled ? MessageBoxIcon.Information : (summary.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        MessageBox.Show(this, msg, L("app_title"), MessageBoxButtons.OK, icon);
    }

    private void ReportProgress(ProgressReport r)
    {
        switch (r.Kind)
        {
            case ProgressKind.Started: bottomProgress.Maximum = Math.Max(1, r.Total); bottomProgress.Value = 0; lblBottomProgress.Text = $"0/{r.Total}"; AppendLog(r.LogLine); break;
            case ProgressKind.Step: break;
            case ProgressKind.MapOk: case ProgressKind.MapFail: case ProgressKind.MapPermissionFail: if (r.Index >= 0) { bottomProgress.Value = Math.Min(r.Index, bottomProgress.Maximum); lblBottomProgress.Text = $"{r.Index}/{r.Total}"; } AppendLog(r.LogLine); break;
            case ProgressKind.MpqMissing: AppendLog(r.StatusMessage); break;
            case ProgressKind.Completed: bottomProgress.Value = bottomProgress.Maximum; lblBottomProgress.Text = $"{r.Total}/{r.Total}"; AppendLog(r.LogLine); break;
            case ProgressKind.Cancelled: AppendLog(r.LogLine); break;
        }
    }

    private void ResetBottomProgress() { bottomProgress.Maximum = 100; bottomProgress.Value = 0; lblBottomProgress.Text = "0/0"; }
    private void InitBottomProgress(int total) { bottomProgress.Maximum = total; bottomProgress.Value = 0; lblBottomProgress.Text = $"0/{total}"; }
    private void OpenLogWindow() { if (_logForm == null || _logForm.IsDisposed) { _logForm = new(); _logForm.FormClosed += (_, _) => _logForm = null; } _logForm.Show(); }
    private void ShowLogWindow() { OpenLogWindow(); _logForm!.BringToFront(); }
    private void AppendLog(string line) { if (string.IsNullOrEmpty(line)) return; _logForm?.AppendLog(line); }

    private List<string> CollectMapsInFolder(string folder)
    { if (_config == null) return new(); var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var pattern in _config.MapExtensions) { try { foreach (var f in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly)) set.Add(f); } catch { } } return set.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(); }

    private void SetRunning(bool running)
    { _running = running; cboLanguage.Enabled = !running; btnReloadConfig.Enabled = !running; rbInstall.Enabled = !running; rbUninstall.Enabled = !running; var installMode = rbInstall.Checked; var configOk = _config != null && _profileItems.Count >= 2; rbProfileA.Enabled = !running && installMode && configOk; rbProfileB.Enabled = !running && installMode && configOk; clbModes.Enabled = !running && installMode && configOk; btnSingleMap.Enabled = !running && configOk; btnFolder.Enabled = !running && configOk; btnCancel.Visible = running; }

    private void Warn(string msg) => MessageBox.Show(this, msg, L("app_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private static string L(string key) => LanguageManager.Get(key);
    private static string LF(string key, params object?[] args) => LanguageManager.GetFormat(key, args);
    private sealed record LangItem(string Code, string Display) { public override string ToString() => Display; }
    private sealed record ProfileItem(string Key, string DisplayName);
    private sealed record ModeItem(string Key, string DisplayName);
}

// ============================================================================
//  日志窗口
// ============================================================================

public sealed partial class LogForm : Form
{
    private readonly TextBox _txtLog;
    private readonly Button _btnClose;

    public LogForm()
    {
        Text = LanguageManager.Get("log_window_title"); Width = 640; Height = 480;
        StartPosition = FormStartPosition.CenterParent; Font = new("微软雅黑", 9F); MinimumSize = new(480, 360);
        _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new("Consolas", 9F), Dock = DockStyle.Fill, BackColor = SystemColors.Window };
        _btnClose = new() { Text = LanguageManager.Get("log_close"), Size = new(100, 30), Dock = DockStyle.Bottom };
        _btnClose.Click += (_, _) => Close();
        Controls.Add(_txtLog); Controls.Add(_btnClose);
    }

    public void AppendLog(string line)
    {
        if (IsDisposed) return; if (InvokeRequired) { Invoke(new Action<string>(AppendLog), line); return; }
        if (string.IsNullOrEmpty(line)) return; _txtLog.AppendText(line + "\r\n");
        if (_txtLog.Lines.Length > 2000) { var start = _txtLog.GetFirstCharIndexFromLine(500); _txtLog.Select(0, start); _txtLog.SelectedText = string.Empty; }
    }
}
