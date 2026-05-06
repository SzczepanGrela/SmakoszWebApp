using System.Text;

namespace Smakosz.Orchestrator.Jobs;

public static class NcfTrainingCsvFormatter
{
    public static Stream FormatAsCsv(IReadOnlyList<NcfTrainingSample> samples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("user_id,dish_id,rating");
        foreach (var s in samples)
            sb.AppendLine($"{s.UserId},{s.DishId},{s.Rating}");
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }
}
