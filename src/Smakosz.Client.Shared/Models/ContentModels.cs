namespace Smakosz.Client.Models;

public class ContentPageDto
{
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public DateTime? LastUpdated { get; set; }
}

public class ContactPageDto
{
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? SupportHours { get; set; }
}
