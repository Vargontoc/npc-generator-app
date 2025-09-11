using FluentValidation;
using Npc.Api.Dtos;

namespace Npc.Api.Validations
{
    public class CharacterCreateValidator : AbstractValidator<CharacterRequest>
    {
        public CharacterCreateValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
            RuleFor(x => x.Age).GreaterThan(0);
            RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
            RuleFor(x => x.AvatarUrl).Must(url => string.IsNullOrEmpty(url) || Uri.IsWellFormedUriString(url, UriKind.Absolute) || Uri.IsWellFormedUriString(url, UriKind.Relative)).WithMessage("Avatar must be a valid URL if provided.");
        }
    }

}