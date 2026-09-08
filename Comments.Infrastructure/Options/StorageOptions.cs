namespace Comments.Infrastructure.Options;

public sealed class StorageOptions
{
    public string Root { get; set; } = "../Comments.Storage/uploads";
    public int MaxTextBytes { get; set; } = 100 * 1024;
}
