using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Infrastructure.Processing;

/// <summary>
/// Consumes queued document IDs and runs their OCR pipeline (DocumentProcessor) off the request
/// thread — a 10-page upload used to hold the HTTP request open for 50-200+ seconds; now upload
/// returns immediately and the client polls GET /documents/{id} for progress.
///
/// On startup, also re-queues any document left Pending/Processing by a prior instance that
/// crashed or was redeployed mid-job: the in-memory queue itself doesn't survive a restart, but
/// the Postgres Status column does, so this is how work actually resumes.
/// </summary>
public class DocumentProcessingWorker(
    DocumentProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedAsync(stoppingToken);

        await foreach (var documentId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessor>();
                await processor.ProcessAsync(documentId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Document processing failed for {DocumentId}.", documentId);
            }
        }
    }

    private async Task RequeueUnfinishedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var unfinished = await documents.ListUnfinishedAsync(ct);
        foreach (var doc in unfinished) queue.Enqueue(doc.Id);
        if (unfinished.Count > 0)
            logger.LogInformation("Re-queued {Count} document(s) left unfinished by a prior run.", unfinished.Count);
    }
}
