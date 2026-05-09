using System.Text.RegularExpressions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetTicketDetail;

public record GetTicketDetailQuery(int TicketId) : IRequest<ErrorOr<AdminTicketDetailDto>>;

public class GetTicketDetailHandler : IRequestHandler<GetTicketDetailQuery, ErrorOr<AdminTicketDetailDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetTicketDetailHandler> _logger;

    public GetTicketDetailHandler(ISmakoszDbContext db, ICurrentUserService currentUser, ILogger<GetTicketDetailHandler>? logger = null)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger ?? NullLogger<GetTicketDetailHandler>.Instance;
    }

    public async Task<ErrorOr<AdminTicketDetailDto>> Handle(GetTicketDetailQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var ticket = await _db.SystemTickets
            .AsNoTracking()
            .Include(t => t.AssignedAdmin)
            .Include(t => t.Requester)
            .FirstOrDefaultAsync(t => t.TicketId == request.TicketId, cancellationToken);

        if (ticket is null)
            return DomainErrors.Ticket.NotFound;

        var dto = new AdminTicketDetailDto
        {
            TicketId = ticket.TicketId,
            TicketType = ticket.TicketType.ToString(),
            ReferenceId = ticket.ReferenceId,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority,
            Description = ticket.Description,
            AssignedAdminUsername = ticket.AssignedAdmin?.Username,
            CreatedAt = ticket.CreatedAt,
            RequesterId = ticket.RequesterId,
            RequesterUsername = ticket.Requester?.Username,
            RequesterEmail = ticket.Requester?.Email
        };

        switch (ticket.TicketType)
        {
            case TicketType.Contact:
                dto.Contact = ParseContact(ticket.Description, ticket.TicketId);
                break;

            case TicketType.Photo:
                var asset = await _db.MediaAssets
                    .AsNoTracking()
                    .Include(a => a.Uploader)
                    .FirstOrDefaultAsync(a => a.AssetId == ticket.ReferenceId, cancellationToken);
                if (asset != null)
                {
                    dto.Photo = new PhotoModerationDto
                    {
                        AssetId = asset.AssetId,
                        PublicId = asset.PublicId,
                        Url = asset.Url,
                        EntityType = asset.EntityType.ToString(),
                        EntityId = asset.EntityId,
                        UploadedByUsername = asset.Uploader?.Username,
                        CreatedAt = asset.CreatedAt
                    };
                }
                break;

            case TicketType.ReviewContent:
                var review = await _db.Reviews
                    .AsNoTracking()
                    .Include(r => r.User)
                    .Include(r => r.Dish)
                    .Include(r => r.Restaurant)
                    .FirstOrDefaultAsync(r => r.ReviewId == (int)ticket.ReferenceId, cancellationToken);
                if (review != null)
                {
                    dto.Review = new ReviewModerationDto
                    {
                        ReviewId = review.ReviewId,
                        PublicId = review.PublicId,
                        Username = review.User?.Username,
                        DishName = review.Dish?.DishName,
                        RestaurantName = review.Restaurant?.RestaurantName,
                        Content = review.Content,
                        DishRating = review.DishRating,
                        CreatedAt = review.CreatedAt
                    };
                }
                break;

            case TicketType.Report:
                var report = await _db.Reports
                    .AsNoTracking()
                    .Include(r => r.Reporter)
                    .FirstOrDefaultAsync(r => r.ReportId == (int)ticket.ReferenceId, cancellationToken);
                if (report != null)
                {
                    dto.Report = new AdminReportDto
                    {
                        ReportId = report.ReportId,
                        EntityType = report.EntityType.ToString(),
                        EntityId = report.EntityId,
                        Reason = report.Description ?? string.Empty,
                        Status = report.Status.ToString(),
                        ReporterUsername = report.Reporter?.Username,
                        CreatedAt = report.CreatedAt
                    };
                }
                break;

            case TicketType.EditRequest:
                var editRequest = await _db.RestaurantEditRequests
                    .AsNoTracking()
                    .Include(r => r.Restaurant)
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.RequestId == (int)ticket.ReferenceId, cancellationToken);
                if (editRequest != null)
                {
                    dto.EditRequest = new EditRequestDto
                    {
                        RequestId = editRequest.RequestId,
                        RestaurantName = editRequest.Restaurant?.RestaurantName,
                        Username = editRequest.User?.Username,
                        ChangeType = editRequest.ChangeType.ToString(),
                        Status = editRequest.Status.ToString(),
                        Payload = editRequest.Payload,
                        CreatedAt = editRequest.CreatedAt
                    };
                }
                break;

            case TicketType.IngredientSuggestion:
                var suggestion = await _db.IngredientSuggestions
                    .AsNoTracking()
                    .Include(s => s.User)
                    .Include(s => s.Restaurant)
                    .FirstOrDefaultAsync(s => s.SuggestionId == (int)ticket.ReferenceId, cancellationToken);
                if (suggestion != null)
                {
                    dto.Suggestion = new IngredientSuggestionDto
                    {
                        SuggestionId = suggestion.SuggestionId,
                        SuggestedName = suggestion.SuggestedName,
                        IsAllergen = suggestion.IsAllergen,
                        IsVegetarian = suggestion.IsVegetarian,
                        IsVegan = suggestion.IsVegan,
                        IsGlutenFree = suggestion.IsGlutenFree,
                        IsLactoseFree = suggestion.IsLactoseFree,
                        Status = suggestion.Status.ToString(),
                        AdminNote = suggestion.AdminNote,
                        Username = suggestion.User?.Username,
                        RestaurantName = suggestion.Restaurant?.RestaurantName,
                        CreatedAt = suggestion.CreatedAt,
                        ReviewedAt = suggestion.ReviewedAt
                    };
                }
                break;

            case TicketType.RestaurantClaim:
                var claimedRestaurant = await _db.Restaurants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RestaurantId == (int)ticket.ReferenceId, cancellationToken);
                if (claimedRestaurant != null)
                {
                    dto.RestaurantId = claimedRestaurant.RestaurantId;
                    dto.RestaurantName = claimedRestaurant.RestaurantName;
                    dto.RestaurantSlug = claimedRestaurant.Slug;
                }
                break;
        }

        return dto;
    }

    private ContactInfoDto ParseContact(string? description, int ticketId)
    {
        if (string.IsNullOrWhiteSpace(description))
            return new ContactInfoDto();

        var senderMatch = Regex.Match(description, @"^\s*Od:\s+(.+?)\s*<\s*([^>\s]+)\s*>", RegexOptions.IgnoreCase);
        var subjectMatch = Regex.Match(description, @"Temat:\s*(.+?)\s*(?:\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        var bodyStart = description.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (bodyStart < 0)
            bodyStart = description.IndexOf("\n\n", StringComparison.Ordinal);
        var separatorLen = bodyStart >= 0 && description[bodyStart] == '\r' ? 4 : 2;
        var message = bodyStart >= 0 ? description[(bodyStart + separatorLen)..] : string.Empty;

        var name = senderMatch.Success ? senderMatch.Groups[1].Value.Trim() : string.Empty;
        var email = senderMatch.Success ? senderMatch.Groups[2].Value.Trim() : string.Empty;
        var subject = subjectMatch.Success ? subjectMatch.Groups[1].Value.Trim() : string.Empty;

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(subject))
        {
            var preview = description.Length > 200 ? description[..200] : description;
            _logger.LogWarning("Contact ticket {TicketId} description did not match expected format. Preview: {Preview}", ticketId, preview);
        }

        return new ContactInfoDto
        {
            Name = name,
            Email = email,
            Subject = subject,
            Message = message
        };
    }
}
