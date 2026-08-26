using System.Diagnostics;
using MewSwitchManager.Core;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private Control BuildEmulationSection()
    {
        var card = CreateCard();
        card.Height = 238;
        card.Padding = new Padding(16);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "EMULATION CENTER",
            ForeColor = Theme.Text,
            Font = Theme.UI(12, FontStyle.Bold)
        };
        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Text = "Curated emulator/frontend catalog • trusted sources • no ROM/BIOS distribution",
            ForeColor = Theme.Muted,
            Font = Theme.Mono(7.2f)
        };

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface2,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            Font = Theme.Mono(8.0f),
            IntegralHeight = false
        };
        foreach (var emulator in EmulatorCatalog.Definitions)
        {
            var mode = emulator.Distribution == EmulatorDistribution.ManualOnly ? "MANUAL" : emulator.Distribution == EmulatorDistribution.OfficialBuildbot ? "OFFICIAL" : "AUTO";
            list.Items.Add($"[{mode,-8}] {emulator.Name,-30} {emulator.Systems}");
        }

        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 5, 0, 0),
            Margin = Padding.Empty
        };
        var details = new Button();
        StyleButton(details, "VIEW DETAILS", Theme.Blue, 132);
        details.Click += (_, _) => ShowEmulatorDetails(list.SelectedIndex >= 0 ? EmulatorCatalog.Definitions[list.SelectedIndex] : EmulatorCatalog.Definitions[0]);
        row.Controls.Add(details);

        var source = new Button();
        StyleButton(source, "OPEN OFFICIAL SOURCE", Theme.Pink, 158);
        source.Click += (_, _) => OpenEmulatorSource(list.SelectedIndex >= 0 ? EmulatorCatalog.Definitions[list.SelectedIndex] : EmulatorCatalog.Definitions[0]);
        row.Controls.Add(source);

        list.SelectedIndex = 0;
        card.Controls.Add(list);
        card.Controls.Add(row);
        card.Controls.Add(info);
        card.Controls.Add(title);
        return card;
    }

    private void ShowEmulatorDetails(EmulatorDefinition definition)
    {
        var distribution = definition.Distribution switch
        {
            EmulatorDistribution.GitHubRelease => "Automatic GitHub release source",
            EmulatorDistribution.OfficialBuildbot => "Official buildbot source",
            _ => "Manual source — not redistributed automatically"
        };
        MessageBox.Show(
            this,
            $"{definition.Name}\n\nSystems: {definition.Systems}\nSource: {definition.Repository}\nDistribution: {distribution}\n\n{definition.Description}\n\n{definition.Notes}",
            "Emulation Center",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenEmulatorSource(EmulatorDefinition definition)
    {
        var url = definition.Distribution == EmulatorDistribution.OfficialBuildbot
            ? "https://docs.libretro.com/guides/install-libnx/"
            : $"https://github.com/{definition.Repository}";
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not open emulator source: {ex.Message}");
        }
    }
}
