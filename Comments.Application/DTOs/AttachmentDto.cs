namespace Comments.Application.DTOs;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, int? Width, int? Height);