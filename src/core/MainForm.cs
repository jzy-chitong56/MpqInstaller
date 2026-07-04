using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MpqInstaller.Core;

public partial class MainForm : Form
{
    private bool _initializing = true;
    private bool _running;
    private CancellationTokenSource? _cts;
    private InstallConfig? _config;
    private string _configError = string.Empty;

    private List<ProfileItem> _profileItems = new();
    private List<ModeItem> _modeItems = new();

    private LogForm? _logForm;

    public MainForm()
    {
        InitializeComponent();
        InitLanguageCombo();
        LoadConfig();
        ApplyLanguage();
        _initializing = false;

        WireEvents();
        UpdateUiEnabledState();
        ResetBottomProgress();
    }

    private void InitLanguageCombo()
    {
        cboLanguage.Items.Clear();
        foreach (var (code, display) in LanguageManager.AvailableLanguages)
            cboLanguage.Items.Add(new LangItem(code, display));
        SelectLanguageCombo(LanguageManager.CurrentLanguage);
    }

    private void SelectLanguageCombo(string code)
    {
        for (var i = 0; i < cboLanguage.Items.Count; i++)
        {
            if (((LangItem)cboLanguage.Items[i]!).Code == code)
            {
                cboLanguage.SelectedIndex = i;
                return;
            }
        }
        if (cboLanguage.Items.Count > 0) cboLanguage.SelectedIndex = 0;
    }

    private void ApplyLanguage()
    {
        Text = L("app_title");

        lblTitle.Text = L("app_title");
        lblTitleVersion.Text = L("title_version");

        lblConfigStatus.Text = L("lbl_config_status") + (_config != null ? L("config_loaded_ok") : L("config_load_failed"));
        btnReloadConfig.Text = L("btn_reload_config");

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
        cboLanguage.SelectedIndexChanged += (_, _) =>
        {
            if (_initializing) return;
            if (cboLanguage.SelectedItem is LangItem item)
            {
                LanguageManager.SetLanguage(item.Code);
                ApplyLanguage();
                RefreshProfileAndModeDisplay();
            }
        };

        btnReloadConfig.Click += (_, _) =>
        {
            LoadConfig();
            RefreshProfileAndModeDisplay();
            UpdateUiEnabledState();
            lblConfigStatus.Text = L("lbl_config_status") + (_config != null ? L("config_loaded_ok") : L("config_load_failed"));
        };

        rbInstall.CheckedChanged += (_, _) => { if (rbInstall.Checked) UpdateUiEnabledState(); };
        rbUninstall.CheckedChanged += (_, _) => { if (rbUninstall.Checked) UpdateUiEnabledState(); };

        rbProfileA.CheckedChanged += (_, _) =>
        {
            if (_initializing || !rbProfileA.Checked) return;
            RefreshModesFromCurrentProfile();
        };
        rbProfileB.CheckedChanged += (_, _) =>
        {
            if (_initializing || !rbProfileB.Checked) return;
            RefreshModesFromCurrentProfile();
        };

        btnSingleMap.Click += (_, _) => RunInstall(false);
        btnFolder.Click += (_, _) => RunInstall(true);
        btnCancel.Click += (_, _) => CancelRun();
        btnShowLog.Click += (_, _) => ShowLogWindow();
    }

    private void UpdateUiEnabledState()
    {
        var installMode = rbInstall.Checked;
        var configOk = _config != null && _profileItems.Count >= 2;

        rbProfileA.Enabled = installMode && configOk;
        rbProfileB.Enabled = installMode && configOk;
        clbModes.Enabled = installMode && configOk;

        btnSingleMap.Enabled = !_running && configOk;
        btnFolder.Enabled = !_running && configOk;
        btnCancel.Visible = _running;
    }

    private void LoadConfig()
    {
        _profileItems.Clear();
        _modeItems.Clear();

        if (ConfigLoader.TryLoad(ConfigLoader.DefaultPath, out var cfg, out var err))
        {
            _config = cfg;
            _configError = string.Empty;
        }
        else
        {
            _config = null;
            _configError = err;
            return;
        }

        foreach (var kv in cfg!.Profiles)
        {
            _profileItems.Add(new ProfileItem(
                kv.Key,
                ConfigLoader.GetDisplayName(kv.Value.DisplayName, kv.Key)));
        }

        if (_profileItems.Count < 2)
        {
            _config = null;
            return;
        }

        if (!_initializing)
            RefreshProfileAndModeDisplay();
    }

