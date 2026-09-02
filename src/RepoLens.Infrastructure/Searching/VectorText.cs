using System.Globalization;


namespace RepoLens.Infrastructure.Searching;

/// <summary>Serializes float vectors to/from PostgreSQL pgvector literals.</summary>
public static class VectorText
{
    public static string ToLiteral(IReadOnlyList<float> vector) =>
        $"[{string.Join(',', vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}]";

    public static float[] FromLiteral(string literal)
    {
        var span = literal.AsSpan().Trim();
        if (span.Length < 2 || span[0] != '[' || span[^1] != ']')
        {
            throw new FormatException($"Not a pgvector literal: {literal[..Math.Min(literal.Length, 40)]}");
        }

        var body = span[1..^1].ToString();
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        // pgvector text output is tolerant of spaces; parse manually so any
        // float representation (fixed/scientific) is accepted.
        return body
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => float.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();
    }

}
