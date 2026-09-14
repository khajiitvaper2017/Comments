using Comments.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Comments.Infrastructure.Search;

public sealed class ElasticsearchIndexInitializer(
    IServiceScopeFactory scopes,
    ILogger<ElasticsearchIndexInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting Elasticsearch comment index synchronization.");
            using var scope = scopes.CreateScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<ICommentIndexMaintenance>();
            var rebuilt = await maintenance.RebuildAsync(stoppingToken);
            logger.LogInformation(rebuilt
                ? "Elasticsearch comment index was rebuilt."
                : "Elasticsearch comment index was already up to date; no rebuild was needed.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not initialize the Elasticsearch comment index.");
        }
    }
}