    private void RefreshProfileAndModeDisplay()
    {
        if (_config == null || _profileItems.Count < 2)
        {
            rbProfileA.Text = "—";
            rbProfileB.Text = "—";
            clbModes.Items.Clear();
            return;
        }

        for (var i = 0; i < _profileItems.Count; i++)
        {
            var kv = _config.Profiles[_profileItems[i].Key];
            var display = ConfigLoader.GetDisplayName(kv.DisplayName, _profileItems[i].Key);
            _profileItems[i] = _profileItems[i] with { DisplayName = display };
        }

        rbProfileA.Text = _profileItems[0].DisplayName;
        rbProfileB.Text = _profileItems[1].DisplayName;

        if (!rbProfileA.Checked && !rbProfileB.Checked)
            rbProfileA.Checked = true;

        RefreshModesFromCurrentProfile();
    }

    private void RefreshModesFromCurrentProfile()
    {
        clbModes.Items.Clear();
        _modeItems.Clear();

        if (_config == null || _profileItems.Count < 2)
            return;

        var profileKey = rbProfileA.Checked ? _profileItems[0].Key : _profileItems[1].Key;
        if (!_config.Profiles.TryGetValue(profileKey, out var profile))
            return;

        foreach (var kv in profile.Modes)
        {
            var display = ConfigLoader.GetDisplayName(kv.Value.DisplayName, kv.Key);
            _modeItems.Add(new ModeItem(kv.Key, display));
            clbModes.Items.Add(display);
        }

        for (var i = 0; i < clbModes.Items.Count; i++)
            clbModes.SetItemChecked(i, true);
    }

