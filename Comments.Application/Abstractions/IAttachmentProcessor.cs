namespace Comments.Application.Abstractions;

public interface IAttachmentProcessor
{
    Task ProcessAsync(Guid attachmentId, CancellationToken ct);
}
