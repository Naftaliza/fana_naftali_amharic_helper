using System.Threading.Channels;
using AmharicHelper.Application.Abstractions;

namespace AmharicHelper.Infrastructure.Processing;

/// <summary>
/// In-memory, single-instance job queue backing <see cref="IDocumentProcessingQueue"/>.
/// DocumentProcessingWorker is the sole reader. See the interface doc for the crash-recovery story
/// (this queue does not survive a restart; DocumentProcessingWorker's startup reconciliation does).
/// </summary>
public class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ChannelReader<Guid> Reader => _channel.Reader;

    public void Enqueue(Guid documentId) => _channel.Writer.TryWrite(documentId);
}
