using MewNX.Infrastructure;

namespace MewNX.Hardware;

public sealed record RcmPayloadResult(bool Started, int ExitCode, string Output, string Error, string ToolPath, string PayloadPath);

public sealed class RcmPayloadService
{
    private readonly ProcessRunner _processes;
    private readonly AppLogger _logger;
    public RcmPayloadService(ProcessRunner processes, AppLogger logger) { _processes = processes; _logger = logger; }

    public string? FindInjector(string appRoot)
    {
        var candidates = new[]
        {
            Path.Combine(appRoot, "tools", "TegraRcmSmash.exe"),
            Path.Combine(appRoot, "tools", "TegraRcmGUI", "TegraRcmSmash.exe"),
            Path.Combine(appRoot, "TegraRcmSmash.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<RcmPayloadResult> InjectAsync(string appRoot, string payloadPath, CancellationToken ct = default)
    {
        if (!File.Exists(payloadPath)) throw new FileNotFoundException("Payload not found.", payloadPath);
        var injector = FindInjector(appRoot);
        if (injector is null) throw new FileNotFoundException("No supported RCM injector was found. Place the supported injector in the application's tools directory.");
        var probe = await _processes.RunAsync(injector, $"-r \"{payloadPath}\"", ct);
        var result = new RcmPayloadResult(probe.ExitCode == 0, probe.ExitCode, probe.StdOut, probe.StdErr, injector, payloadPath);
        if (!result.Started) _logger.Warn($"RCM payload injection failed with exit code {result.ExitCode}: {result.Error}");
        else _logger.Info($"RCM payload injected: {Path.GetFileName(payloadPath)}");
        return result;
    }
}
