using System.Text.Json;
using System.Text.Json.Serialization;
using ContractIQ.Application.Knowledge;

namespace ContractIQ.Infrastructure.Knowledge;

internal sealed class FileSystemKnowledgeDocumentCatalog(KnowledgeOptions options)
    : IKnowledgeDocumentCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<KnowledgeDocumentSource>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        string contentRoot = ResolveContentRoot(options.ContentRoot);
        string manifestPath = Path.Combine(contentRoot, "manifest.json");

        await using FileStream manifest = File.OpenRead(manifestPath);
        KnowledgeManifestEntry[] entries = await JsonSerializer.DeserializeAsync<KnowledgeManifestEntry[]>(
            manifest,
            SerializerOptions,
            cancellationToken) ?? [];

        var documents = new List<KnowledgeDocumentSource>(entries.Length);

        foreach (KnowledgeManifestEntry entry in entries)
        {
            string sourcePath = Path.GetFullPath(Path.Combine(contentRoot, entry.File));
            EnsureInsideContentRoot(contentRoot, sourcePath);
            string content = await File.ReadAllTextAsync(sourcePath, cancellationToken);

            documents.Add(new KnowledgeDocumentSource(
                entry.DocumentKey,
                entry.Title,
                entry.DocumentType,
                entry.Version,
                entry.Language,
                entry.CustomerId,
                entry.ContractId,
                entry.EffectiveFrom,
                entry.EffectiveTo,
                Path.GetRelativePath(contentRoot, sourcePath).Replace('\\', '/'),
                content));
        }

        return documents;
    }

    private static string ResolveContentRoot(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, configuredPath);

            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(configuredPath);
    }

    private static void EnsureInsideContentRoot(string contentRoot, string sourcePath)
    {
        string root = Path.GetFullPath(contentRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!sourcePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Knowledge manifest references a file outside its content root.");
        }
    }

    private sealed record KnowledgeManifestEntry(
        string DocumentKey,
        string Title,
        KnowledgeDocumentType DocumentType,
        string Version,
        string Language,
        Guid? CustomerId,
        Guid? ContractId,
        DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        string File);
}
