using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ReviewIngredientSuggestion;

public record ReviewIngredientSuggestionCommand(
    int SuggestionId,
    bool Approve,
    string? AdminNote,
    bool? IsAllergen = null,
    bool? IsVegetarian = null,
    bool? IsVegan = null,
    bool? IsGlutenFree = null,
    bool? IsLactoseFree = null,
    string? IconUrl = null) : IRequest<ErrorOr<Success>>;

public class ReviewIngredientSuggestionHandler
    : IRequestHandler<ReviewIngredientSuggestionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public ReviewIngredientSuggestionHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<Success>> Handle(
        ReviewIngredientSuggestionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var suggestion = await _db.IngredientSuggestions
            .FirstOrDefaultAsync(s => s.SuggestionId == request.SuggestionId, cancellationToken);

        if (suggestion is null)
            return Error.NotFound("SUGGESTION_NOT_FOUND", "Sugestia nie została znaleziona");

        if (suggestion.Status != IngredientSuggestionStatus.Pending)
            return Error.Validation("SUGGESTION_ALREADY_REVIEWED", "Sugestia została już rozpatrzona");

        suggestion.AdminNote = request.AdminNote;
        suggestion.ReviewedByAdminId = _currentUser.UserId;
        suggestion.ReviewedAt = _dateTime.UtcNow;

        if (request.Approve)
        {
            var exists = await _db.Ingredients
                .AnyAsync(i => i.IngredientName.ToLower() == suggestion.SuggestedName.ToLower(), cancellationToken);

            if (exists)
            {
                suggestion.Status = IngredientSuggestionStatus.Merged;
            }
            else
            {
                var ingredient = new Ingredient
                {
                    IngredientName = suggestion.SuggestedName,
                    IconUrl = request.IconUrl,
                    IsAllergen = request.IsAllergen ?? false,
                    IsVegetarian = request.IsVegetarian ?? true,
                    IsVegan = request.IsVegan ?? true,
                    IsGlutenFree = request.IsGlutenFree ?? true,
                    IsLactoseFree = request.IsLactoseFree ?? true,
                    CreatedAt = _dateTime.UtcNow
                };

                _db.Ingredients.Add(ingredient);
                await _db.SaveChangesAsync(cancellationToken);

                suggestion.MergedIngredientId = ingredient.IngredientId;
                suggestion.Status = IngredientSuggestionStatus.Approved;
            }
        }
        else
        {
            suggestion.Status = IngredientSuggestionStatus.Rejected;
        }

        if (suggestion.UserId.HasValue)
        {
            var message = request.Approve
                ? $"Twoja sugestia składnika \"{suggestion.SuggestedName}\" została zaakceptowana."
                : $"Twoja sugestia składnika \"{suggestion.SuggestedName}\" została odrzucona.";

            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == suggestion.UserId.Value, cancellationToken);
            var (sendPush, pushStatusVal) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            _db.Notifications.Add(new Notification
            {
                UserId = suggestion.UserId.Value,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Severity = request.Approve ? NotificationSeverity.Success : NotificationSeverity.Info,
                Title = request.Approve ? "Sugestia zaakceptowana" : "Sugestia odrzucona",
                Message = message,
                SendPush = sendPush,
                PushStatus = pushStatusVal,
                CreatedAt = _dateTime.UtcNow
            });
        }

        var relatedTicket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketType == TicketType.IngredientSuggestion
                && t.ReferenceId == suggestion.SuggestionId
                && t.Status != TicketStatus.Resolved
                && t.Status != TicketStatus.Closed, cancellationToken);
        if (relatedTicket != null)
        {
            relatedTicket.Status = TicketStatus.Resolved;
            relatedTicket.AssignedAdminId = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
