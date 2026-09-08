using Comments.Application.Data;
using Comments.Domain.Entities;

namespace Comments.Application.Abstractions;

public interface IAttachmentStorageService
{
    Task<Attachment> SaveAsync(AttachmentInput input, CancellationToken ct);
}
