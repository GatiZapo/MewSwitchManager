namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private void InitializeEmulationCenter()
    {
        if (_content.GetControlFromPosition(0, 8) is not null) return;
        _content.RowCount = Math.Max(_content.RowCount, 9);
        _content.Controls.Add(BuildEmulationSection(), 0, 8);
    }
}
