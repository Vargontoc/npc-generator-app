using FluentValidation;
using Npc.Api.Dtos;

namespace Npc.Api.Validations
{
    public class TtsSynthesizeRequestValidator : AbstractValidator<TtsSynthesizeRequest>
    {
        public TtsSynthesizeRequestValidator()
        {
            RuleFor(x => x.Text)
                .NotEmpty()
                .MinimumLength(1)
                .MaximumLength(5000)
                .WithMessage("Text must be between 1 and 5000 characters");

            RuleFor(x => x.Voice)
                .NotEmpty()
                .MinimumLength(1)
                .MaximumLength(100)
                .WithMessage("Voice must be specified and valid");

            RuleFor(x => x.Format)
                .Must(format => new[] { "wav", "mp3", "ogg", "flac" }.Contains(format.ToLower()))
                .WithMessage("Format must be one of: wav, mp3, ogg, flac");

            RuleFor(x => x.Sample_Rate)
                .InclusiveBetween(8000, 48000)
                .When(x => x.Sample_Rate.HasValue)
                .WithMessage("Sample rate must be between 8000 and 48000 Hz");

            RuleFor(x => x.Length_Scale)
                .InclusiveBetween(0.1, 3.0)
                .When(x => x.Length_Scale.HasValue)
                .WithMessage("Length scale must be between 0.1 and 3.0");

            RuleFor(x => x.Noise_Scale)
                .InclusiveBetween(0.0, 1.0)
                .When(x => x.Noise_Scale.HasValue)
                .WithMessage("Noise scale must be between 0.0 and 1.0");
        }
    }

    public class ImageRequestValidator : AbstractValidator<ImageRequest>
    {
        public ImageRequestValidator()
        {
            RuleFor(x => x.Prompt)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(2000)
                .WithMessage("Prompt must be between 3 and 2000 characters");

            RuleFor(x => x.Negative_Prompt)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrEmpty(x.Negative_Prompt))
                .WithMessage("Negative prompt cannot exceed 1000 characters");

            RuleFor(x => x.Width)
                .Must(w => new[] { 256, 512, 768, 1024, 1536 }.Contains(w))
                .WithMessage("Width must be one of: 256, 512, 768, 1024, 1536");

            RuleFor(x => x.Height)
                .Must(h => new[] { 256, 512, 768, 1024, 1536 }.Contains(h))
                .WithMessage("Height must be one of: 256, 512, 768, 1024, 1536");

            RuleFor(x => x.Steps)
                .InclusiveBetween(1, 100)
                .WithMessage("Steps must be between 1 and 100");

            RuleFor(x => x.Cfg)
                .InclusiveBetween(1.0, 30.0)
                .WithMessage("CFG must be between 1.0 and 30.0");
        }
    }

    public class AssignAvatarRequestValidator : AbstractValidator<AssignAvatarRequest>
    {
        public AssignAvatarRequestValidator()
        {
            RuleFor(x => x.JobId)
                .NotEmpty()
                .WithMessage("Job ID is required");
        }
    }
}