using FluentValidation;
using GameLibApi.DTOs;

namespace GameLibApi.Validators;

public class GameValidators : AbstractValidator<CreateGameDto>
{
    public GameValidators()
    {
        // Правило для Title
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Название игры обязательно")
            .MaximumLength(200).WithMessage("Название не может быть длиннее 200 символов");

        // Price
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Цена не может быть отрицательной");

        // Description 
        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Описание слишком длинное");
        
        // Если указан Разработчик, то он не должен быть короче 2 символов
        RuleFor(x => x.Developer)
            .MinimumLength(2).When(x => !string.IsNullOrEmpty(x.Developer));
    }
}

public class UpdateGameDtoValidator : AbstractValidator<UpdateGameDto>
{
    public UpdateGameDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be non-negative")
            .LessThanOrEqualTo(1000000).WithMessage("Price is too high");

        RuleFor(x => x.Developer)
            .MaximumLength(200).WithMessage("Developer name must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Developer));

        RuleFor(x => x.ReleaseDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Release date cannot be in the future")
            .When(x => x.ReleaseDate.HasValue);
    }
}