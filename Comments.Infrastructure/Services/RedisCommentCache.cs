using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Microsoft.Extensions.Caching.Distributed;

namespace Comments.Infrastructure.Services;

public sealed class RedisCommentCache(IDistributedCache cache) : ICommentCache
{
    // Versioned keys keep cursor slices isolated after writes.
    private const string VersionKey = "comments:cache-version:v6";

    public async Task<CommentPageDto?> GetAsync(string sort, bool descending, string? cursor = null,
        CancellationToken ct = default)
    {
        var version = await GetVersionAsync(ct);
        var value = await cache.GetStringAsync(Key(version, cursor, sort, descending), ct);
        return value is null ? null : JsonSerializer.Deserialize<CommentPageDto>(value);
    }

    public async Task SetAsync(string sort, bool descending, CommentPageDto value, string? cursor = null,
        CancellationToken ct = default)
    {
        var version = await GetVersionAsync(ct);
        await cache.SetStringAsync(
            Key(version, cursor, sort, descending),
            JsonSerializer.Serialize(value),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
            ct);
    }

    public async Task InvalidateAsync(CancellationToken ct)
    {
        var version = await GetVersionAsync(ct);
        await cache.SetStringAsync(VersionKey, (version + 1).ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) }, ct);
    }

    private async Task<long> GetVersionAsync(CancellationToken ct)
    {
        var value = await cache.GetStringAsync(VersionKey, ct);
        return long.TryParse(value, out var version) ? version : 1;
    }

    private static string Key(long version, string? cursor, string sort, bool descending)
    {
        return $"comments:v{version}:cursor:{cursor ?? "first"}:sort:{sort}:descending:{descending}";
    }
}
