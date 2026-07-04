using System.Collections.Generic;

namespace MpqInstaller.Core;

public enum ActionType
{
    HtSize,
    Add,
    Delete,
    Flush,
}

public readonly record struct InstallAction(
    ActionType Type,
    string Map,
    string Source,
    string Dest,
    int Size,
    string LogMessage);

public static class InstallPlanBuilder
{
    public static List<InstallAction> BuildInstall(
        MpqEditor mpq,
        InstallConfig config,
        ProfileConfig profile,
        ModeConfig mode,
        string map)
    {
        var plan = new List<InstallAction>();

        plan.Add(new InstallAction(
            ActionType.HtSize, map, string.Empty, string.Empty, config.HashTableSize,
            LanguageManager.GetFormat("step_htsize", config.HashTableSize)));

        if (profile.BaseActions != null)
        {
            foreach (var a in profile.BaseActions)
            {
                var src = mpq.ResolveSource(profile.SubDir, a.Source);
                plan.Add(new InstallAction(
                    ActionType.Add, map, src, a.Dest, 0,
                    LanguageManager.GetFormat("step_add", a.Source, a.Dest)));
            }
        }

        if (mode.ExtraActions != null)
        {
            foreach (var a in mode.ExtraActions)
            {
                var src = mpq.ResolveSource(profile.SubDir, a.Source);
                plan.Add(new InstallAction(
                    ActionType.Add, map, src, a.Dest, 0,
                    LanguageManager.GetFormat("step_add", a.Source, a.Dest)));
            }
        }

        plan.Add(new InstallAction(
            ActionType.Flush, map, string.Empty, string.Empty, 0,
            LanguageManager.Get("step_flush")));

        return plan;
    }

    public static List<InstallAction> BuildUninstall(InstallConfig config, string map)
    {
        var plan = new List<InstallAction>();
        if (config.UninstallFiles != null)
        {
            foreach (var f in config.UninstallFiles)
                plan.Add(new InstallAction(
                    ActionType.Delete, map, string.Empty, f, 0,
                    LanguageManager.GetFormat("step_delete", f)));
        }
        plan.Add(new InstallAction(
            ActionType.Flush, map, string.Empty, string.Empty, 0,
            LanguageManager.Get("step_flush")));
        return plan;
    }
}
