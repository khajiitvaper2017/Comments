using Comments.Application.Abstractions;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
            var indexer = scope.ServiceProvider.GetRequiredService<ICommentIndexer>();
            var ids = await db.Comments.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync(stoppingToken);

            foreach (var id in ids)
                await indexer.IndexAsync(id, stoppingToken);
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
