using System.Security.Cryptography;
using System.Text;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// 简单嵌入提供者 - 基于字符特征的本地向量化
/// </summary>
/// <remarks>
/// <para>这是一个简化的本地实现，用于测试和演示目的。</para>
/// <para>生产环境应使用 OpenAI/Azure 的 Embedding API。</para>
/// <para>
/// 实现原理：
/// 1. 将文本转为小写并分词
/// 2. 计算每个词的哈希值
/// 3. 将哈希值映射到向量空间
/// 4. 归一化向量
/// </para>
/// </remarks>
public class SimpleEmbeddingProvider : IEmbeddingProvider
{
    private readonly int _dimensions;
    private readonly ILogger<SimpleEmbeddingProvider> _logger;

    /// <summary>
    /// 创建简单嵌入提供者
    /// </summary>
    /// <param name="dimensions">向量维度（默认 384）</param>
    /// <param name="logger">日志记录器</param>
    public SimpleEmbeddingProvider(
        int dimensions = 384,
        ILogger<SimpleEmbeddingProvider>? logger = null
    )
    {
        _dimensions = dimensions;
        _logger = logger ?? NullLogger<SimpleEmbeddingProvider>.Instance;
    }

    /// <inheritdoc />
    public string Name => "SimpleEmbedding";

    /// <inheritdoc />
    public int Dimensions => _dimensions;

    /// <inheritdoc />
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new float[_dimensions]);
        }

        var embedding = ComputeEmbedding(text);
        return Task.FromResult(embedding);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default
    )
    {
        var embeddings = texts.Select(ComputeEmbedding).ToList();
        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    /// <summary>
    /// 计算文本的嵌入向量
    /// </summary>
    private float[] ComputeEmbedding(string text)
    {
        var vector = new float[_dimensions];

        // 预处理文本
        var normalizedText = text.ToLowerInvariant();
        var tokens = Tokenize(normalizedText);

        if (tokens.Count == 0)
        {
            return vector;
        }

        // 为每个词计算特征并累加到向量
        foreach (var token in tokens)
        {
            var tokenHash = ComputeHash(token);

            // 使用哈希值的不同部分映射到向量的不同位置
            for (var i = 0; i < Math.Min(tokenHash.Length * 8, _dimensions); i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;

                if (byteIndex < tokenHash.Length)
                {
                    var bit = (tokenHash[byteIndex] >> bitIndex) & 1;
                    vector[i % _dimensions] += bit == 1 ? 1f : -1f;
                }
            }

            // 添加 n-gram 特征
            for (var n = 2; n <= Math.Min(3, token.Length); n++)
            {
                for (var j = 0; j <= token.Length - n; j++)
                {
                    var ngram = token.Substring(j, n);
                    var ngramHash = ComputeHash(ngram);
                    var index = Math.Abs(BitConverter.ToInt32(ngramHash, 0)) % _dimensions;
                    vector[index] += 0.5f;
                }
            }
        }

        // 归一化向量
        Normalize(vector);

        _logger.LogDebug(
            "Computed embedding for text ({Length} chars, {Tokens} tokens)",
            text.Length,
            tokens.Count
        );

        return vector;
    }

    /// <summary>
    /// 分词
    /// </summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c > 127) // 包含中文字符
            {
                currentToken.Append(c);
            }
            else if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }

    /// <summary>
    /// 计算字符串的 SHA256 哈希
    /// </summary>
    private static byte[] ComputeHash(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }

    /// <summary>
    /// 归一化向量（L2 范数）
    /// </summary>
    private static void Normalize(float[] vector)
    {
        var norm = 0f;
        foreach (var v in vector)
        {
            norm += v * v;
        }

        norm = MathF.Sqrt(norm);

        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }
    }
}
