using System.Text.Json;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class GameCenterQueue
{
    private readonly JsonStore<List<DownloadJob>> _store;
    private readonly List<DownloadJob> _jobs;
    private readonly ResumableDownloadService _downloader;
    private readonly ContentProcessor _processor;
    private readonly AppLogger _logger;

    public IReadOnlyList<DownloadJob> Jobs => _jobs;
    public event Action? Changed;

    public GameCenterQueue(AppPaths paths, AppLogger logger)
    {
        _store = new JsonStore<List<DownloadJob>>(Path.Combine(paths.DataDirectory, "game-center-queue.json"));
        _jobs = _store.LoadOrCreate();
        _downloader = new ResumableDownloadService(new HttpClient(), logger);
        _processor = new ContentProcessor(logger);
        _logger = logger;
    }

    public DownloadJob AddDirectUrl(string name, string url, string? sha256 = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new ArgumentException("Only HTTP(S) direct file URLs are accepted.", nameof(url));
        var job = new DownloadJob(Guid.NewGuid().ToString("N"), name, uri.ToString(), DownloadSourceKind.DirectUrl, Path.Combine(Path.GetDirectoryName(_store.Path)!, "game-downloads"), sha256);
        _jobs.Add(job); Save(); return job;
    }

    public DownloadJob AddLocalFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File not found.", path);
        var job = new DownloadJob(Guid.NewGuid().ToString("N"), Path.GetFileName(path), path, DownloadSourceKind.LocalFile, Path.Combine(Path.GetDirectoryName(_store.Path)!, "game-downloads"));
        _jobs.Add(job); Save(); return job;
    }

    public async Task ProcessAsync(DownloadJob job, Func<PreparedContent, Task> installAndVerifyAsync, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        try
        {
            string source;
            if (job.SourceKind == DownloadSourceKind.DirectUrl)
                source = await _downloader.DownloadAsync(job, progress, ct);
            else source = job.Source;

            job.State = DownloadJobState.Processing; Save();
            var prepared = await _processor.PrepareAsync(source, Path.Combine(job.WorkingDirectory, job.Id), ct);
            job.PreparedPath = prepared.Path; job.State = DownloadJobState.Ready; Save();
            job.State = DownloadJobState.Installing; Save();
            await installAndVerifyAsync(prepared);
            job.State = DownloadJobState.Verifying; Save();
            job.State = DownloadJobState.Completed; job.Error = null; Save();
            if (job.SourceKind == DownloadSourceKind.DirectUrl) TryDelete(source);
            _processor.GetType();
            ContentProcessor.Cleanup(Path.Combine(job.WorkingDirectory, job.Id), []);
            Save();
        }
        catch (OperationCanceledException) { job.State = DownloadJobState.Cancelled; Save(); throw; }
        catch (Exception ex) { job.State = DownloadJobState.Failed; job.Error = ex.Message; Save(); _logger.Error($"Game Center job failed: {job.Name}", ex); throw; }
    }

    private void Save() { _store.Save(_jobs); Changed?.Invoke(); }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
