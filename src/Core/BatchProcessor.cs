using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MpqInstaller.Config;
using MpqInstaller.i18n;

namespace MpqInstaller.Core;

public enum ProgressKind
{
    Started,
    Step,
    MapOk,
    MapFail,
    MapPermissionFail,
    MpqMissing,
    Completed,
    Cancelled,
}

public readonly record struct ProgressReport(
    ProgressKind Kind,
    int Index,
    int Total,
    string MapName,
    string StatusMessage,
    string LogLine);

public sealed class BatchSummary
{
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public bool Cancelled { get; set; }
    public bool Aborted { get; set; }
    public List<string> Failures { get; } = new();
}

public static class BatchProcessor
{
    public static async Task<BatchSummary> RunInstallAsync(
        IReadOnlyList<string> maps,
        InstallConfig config,
        ProfileConfig profile,
        ModeConfig mode,
        MpqEditor mpq,
        IProgress<ProgressReport>? progress,
        CancellationToken ct)
    {
        var summary = new BatchSummary { Total = maps.Count };

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
            for (var i = 0; i < maps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var map = maps[i];
                var mapName = Path.GetFileName(map);
                var idx = i + 1;

                var plan = InstallPlanBuilder.BuildInstall(mpq, config, profile, mode, map);

                bool permissionFail = false;
                int lastNonZero = 0;
                string failOutput = string.Empty;

                foreach (var action in plan)
                {
                    ct.ThrowIfCancellationRequested();
                    Report(progress, ProgressKind.Step, idx, maps.Count, mapName,
                        LanguageManager.GetFormat("status_step", idx, maps.Count, mapName, action.LogMessage),
                        string.Empty);

                    var r = ExecuteAction(mpq, action);
                    if (!r.IsSuccess)
                    {
                        if (r.Kind == MpqExitCode.PermissionOrUac)
                            permissionFail = true;
                        lastNonZero = r.ExitCode;
                        if (!string.IsNullOrEmpty(r.Output))
                            failOutput = r.Output;
                    }
                }

                if (permissionFail)
                {
                    summary.Failed++;
                    var line = LanguageManager.GetFormat("log_map_permission_fail", idx, maps.Count, mapName);
                    summary.Failures.Add(line);
                    Report(progress, ProgressKind.MapPermissionFail, idx, maps.Count, mapName,
                        LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                        line + "  " + LanguageManager.Get("reason_permission"));
                }
                else if (lastNonZero != 0)
                {
                    summary.Failed++;
                    var line = LanguageManager.GetFormat("log_map_fail", idx, maps.Count, mapName, lastNonZero);
                    summary.Failures.Add(line);
                    Report(progress, ProgressKind.MapFail, idx, maps.Count, mapName,
                        LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                        line + (string.IsNullOrEmpty(failOutput) ? string.Empty : "  " + failOutput));
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
        IReadOnlyList<string> maps,
        InstallConfig config,
        MpqEditor mpq,
        IProgress<ProgressReport>? progress,
        CancellationToken ct)
    {
        var summary = new BatchSummary { Total = maps.Count };

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
            for (var i = 0; i < maps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var map = maps[i];
                var mapName = Path.GetFileName(map);
                var idx = i + 1;

                var plan = InstallPlanBuilder.BuildUninstall(config, map);

                bool permissionFail = false;
                int lastNonZero = 0;
                string failOutput = string.Empty;

                foreach (var action in plan)
                {
                    ct.ThrowIfCancellationRequested();
                    Report(progress, ProgressKind.Step, idx, maps.Count, mapName,
                        LanguageManager.GetFormat("status_step", idx, maps.Count, mapName, action.LogMessage),
                        string.Empty);

                    var r = ExecuteAction(mpq, action);
                    if (!r.IsSuccess)
                    {
                        if (r.Kind == MpqExitCode.PermissionOrUac)
                            permissionFail = true;
                        lastNonZero = r.ExitCode;
                        if (!string.IsNullOrEmpty(r.Output))
                            failOutput = r.Output;
                    }
                }

                if (permissionFail)
                {
                    summary.Failed++;
                    var line = LanguageManager.GetFormat("log_map_permission_fail", idx, maps.Count, mapName);
                    summary.Failures.Add(line);
                    Report(progress, ProgressKind.MapPermissionFail, idx, maps.Count, mapName,
                        LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                        line + "  " + LanguageManager.Get("reason_permission"));
                }
                else if (lastNonZero != 0)
                {
                    summary.Failed++;
                    var line = LanguageManager.GetFormat("log_map_fail", idx, maps.Count, mapName, lastNonZero);
                    summary.Failures.Add(line);
                    Report(progress, ProgressKind.MapFail, idx, maps.Count, mapName,
                        LanguageManager.GetFormat("status_map_fail", idx, maps.Count, mapName),
                        line + (string.IsNullOrEmpty(failOutput) ? string.Empty : "  " + failOutput));
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

    private static void Report(IProgress<ProgressReport>? progress,
        ProgressKind kind, int idx, int total, string mapName, string status, string log)
    {
        progress?.Report(new ProgressReport(kind, idx, total, mapName, status, log));
    }

    private static MpqCommandResult ExecuteAction(MpqEditor mpq, in InstallAction a)
        => a.Type switch
        {
            ActionType.HtSize => mpq.HtSize(a.Map, a.Size),
            ActionType.Add => mpq.Add(a.Map, a.Source, a.Dest),
            ActionType.Delete => mpq.Delete(a.Map, a.Dest),
            ActionType.Flush => mpq.Flush(a.Map),
            _ => new MpqCommandResult(0, string.Empty),
        };
}
