using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AI_AddPaginationConfigKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO system.config (key, value, description, is_secret, is_public)
                SELECT 'business.default_page_size', value,
                       'Domyslny rozmiar strony dla business panel queries (GetBusinessReviews, GetBusinessDishes).',
                       is_secret, false
                FROM system.config WHERE key = 'search.default_page_size'
                ON CONFLICT (key) DO NOTHING;

                INSERT INTO system.config (key, value, description, is_secret, is_public)
                SELECT 'business.max_page_size', value,
                       'Max clamp rozmiaru strony dla business panel queries.',
                       is_secret, false
                FROM system.config WHERE key = 'search.max_page_size'
                ON CONFLICT (key) DO NOTHING;

                DELETE FROM system.config WHERE key IN ('search.default_page_size', 'search.max_page_size');

                INSERT INTO system.config (key, value, description, is_secret, is_public) VALUES
                  ('search.dishes_page_size', '6',
                   'Liczba dan per strona w /search?type=dishes (frontend Search.razor).', false, true),
                  ('search.restaurants_page_size', '12',
                   'Liczba restauracji per strona w /search?type=restaurants (frontend Search.razor).', false, true),
                  ('admin.list_page_size', '10',
                   'Liczba rekordow per accordion w /admin/users/{id} (Tickets, SecurityLogs, Reviews).', false, false)
                ON CONFLICT (key) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO system.config (key, value, description, is_secret, is_public)
                SELECT 'search.default_page_size', value, 'Domyslny rozmiar strony API.', is_secret, true
                FROM system.config WHERE key = 'business.default_page_size'
                ON CONFLICT (key) DO NOTHING;

                INSERT INTO system.config (key, value, description, is_secret, is_public)
                SELECT 'search.max_page_size', value, 'Max rozmiar strony API.', is_secret, true
                FROM system.config WHERE key = 'business.max_page_size'
                ON CONFLICT (key) DO NOTHING;

                DELETE FROM system.config WHERE key IN
                  ('business.default_page_size', 'business.max_page_size',
                   'search.dishes_page_size', 'search.restaurants_page_size', 'admin.list_page_size');
                """);
        }
    }
}
