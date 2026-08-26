namespace MewSwitchManager.Hardware;

public enum AutoRcmState { Unknown, Enabled, Disabled }
public sealed record AutoRcmPlan(AutoRcmState Current, AutoRcmState Requested, bool RequiresHekate, string Warning, string Instructions);

public sealed class AutoRcmService
{
    public AutoRcmPlan Plan(AutoRcmState current, bool enable)
    {
        var requested = enable ? AutoRcmState.Enabled : AutoRcmState.Disabled;
        var warning = enable
            ? "AutoRCM changes the console boot path. Keep a known-good payload and battery charge available before enabling it."
            : "Disabling AutoRCM changes the console boot path. Ensure you can enter RCM manually before proceeding.";
        var instructions = enable
            ? "Boot Hekate → Tools → Arch Bit • AutoRCM → ON. Verify the state after reboot."
            : "Boot Hekate → Tools → Arch Bit • AutoRCM → OFF. Verify the state after reboot.";
        return new AutoRcmPlan(current, requested, true, warning, instructions);
    }

    public bool CanExecute(AutoRcmPlan plan, bool rcmDetected, bool payloadAvailable)
        => plan.RequiresHekate && rcmDetected && payloadAvailable && plan.Current != plan.Requested;
}
