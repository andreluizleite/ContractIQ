using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ContractIQ.Application.Knowledge;

public sealed partial class MarkdownKnowledgeChunker(int maximumCharacters = 1_200)
{
    private readonly int _maximumCharacters = maximumCharacters > 100
        ? maximumCharacters
        : throw new ArgumentOutOfRangeException(nameof(maximumCharacters));

    public IReadOnlyList<KnowledgeChunkDraft> Chunk(string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markdown);

        var chunks = new List<KnowledgeChunkDraft>();
        var buffer = new StringBuilder();
        string section = "Document";
        int page = 1;

        void Flush()
        {
            string content = buffer.ToString().Trim();
            buffer.Clear();

            if (content.Length == 0)
            {
                return;
            }

            chunks.Add(new KnowledgeChunkDraft(
                chunks.Count,
                section,
                page,
                content,
                ComputeChecksum(content)));
        }

        foreach (string rawLine in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            string line = rawLine.Trim();
            Match pageMatch = PageMarkerRegex().Match(line);

            if (pageMatch.Success)
            {
                Flush();
                page = int.Parse(pageMatch.Groups[1].Value);
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                section = line[3..].Trim();
                continue;
            }

            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            if (buffer.Length > 0 && buffer.Length + line.Length + 1 > _maximumCharacters)
            {
                Flush();
            }

            if (buffer.Length > 0)
            {
                buffer.Append(' ');
            }

            buffer.Append(line);
        }

        Flush();
        return chunks;
    }

    public static string ComputeChecksum(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [GeneratedRegex("^<!--\\s*page:\\s*(\\d+)\\s*-->$", RegexOptions.IgnoreCase)]
    private static partial Regex PageMarkerRegex();
}

public sealed record KnowledgeChunkDraft(
    int Index,
    string Section,
    int Page,
    string Content,
    string Checksum);
