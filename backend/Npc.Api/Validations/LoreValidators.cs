using FluentValidation;
using Npc.Api.Dtos;

namespace Npc.Api.Validations
{
    public class LoreRequestValidator : AbstractValidator<LoreRequest>
    {
        public LoreRequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(200)
                .WithMessage("Title must be between 3 and 200 characters");

            RuleFor(x => x.Text)
                .MaximumLength(10000)
                .When(x => x.Text != null)
                .WithMessage("Text cannot exceed 10000 characters");

            RuleFor(x => x.WorldId)
                .Must(id => id == null || id != Guid.Empty)
                .WithMessage("World ID must be valid if provided");
        }
    }

    public class LoreSuggestRequestValidator : AbstractValidator<LoreSuggestRequest>
    {
        public LoreSuggestRequestValidator()
        {
            RuleFor(x => x.Prompt)
                .NotEmpty()
                .MinimumLength(10)
                .MaximumLength(1000)
                .WithMessage("Prompt must be between 10 and 1000 characters");

            RuleFor(x => x.Count)
                .GreaterThan(0)
                .LessThanOrEqualTo(10)
                .WithMessage("Count must be between 1 and 10");

            RuleFor(x => x.WorldId)
                .Must(id => id == null || id != Guid.Empty)
                .WithMessage("World ID must be valid if provided");
        }
    }
}