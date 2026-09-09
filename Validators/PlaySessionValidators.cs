using FluentValidation;
using GameLibApi.DTOs;

namespace GameLibApi.Validators;

public class CreatePlaySessionDtoValidator : AbstractValidator<CreatePlaySessionDto>
{
    // Совпадает с набором значений, которым заполняет таблицу генератор данных.
    private static readonly string[] AllowedPlatforms = { "PC", "PlayStation", "Xbox", "Switch" };

    public CreatePlaySessionDtoValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("GameId must be a positive number");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 1440).WithMessage("DurationMinutes must be between 1 and 1440");

        RuleFor(x => x.Platform)
            .NotEmpty().WithMessage("Platform is required")
            .Must(p => AllowedPlatforms.Contains(p))
            .WithMessage($"Platform must be one of: {string.Join(", ", AllowedPlatforms)}");
    }
}
