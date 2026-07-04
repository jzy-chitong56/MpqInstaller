using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace MpqInstaller.Core
{
    // ============================================================================
    //  语言管理
    // ============================================================================

    public static class LanguageManager
    {
        public const string ChineseSimplified = "zh-CN";
        public const string English = "en-US";
        public static readonly (string Code, string DisplayName)[] AvailableLanguages = new[] { (ChineseSimplified, "简体中文"), (English, "English") };
        private static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>();
        private static Dictionary<string, string> _strings = new Dictionary<string, string>();
        private static string _current = English;
        public static string CurrentLanguage { get { return _current; } }

        public static void Initialize(string defaultCode)
        {
            Load(English);
            string code = defaultCode;
            bool found = false;
            for (int i = 0; i < AvailableLanguages.Length; i++)
            {
                if (AvailableLanguages[i].Code == defaultCode) { found = true; break; }
            }
            if (!found) code = English;
            SetLanguage(code);
        }

        public static bool SetLanguage(string code)
        {
            Dictionary<string, string> loaded = Load(code);
            if (loaded == null) return false;
            _strings = loaded;
            _current = code;
            try
            {
                CultureInfo cult = code == ChineseSimplified
                    ? CultureInfo.GetCultureInfo("zh-CN")
                    : CultureInfo.GetCultureInfo("en-US");
                Thread.CurrentThread.CurrentUICulture = cult;
                Thread.CurrentThread.CurrentCulture = cult;
            }
            catch { }
            return true;
        }

        public static string Get(string key)
        {
            string v;
            if (_strings.TryGetValue(key, out v)) return v;
            string fb;
            if (Fallback.TryGetValue(key, out fb)) return fb;
            return key;
        }

        public static string GetFormat(string key, params object[] args)
        {
            string fmt = Get(key);
            try
            {
                if (args == null || args.Length == 0) return fmt;
                return string.Format(CultureInfo.CurrentCulture, fmt, args);
            }
            catch (FormatException) { return fmt; }
        }

        private static Dictionary<string, string> Load(string code)
        {
            string fileName;
            if (code == ChineseSimplified) fileName = "zh-CN.json";
            else if (code == English) fileName = "en-US.json";
            else return null;

            Assembly asm = Assembly.GetExecutingAssembly();
            string[] names = asm.GetManifestResourceNames();
            string resName = null;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith(fileName, StringComparison.Ordinal)) { resName = names[i]; break; }
            }
            if (resName == null) return null;

            using (Stream stream = asm.GetManifestResourceStream(resName))
            {
                if (stream == null) return null;
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    Dictionary<string, string> dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (dict == null) return null;
                    if (code == English)
                    {
                        Fallback.Clear();
                        foreach (KeyValuePair<string, string> kv in dict)
                            Fallback[kv.Key] = kv.Value;
                    }
                    return dict;
                }
            }
        }
    }

    // ============================================================================
    //  配置加载
    // ============================================================================

    public static class ConfigLoader
    {
        public const string DefaultFileName = "config.json";
        public static string DefaultPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultFileName); } }

        public static bool TryLoad(string path, out InstallConfig config, out string errorMessage)
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
                string json = File.ReadAllText(path);
                config = JsonConvert.DeserializeObject<InstallConfig>(json);
                if (config == null)
                {
                    errorMessage = LanguageManager.Get("err_config_empty");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(config.MpqEditorPath)) config.MpqEditorPath = "MPQEditor.exe";
                if (string.IsNullOrWhiteSpace(config.FilesBaseDir)) config.FilesBaseDir = "Files";
                if (config.MapExtensions == null || config.MapExtensions.Length == 0)
                    config.MapExtensions = new string[] { "*.w3x", "*.w3m" };
                if (config.HashTableSize <= 0) config.HashTableSize = 128;
                if (config.Profiles == null) config.Profiles = new Dictionary<string, ProfileConfig>();
                if (config.WarningTexts == null) config.WarningTexts = new Dictionary<string, string>();
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

        public static string GetDisplayName(Dictionary<string, string> dict, string fallback)
        {
            if (dict == null || dict.Count == 0) return fallback;
            string cur = LanguageManager.CurrentLanguage;
            string v;
            if (dict.TryGetValue(cur, out v) && !string.IsNullOrEmpty(v)) return v;
            string en;
            if (dict.TryGetValue(LanguageManager.English, out en) && !string.IsNullOrEmpty(en)) return en;
            foreach (KeyValuePair<string, string> kv in dict)
                if (!string.IsNullOrEmpty(kv.Value)) return kv.Value;
            return fallback;
        }

        public static string GetWarningText(InstallConfig config)
        {
            if (config.WarningTexts == null || config.WarningTexts.Count == 0) return null;
            string cur = LanguageManager.CurrentLanguage;
            string v;
            if (config.WarningTexts.TryGetValue(cur, out v) && !string.IsNullOrEmpty(v)) return v;
            string en;
            if (config.WarningTexts.TryGetValue(LanguageManager.English, out en) && !string.IsNullOrEmpty(en)) return en;
            foreach (KeyValuePair<string, string> kv in config.WarningTexts)
                if (!string.IsNullOrEmpty(kv.Value)) return kv.Value;
            return null;
        }
    }

    // ============================================================================
    //  MPQ 调用 + 安装计划 + 批量执行
    // ============================================================================

    public enum MpqExitCode { Success = 0, PermissionOrUac = 5, OtherError = -1 }

    public struct MpqCommandResult
    {
        public int ExitCode;
        public string Output;

        public MpqExitCode Kind
        {
            get
            {
                switch (ExitCode)
                {
                    case 0: return MpqExitCode.Success;
                    case 5: return MpqExitCode.PermissionOrUac;
                    default: return MpqExitCode.OtherError;
                }
            }
        }
        public bool IsSuccess { get { return ExitCode == 0; } }

        public MpqCommandResult(int exitCode, string output) { ExitCode = exitCode; Output = output; }
    }

    public class MpqEditor
    {
        private readonly string _baseDir;
        public string ExePath { get { return _exePath; } }
        private readonly string _exePath;

        public MpqEditor(string exePath, string baseDir) { _exePath = exePath; _baseDir = baseDir; }

        public static void ResolvePaths(InstallConfig config, out string exePath, out string baseDir)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string exe = Path.IsPathRooted(config.MpqEditorPath)
                ? config.MpqEditorPath
                : Path.Combine(exeDir, config.MpqEditorPath);
            string bdir = Path.IsPathRooted(config.FilesBaseDir)
                ? config.FilesBaseDir
                : Path.Combine(exeDir, config.FilesBaseDir);
            exePath = Path.GetFullPath(exe);
            baseDir = Path.GetFullPath(bdir);
        }

        public bool Exists() { return File.Exists(_exePath); }

        public string ResolveSource(string profileSubDir, string relativeSource)
        {
            List<string> segs = new List<string> { _baseDir };
            if (!string.IsNullOrEmpty(profileSubDir)) segs.Add(profileSubDir);
            segs.Add(relativeSource);
            for (int i = 0; i < segs.Count; i++) segs[i] = segs[i].Replace('/', '\\');
            return string.Join("\\", segs);
        }

        public MpqCommandResult HtSize(string map, int size) { return Run("htsize", map, size.ToString()); }
        public MpqCommandResult Add(string map, string source, string dest) { return Run("a", map, source, dest); }
        public MpqCommandResult Delete(string map, string pathInMpq) { return Run("d", map, pathInMpq); }
        public MpqCommandResult Flush(string map) { return Run("f", map); }

        private MpqCommandResult Run(params string[] args)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = _exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.Default,
                StandardErrorEncoding = System.Text.Encoding.Default,
            };
            List<string> quoted = new List<string>();
            foreach (string a in args)
            {
                if (a.Contains(" ")) quoted.Add("\"" + a + "\"");
                else quoted.Add(a);
            }
            psi.Arguments = string.Join(" ", quoted);

            using (Process p = new Process { StartInfo = psi })
            {
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                string output = string.IsNullOrEmpty(stderr) ? stdout : stdout + Environment.NewLine + stderr;
                return new MpqCommandResult(p.ExitCode, (output ?? string.Empty).Trim());
            }
        }
    }

    public enum ActionType { HtSize, Add, Delete, Flush }

    public struct InstallAction
    {
        public ActionType Type;
        public string Map;
        public string Source;
        public string Dest;
        public int Size;
        public string LogMessage;

        public InstallAction(ActionType type, string map, string source, string dest, int size, string logMessage)
        { Type = type; Map = map; Source = source; Dest = dest; Size = size; LogMessage = logMessage; }
    }

    public static class InstallPlanBuilder
    {
        public static List<InstallAction> BuildInstall(MpqEditor mpq, InstallConfig config, ProfileConfig profile, ModeConfig mode, string map)
        {
            List<InstallAction> plan = new List<InstallAction>();
            plan.Add(new InstallAction(ActionType.HtSize, map, string.Empty, string.Empty, config.HashTableSize,
                LanguageManager.GetFormat("step_htsize", config.HashTableSize)));
            if (profile.BaseActions != null)
                foreach (WriteActionConfig a in profile.BaseActions)
                {
                    string src = mpq.ResolveSource(profile.SubDir, a.Source);
                    plan.Add(new InstallAction(ActionType.Add, map, src, a.Dest, 0,
                        LanguageManager.GetFormat("step_add", a.Source, a.Dest)));
                }
            if (mode.ExtraActions != null)
                foreach (WriteActionConfig a in mode.ExtraActions)
                {
                    string src = mpq.ResolveSource(profile.SubDir, a.Source);
                    plan.Add(new InstallAction(ActionType.Add, map, src, a.Dest, 0,
                        LanguageManager.GetFormat("step_add", a.Source, a.Dest)));
                }
            plan.Add(new InstallAction(ActionType.Flush, map, string.Empty, string.Empty, 0, LanguageManager.Get("step_flush")));
            return plan;
        }

        public static List<InstallAction> BuildUninstall(InstallConfig config, string map)
        {
            List<InstallAction> plan = new List<InstallAction>();
            if (config.UninstallFiles != null)
                foreach (string f in config.UninstallFiles)
                    plan.Add(new InstallAction(ActionType.Delete, map, string.Empty, f, 0,
                        LanguageManager.GetFormat("step_delete", f)));
            plan.Add(new InstallAction(ActionType.Flush, map, string.Empty, string.Empty, 0, LanguageManager.Get("step_flush")));
            return plan;
        }
    }

    public enum ProgressKind { Started, Step, MapOk, MapFail, MapPermissionFail, MpqMissing, Completed, Cancelled }

    public struct ProgressReport
    {
        public ProgressKind Kind;
        public int Index;
        public int Total;
        public string MapName;
        public string StatusMessage;
        public string LogLine;

        public ProgressReport(ProgressKind kind, int index, int total, string mapName, string statusMessage, string logLine)
        { Kind = kind; Index = index; Total = total; MapName = mapName; StatusMessage = statusMessage; LogLine = logLine; }
    }

    public class BatchSummary
    {
        public int Total;
        public int Success;
        public int Failed;
        public bool Cancelled;
        public bool Aborted;
        public List<string> Failures = new List<string>();
    }

    public static class BatchProcessor
    {
        public static async Task<BatchSummary> RunInstallAsync(
            IList<string> maps, InstallConfig config, ProfileConfig profile, ModeConfig mode,
            MpqEditor mpq, IProgress<ProgressReport> progress, CancellationToken ct)
        {
            BatchSummary summary = new BatchSummary { Total = maps.Count };
            if (!mpq.Exists())
            {
                Report(progress, ProgressKind.MpqMissing, 0, maps.Count, string.Empty,
                    LanguageManager.GetFormat("err_mpq_missing", mpq.ExePath), string.Empty);
                summary.Aborted = true;
                return summary;
            }
            Report(progress, ProgressKind.Started, 0, maps.Count, string.Empty,
                LanguageManager.GetFormat("status_started", maps.Count),
                LanguageManager.GetFormat("log_started", maps.Count));
            try
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    string map = maps[i];
                    string mapName = Path.GetFileName(map);
                    int idx = i + 1;
                    List<InstallAction> plan = InstallPlanBuilder.BuildInstall(mpq, config, profile, mode, map);
                    bool permFail = false;
                    int lastNonZero = 0;
                    string failOut = string.Empty;
                    foreach (InstallAction action in plan)
                    {
                        ct.ThrowIfCancellationRequested();
                        Report(progress, ProgressKind.Step, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_step", idx, maps.Count, mapName, action.LogMessage), string.Empty);
                        MpqCommandResult r = ExecuteAction(mpq, action);
                        if (!r.IsSuccess)
                        {
                            if (r.Kind == MpqExitCode.PermissionOrUac) permFail = true;
                            lastNonZero = r.ExitCode;
                            if (!string.IsNullOrEmpty(r.Output)) failOut = r.Output;
                        }
                    }
                    if (permFail)
                    {
                        summary.Failed++;
                        string line = LanguageManager.GetFormat("log_map_permission_fail", idx, maps.Count, mapName);
                        summary.Failures.Add(line);
                        Report(progress, ProgressKind.MapPermissionFail, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                            line + "  " + LanguageManager.Get("reason_permission"));
                    }
                    else if (lastNonZero != 0)
                    {
                        summary.Failed++;
                        string line = LanguageManager.GetFormat("log_map_fail", idx, maps.Count, mapName, lastNonZero);
                        summary.Failures.Add(line);
                        Report(progress, ProgressKind.MapFail, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                            line + (string.IsNullOrEmpty(failOut) ? string.Empty : "  " + failOut));
                    }
                    else
                    {
                        summary.Success++;
                        Report(progress, ProgressKind.MapOk, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_map_ok", idx, maps.Count, mapName),
                            LanguageManager.GetFormat("log_map_ok", idx, maps.Count, mapName));
                    }
                    await Task.Yield();
                }
                Report(progress, ProgressKind.Completed, maps.Count, maps.Count, string.Empty,
                    LanguageManager.GetFormat("status_completed", summary.Success, summary.Failed),
                    LanguageManager.GetFormat("log_completed", summary.Success, summary.Failed));
            }
            catch (OperationCanceledException)
            {
                summary.Cancelled = true;
                Report(progress, ProgressKind.Cancelled, summary.Success + summary.Failed, maps.Count, string.Empty,
                    LanguageManager.GetFormat("status_cancelled", summary.Success, summary.Failed),
                    LanguageManager.GetFormat("log_cancelled", summary.Success, summary.Failed));
            }
            return summary;
        }

        public static async Task<BatchSummary> RunUninstallAsync(
            IList<string> maps, InstallConfig config, MpqEditor mpq,
            IProgress<ProgressReport> progress, CancellationToken ct)
        {
            BatchSummary summary = new BatchSummary { Total = maps.Count };
            if (!mpq.Exists())
            {
                Report(progress, ProgressKind.MpqMissing, 0, maps.Count, string.Empty,
                    LanguageManager.GetFormat("err_mpq_missing", mpq.ExePath), string.Empty);
                summary.Aborted = true;
                return summary;
            }
            Report(progress, ProgressKind.Started, 0, maps.Count, string.Empty,
                LanguageManager.GetFormat("status_started", maps.Count),
                LanguageManager.GetFormat("log_started", maps.Count));
            try
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    string map = maps[i];
                    string mapName = Path.GetFileName(map);
                    int idx = i + 1;
                    List<InstallAction> plan = InstallPlanBuilder.BuildUninstall(config, map);
                    bool permFail = false;
                    int lastNonZero = 0;
                    string failOut = string.Empty;
                    foreach (InstallAction action in plan)
                    {
                        ct.ThrowIfCancellationRequested();
                        Report(progress, ProgressKind.Step, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_step", idx, maps.Count, mapName, action.LogMessage), string.Empty);
                        MpqCommandResult r = ExecuteAction(mpq, action);
                        if (!r.IsSuccess)
                        {
                            if (r.Kind == MpqExitCode.PermissionOrUac) permFail = true;
                            lastNonZero = r.ExitCode;
                            if (!string.IsNullOrEmpty(r.Output)) failOut = r.Output;
                        }
                    }
                    if (permFail)
                    {
                        summary.Failed++;
                        string line = LanguageManager.GetFormat("log_map_permission_fail", idx, maps.Count, mapName);
                        summary.Failures.Add(line);
                        Report(progress, ProgressKind.MapPermissionFail, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                            line + "  " + LanguageManager.Get("reason_permission"));
                    }
                    else if (lastNonZero != 0)
                    {
                        summary.Failed++;
                        string line = LanguageManager.GetFormat("log_map_fail", idx, maps.Count, mapName, lastNonZero);
                        summary.Failures.Add(line);
                        Report(progress, ProgressKind.MapFail, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                            line + (string.IsNullOrEmpty(failOut) ? string.Empty : "  " + failOut));
                    }
                    else
                    {
                        summary.Success++;
                        Report(progress, ProgressKind.MapOk, idx, maps.Count, mapName,
                            LanguageManager.GetFormat("status_map_ok", idx, maps.Count, mapName),
                            LanguageManager.GetFormat("log_map_ok", idx, maps.Count, mapName));
                    }
                    await Task.Yield();
                }
                Report(progress, ProgressKind.Completed, maps.Count, maps.Count, string.Empty,
                    LanguageManager.GetFormat("status_completed", summary.Success, summary.Failed),
                    LanguageManager.GetFormat("log_completed", summary.Success, summary.Failed));
            }
            catch (OperationCanceledException)
            {
                summary.Cancelled = true;
                Report(progress, ProgressKind.Cancelled, summary.Success + summary.Failed, maps.Count, string.Empty,
                    LanguageManager.GetFormat("status_cancelled", summary.Success, summary.Failed),
                    LanguageManager.GetFormat("log_cancelled", summary.Success, summary.Failed));
            }
            return summary;
        }

        private static void Report(IProgress<ProgressReport> progress, ProgressKind kind, int idx, int total, string mapName, string status, string log)
        {
            if (progress != null) progress.Report(new ProgressReport(kind, idx, total, mapName, status, log));
        }

        private static MpqCommandResult ExecuteAction(MpqEditor mpq, InstallAction a)
        {
            switch (a.Type)
            {
                case ActionType.HtSize: return mpq.HtSize(a.Map, a.Size);
                case ActionType.Add: return mpq.Add(a.Map, a.Source, a.Dest);
                case ActionType.Delete: return mpq.Delete(a.Map, a.Dest);
                case ActionType.Flush: return mpq.Flush(a.Map);
                default: return new MpqCommandResult(0, string.Empty);
            }
        }
    }

    // ============================================================================
    //  主窗体
    // ============================================================================

    public sealed class MainForm : Form
    {
        private System.ComponentModel.IContainer components = null;
        private Panel pnlTitle;
        private ComboBox cboLanguage;
        private Label lblOperation;
        private RadioButton rbInstall;
        private RadioButton rbUninstall;
        private Panel pnlSep1;
        private Label lblProfile;
        private RadioButton rbProfileA;
        private RadioButton rbProfileB;
        private Panel pnlSep2;
        private Label lblMode;
        private RadioButton[] rbModes;
        private Panel pnlSep3;
        private Button btnSingleMap;
        private Button btnFolder;
        private Button btnCancel;
        private Button btnShowLog;

        private bool _initializing = true;
        private bool _running;
        private CancellationTokenSource _cts;
        private InstallConfig _config;
        private string _configError = string.Empty;
        private List<ProfileItem> _profileItems = new List<ProfileItem>();
        private List<ModeItem> _modeItems = new List<ModeItem>();
        private LogForm _logForm;

        public MainForm()
        {
            InitializeComponent();
            InitLanguageCombo();
            LoadConfig();
            ApplyLanguage();
            _initializing = false;
            WireEvents();
            UpdateUiEnabledState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            pnlTitle = new Panel();
            cboLanguage = new ComboBox();
            lblOperation = new Label();
            rbInstall = new RadioButton();
            rbUninstall = new RadioButton();
            pnlSep1 = new Panel();
            lblProfile = new Label();
            rbProfileA = new RadioButton();
            rbProfileB = new RadioButton();
            pnlSep2 = new Panel();
            lblMode = new Label();
            pnlSep3 = new Panel();
            btnSingleMap = new Button();
            btnFolder = new Button();
            btnCancel = new Button();
            btnShowLog = new Button();
            SuspendLayout();

            pnlTitle.BackColor = Color.FromArgb(0, 120, 215);
            pnlTitle.Dock = DockStyle.Top;
            pnlTitle.Size = new Size(420, 40);
            cboLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLanguage.FlatStyle = FlatStyle.Flat;
            cboLanguage.Font = new Font("微软雅黑", 9F);
            cboLanguage.Location = new Point(316, 8);
            cboLanguage.Size = new Size(90, 25);
            pnlTitle.Controls.Add(cboLanguage);

            lblOperation.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            lblOperation.ForeColor = Color.FromArgb(50, 50, 50);
            lblOperation.Location = new Point(12, 50);
            lblOperation.Size = new Size(120, 20);
            rbInstall.Font = new Font("微软雅黑", 9F);
            rbInstall.Location = new Point(24, 77);
            rbInstall.AutoSize = true;
            rbInstall.Checked = true;
            rbUninstall.Font = new Font("微软雅黑", 9F);
            rbUninstall.Location = new Point(230, 77);
            rbUninstall.AutoSize = true;
            pnlSep1.BackColor = Color.FromArgb(225, 225, 225);
            pnlSep1.Location = new Point(12, 110);
            pnlSep1.Size = new Size(396, 1);

            lblProfile.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            lblProfile.ForeColor = Color.FromArgb(50, 50, 50);
            lblProfile.Location = new Point(12, 120);
            lblProfile.Size = new Size(120, 20);
            rbProfileA.Font = new Font("微软雅黑", 9F);
            rbProfileA.Location = new Point(24, 147);
            rbProfileA.AutoSize = true;
            rbProfileA.Checked = true;
            rbProfileB.Font = new Font("微软雅黑", 9F);
            rbProfileB.Location = new Point(230, 147);
            rbProfileB.AutoSize = true;
            pnlSep2.BackColor = Color.FromArgb(225, 225, 225);
            pnlSep2.Location = new Point(12, 180);
            pnlSep2.Size = new Size(396, 1);

            lblMode.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            lblMode.ForeColor = Color.FromArgb(50, 50, 50);
            lblMode.Location = new Point(12, 190);
            lblMode.Size = new Size(120, 20);
            pnlSep3.BackColor = Color.FromArgb(225, 225, 225);
            pnlSep3.Location = new Point(12, 310);
            pnlSep3.Size = new Size(396, 1);

            btnSingleMap.Font = new Font("微软雅黑", 11F);
            btnSingleMap.Location = new Point(12, 328);
            btnSingleMap.Size = new Size(195, 48);
            btnSingleMap.FlatStyle = FlatStyle.System;
            btnFolder.Font = new Font("微软雅黑", 11F);
            btnFolder.Location = new Point(213, 328);
            btnFolder.Size = new Size(195, 48);
            btnFolder.FlatStyle = FlatStyle.System;
            btnCancel.Font = new Font("微软雅黑", 9F);
            btnCancel.Location = new Point(12, 390);
            btnCancel.Size = new Size(80, 26);
            btnCancel.Visible = false;
            btnShowLog.Font = new Font("微软雅黑", 9F);
            btnShowLog.ForeColor = Color.FromArgb(0, 120, 215);
            btnShowLog.FlatStyle = FlatStyle.Flat;
            btnShowLog.Location = new Point(320, 390);
            btnShowLog.Size = new Size(88, 26);

            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(420, 450);
            Controls.Add(pnlTitle);
            Controls.Add(lblOperation);
            Controls.Add(rbInstall);
            Controls.Add(rbUninstall);
            Controls.Add(pnlSep1);
            Controls.Add(lblProfile);
            Controls.Add(rbProfileA);
            Controls.Add(rbProfileB);
            Controls.Add(pnlSep2);
            Controls.Add(lblMode);
            Controls.Add(pnlSep3);
            Controls.Add(btnSingleMap);
            Controls.Add(btnFolder);
            Controls.Add(btnCancel);
            Controls.Add(btnShowLog);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(420, 450);
            MaximumSize = new Size(420, 450);
            ResumeLayout(false);
            PerformLayout();
        }

        private void InitLanguageCombo()
        {
            cboLanguage.Items.Clear();
            for (int i = 0; i < LanguageManager.AvailableLanguages.Length; i++)
            {
                var l = LanguageManager.AvailableLanguages[i];
                cboLanguage.Items.Add(new LangItem(l.Code, l.DisplayName));
            }
            SelectLanguageCombo(LanguageManager.CurrentLanguage);
        }

        private void SelectLanguageCombo(string code)
        {
            for (int i = 0; i < cboLanguage.Items.Count; i++)
            {
                LangItem item = (LangItem)cboLanguage.Items[i];
                if (item.Code == code) { cboLanguage.SelectedIndex = i; return; }
            }
            if (cboLanguage.Items.Count > 0) cboLanguage.SelectedIndex = 0;
        }

        private void ApplyLanguage()
        {
            Text = L("app_title");
            lblOperation.Text = L("lbl_operation");
            rbInstall.Text = L("op_install");
            rbUninstall.Text = L("op_uninstall");
            lblProfile.Text = L("lbl_profile");
            lblMode.Text = L("lbl_mode");
            btnSingleMap.Text = L("btn_single_map");
            btnFolder.Text = L("btn_folder");
            btnShowLog.Text = L("btn_show_log");
            btnCancel.Text = L("btn_cancel");
        }

        private void WireEvents()
        {
            cboLanguage.SelectedIndexChanged += (s, e) =>
            {
                if (_initializing) return;
                LangItem item = cboLanguage.SelectedItem as LangItem;
                if (item != null)
                {
                    LanguageManager.SetLanguage(item.Code);
                    ApplyLanguage();
                    RefreshProfileAndModeDisplay();
                }
            };
            rbInstall.CheckedChanged += (s, e) => { if (rbInstall.Checked) UpdateUiEnabledState(); };
            rbUninstall.CheckedChanged += (s, e) => { if (rbUninstall.Checked) UpdateUiEnabledState(); };
            rbProfileA.CheckedChanged += (s, e) => { if (_initializing || !rbProfileA.Checked) return; RefreshModesFromCurrentProfile(); };
            rbProfileB.CheckedChanged += (s, e) => { if (_initializing || !rbProfileB.Checked) return; RefreshModesFromCurrentProfile(); };
            btnSingleMap.Click += (s, e) => RunInstall(false);
            btnFolder.Click += (s, e) => RunInstall(true);
            btnCancel.Click += (s, e) => CancelRun();
            btnShowLog.Click += (s, e) => ShowLogWindow();
        }

        private void UpdateUiEnabledState()
        {
            bool installMode = rbInstall.Checked;
            bool configOk = _config != null && _profileItems.Count >= 2;
            rbProfileA.Enabled = installMode && configOk;
            rbProfileB.Enabled = installMode && configOk;
            EnableModes(installMode && configOk);
            btnSingleMap.Enabled = !_running && configOk;
            btnFolder.Enabled = !_running && configOk;
            btnCancel.Visible = _running;
        }

        private void EnableModes(bool enabled)
        {
            if (rbModes != null)
                foreach (RadioButton rb in rbModes) rb.Enabled = enabled;
        }

        private void LoadConfig()
        {
            _profileItems.Clear();
            _modeItems.Clear();
            InstallConfig cfg;
            string err;
            if (ConfigLoader.TryLoad(ConfigLoader.DefaultPath, out cfg, out err))
            {
                _config = cfg;
                _configError = string.Empty;
            }
            else { _config = null; _configError = err; return; }
            foreach (KeyValuePair<string, ProfileConfig> kv in cfg.Profiles)
                _profileItems.Add(new ProfileItem(kv.Key, ConfigLoader.GetDisplayName(kv.Value.DisplayName, kv.Key)));
            if (_profileItems.Count < 2) { _config = null; return; }
            if (!_initializing) RefreshProfileAndModeDisplay();
        }

        private void RefreshProfileAndModeDisplay()
        {
            if (_config == null || _profileItems.Count < 2)
            { rbProfileA.Text = "—"; rbProfileB.Text = "—"; ClearModes(); return; }
            for (int i = 0; i < _profileItems.Count; i++)
            {
                ProfileConfig kv = _config.Profiles[_profileItems[i].Key];
                string display = ConfigLoader.GetDisplayName(kv.DisplayName, _profileItems[i].Key);
                _profileItems[i] = new ProfileItem(_profileItems[i].Key, display);
            }
            rbProfileA.Text = _profileItems[0].DisplayName;
            rbProfileB.Text = _profileItems[1].DisplayName;
            if (!rbProfileA.Checked && !rbProfileB.Checked) rbProfileA.Checked = true;
            RefreshModesFromCurrentProfile();
        }

        private void RefreshModesFromCurrentProfile()
        {
            ClearModes();
            if (_config == null || _profileItems.Count < 2) return;
            string profileKey = rbProfileA.Checked ? _profileItems[0].Key : _profileItems[1].Key;
            ProfileConfig profile;
            if (!_config.Profiles.TryGetValue(profileKey, out profile)) return;
            List<KeyValuePair<string, ModeConfig>> modes = profile.Modes.ToList();
            if (modes.Count == 0) return;
            _modeItems.Clear();
            foreach (KeyValuePair<string, ModeConfig> kv in modes)
            {
                string display = ConfigLoader.GetDisplayName(kv.Value.DisplayName, kv.Key);
                _modeItems.Add(new ModeItem(kv.Key, display));
            }
            rbModes = new RadioButton[modes.Count];
            int y = 217;
            for (int i = 0; i < modes.Count; i++)
            {
                rbModes[i] = new RadioButton();
                rbModes[i].Font = new Font("微软雅黑", 9F);
                rbModes[i].Location = new Point((i % 2 == 0 ? 24 : 230), y);
                rbModes[i].AutoSize = true;
                rbModes[i].Text = _modeItems[i].DisplayName;
                if (i % 2 == 1) y += 26;
                Controls.Add(rbModes[i]);
            }
            if (rbModes.Length > 0) rbModes[0].Checked = true;
        }

        private void ClearModes()
        {
            if (rbModes != null)
            {
                foreach (RadioButton rb in rbModes) Controls.Remove(rb);
                rbModes = null;
            }
        }

        private ModeConfig GetSelectedMode()
        {
            if (rbModes == null || _modeItems.Count == 0) return null;
            string profileKey = rbProfileA.Checked ? _profileItems[0].Key : _profileItems[1].Key;
            ProfileConfig profile;
            if (!_config.Profiles.TryGetValue(profileKey, out profile)) return null;
            for (int i = 0; i < rbModes.Length; i++)
            {
                if (rbModes[i].Checked)
                {
                    ModeConfig m;
                    if (profile.Modes.TryGetValue(_modeItems[i].Key, out m)) return m;
                }
            }
            return null;
        }

        private void RunInstall(bool batchMode)
        {
            if (_running || _config == null) return;
            bool isInstall = rbInstall.Checked;
            List<string> maps;
            if (batchMode)
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = L("dlg_select_folder");
                    fbd.ShowNewFolderButton = false;
                    if (fbd.ShowDialog(this) != DialogResult.OK) return;
                    maps = CollectMapsInFolder(fbd.SelectedPath);
                    if (maps.Count == 0) { Warn(L("err_no_maps_in_folder")); return; }
                }
            }
            else
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = L("dlg_select_map");
                    ofd.Filter = L("filter_maps");
                    ofd.CheckFileExists = true;
                    if (ofd.ShowDialog(this) != DialogResult.OK) return;
                    maps = new List<string> { ofd.FileName };
                }
            }

            string exePath, baseDir;
            MpqEditor.ResolvePaths(_config, out exePath, out baseDir);
            if (!File.Exists(exePath)) { Warn(LF("err_mpq_missing", exePath)); return; }

            ProfileConfig profile = null;
            ModeConfig selectedMode = null;
            if (isInstall)
            {
                string profileKey = rbProfileA.Checked ? _profileItems[0].Key : _profileItems[1].Key;
                if (!_config.Profiles.TryGetValue(profileKey, out profile)) { Warn(L("err_profile_invalid")); return; }
                selectedMode = GetSelectedMode();
                if (selectedMode == null) { Warn(L("err_no_modes_checked")); return; }
            }
            else
            {
                if (_config.UninstallFiles == null || _config.UninstallFiles.Length == 0)
                { Warn(L("err_config_empty") + " UninstallFiles"); return; }
            }

            string warningText = ConfigLoader.GetWarningText(_config);
            if (string.IsNullOrEmpty(warningText))
                warningText = isInstall ? L("warn_default_install") : L("warn_default_uninstall");
            if (MessageBox.Show(this, warningText, L("warn_title"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            SetRunning(true);
            OpenLogWindow();
            _cts = new CancellationTokenSource();
            MpqEditor mpq = new MpqEditor(exePath, baseDir);
            Progress<ProgressReport> progress = new Progress<ProgressReport>(ReportProgress);
            Task<BatchSummary> task = Task.Run(async () =>
            {
                BatchSummary totalSummary = new BatchSummary { Total = maps.Count };
                if (isInstall)
                {
                    BatchSummary result = await BatchProcessor.RunInstallAsync(maps, _config, profile, selectedMode, mpq, progress, _cts.Token);
                    totalSummary = result;
                }
                else
                {
                    BatchSummary result = await BatchProcessor.RunUninstallAsync(maps, _config, mpq, progress, _cts.Token);
                    totalSummary = result;
                }
                return totalSummary;
            });
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Invoke((Action)(() =>
                    {
                        Exception ex = t.Exception;
                        string msg = ex != null && ex.InnerException != null ? ex.InnerException.Message : (ex != null ? ex.Message : "Unknown error");
                        AppendLog(msg);
                        SetRunning(false);
                    }));
                else
                    Invoke((Action)(() => OnFinished(t.Result)));
            }, TaskScheduler.Default);
        }

        private void CancelRun() { if (!_running) return; if (_cts != null) _cts.Cancel(); }

        private void OnFinished(BatchSummary summary)
        {
            SetRunning(false);
            if (summary.Aborted) return;
            int total = summary.Total;
            string msg = summary.Cancelled
                ? LF("summary_cancelled", summary.Success, summary.Failed, total)
                : LF("summary_done", summary.Success, summary.Failed, total);
            AppendLog(msg);
            MessageBoxIcon icon = summary.Cancelled ? MessageBoxIcon.Information
                : (summary.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            MessageBox.Show(this, msg, L("app_title"), MessageBoxButtons.OK, icon);
        }

        private void ReportProgress(ProgressReport r)
        {
            switch (r.Kind)
            {
                case ProgressKind.Started: AppendLog(r.LogLine); break;
                case ProgressKind.Step: break;
                case ProgressKind.MapOk:
                case ProgressKind.MapFail:
                case ProgressKind.MapPermissionFail: AppendLog(r.LogLine); break;
                case ProgressKind.MpqMissing: AppendLog(r.StatusMessage); break;
                case ProgressKind.Completed: AppendLog(r.LogLine); break;
                case ProgressKind.Cancelled: AppendLog(r.LogLine); break;
            }
        }

        private void OpenLogWindow()
        {
            if (_logForm == null || _logForm.IsDisposed)
            {
                _logForm = new LogForm();
                _logForm.FormClosed += (s, e) => _logForm = null;
            }
            _logForm.Show();
        }

        private void ShowLogWindow() { OpenLogWindow(); _logForm.BringToFront(); }
        private void AppendLog(string line) { if (string.IsNullOrEmpty(line)) return; if (_logForm != null) _logForm.AppendLog(line); }

        private List<string> CollectMapsInFolder(string folder)
        {
            if (_config == null) return new List<string>();
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pattern in _config.MapExtensions)
            {
                try
                {
                    foreach (string f in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
                        set.Add(f);
                }
                catch { }
            }
            return set.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void SetRunning(bool running)
        {
            _running = running;
            cboLanguage.Enabled = !running;
            rbInstall.Enabled = !running;
            rbUninstall.Enabled = !running;
            bool installMode = rbInstall.Checked;
            bool configOk = _config != null && _profileItems.Count >= 2;
            rbProfileA.Enabled = !running && installMode && configOk;
            rbProfileB.Enabled = !running && installMode && configOk;
            EnableModes(!running && installMode && configOk);
            btnSingleMap.Enabled = !running && configOk;
            btnFolder.Enabled = !running && configOk;
            btnCancel.Visible = running;
        }

        private void Warn(string msg) { MessageBox.Show(this, msg, L("app_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        private static string L(string key) { return LanguageManager.Get(key); }
        private static string LF(string key, params object[] args) { return LanguageManager.GetFormat(key, args); }

        private class LangItem
        {
            public string Code;
            public string Display;
            public LangItem(string code, string display) { Code = code; Display = display; }
            public override string ToString() { return Display; }
        }

        private class ProfileItem
        {
            public string Key;
            public string DisplayName;
            public ProfileItem(string key, string displayName) { Key = key; DisplayName = displayName; }
        }

        private class ModeItem
        {
            public string Key;
            public string DisplayName;
            public ModeItem(string key, string displayName) { Key = key; DisplayName = displayName; }
        }
    }

    // ============================================================================
    //  日志窗口
    // ============================================================================

    public sealed class LogForm : Form
    {
        private readonly TextBox _txtLog;
        private readonly Button _btnClose;

        public LogForm()
        {
            Text = LanguageManager.Get("log_window_title");
            Width = 640;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("微软雅黑", 9F);
            MinimumSize = new Size(480, 360);
            _txtLog = new TextBox();
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Both;
            _txtLog.WordWrap = false;
            _txtLog.Font = new Font("Consolas", 9F);
            _txtLog.Dock = DockStyle.Fill;
            _txtLog.BackColor = SystemColors.Window;
            _btnClose = new Button();
            _btnClose.Text = LanguageManager.Get("log_close");
            _btnClose.Size = new Size(100, 30);
            _btnClose.Dock = DockStyle.Bottom;
            _btnClose.Click += (s, e) => Close();
            Controls.Add(_txtLog);
            Controls.Add(_btnClose);
        }

        public void AppendLog(string line)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(new Action<string>(AppendLog), line); return; }
            if (string.IsNullOrEmpty(line)) return;
            _txtLog.AppendText(line + "\r\n");
            if (_txtLog.Lines.Length > 2000)
            {
                int start = _txtLog.GetFirstCharIndexFromLine(500);
                _txtLog.Select(0, start);
                _txtLog.SelectedText = string.Empty;
            }
        }
    }
}
