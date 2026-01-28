using Dawning.Agents.Abstractions.RAG;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// RAG 配置选项验证器
/// </summary>
public class RAGOptionsValidator : AbstractValidator<RAGOptions>
{
    public RAGOptionsValidator()
    {
        RuleFor(x => x.ChunkSize)
            .GreaterThan(0)
            .WithMessage("ChunkSize 必须大于 0")
            .LessThanOrEqualTo(10000)
            .WithMessage("ChunkSize 不能超过 10000");

        RuleFor(x => x.ChunkOverlap)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ChunkOverlap 不能为负数")
            .LessThan(x => x.ChunkSize)
            .WithMessage("ChunkOverlap 必须小于 ChunkSize");

        RuleFor(x => x.TopK)
            .GreaterThan(0)
            .WithMessage("TopK 必须大于 0")
            .LessThanOrEqualTo(100)
            .WithMessage("TopK 不能超过 100");

        RuleFor(x => x.MinScore)
            .InclusiveBetween(0f, 1f)
            .WithMessage("MinScore 必须在 0.0 到 1.0 之间");

        RuleFor(x => x.EmbeddingModel)
            .NotEmpty()
            .WithMessage("EmbeddingModel 不能为空");

        RuleFor(x => x.ContextTemplate)
            .NotEmpty()
            .WithMessage("ContextTemplate 不能为空");
    }
}
