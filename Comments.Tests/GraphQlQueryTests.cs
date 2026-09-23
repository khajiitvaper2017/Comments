using System.Text.Json;
using Comments.Api.Configuration;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Application.Requests;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Comments.Tests;

public sealed class GraphQlQueryTests
{
    private static readonly Guid ParentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GraphQlCommentsCallsCommentService()
    {
        await using var provider = CreateProvider();

        var result = await Execute(provider, """
                                             {
                                               comments(sort: "userName", descending: false, cursor: "cursor") {
                                                 nextCursor
                                                 sort
                                                 descending
                                                 items {
                                                   text
                                                   userName
                                                   replies {
                                                     text
                                                     replies { text }
                                                   }
                                                 }
                                               }
                                             }
                                             """);

        var data = Data(result);
        var comments = data.GetProperty("comments");
        var items = comments.GetProperty("items");
        var root = items[1];
        Assert.Equal("next", comments.GetProperty("nextCursor").GetString());
        Assert.Equal("alice", items[0].GetProperty("userName").GetString());
        Assert.Equal("mike", items[1].GetProperty("userName").GetString());
        Assert.Equal("zoe", items[2].GetProperty("userName").GetString());
        Assert.Equal("root", root.GetProperty("text").GetString());
        Assert.Equal("reply", root.GetProperty("replies")[0].GetProperty("text").GetString());
        Assert.Equal("nested reply", root.GetProperty("replies")[0].GetProperty("replies")[0]
            .GetProperty("text").GetString());
    }

    [Fact]
    public async Task GraphQlRepliesCallsCommentService()
    {
        await using var provider = CreateProvider();

        var result = await Execute(provider, $$"""
                                               {
                                                 replies(parentId: "{{ParentId}}") {
                                                   text
                                                   replies { text }
                                                 }
                                               }
                                               """);

        var data = Data(result);
        Assert.Equal("reply", data.GetProperty("replies")[0].GetProperty("text").GetString());
        Assert.Equal("nested reply", data.GetProperty("replies")[0].GetProperty("replies")[0]
            .GetProperty("text").GetString());
    }

    [Fact]
    public async Task GraphQlAncestorsCallsCommentService()
    {
        await using var provider = CreateProvider();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var result = await Execute(provider, $"{{ ancestors(ids: [\"{ids[0]}\", \"{ids[1]}\"]) {{ id text }} }}");

        var data = Data(result);
        Assert.Equal("ancestor", data.GetProperty("ancestors")[0].GetProperty("text").GetString());
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
        await using var provider = CreateProvider();
        var executor = await provider.GetRequiredService<IRequestExecutorProvider>()
            .GetExecutorAsync();

        var selection = "id";
        for (var depth = 0; depth < 64; depth++)
            selection = $"replies {{ {selection} }}";

        var result = await executor.ExecuteAsync(
            $"{{ replies(parentId: \"00000000-0000-0000-0000-000000000000\") {{ {selection} }} }}");

        Assert.Empty(result.ExpectOperationResult().Errors);
    }

    private static async Task<IExecutionResult> Execute(ServiceProvider provider, string query)
    {
        var executor = await provider.GetRequiredService<IRequestExecutorProvider>()
            .GetExecutorAsync();
        return await executor.ExecuteAsync(query);
    }

    private static JsonElement Data(IExecutionResult result)
    {
        var operation = result.ExpectOperationResult();
        Assert.Empty(operation.Errors);
        using var document = JsonDocument.Parse(result.ToJson());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static CommentDto Comment(
        string text,
        IReadOnlyList<CommentDto>? replies = null,
        string userName = "user")
    {
        return new CommentDto(
            Guid.NewGuid(), null, userName, $"{userName}@example.com", null, text,
            DateTime.UtcNow, [], replies ?? []);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var commentService = new Mock<ICommentService>();

        // Some mock data for testing purposes
        commentService.Setup(expression: x => x.GetRootsAsync("userName", false, "cursor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CommentPageDto(Items:
                [
                    Comment(text: "alice comment", userName: "alice"),
                    Comment(text: "root", replies: [Comment(text: "reply", replies: [Comment(text: "nested reply")])], userName: "mike"),
                    Comment(text: "zoe comment", userName: "zoe")
                ],
                NextCursor: "next", Sort: "userName", Descending: false));
        commentService.Setup(expression: x => x.GetRepliesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: [Comment(text: "reply", replies: [Comment(text: "nested reply")])]);
        commentService.Setup(expression: x => x.GetAncestorsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: [Comment(text: "ancestor")]);

        var search = new Mock<ICommentSearch>();
        search.Setup(expression: x => x.SearchAsync(It.IsAny<SearchCommentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: (SearchCommentRequest request, CancellationToken _) =>
                new CommentPageDto(Items:
                    [
                        Comment(
                            text: $"{request.Query}|partial={request.Partial}|text={request.SearchText}|" +
                                  $"user={request.SearchUserName}|comments={request.SearchComments}|" +
                                  $"replies={request.SearchReplies}|cursor={request.Cursor}")
                    ],
                    NextCursor: request.Cursor, Sort: "search", Descending: false));

        services.AddSingleton(implementationInstance: commentService.Object);
        services.AddSingleton(implementationInstance: search.Object);
        services.AddGraphQL();
        return services.BuildServiceProvider();
    }
}
