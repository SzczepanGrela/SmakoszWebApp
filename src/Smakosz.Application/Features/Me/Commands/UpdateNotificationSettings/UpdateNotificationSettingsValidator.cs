using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.UpdateNotificationSettings;

public class UpdateNotificationSettingsValidator : AbstractValidator<UpdateNotificationSettingsCommand>
{
    public UpdateNotificationSettingsValidator()
    {
    }
}
