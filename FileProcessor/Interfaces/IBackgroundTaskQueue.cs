using FileProcessor.Models;

namespace FileProcessor.Interfaces
{
    public interface IBackgroundTaskQueue
    {
        Task QueueBackgroundWorkItemAsync(FileProcessingTask task);
        Task<FileProcessingTask> DequeueAsync(CancellationToken cancellationToken);
    }
}
