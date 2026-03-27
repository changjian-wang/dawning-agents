using Dawning.Agents.Abstractions.RAG;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="RAGOptions"/>.
/// </summary>
public class RAGOptionsValidator : AbstractValidator<RAGOptions>
{
    public RAGOptionsValidator()
    {
        RuleFor(x => x.ChunkSize)
            .GreaterThan(0)
            .WithMessage("ChunkSize must be greater than 0.")
            .LessThanOrEqualTo(10000)
            .WithMessage("ChunkSize must not exceed 10000.");

        RuleFor(x => x.ChunkOverlap)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ChunkOverlap must not be negative.")
            .LessThan(x => x.ChunkSize)
            .WithMessage("ChunkOverlap must be less than ChunkSize.");

        RuleFor(x => x.TopK)
            .GreaterThan(0)
            .WithMessage("TopK must be greater than 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("TopK must not exceed 100.");

        RuleFor(x => x.MinScore)
            .InclusiveBetween(0f, 1f)
            .WithMessage("MinScore must be between 0.0 and 1.0.");

        RuleFor(x => x.EmbeddingModel).NotEmpty().WithMessage("EmbeddingModel must not be empty.");

        RuleFor(x => x.ContextTemplate).NotEmpty().WithMessage("ContextTemplate must not be empty.");
    }
}
