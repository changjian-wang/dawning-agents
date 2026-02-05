using System.Diagnostics;
using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.Cache;
using Dawning.Agents.Core.RAG;
using Dawning.Agents.Samples.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Samples.RAG;

/// <summary>
/// RAG 示例 - 展示向量存储和语义缓存
/// </summary>
public class RAGSample : SampleBase
{
    protected override string SampleName => "RAG & Semantic Cache";

    protected override void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 注册 Embedding Provider
        services.AddSingleton<IEmbeddingProvider, SimpleEmbeddingProvider>();

        // 注册 VectorStore
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        // 注册 SemanticCache
        services.Configure<SemanticCacheOptions>(
            configuration.GetSection(SemanticCacheOptions.SectionName)
        );
        services.AddSingleton<ISemanticCache, SemanticCache>();
    }

    protected override async Task ExecuteAsync()
    {
        ConsoleHelper.PrintTitle("选择演示");
        Console.WriteLine("  [1] 向量存储 - 文档嵌入和检索");
        Console.WriteLine("  [2] 语义缓存 - 减少重复 LLM 调用");
        Console.WriteLine("  [3] 知识库问答 - RAG 完整流程");
        Console.WriteLine("  [A] 运行全部");
        Console.WriteLine();
        Console.Write("请选择 (1-3/A): ");

        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "1":
                await RunVectorStoreDemoAsync();
                break;
            case "2":
                await RunSemanticCacheDemoAsync();
                break;
            case "3":
                await RunKnowledgeBaseDemoAsync();
                break;
            case "A":
                await RunVectorStoreDemoAsync();
                ConsoleHelper.WaitForKey();
                await RunSemanticCacheDemoAsync();
                ConsoleHelper.WaitForKey();
                await RunKnowledgeBaseDemoAsync();
                break;
            default:
                await RunSemanticCacheDemoAsync();
                break;
        }
    }

    /// <summary>
    /// 向量存储演示
    /// </summary>
    private async Task RunVectorStoreDemoAsync()
    {
        ConsoleHelper.PrintTitle("向量存储演示");
        ConsoleHelper.PrintInfo("演示文档嵌入和相似性检索");
        Console.WriteLine();

        var vectorStore = GetService<IVectorStore>();
        var embeddingProvider = GetService<IEmbeddingProvider>();

        // 准备文档
        var documents = new[]
        {
            "Dawning.Agents 是一个企业级 .NET AI Agent 框架",
            "框架支持五种 Memory 策略：Buffer, Window, Summary, Adaptive, Vector",
            "SemanticCache 可以缓存相似查询，减少 LLM 调用成本",
            "MCP 协议允许与 Claude Desktop 和 Cursor 互操作",
            "ReAct Agent 使用 Thought-Action-Observation 循环解决问题",
        };

        // 嵌入并存储文档
        ConsoleHelper.PrintStep(1, "嵌入文档到向量存储");
        for (int i = 0; i < documents.Length; i++)
        {
            var embedding = await embeddingProvider.EmbedAsync(documents[i]);
            var chunk = new DocumentChunk
            {
                Id = $"doc_{i}",
                Content = documents[i],
                Embedding = embedding,
                DocumentId = "demo",
                ChunkIndex = i,
            };
            await vectorStore.AddAsync(chunk);
            ConsoleHelper.PrintDim($"  已添加: {documents[i][..Math.Min(50, documents[i].Length)]}...");
        }

        Console.WriteLine();
        ConsoleHelper.PrintSuccess($"已存储 {vectorStore.Count} 个文档片段");

        // 检索测试
        ConsoleHelper.PrintStep(2, "相似性检索");
        var queries = new[]
        {
            "什么是 Memory 策略？",
            "如何减少 API 调用成本？",
            "什么是 MCP？",
        };

        foreach (var query in queries)
        {
            Console.WriteLine();
            ConsoleHelper.PrintInfo($"查询: {query}");

            var queryEmbedding = await embeddingProvider.EmbedAsync(query);
            var results = await vectorStore.SearchAsync(queryEmbedding, topK: 2, minScore: 0.0f);

            foreach (var result in results)
            {
                ConsoleHelper.PrintDim($"  [{result.Score:F3}] {result.Chunk.Content}");
            }
        }
    }

    /// <summary>
    /// 语义缓存演示
    /// </summary>
    private async Task RunSemanticCacheDemoAsync()
    {
        ConsoleHelper.PrintTitle("语义缓存演示");
        ConsoleHelper.PrintInfo("演示如何用 SemanticCache 减少重复 LLM 调用");
        Console.WriteLine();

        var cache = GetService<ISemanticCache>();
        var provider = GetService<ILLMProvider>();

        // 清空缓存
        await cache.ClearAsync();

        // 测试查询
        var queries = new[]
        {
            "什么是人工智能？",
            "AI 是什么意思？",            // 语义相似
            "解释一下机器学习",
            "什么是 machine learning？",  // 语义相似
            "今天天气怎么样？",           // 不相关
        };

        ConsoleHelper.PrintStep(1, "首次查询（缓存未命中）");
        Console.WriteLine();

        // 预热缓存
        var firstQuery = queries[0];
        var sw = Stopwatch.StartNew();
        var response = await provider.ChatAsync(new List<ChatMessage>
        {
            new("user", firstQuery),
        });
        sw.Stop();

        await cache.SetAsync(firstQuery, response.Content!);
        ConsoleHelper.PrintInfo($"查询: {firstQuery}");
        ConsoleHelper.PrintDim($"响应: {response.Content?[..Math.Min(100, response.Content?.Length ?? 0)]}...");
        ConsoleHelper.PrintWarning($"耗时: {sw.ElapsedMilliseconds}ms (LLM 调用)");

        Console.WriteLine();
        ConsoleHelper.PrintStep(2, "相似查询（测试缓存命中）");
        Console.WriteLine();

        // 测试缓存
        foreach (var query in queries.Skip(1))
        {
            sw.Restart();
            var cached = await cache.GetAsync(query);
            sw.Stop();

            ConsoleHelper.PrintInfo($"查询: {query}");

            if (cached != null)
            {
                ConsoleHelper.PrintSuccess($"缓存命中! 相似度: {cached.SimilarityScore:F3}");
                ConsoleHelper.PrintDim($"原始查询: {cached.OriginalQuery}");
                ConsoleHelper.PrintColored($"耗时: {sw.ElapsedMilliseconds}ms (缓存)", ConsoleColor.Green);
            }
            else
            {
                ConsoleHelper.PrintWarning("缓存未命中，需要调用 LLM");

                sw.Restart();
                var llmResponse = await provider.ChatAsync(new List<ChatMessage>
                {
                    new("user", query),
                });
                sw.Stop();

                await cache.SetAsync(query, llmResponse.Content!);
                ConsoleHelper.PrintColored($"耗时: {sw.ElapsedMilliseconds}ms (LLM 调用)", ConsoleColor.Yellow);
            }
            Console.WriteLine();
        }

        // 统计
        ConsoleHelper.PrintSection("统计");
        Console.WriteLine($"  缓存条目数: {cache.Count}");
        ConsoleHelper.PrintSuccess("语义缓存可显著降低相似查询的响应时间和成本");
    }

    /// <summary>
    /// 知识库问答演示
    /// </summary>
    private async Task RunKnowledgeBaseDemoAsync()
    {
        ConsoleHelper.PrintTitle("知识库问答演示");
        ConsoleHelper.PrintInfo("演示 RAG 完整流程：检索 → 增强 → 生成");
        Console.WriteLine();

        var vectorStore = GetService<IVectorStore>();
        var embeddingProvider = GetService<IEmbeddingProvider>();
        var provider = GetService<ILLMProvider>();

        // 清空并重建知识库
        await vectorStore.ClearAsync();

        // 添加知识库文档
        var knowledgeBase = new[]
        {
            "公司名称：Dawning Tech，成立于 2024 年，专注于 AI Agent 技术",
            "主要产品：Dawning.Agents 框架，一个企业级 .NET AI Agent 解决方案",
            "技术栈：.NET 10.0, C# 13, 支持 Ollama/OpenAI/Azure OpenAI",
            "联系方式：support@dawning.tech，工作时间 9:00-18:00",
            "退款政策：购买后 30 天内可全额退款，需提供购买凭证",
            "技术支持：提供 7x24 小时在线支持，响应时间不超过 2 小时",
        };

        ConsoleHelper.PrintStep(1, "构建知识库");
        for (int i = 0; i < knowledgeBase.Length; i++)
        {
            var embedding = await embeddingProvider.EmbedAsync(knowledgeBase[i]);
            await vectorStore.AddAsync(new DocumentChunk
            {
                Id = $"kb_{i}",
                Content = knowledgeBase[i],
                Embedding = embedding,
                DocumentId = "company_kb",
            });
        }
        ConsoleHelper.PrintSuccess($"已加载 {knowledgeBase.Length} 条知识");

        // RAG 问答
        Console.WriteLine();
        ConsoleHelper.PrintStep(2, "RAG 问答");

        var questions = new[]
        {
            "公司什么时候成立的？",
            "如何联系技术支持？",
            "退款政策是什么？",
        };

        foreach (var question in questions)
        {
            Console.WriteLine();
            ConsoleHelper.PrintInfo($"问题: {question}");

            // 1. 检索相关文档
            var queryEmbedding = await embeddingProvider.EmbedAsync(question);
            var relevantDocs = await vectorStore.SearchAsync(queryEmbedding, topK: 2, minScore: 0.0f);

            var context = string.Join("\n", relevantDocs.Select(d => d.Chunk.Content));
            ConsoleHelper.PrintDim($"检索到 {relevantDocs.Count} 条相关文档");

            // 2. 增强生成
            var messages = new List<ChatMessage>
            {
                new("system", $"你是客服助手。根据以下知识库回答问题：\n\n{context}"),
                new("user", question),
            };

            var response = await provider.ChatAsync(messages);
            ConsoleHelper.PrintColored($"回答: {response.Content}", ConsoleColor.Green);
        }
    }
}
