using FileProcessor.Interfaces;
using FileProcessor.Models;
using System.Threading.Channels;

namespace FileProcessor.Domain
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<FileProcessingTask> _queue;

        public BackgroundTaskQueue()
        {
            _queue = Channel.CreateUnbounded<FileProcessingTask>();
        }

        public async Task QueueBackgroundWorkItemAsync(FileProcessingTask task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            await _queue.Writer.WriteAsync(task);
        }

        public async Task<FileProcessingTask> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
