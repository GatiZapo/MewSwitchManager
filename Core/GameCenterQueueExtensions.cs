using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public static class GameCenterQueueExtensions
{
    public static void RecoverInterruptedJobs(this GameCenterQueue queue)
    {
        foreach (var job in queue.Jobs)
        {
            if (job.State is DownloadJobState.Downloading or DownloadJobState.Processing or DownloadJobState.Installing or DownloadJobState.Verifying)
                job.State = DownloadJobState.Queued;
        }
    }

    public static void Cancel(this GameCenterQueue queue, DownloadJob job)
    {
        if (job.State is DownloadJobState.Completed or DownloadJobState.Cancelled) return;
        job.State = DownloadJobState.Cancelled;
    }

    public static IEnumerable<DownloadJob> Pending(this GameCenterQueue queue)
        => queue.Jobs.Where(j => j.State is DownloadJobState.Queued or DownloadJobState.Downloading or DownloadJobState.Processing or DownloadJobState.Ready or DownloadJobState.Installing or DownloadJobState.Verifying);
}
