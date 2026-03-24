using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities.System;

public class SystemTicket : IAuditableEntity, IVersioned
{
    public int TicketId { get; set; }
    public TicketType TicketType { get; set; }
    public long ReferenceId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public int Priority { get; set; } = 3;
    public string? Description { get; set; }
    public int? AssignedAdminId { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; } = 1;

    public User? AssignedAdmin { get; set; }
}
