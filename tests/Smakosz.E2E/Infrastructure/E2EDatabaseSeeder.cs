using Microsoft.EntityFrameworkCore;
using Npgsql;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.E2E.Infrastructure;

public static class E2EDatabaseSeeder
{
    // Tables truncated on per-test reset. Dictionaries seeded by EF migrations
    // (rejection_reasons, system.home_page_cache, system.site_stats) are
    // intentionally excluded so migration InsertData rows survive the reset.
    // SeedData own additions to dictionaries (cuisine_types, tags including
    // dish_category) use unique high IDs (1001+) and are wiped explicitly
    // below so the re-seed below stays idempotent without colliding.
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

    // Restaurant_themes is excluded from TruncateTables CASCADE because the
    // theme_id 1 'inne' fallback row seeded by AC1 migration must survive
    // resets. SeedData adds theme_id 1001 to 1003 with FK Restrict to
    // cuisine_types, so we explicitly wipe high-id rows before deleting
    // matching cuisine_types high-id rows.

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

        // Also wipe dictionary rows that SeedData itself adds (high IDs or
        // E2E-prefixed codes) so the re-seed below stays idempotent without
        // touching migration rows. Themes wiped before cuisines because the
        // FK fk_restaurant_themes_cuisine_types_cuisine_type_id is Restrict.
        var deleteOwnDictionaryRows = @"
            DELETE FROM restaurant_themes WHERE theme_id >= 1001;
            DELETE FROM cuisine_types WHERE cuisine_type_id >= 1001;
            DELETE FROM tags WHERE tag_id >= 1001;
            DELETE FROM rejection_reasons WHERE reason_code IN ('text_spam', 'text_offtopic', 'photo_inappropriate');
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
