namespace RepoLens.Application.Enrichment;

/// <summary>
/// Normalizes raw content (currently: README markdown) into plain text.
/// Implementation lives in Infrastructure; Application only consumes text.
/// </summary>
public interface ITextNormalizer
{
    string ToPlainText(string raw);
}
