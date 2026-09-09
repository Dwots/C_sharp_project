using FluentValidation;
using GameLibApi.DTOs;

namespace GameLibApi.Validators;

public class AddGameToLibraryDtoValidator : AbstractValidator<AddGameToLibraryDto>
{
    public AddGameToLibraryDtoValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("GameId must be a positive number");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 10).WithMessage("Rating must be between 1 and 10")
            .When(x => x.Rating.HasValue);
    }
}

public class UpdateUserGameDtoValidator : AbstractValidator<UpdateUserGameDto>
{
    public UpdateUserGameDtoValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 10).WithMessage("Rating must be between 1 and 10")
            .When(x => x.Rating.HasValue);
    }
}