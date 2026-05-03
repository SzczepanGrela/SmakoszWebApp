using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateCuisineType;

public record UpdateCuisineTypeCommand(int CuisineTypeId, string? Name, string? DisplayName, string? Icon, bool? IsActive)
    : IRequest<ErrorOr<Success>>;

public class UpdateCuisineTypeHandler : IRequestHandler<UpdateCuisineTypeCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateCuisineTypeHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateCuisineTypeCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var cuisine = await _db.CuisineTypes
            .FirstOrDefaultAsync(c => c.CuisineTypeId == request.CuisineTypeId, cancellationToken);

        if (cuisine is null)
            return DomainErrors.CuisineType.NotFound;

        if (request.Name is not null && !string.Equals(request.Name, cuisine.Name, StringComparison.OrdinalIgnoreCase))
        {
            var nameTaken = await _db.CuisineTypes
                .AnyAsync(c => c.CuisineTypeId != request.CuisineTypeId && c.Name.ToLower() == request.Name.ToLower(), cancellationToken);
            if (nameTaken)
                return DomainErrors.CuisineType.AlreadyExists;
        }

        var oldValues = JsonSerializer.Serialize(new { cuisine.Name, cuisine.DisplayName, cuisine.Icon, cuisine.IsActive });

        if (request.Name is not null)
            cuisine.Name = request.Name;
        if (request.DisplayName is not null)
            cuisine.DisplayName = request.DisplayName;
        if (request.Icon is not null)
            cuisine.Icon = request.Icon;
        if (request.IsActive.HasValue)
            cuisine.IsActive = request.IsActive.Value;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "CuisineTypes",
            RecordId = cuisine.CuisineTypeId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { cuisine.Name, cuisine.DisplayName, cuisine.Icon, cuisine.IsActive })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
