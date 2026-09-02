using Markdig;
using RepoLens.Application.Enrichment;

namespace RepoLens.Infrastructure.Content;

/// <summary>
/// Markdown → plain-text normalization for stored README content. Uses Markdig's
/// plain-text renderer; failure on exotic input degrades to the raw text rather
/// than throwing (searchable content is better than no content).
/// </summary>
public sealed class MarkdigTextNormalizer : ITextNormalizer
{
    public string ToPlainText(string raw)
    {
        try
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            return Markdig.Markdown.ToPlainText(raw, pipeline);
        }
        catch
        {
            return raw;
        }
    }
}
