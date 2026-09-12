namespace Comments.Infrastructure.Options;

public sealed class ElasticsearchOptions
{
    public string Uri { get; set; } = "http://localhost:9200";
    public string Index { get; set; } = "comments";
}
