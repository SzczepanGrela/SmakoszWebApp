using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Common.Helpers;

public record RejectionResolution(string ResolvedText, IReadOnlyList<string> AppliedCodes);

public static class RejectionReasonResolver
{
    private const string ModeratorNotePrefix = "Dodatkowa uwaga moderatora: ";
    private const string Separator = "\n\n";

    public static async Task<ErrorOr<RejectionResolution>> ResolveAsync(
        ISmakoszDbContext db,
        IReadOnlyList<string>? reasonCodes,
        string? moderatorNote,
        RejectionReasonCategory expectedCategory,
        CancellationToken cancellationToken)
    {
        var codes = reasonCodes?
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? new List<string>();

        var trimmedNote = string.IsNullOrWhiteSpace(moderatorNote) ? null : moderatorNote.Trim();

        if (codes.Count == 0 && trimmedNote is null)
            return DomainErrors.RejectionReason.RejectionRequiresReason;

        var parts = new List<string>();

        if (codes.Count > 0)
        {
            var matched = await db.RejectionReasons
                .Where(r => codes.Contains(r.ReasonCode))
                .ToListAsync(cancellationToken);

            if (matched.Count != codes.Count)
                return DomainErrors.RejectionReason.UnknownReasonCode;

            if (matched.Any(r => !r.IsActive))
                return DomainErrors.RejectionReason.InactiveReason;

            if (matched.Any(r => r.Category != expectedCategory))
                return DomainErrors.RejectionReason.CategoryMismatch;

            parts.AddRange(codes
                .Select(code => matched.First(r => r.ReasonCode == code).UserMessageTemplate));
        }

        if (trimmedNote is not null)
            parts.Add(ModeratorNotePrefix + trimmedNote);

        var resolvedText = string.Join(Separator, parts);

        return new RejectionResolution(resolvedText, codes);
    }
}
