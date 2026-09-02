using RepoLens.Application.Enrichment;
using RepoLens.Application.Searching;

namespace RepoLens.UnitTests.Searching;

public sealed class SearchDocumentBuilderTests
{
    private static RepositoryDetailSource Source(string name = "pgroll", string? readme = "# Title\n\ndescription here") =>
        new()
        {
            Id = 1,
            GitHubId = 101,
            Owner = "xataio",
            Name = name,
            FullName = $"xataio/{name}",
            Description = "Zero-downtime migrations",
            HtmlUrl = "https://github.com/xataio/pgroll",
            DefaultBranch = "main",
            PrimaryLanguage = "Go",
            Stars = 100,
            Forks = 1,
            OpenIssues = 1,
            IsArchived = false,
            IsFork = false,
            LicenseSpdx = "Apache-2.0",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            PushedAtUtc = null,
            DiscoveredAtUtc = DateTimeOffset.UtcNow,
            EnrichedAtUtc = DateTimeOffset.UtcNow,
            Topics = ["postgres", "migrations"],
            Languages = [],
            ReadmeText = readme,
            ReadmeContentHash = null,
            ReadmeFetchedAtUtc = null,
        };

    [Fact]
    public void Build_SortsAndJoinsTopicsCaseInsensitively()
    {
        var document = SearchDocumentBuilder.Build(Source());

        Assert.Equal("migrations postgres", document.Topics);
        Assert.Equal("pgroll", document.Name);
    }

    [Fact]
    public void Build_HashChangesWhenContentChanges()
    {
        var original = SearchDocumentBuilder.Build(Source());
        var changedReadme = SearchDocumentBuilder.Build(Source(readme: "a completely different readme"));
        var changedName = SearchDocumentBuilder.Build(Source(name: "pgroll-v2"));

        Assert.NotEqual(original.ContentHash, changedReadme.ContentHash);
        Assert.NotEqual(original.ContentHash, changedName.ContentHash);
    }

    [Fact]
    public void Build_IdenticalContentProducesIdenticalHash()
    {
        var first = SearchDocumentBuilder.Build(Source());
        var second = SearchDocumentBuilder.Build(Source());

        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void EmbeddingText_CapsReadmeLength()
    {
        var longReadme = new string('x', SearchDocumentBuilder.EmbeddingReadmeMaxLength + 500);
        var document = SearchDocumentBuilder.Build(Source(readme: longReadme));

        var text = SearchDocumentBuilder.EmbeddingText(document);

        Assert.True(text.Length <= SearchDocumentBuilder.EmbeddingReadmeMaxLength + 256,
            "Embedding text should cap the README contribution.");
    }
}
