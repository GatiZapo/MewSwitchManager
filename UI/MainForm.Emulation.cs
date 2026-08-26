using MewSwitchManager.Core;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private EmulationInstaller _emulationInstaller = null!;
    private readonly Button _emulationAllButton = new();
    private readonly Button _emulationOneButton = new();

    private Control BuildEmulationSection()
    {
        _emulationInstaller ??= new EmulationInstaller(AppPaths.Create(_config), _logger);
        var card = CreateCard();
        card.Height = 292;
        card.Padding = new Padding(16);

        var title = new Label { Dock = DockStyle.Top, Height = 28, Text = "EMULATION CENTER", ForeColor = Theme.Text, Font = Theme.UI(12, FontStyle.Bold) };
        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 55,
            Text = "TICO + RETROARCH + ALL TICO CORES\nAutomatic download • SHA-256 verification • checkpoint • transactional rollback • user data preserved",
            ForeColor = Theme.Muted,
            Font = Theme.Mono(7.2f)
        };
        var list = new ListBox { Dock = DockStyle.Fill, BackColor = Theme.Surface2, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Font = Theme.Mono(8.0f), IntegralHeight = false };
        foreach (var package in EmulatorCatalog.Definitions)
        {
            var kind = package.SourceKind == EmulationSourceKind.OfficialBundle ? "OFFICIAL" : "GITHUB";
            list.Items.Add($"[{kind,-8}] {package.Name,-34} {package.Systems}");
        }
        list.SelectedIndex = 0;

        var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 78, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Theme.Surface, Padding = new Padding(0, 5, 0, 0), Margin = Padding.Empty };
        StyleButton(_emulationAllButton, "INSTALL EVERYTHING", Theme.Pink, 190);
        _emulationAllButton.Click += async (_, _) => await InstallFullEmulationStackAsync();
        row.Controls.Add(_emulationAllButton);
        StyleButton(_emulationOneButton, "INSTALL / UPDATE SELECTED", Theme.Blue, 190);
        _emulationOneButton.Click += async (_, _) => await InstallSelectedEmulationAsync(list.SelectedIndex >= 0 ? EmulatorCatalog.Definitions[list.SelectedIndex] : EmulatorCatalog.Definitions[0]);
        row.Controls.Add(_emulationOneButton);
        var details = new Button();
        StyleButton(details, "DETAILS", Theme.Amber, 100);
        details.Click += (_, _) => ShowEmulationDetails(list.SelectedIndex >= 0 ? EmulatorCatalog.Definitions[list.SelectedIndex] : EmulatorCatalog.Definitions[0]);
        row.Controls.Add(details);

        card.Controls.Add(list); card.Controls.Add(row); card.Controls.Add(info); card.Controls.Add(title);
        return card;
    }

    private async Task InstallFullEmulationStackAsync()
    {
        if (_operationCts is not null) return;
        var target = GetExperienceTarget();
        if (target is null) { MessageBox.Show(this, "Connect the Switch SD card first.", "Emulation Center", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        var packages = EmulatorCatalog.FullStack;
        var confirm = MessageBox.Show(this,
            "MewNX will install the complete emulation stack:\n\n• tico frontend\n• RetroArch + its full official Switch core/asset bundle\n• All currently released Tico cores for the stock systems\n\n~4 GB free space is recommended.\n\nA checkpoint is created first. Existing RetroArch configuration, saves, states, playlists, thumbnails and BIOS/system files are preserved. ROMs and BIOS dumps are never downloaded by MewNX.\n\nIf any required component fails, MewNX automatically rolls the whole emulation installation back instead of leaving a partial stack.\n\nContinue?",
            "INSTALL EVERYTHING — EMULATION", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        _experienceCheckpoint.Create(target, "Automatic checkpoint before full emulation stack");
        _operationCts = new CancellationTokenSource();
        try
        {
            _emulationAllButton.Enabled = false; _emulationOneButton.Enabled = false;
            SetStatus("●  INSTALLING FULL EMULATION STACK", Theme.Pink);
            var completed = await _emulationInstaller.InstallFullStackAsync(target, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token);
            SetStatus($"●  EMULATION {completed.Count}/{packages.Count} READY", Theme.Green);
            MessageBox.Show(this, $"Complete emulation stack installed successfully.\n\nInstalled: {completed.Count}/{packages.Count} components.\n\nRetroArch, tico and all managed Tico cores are ready. Your existing user data was preserved.", "Emulation Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshExperienceAsync();
        }
        catch (OperationCanceledException) { SetStatus("●  EMULATION INSTALL CANCELLED", Theme.Amber); }
        catch (Exception ex) { _logger.Error("Full emulation installation failed", ex); SetStatus("●  EMULATION INSTALL FAILED — ROLLED BACK", Theme.Red); MessageBox.Show(this, $"The complete emulation stack could not be installed.\n\nMewNX rolled back the managed emulation changes.\n\n{ex.Message}", "Emulation Center", MessageBoxButtons.OK, MessageBoxIcon.Error); await RefreshExperienceAsync(); }
        finally { _emulationAllButton.Enabled = true; _emulationOneButton.Enabled = true; _operationCts.Dispose(); _operationCts = null; }
    }

    private async Task InstallSelectedEmulationAsync(EmulationPackageDefinition definition)
    {
        if (_operationCts is not null) return;
        var target = GetExperienceTarget();
        if (target is null) { MessageBox.Show(this, "Connect the Switch SD card first.", "Emulation Center", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        _experienceCheckpoint.Create(target, $"Automatic checkpoint before {definition.Name}");
        _operationCts = new CancellationTokenSource();
        try
        {
            _emulationAllButton.Enabled = false; _emulationOneButton.Enabled = false;
            SetStatus($"●  INSTALLING {definition.Name.ToUpperInvariant()}", Theme.Pink);
            var result = await _emulationInstaller.InstallOrUpdateAsync(definition, target, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token);
            SetStatus("●  EMULATION COMPONENT UPDATED", Theme.Green);
            MessageBox.Show(this, result.Message, "Emulation Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshExperienceAsync();
        }
        catch (OperationCanceledException) { SetStatus("●  EMULATION INSTALL CANCELLED", Theme.Amber); }
        catch (Exception ex) { _logger.Error($"Emulation component {definition.Name} failed", ex); SetStatus("●  EMULATION COMPONENT FAILED — ROLLED BACK", Theme.Red); MessageBox.Show(this, $"{definition.Name} could not be installed.\n\nMewNX rolled back the component change.\n\n{ex.Message}", "Emulation Center", MessageBoxButtons.OK, MessageBoxIcon.Error); await RefreshExperienceAsync(); }
        finally { _emulationAllButton.Enabled = true; _emulationOneButton.Enabled = true; _operationCts.Dispose(); _operationCts = null; }
    }

    private void ShowEmulationDetails(EmulationPackageDefinition definition)
    {
        var source = definition.SourceKind == EmulationSourceKind.OfficialBundle ? "Official Libretro Switch buildbot" : definition.Repository;
        MessageBox.Show(this,
            $"{definition.Name}\n\nSystems: {definition.Systems}\nSource: {source}\nDestination: {(string.IsNullOrWhiteSpace(definition.Destination) ? "SD root (official bundle)" : definition.Destination)}\n\n{definition.Description}\n\nMewNX downloads only redistributable emulator/frontend/core software. Game ROMs, BIOS dumps, keys and console firmware are user-provided and are never bundled or downloaded.",
            "Emulation Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
