using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace Comments.Api.Realtime;

public sealed class SignalRRealtimeNotifier(IHubContext<DiscussionHub> hub) : IRealtimeNotifier
{
    public Task NotifyCommentChangedAsync(CommentDto comment, CancellationToken ct)
    {
        return hub.Clients.All.SendAsync("commentChanged", comment, ct);
    }
}
