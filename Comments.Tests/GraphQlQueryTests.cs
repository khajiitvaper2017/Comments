using Comments.Api.Configuration;
using Comments.Api.GraphQL;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Comments.Tests;

public sealed class GraphQlQueryTests
{
    [Fact]
    public async Task CommentsQueryReturnsPageFromCommentService()
    {
        var service = new Mock<ICommentService>();
        var expected = new CommentPageDto([], null, "userName", false);
        service.Setup(x => x.GetRootsAsync("userName", false, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(expected);

        var result = await new Query().Comments(
            "userName", false, service.Object, CancellationToken.None);

        Assert.Same(expected, result);
        service.Verify(x => x.GetRootsAsync("userName", false, It.IsAny<CancellationToken>(), null), Times.Once);
    }

    [Fact]
    public async Task RepliesQueryReturnsRepliesFromCommentService()
    {
        var service = new Mock<ICommentService>();
        var parentId = Guid.NewGuid();
        var expected = Array.Empty<CommentDto>();
        service.Setup(x => x.GetRepliesAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await new Query().Replies(parentId, service.Object, CancellationToken.None);

        Assert.Same(expected, result);
        service.Verify(x => x.GetRepliesAsync(parentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchQueryReturnsPageFromSearchService()
    {
        var search = new Mock<ICommentSearch>();
        var expected = new CommentPageDto([], null, "search", false);
        search.Setup(x => x.SearchAsync("term", true, true, true, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(expected);

        var result = await new Query().Search("term", true, true, true, search.Object, CancellationToken.None);

        Assert.Same(expected, result);
        search.Verify(x => x.SearchAsync("term", true, true, true, It.IsAny<CancellationToken>(), null), Times.Once);
    }

    [Fact]
    public async Task GraphQlSchemaHasNoMutationType()
    {
        await using var provider = CreateProvider();

        var executor = await provider.GetRequiredService<IRequestExecutorProvider>()
            .GetExecutorAsync();

        Assert.Null(executor.Schema.MutationType);
    }

    [Fact]
    public async Task GraphQlAllowsTheConfiguredReplyDepth()
    {
        var service = new Mock<ICommentService>();
        service.Setup(x => x.GetRepliesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        await using var provider = CreateProvider(service.Object);
        var executor = await provider.GetRequiredService<IRequestExecutorProvider>()
            .GetExecutorAsync();

        var selection = "id";
        for (var depth = 0; depth < 24; depth++)
            selection = $"replies {{ {selection} }}";

        var result = await executor.ExecuteAsync(
            $"{{ replies(parentId: \"00000000-0000-0000-0000-000000000000\") {{ {selection} }} }}");

        Assert.Empty(result.ExpectOperationResult().Errors);
    }

    private static ServiceProvider CreateProvider(ICommentService? service = null)
    {
        var services = new ServiceCollection();
        if (service is not null)
            services.AddSingleton(service);
        services.AddCommentsApi();
        return services.BuildServiceProvider();
    }
}
