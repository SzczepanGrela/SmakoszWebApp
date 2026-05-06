using System.Text.Json;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Common.Helpers;

public static class AuditLogHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AuditLog BuildEntry(
        string tableName,
        int recordId,
        AuditOperation operation,
        string? changedBy,
        object? oldSnapshot,
        object? newSnapshot)
    {
        return new AuditLog
        {
            TableName = tableName,
            RecordId = recordId,
            Operation = operation,
            ChangedBy = changedBy ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldSnapshot is not null ? JsonSerializer.Serialize(oldSnapshot, JsonOptions) : null,
            NewValues = newSnapshot is not null ? JsonSerializer.Serialize(newSnapshot, JsonOptions) : null
        };
    }
}
