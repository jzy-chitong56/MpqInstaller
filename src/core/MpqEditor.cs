using System;
using System.Diagnostics;
using System.IO;

namespace MpqInstaller.Core;

public enum MpqExitCode
{
    Success = 0,
    PermissionOrUac = 5,
    OtherError = -1,
}

public readonly record struct MpqCommandResult(int ExitCode, string Output)
{
    public MpqExitCode Kind => ExitCode switch
    {
        0 => MpqExitCode.Success,
        5 => MpqExitCode.PermissionOrUac,
        _ => MpqExitCode.OtherError,
    };
    public bool IsSuccess => ExitCode == 0;
}

public sealed class MpqEditor
{
    private readonly string _baseDir;
    public string ExePath { get; }

    public MpqEditor(string exePath, string baseDir)
    {
        ExePath = exePath;
        _baseDir = baseDir;
    }

    public static (string exePath, string baseDir) ResolvePaths(InstallConfig config)
    {
        var exeDir = AppContext.BaseDirectory;
        var exe = Path.IsPathRooted(config.MpqEditorPath)
            ? config.MpqEditorPath
            : Path.Combine(exeDir, config.MpqEditorPath);
        var baseDir = Path.IsPathRooted(config.FilesBaseDir)
            ? config.FilesBaseDir
            : Path.Combine(exeDir, config.FilesBaseDir);
        return (Path.GetFullPath(exe), Path.GetFullPath(baseDir));
    }

    public bool Exists() => File.Exists(ExePath);

    public string ResolveSource(string? profileSubDir, string relativeSource)
    {
        var segs = new System.Collections.Generic.List<string> { _baseDir };
        if (!string.IsNullOrEmpty(profileSubDir))
            segs.Add(profileSubDir);
        segs.Add(relativeSource);
        for (var i = 0; i < segs.Count; i++)
            segs[i] = segs[i].Replace('/', '\\');
        return string.Join('\\', segs);
    }

    public MpqCommandResult HtSize(string map, int size)
        => Run("htsize", map, size.ToString());

    public MpqCommandResult Add(string map, string source, string dest)
        => Run("a", map, source, dest);

    public MpqCommandResult Delete(string map, string pathInMpq)
        => Run("d", map, pathInMpq);

    public MpqCommandResult Flush(string map)
        => Run("f", map);

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
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = new Process { StartInfo = psi };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        var output = string.IsNullOrEmpty(stderr) ? stdout : stdout + Environment.NewLine + stderr;
        return new MpqCommandResult(p.ExitCode, output?.Trim() ?? string.Empty);
    }
}
