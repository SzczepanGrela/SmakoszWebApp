using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateCity;

public record UpdateCityCommand(int CityId, string? Name, string? Region) : IRequest<ErrorOr<Success>>;

public class UpdateCityHandler : IRequestHandler<UpdateCityCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateCityHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateCityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var city = await _db.Cities
            .FirstOrDefaultAsync(c => c.CityId == request.CityId, cancellationToken);

        if (city is null)
            return DomainErrors.City.NotFound;

        var oldValues = JsonSerializer.Serialize(new { city.CityName, city.Region });

        if (request.Name is not null)
            city.CityName = request.Name;

        if (request.Region is not null)
            city.Region = request.Region;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "Cities",
            RecordId = city.CityId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { city.CityName, city.Region })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
