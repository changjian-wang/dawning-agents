using System.Text;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Document chunker that splits long text into small chunks suitable for vectorization.
/// </summary>
/// <remarks>
/// <para>Supports multiple chunking strategies:</para>
/// <list type="bullet">
/// <item>Fixed-size chunking (with overlap).</item>
/// <item>Paragraph-based chunking.</item>
/// <item>Sentence-based chunking.</item>
/// </list>
/// </remarks>
public sealed class DocumentChunker
{
    private readonly RAGOptions _options;
    private readonly ILogger<DocumentChunker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentChunker"/> class.
    /// </summary>
    public DocumentChunker(
        IOptions<RAGOptions>? options = null,
        ILogger<DocumentChunker>? logger = null
    )
    {
        _options = options?.Value ?? new RAGOptions();
        _logger = logger ?? NullLogger<DocumentChunker>.Instance;
    }

    /// <summary>
    /// Splits text into document chunks.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="documentId">Optional document ID.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>A list of document chunks (without embeddings).</returns>
    public IReadOnlyList<DocumentChunk> ChunkText(
        string text,
        string? documentId = null,
        Dictionary<string, string>? metadata = null
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        documentId ??= Guid.NewGuid().ToString("N");
        metadata ??= [];

        var chunks = new List<DocumentChunk>();
        var chunkSize = _options.ChunkSize;
        var overlap = _options.ChunkOverlap;

        // Split by paragraphs first
        var paragraphs = SplitIntoParagraphs(text);

        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            // Further split paragraphs that exceed the chunk size
            if (paragraph.Length > chunkSize)
            {
                // Save current accumulated content first
                if (currentChunk.Length > 0)
                {
                    chunks.Add(
                        CreateChunk(
                            currentChunk.ToString().Trim(),
                            documentId,
                            chunkIndex++,
                            metadata
                        )
                    );
                    currentChunk.Clear();
                }

                // Split large paragraph
                var subChunks = SplitLargeParagraph(paragraph, chunkSize, overlap);
                foreach (var sub in subChunks)
                {
                    chunks.Add(CreateChunk(sub, documentId, chunkIndex++, metadata));
                }
            }
            else if (currentChunk.Length + paragraph.Length + 1 > chunkSize)
            {
                // Current chunk is full, save and start a new one
                if (currentChunk.Length > 0)
                {
                    chunks.Add(
                        CreateChunk(
                            currentChunk.ToString().Trim(),
                            documentId,
                            chunkIndex++,
                            metadata
                        )
                    );

                    // Add overlap portion
                    var overlapText = GetOverlapText(currentChunk.ToString(), overlap);
                    currentChunk.Clear();
                    currentChunk.Append(overlapText);
                }

                currentChunk.Append(paragraph);
                currentChunk.Append('\n');
            }
            else
            {
                // Continue accumulating
                currentChunk.Append(paragraph);
                currentChunk.Append('\n');
            }
        }

        // Save the last chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(
                CreateChunk(currentChunk.ToString().Trim(), documentId, chunkIndex, metadata)
            );
        }

        _logger.LogDebug(
            "Chunked document {DocumentId} into {Count} chunks (size={Size}, overlap={Overlap})",
            documentId,
            chunks.Count,
            chunkSize,
            overlap
        );

        return chunks;
    }

    /// <summary>
    /// Splits text into paragraphs.
    /// </summary>
    private static List<string> SplitIntoParagraphs(string text)
    {
        return text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    /// <summary>
    /// Splits an oversized paragraph into smaller chunks.
    /// </summary>
    private static List<string> SplitLargeParagraph(string paragraph, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var start = 0;

        // Ensure overlap does not exceed half the chunk size to prevent infinite loops
        var safeOverlap = Math.Min(overlap, chunkSize / 2);

        while (start < paragraph.Length)
        {
            var length = Math.Min(chunkSize, paragraph.Length - start);
            var chunk = paragraph.Substring(start, length);

            // Try to split at sentence boundaries
            if (start + length < paragraph.Length)
            {
                var lastPeriod = chunk.LastIndexOfAny(['.', '!', '?', '。', '！', '？']);
                if (lastPeriod > chunkSize / 2)
                {
                    chunk = chunk[..(lastPeriod + 1)];
                    length = chunk.Length;
                }
            }

            chunks.Add(chunk.Trim());

            // Advance by at least 1 character to prevent infinite loops
            var advance = Math.Max(1, length - safeOverlap);
            start += advance;
        }

        return chunks;
    }

    /// <summary>
    /// Gets the overlap text from the end of a chunk.
    /// </summary>
    private static string GetOverlapText(string text, int overlapSize)
    {
        if (text.Length <= overlapSize)
        {
            return text;
        }

        // Try to start from a sentence boundary
        var overlapStart = text.Length - overlapSize;
        var sentenceStart = text.IndexOfAny(['.', '!', '?', '。', '！', '？'], overlapStart);

        if (sentenceStart > 0 && sentenceStart < text.Length - 1)
        {
            return text[(sentenceStart + 1)..].Trim();
        }

        return text[overlapStart..].Trim();
    }

    /// <summary>
    /// Creates a document chunk.
    /// </summary>
    private static DocumentChunk CreateChunk(
        string content,
        string documentId,
        int chunkIndex,
        Dictionary<string, string> metadata
    )
    {
        return new DocumentChunk
        {
            Id = $"{documentId}_{chunkIndex}",
            Content = content,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Metadata = new Dictionary<string, string>(metadata),
        };
    }
}
