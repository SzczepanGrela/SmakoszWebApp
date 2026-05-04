using Microsoft.EntityFrameworkCore;
using Npgsql;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.E2E.Infrastructure;

public static class E2EDatabaseSeeder
{
    // Tables truncated on per-fixture reset. Dictionaries seeded by EF migrations
    // (cuisine_types, tags with category dish_category, rejection_reasons,
    // system.home_page_cache, system.site_stats) are intentionally excluded so
    // migration InsertData rows survive the reset. SeedData own additions to
    // dictionaries use unique high IDs (1001+) and are guarded for idempotency
    // so the second SeedAsync call after a reset does not collide.
    private static readonly string[] TruncateTables =
    {
        "system.ai_logs", "system.banned_identifiers", "system.config",
        "system.email_logs", "system.files_to_delete", "system.forbidden_words",
        "system.job_progress", "system.jobs",
        "system.logs", "system.moderation_logs", "system.moderation_results",
        "system.nodes", "system.security_logs", "system.service_accounts",
        "system.tickets",
        "audit_logs", "cities", "data_correction_requests",
        "dish_archetypes", "dish_ingredients", "dish_section_assignments",
        "dish_tags", "dish_variants", "dishes", "favorite_restaurants",
        "ingredient_suggestions", "ingredients", "media_assets", "menu_sections",
        "notifications", "push_subscriptions",
        "report_reason_assignments", "report_reason_definitions", "reports",
        "restaurant_edit_requests", "restaurant_opening_hours", "restaurant_tags",
        "restaurants", "review_likes", "reviews", "saved_dishes",
        "search_histories", "user_follows", "user_notification_settings",
        "user_sessions", "users", "verification_codes"
    };

    public static async Task SeedAsync()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        await Smakosz.E2E.SeedData.SeedData.SeedAsync(context);
    }

    public static async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        // Also wipe dictionary rows that SeedData itself adds (high IDs) so the
        // re-seed below stays idempotent without touching migration rows.
        var deleteOwnDictionaryRows = @"
            DELETE FROM cuisine_types WHERE cuisine_type_id >= 1001;
            DELETE FROM tags WHERE tag_id >= 1001;
        ";

        var truncateSql = $"TRUNCATE TABLE {string.Join(", ", TruncateTables)} RESTART IDENTITY CASCADE;";

        await using (var cmd = new NpgsqlCommand(truncateSql + deleteOwnDictionaryRows, conn))
            await cmd.ExecuteNonQueryAsync();

        await using var ctx = CreateContext();
        await Smakosz.E2E.SeedData.SeedData.SeedAsync(ctx);
    }

    public static async Task CleanupAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    public static SmakoszDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmakoszDbContext>()
            .UseNpgsql(TestConstants.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SmakoszDbContext(options);
    }
}
