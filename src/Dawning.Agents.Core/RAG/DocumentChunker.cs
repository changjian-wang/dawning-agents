using System.Text;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// 文档分块器 - 将长文本分割为适合向量化的小块
/// </summary>
/// <remarks>
/// <para>支持多种分块策略：</para>
/// <list type="bullet">
/// <item>固定大小分块（带重叠）</item>
/// <item>按段落分块</item>
/// <item>按句子分块</item>
/// </list>
/// </remarks>
public sealed class DocumentChunker
{
    private readonly RAGOptions _options;
    private readonly ILogger<DocumentChunker> _logger;

    /// <summary>
    /// 创建文档分块器
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
    /// 将文本分割为块
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="documentId">文档 ID（可选）</param>
    /// <param name="metadata">元数据（可选）</param>
    /// <returns>文档块列表（不含嵌入向量）</returns>
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

        // 先按段落分割
        var paragraphs = SplitIntoParagraphs(text);

        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            // 如果单个段落就超过 chunk size，需要进一步分割
            if (paragraph.Length > chunkSize)
            {
                // 先保存当前累积的内容
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

                // 分割大段落
                var subChunks = SplitLargeParagraph(paragraph, chunkSize, overlap);
                foreach (var sub in subChunks)
                {
                    chunks.Add(CreateChunk(sub, documentId, chunkIndex++, metadata));
                }
            }
            else if (currentChunk.Length + paragraph.Length + 1 > chunkSize)
            {
                // 当前块已满，保存并开始新块
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

                    // 添加重叠部分
                    var overlapText = GetOverlapText(currentChunk.ToString(), overlap);
                    currentChunk.Clear();
                    currentChunk.Append(overlapText);
                }

                currentChunk.Append(paragraph);
                currentChunk.Append('\n');
            }
            else
            {
                // 继续累积
                currentChunk.Append(paragraph);
                currentChunk.Append('\n');
            }
        }

        // 保存最后一个块
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
    /// 按段落分割文本
    /// </summary>
    private static List<string> SplitIntoParagraphs(string text)
    {
        return text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    /// <summary>
    /// 分割超大段落
    /// </summary>
    private static List<string> SplitLargeParagraph(string paragraph, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var start = 0;

        // 确保 overlap 不超过 chunkSize 的一半，防止无限循环
        var safeOverlap = Math.Min(overlap, chunkSize / 2);

        while (start < paragraph.Length)
        {
            var length = Math.Min(chunkSize, paragraph.Length - start);
            var chunk = paragraph.Substring(start, length);

            // 尝试在句子边界处分割
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

            // 确保至少前进 1 个字符，防止无限循环
            var advance = Math.Max(1, length - safeOverlap);
            start += advance;
        }

        return chunks;
    }

    /// <summary>
    /// 获取重叠文本
    /// </summary>
    private static string GetOverlapText(string text, int overlapSize)
    {
        if (text.Length <= overlapSize)
        {
            return text;
        }

        // 尝试从句子边界开始
        var overlapStart = text.Length - overlapSize;
        var sentenceStart = text.IndexOfAny(['.', '!', '?', '。', '！', '？'], overlapStart);

        if (sentenceStart > 0 && sentenceStart < text.Length - 1)
        {
            return text[(sentenceStart + 1)..].Trim();
        }

        return text[overlapStart..].Trim();
    }

    /// <summary>
    /// 创建文档块
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