    private void RunInstall(bool batchMode)
    {
        if (_running || _config == null) return;

        var isInstall = rbInstall.Checked;

        List<string>? maps;
        if (batchMode)
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = L("dlg_select_folder"),
                ShowNewFolderButton = false,
            };
            if (fbd.ShowDialog(this) != DialogResult.OK) return;
            maps = CollectMapsInFolder(fbd.SelectedPath);
            if (maps.Count == 0)
            {
                Warn(L("err_no_maps_in_folder"));
                return;
            }
        }
        else
        {
            using var ofd = new OpenFileDialog
            {
                Title = L("dlg_select_map"),
                Filter = L("filter_maps"),
                CheckFileExists = true,
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            maps = new List<string> { ofd.FileName };
        }

        var (exePath, baseDir) = MpqEditor.ResolvePaths(_config);
        if (!File.Exists(exePath))
        {
            Warn(LF("err_mpq_missing", exePath));
            return;
        }

        ProfileConfig? profile = null;
        List<ModeConfig>? selectedModes = null;
        string profileKey;

        if (isInstall)
        {
            profileKey = rbProfileA.Checked ? _profileItems[0].Key : _profileItems[1].Key;
            if (!_config.Profiles.TryGetValue(profileKey, out profile))
            {
                Warn(L("err_profile_invalid"));
                return;
            }

            selectedModes = new List<ModeConfig>();
            for (var i = 0; i < clbModes.CheckedItems.Count; i++)
            {
                var idx = clbModes.Items.IndexOf(clbModes.CheckedItems[i]!);
                if (idx >= 0 && idx < _modeItems.Count)
                {
                    if (profile.Modes.TryGetValue(_modeItems[idx].Key, out var m))
                        selectedModes.Add(m);
                }
            }

            if (selectedModes.Count == 0)
            {
                Warn(L("err_no_modes_checked"));
                return;
            }
        }
        else
        {
            if (_config.UninstallFiles == null || _config.UninstallFiles.Length == 0)
            {
                Warn(L("err_config_empty") + " UninstallFiles");
                return;
            }
        }

        var warningText = ConfigLoader.GetWarningText(_config);
        if (string.IsNullOrEmpty(warningText))
            warningText = isInstall ? L("warn_default_install") : L("warn_default_uninstall");
        if (MessageBox.Show(this, warningText, L("warn_title"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetRunning(true);
        InitBottomProgress(maps.Count);
        OpenLogWindow();

        _cts = new CancellationTokenSource();
        var mpq = new MpqEditor(exePath, baseDir);
        var progress = new Progress<ProgressReport>(ReportProgress);

        Task.Run(async () =>
        {
            BatchSummary totalSummary = new() { Total = maps.Count };

            if (isInstall)
            {
                foreach (var mode in selectedModes!)
                {
                    var result = await BatchProcessor.RunInstallAsync(
                        maps, _config!, profile!, mode, mpq,
                        progress, _cts.Token);

                    if (result.Aborted)
                    {
                        totalSummary.Aborted = true;
                        break;
                    }
                    totalSummary.Success = Math.Max(totalSummary.Success, result.Success);
                    totalSummary.Failed = Math.Max(totalSummary.Failed, result.Failed);
                    if (result.Cancelled)
                    {
                        totalSummary.Cancelled = true;
                        break;
                    }
                }
            }
            else
            {
                var result = await BatchProcessor.RunUninstallAsync(
                    maps, _config!, mpq, progress, _cts.Token);
                totalSummary = result;
            }

            return totalSummary;

        }).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Invoke(() =>
                {
                    AppendLog(t.Exception?.InnerException?.Message ?? t.Exception?.Message ?? "Unknown error");
                    SetRunning(false);
                });
            }
            else
            {
                Invoke(() => OnFinished(t.Result));
            }
        }, TaskScheduler.Default);
    }

    private void CancelRun()
    {
        if (!_running) return;
        _cts?.Cancel();
    }

    private void OnFinished(BatchSummary summary)
    {
        SetRunning(false);

        if (summary.Aborted)
            return;

        var total = summary.Total;
        var msg = summary.Cancelled
            ? LF("summary_cancelled", summary.Success, summary.Failed, total)
            : LF("summary_done", summary.Success, summary.Failed, total);

        AppendLog(msg);
        var icon = summary.Cancelled ? MessageBoxIcon.Information :
            (summary.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        MessageBox.Show(this, msg, L("app_title"), MessageBoxButtons.OK, icon);
    }

    private void ReportProgress(ProgressReport r)
    {
        switch (r.Kind)
        {
            case ProgressKind.Started:
                bottomProgress.Maximum = Math.Max(1, r.Total);
                bottomProgress.Value = 0;
                lblBottomProgress.Text = $"0/{r.Total}";
                AppendLog(r.LogLine);
                break;
            case ProgressKind.Step:
                break;
            case ProgressKind.MapOk:
            case ProgressKind.MapFail:
            case ProgressKind.MapPermissionFail:
                if (r.Index >= 0)
                {
                    bottomProgress.Value = Math.Min(r.Index, bottomProgress.Maximum);
                    lblBottomProgress.Text = $"{r.Index}/{r.Total}";
                }
                AppendLog(r.LogLine);
                break;
            case ProgressKind.MpqMissing:
                AppendLog(r.StatusMessage);
                break;
            case ProgressKind.Completed:
                bottomProgress.Value = bottomProgress.Maximum;
                lblBottomProgress.Text = $"{r.Total}/{r.Total}";
                AppendLog(r.LogLine);
                break;
            case ProgressKind.Cancelled:
                AppendLog(r.LogLine);
                break;
        }
    }

    private void ResetBottomProgress()
    {
        bottomProgress.Maximum = 100;
        bottomProgress.Value = 0;
        lblBottomProgress.Text = "0/0";
    }

    private void InitBottomProgress(int total)
    {
        bottomProgress.Maximum = total;
        bottomProgress.Value = 0;
        lblBottomProgress.Text = $"0/{total}";
    }

    private void OpenLogWindow()
    {
        if (_logForm == null || _logForm.IsDisposed)
        {
            _logForm = new LogForm();
            _logForm.FormClosed += (_, _) => _logForm = null;
        }
        _logForm.Show();
    }

    private void ShowLogWindow()
    {
        OpenLogWindow();
        _logForm!.BringToFront();
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        _logForm?.AppendLog(line);
    }

    private List<string> CollectMapsInFolder(string folder)
    {
        if (_config == null) return new List<string>();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in _config.MapExtensions)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
                    set.Add(f);
            }
            catch
            {
            }
        }
        return set.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void SetRunning(bool running)
    {
        _running = running;
        cboLanguage.Enabled = !running;
        btnReloadConfig.Enabled = !running;
        rbInstall.Enabled = !running;
        rbUninstall.Enabled = !running;
        var installMode = rbInstall.Checked;
        var configOk = _config != null && _profileItems.Count >= 2;
        rbProfileA.Enabled = !running && installMode && configOk;
        rbProfileB.Enabled = !running && installMode && configOk;
        clbModes.Enabled = !running && installMode && configOk;
        btnSingleMap.Enabled = !running && configOk;
        btnFolder.Enabled = !running && configOk;
        btnCancel.Visible = running;
    }

    private void Warn(string msg)
        => MessageBox.Show(this, msg, L("app_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static string L(string key) => LanguageManager.Get(key);
    private static string LF(string key, params object?[] args) => LanguageManager.GetFormat(key, args);

    private sealed record LangItem(string Code, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record ProfileItem(string Key, string DisplayName);

    private sealed record ModeItem(string Key, string DisplayName);
}
