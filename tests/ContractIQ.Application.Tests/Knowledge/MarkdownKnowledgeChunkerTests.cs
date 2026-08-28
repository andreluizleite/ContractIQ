using ContractIQ.Application.Knowledge;
using Xunit;

namespace ContractIQ.Application.Tests.Knowledge;

public sealed class MarkdownKnowledgeChunkerTests
{
    [Fact]
    public void Chunk_preserves_section_page_and_citation_content()
    {
        const string markdown = """
            # Agreement

            <!-- page: 2 -->

            ## Termination

            Thirty days notice is required.

            A penalty may apply.
            """;
        var chunker = new MarkdownKnowledgeChunker();

        IReadOnlyList<KnowledgeChunkDraft> chunks = chunker.Chunk(markdown);

        Assert.Equal(3, chunks.Count);
        Assert.Equal("Document", chunks[0].Section);
        Assert.Equal(1, chunks[0].Page);
        Assert.Equal("Termination", chunks[1].Section);
        Assert.Equal(2, chunks[1].Page);
        Assert.Equal("Thirty days notice is required.", chunks[1].Content);
        Assert.Equal(64, chunks[1].Checksum.Length);
    }
}
