using MewSwitchManager.Infrastructure;

namespace MewSwitchManager.Hardware;

public sealed record RcmStatus(bool Connected, string DeviceId, string Message);

public sealed class RcmService
{
    private readonly ProcessRunner _runner;
    private readonly AppLogger _logger;

    public RcmService(ProcessRunner runner, AppLogger logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task<RcmStatus> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await _runner.RunAsync("powershell.exe", "-NoProfile -Command \"Get-PnpDevice -PresentOnly | Select-Object -ExpandProperty InstanceId\"", ct);
            var connected = r.StdOut.Contains("VID_0955&PID_7321", StringComparison.OrdinalIgnoreCase);
            return connected
                ? new RcmStatus(true, "VID_0955&PID_7321", "Nintendo Switch RCM device detected.")
                : new RcmStatus(false, "", "RCM not detected. The Switch must be physically booted into RCM before a payload can be sent.");
        }
        catch (Exception ex)
        {
            _logger.Warn($"RCM probe failed: {ex.Message}");
            return new RcmStatus(false, "", "Windows could not query USB devices.");
        }
    }

    public string GetEntryGuide() =>
        "RCM ENTRY\n\n" +
        "1. Power the Switch fully off.\n" +
        "2. Use the appropriate RCM method for your hardware (for an unpatched Erista, the RCM button/jig method).\n" +
        "3. Hold the required volume button while pressing Power.\n" +
        "4. Connect USB and press REFRESH.\n\n" +
        "MewSwitch cannot safely force a normal retail Switch into RCM over USB. A payload sender only works after the console is already exposing the RCM USB device.\n\n" +
        "AutoRCM can make future boots enter RCM, but changing boot0 is a separate, high-risk operation and is deliberately not performed automatically by the manager.";
}
