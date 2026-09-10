using Comments.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Comments.Api.Realtime;

public sealed class SignalRRealtimeNotifier(IHubContext<DiscussionHub> hub) : IRealtimeNotifier
{
    public Task NotifyCommentChangedAsync(Guid commentId, CancellationToken ct)
    {
        return hub.Clients.All.SendAsync("commentChanged", new { commentId }, ct);
    }
}
