using FluentValidation;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranch;

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.BranchNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BranchNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.UpdateUser).NotEmpty();

        // Base64 Logo Validation
        RuleFor(x => x.BranchLogoBase64)
            .Must(BeValidBase64)
            .WithMessage("Branch logo must be a valid Base64 string")
            .Must(BeValidBase64Size)
            .WithMessage("Branch logo size cannot exceed 5MB when decoded")
            .When(x => !string.IsNullOrEmpty(x.BranchLogoBase64));
    }

    private static bool BeValidBase64(string? base64String)
    {
        if (string.IsNullOrEmpty(base64String))
            return true;

        try
        {
            var base64Data = base64String;
            if (base64String.Contains(','))
            {
                base64Data = base64String.Split(',')[1];
            }

            Convert.FromBase64String(base64Data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidBase64Size(string? base64String)
    {
        if (string.IsNullOrEmpty(base64String))
            return true;

        try
        {
            var base64Data = base64String;
            if (base64String.Contains(','))
            {
                base64Data = base64String.Split(',')[1];
            }

            var bytes = Convert.FromBase64String(base64Data);
            const int maxSize = 5 * 1024 * 1024; // 5MB
            return bytes.Length <= maxSize;
        }
        catch
        {
            return false;
        }
    }
}
