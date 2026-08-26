using MewSwitchManager.Core;
using MewSwitchManager.Hardware;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private readonly SwitchExperienceService _experienceService = null!;
    private readonly SwitchCheckpoint _experienceCheckpoint = null!;
    private SwitchExperienceCard? _experienceCard;

    private void InitializeExperience(AppPaths paths)
    {
        // The card lives inside the existing health area so the dashboard remains one coherent surface.
        var host = _content.GetControlFromPosition(0, 2) as Panel;
        if (host is null || _experienceCard is not null) return;

        var service = new SwitchExperienceService(_logger);
        var checkpoint = new SwitchCheckpoint(paths, _logger);
        typeof(MainForm).GetField("_experienceService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(this, service);
        typeof(MainForm).GetField("_experienceCheckpoint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(this, checkpoint);

        _experienceCard = new SwitchExperienceCard();
        _experienceCard.RecommendedClicked += async (_, _) => await PrepareRecommendedAsync();
        _experienceCard.CheckpointClicked += (_, _) => CreateExperienceCheckpoint();
        _experienceCard.RescanClicked += async (_, _) => await RefreshExperienceAsync();
        host.Height = 482;
        _experienceCard.Height = 230;
        host.Controls.Add(_experienceCard);
        host.Controls.SetChildIndex(_experienceCard, 0);
        _experienceCard.ShowLoading();
    }

    private string? GetExperienceTarget()
    {
        if (_switchStorageSelector.SelectedItem is RemovableDrive drive) return drive.Root;
        return new RemovableDriveService().Scan().FirstOrDefault()?.Root;
    }

    private async Task RefreshExperienceAsync()
    {
        if (_experienceCard is null) return;
        var target = GetExperienceTarget();
        if (target is null)
        {
            _experienceCard.Show(new SwitchExperienceSummary(
                "Connect the Switch SD card to begin.",
                Array.Empty<string>(),
                new[] { "No Switch storage target detected." },
                Array.Empty<ToolRecommendation>(),
                new SwitchSdReport("", 0, 0, false, false, false, false, false, false, new[] { "No target detected." }),
                Array.Empty<SwitchToolHealth>()));
            return;
        }

        try
        {
            _experienceCard.ShowLoading();
            var result = await Task.Run(() => _experienceService.Inspect(target));
            _experienceCard.Show(result);
        }
        catch (Exception ex)
        {
            _logger.Error("Switch experience scan failed", ex);
            _experienceCard.Show(new SwitchExperienceSummary("Unable to inspect Switch storage.", Array.Empty<string>(), new[] { ex.Message }, Array.Empty<ToolRecommendation>(), new SwitchSdReport(target, 0, 0, false, false, false, false, false, false, new[] { ex.Message }), Array.Empty<SwitchToolHealth>()));
        }
    }

    private void CreateExperienceCheckpoint()
    {
        var target = GetExperienceTarget();
        if (target is null) { MessageBox.Show(this, "Connect the Switch SD card first.", "Checkpoint", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        try
        {
            var path = _experienceCheckpoint.Create(target, "Manual dashboard checkpoint");
            SetStatus("●  CHECKPOINT CREATED", Theme.Green);
            MessageBox.Show(this, $"Checkpoint created successfully.\n\n{path}", "MewSwitch Checkpoint", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { _logger.Error("Checkpoint creation failed", ex); MessageBox.Show(this, ex.Message, "Checkpoint failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task PrepareRecommendedAsync()
    {
        var target = GetExperienceTarget();
        if (target is null) { MessageBox.Show(this, "Connect the Switch SD card first.", "Recommended setup", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        SwitchExperienceSummary result;
        try { result = await Task.Run(() => _experienceService.Inspect(target)); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Recommended setup", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        var recommendations = result.Recommendations.Where(x => x.Recommended).ToArray();
        if (recommendations.Length == 0) { MessageBox.Show(this, "Your current setup has no mandatory recommendations.", "Recommended setup", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        var names = string.Join(Environment.NewLine, recommendations.Select(x => "• " + x.ToolId));
        if (MessageBox.Show(this, $"MewSwitch recommends:\n\n{names}\n\nA checkpoint will be created first. Continue?", "Prepare recommended setup", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

        _experienceCheckpoint.Create(target, "Automatic checkpoint before recommended setup");
        _operationCts = new CancellationTokenSource();
        try
        {
            SetStatus("●  PREPARING RECOMMENDED SETUP", Theme.Pink);
            var installed = 0;
            foreach (var recommendation in recommendations)
            {
                _operationCts.Token.ThrowIfCancellationRequested();
                var definition = SwitchToolCatalog.Definitions.FirstOrDefault(x => x.Id == recommendation.ToolId);
                if (definition is null) continue;
                try
                {
                    await _toolInstaller.InstallOrUpdateAsync(definition, target, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token);
                    installed++;
                }
                catch (Exception ex) { _logger.Warn($"Recommended tool {definition.Name} was skipped: {ex.Message}"); }
            }
            SetStatus($"●  RECOMMENDED SETUP {installed}/{recommendations.Length}", installed == recommendations.Length ? Theme.Green : Theme.Amber);
            await RefreshExperienceAsync();
        }
        catch (OperationCanceledException) { SetStatus("●  RECOMMENDED SETUP CANCELLED", Theme.Amber); }
        finally { _operationCts.Dispose(); _operationCts = null; }
    }
}
