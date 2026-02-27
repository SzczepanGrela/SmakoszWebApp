using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateSystemConfig;

public record UpdateSystemConfigCommand(string Key, string Value) : IRequest<ErrorOr<Success>>;

public class UpdateSystemConfigValidator : AbstractValidator<UpdateSystemConfigCommand>
{
    public UpdateSystemConfigValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Klucz konfiguracji jest wymagany");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Wartość konfiguracji jest wymagana");
    }
}

public class UpdateSystemConfigHandler : IRequestHandler<UpdateSystemConfigCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateSystemConfigHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateSystemConfigCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == request.Key, cancellationToken);

        var oldValue = config?.Value;
        var isInsert = config is null;

        if (config is null)
        {
            config = new SystemConfig
            {
                Key = request.Key,
                Value = request.Value,
                UpdatedAt = _dateTime.UtcNow,
                UpdatedBy = _currentUser.UserId
            };
            _db.SystemConfigs.Add(config);
        }
        else
        {
            config.Value = request.Value;
            config.UpdatedAt = _dateTime.UtcNow;
            config.UpdatedBy = _currentUser.UserId;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "SystemConfigs",
            RecordId = 0,
            Operation = isInsert ? AuditOperation.Insert : AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = _dateTime.UtcNow,
            OldValues = oldValue is not null ? JsonSerializer.Serialize(new { Key = request.Key, Value = oldValue }) : null,
            NewValues = JsonSerializer.Serialize(new { request.Key, request.Value })
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
