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
    public async Task ReadQueriesReturnDataFromApplicationServices()
    {
        var comments = new Mock<ICommentService>();
        var search = new Mock<ICommentSearch>();
        var page = new CommentPageDto([], 1, 25, 0, "createdAt", true);
        var replies = Array.Empty<CommentDto>();

        comments.Setup(x => x.GetRootsAsync(1, "createdAt", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        comments.Setup(x => x.GetRepliesAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(replies);
        search.Setup(x => x.SearchAsync("term", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        await using var provider = new ServiceCollection()
            .AddSingleton(comments.Object)
            .AddSingleton(search.Object)
            .AddGraphQLServer()
            .AddQueryType<Query>()
            .Services
            .BuildServiceProvider();

        var executor = await provider.GetRequiredService<IRequestExecutorProvider>()
            .GetExecutorAsync();
        var result = await executor.ExecuteAsync("""
                                                 {
                                                   comments { page totalCount }
                                                   replies(parentId: "00000000-0000-0000-0000-000000000000") { id }
                                                   search(query: "term") { page totalCount }
                                                 }
                                                 """);

        var operationResult = result.ExpectOperationResult();
        Assert.Empty(operationResult.Errors);
        comments.VerifyAll();
        search.VerifyAll();
        Assert.Null(executor.Schema.MutationType);
    }
}
