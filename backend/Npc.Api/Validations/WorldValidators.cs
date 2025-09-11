using FluentValidation;
using Npc.Api.Dtos;

namespace Npc.Api.Validations
{
    public class WorldValidator : AbstractValidator<WorldRequest>
    {
        public WorldValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Description).MaximumLength(4000).When(x => x.Description != null);
        }
    }    
}