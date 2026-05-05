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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
    public int? RequesterId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByAdminId { get; set; }
    public string? Resolution { get; set; }

    public User? AssignedAdmin { get; set; }
    public User? Requester { get; set; }
    public User? ResolvedByAdmin { get; set; }
}
