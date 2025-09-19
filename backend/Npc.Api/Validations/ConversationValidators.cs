using FluentValidation;
using Npc.Api.Dtos;

namespace Npc.Api.Validations
{
    public class ConversationCreateValidator : AbstractValidator<ConversationCreateRequest>
    {
        public ConversationCreateValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(200)
                .WithMessage("Title must be between 3 and 200 characters");
        }
    }

    public class UtteranceCreateValidator : AbstractValidator<UtteranceCreateRequest>
    {
        public UtteranceCreateValidator()
        {
            RuleFor(x => x.Text)
                .NotEmpty()
                .MinimumLength(1)
                .MaximumLength(5000)
                .WithMessage("Text must be between 1 and 5000 characters");

            RuleFor(x => x.CharacterId)
                .Must(id => id == null || id != Guid.Empty)
                .WithMessage("Character ID must be valid if provided");
        }
    }

    public class UtteranceUpdateValidator : AbstractValidator<UtteranceUpdateRequest>
    {
        public UtteranceUpdateValidator()
        {
            RuleFor(x => x.Text)
                .NotEmpty()
                .MinimumLength(1)
                .MaximumLength(5000)
                .WithMessage("Text must be between 1 and 5000 characters");

            RuleFor(x => x.CharacterId)
                .Must(id => id == null || id != Guid.Empty)
                .WithMessage("Character ID must be valid if provided");

            RuleFor(x => x.Version)
                .GreaterThan(0)
                .WithMessage("Version must be greater than 0");

            RuleFor(x => x.Tags)
                .Must(tags => tags == null || tags.All(t => !string.IsNullOrWhiteSpace(t) && t.Length <= 50))
                .WithMessage("Tags must not be empty and max 50 characters each");
        }
    }

    public class BranchCreateValidator : AbstractValidator<BranchCreateRequest>
    {
        public BranchCreateValidator()
        {
            RuleFor(x => x.FromUtteranceId)
                .NotEmpty()
                .WithMessage("From utterance ID is required");

            RuleFor(x => x.ToUtteranceId)
                .NotEmpty()
                .WithMessage("To utterance ID is required");

            RuleFor(x => x.Weight)
                .GreaterThan(0)
                .LessThanOrEqualTo(100)
                .When(x => x.Weight.HasValue)
                .WithMessage("Weight must be between 0 and 100 if provided");
        }
    }
}