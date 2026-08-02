using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Documents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Infrastructure.Processing;

/// <summary>
/// Deletes documents past their RetainUntil date (default 24 months from upload — see
/// 021_legal_consent.sql). Runs once shortly after startup, then on a fixed interval; a missed
/// run (host down over the interval) is caught by the next tick since the query is always
/// "everything currently expired", not "expired since the last run".
///
/// Deletes through IMediator.Send(DeleteDocumentCommand) rather than duplicating its logic, so
/// the child-deletion order (chat messages → analysis → document row → page files) and any
/// future change to it stays in exactly one place.
/// </summary>
public class RetentionSweepWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RetentionSweepWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);

    /// <summary>Cap per sweep so a large backlog (e.g. the first run after this feature ships)
    /// doesn't hold the timer loop for an extended stretch — the rest is picked up next tick.</summary>
    private const int BatchLimit = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Retention sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var expired = await documents.ListExpiredAsync(DateTime.UtcNow, BatchLimit, ct);
        foreach (var doc in expired)
        {
            var result = await mediator.Send(new DeleteDocumentCommand(doc.UserId, doc.Id), ct);
            if (!result.Success)
                logger.LogWarning("Retention sweep could not delete document {DocumentId}: {Error}", doc.Id, result.Error);
        }

        if (expired.Count > 0)
            logger.LogInformation("Retention sweep deleted {Count} expired document(s).", expired.Count);
    }
}
