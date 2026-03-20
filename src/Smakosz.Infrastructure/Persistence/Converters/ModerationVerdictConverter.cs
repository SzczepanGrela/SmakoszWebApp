using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Persistence.Converters;

public class ModerationVerdictConverter : ValueConverter<ModerationVerdict, string>
{
    private static readonly Dictionary<ModerationVerdict, string> ToDb = new()
    {
        [ModerationVerdict.Approved] = "approve",
        [ModerationVerdict.Rejected] = "reject",
        [ModerationVerdict.NeedsReview] = "needs_review"
    };

    private static readonly Dictionary<string, ModerationVerdict> FromDb = ToDb
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    public ModerationVerdictConverter()
        : base(
            v => ToDb[v],
            v => FromDb[v])
    {
    }
}
