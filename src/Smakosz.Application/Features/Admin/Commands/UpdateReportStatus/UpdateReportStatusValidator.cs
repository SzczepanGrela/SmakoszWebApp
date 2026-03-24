using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.UpdateReportStatus;

public class UpdateReportStatusValidator : AbstractValidator<UpdateReportStatusCommand>
{
    public UpdateReportStatusValidator()
    {
        RuleFor(x => x.ReportId)
            .GreaterThan(0).WithMessage("Identyfikator zgłoszenia musi być większy od 0");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status jest wymagany");
    }
}
