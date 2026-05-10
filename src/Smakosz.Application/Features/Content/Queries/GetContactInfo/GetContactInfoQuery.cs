using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Content.Queries.GetContactInfo;

public record GetContactInfoQuery() : IRequest<ErrorOr<ContactInfoDto>>;

public class ContactInfoDto
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Hours { get; set; }
}

public class GetContactInfoHandler : IRequestHandler<GetContactInfoQuery, ErrorOr<ContactInfoDto>>
{
    private readonly ISmakoszDbContext _db;

    public GetContactInfoHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<ContactInfoDto>> Handle(GetContactInfoQuery request, CancellationToken cancellationToken)
    {
        var configs = await _db.SystemConfigs.AsNoTracking()
            .Where(c => c.IsPublic && c.Key.StartsWith("contact."))
            .ToListAsync(cancellationToken);

        var dict = configs.ToDictionary(c => c.Key, c => c.Value);

        return new ContactInfoDto
        {
            Email = NullIfBlank(dict.GetValueOrDefault("contact.email")),
            Phone = NullIfBlank(dict.GetValueOrDefault("contact.phone")),
            Address = NullIfBlank(dict.GetValueOrDefault("contact.address")),
            Hours = NullIfBlank(dict.GetValueOrDefault("contact.hours"))
        };
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
