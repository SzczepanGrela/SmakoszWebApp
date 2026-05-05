using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropPhoneNormalizationTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_normalize_user_phone ON users;
                DROP TRIGGER IF EXISTS trg_normalize_restaurant_phone ON restaurants;
                DROP FUNCTION IF EXISTS normalize_phone_number();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION normalize_phone_number()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.phone IS NOT NULL THEN
                        NEW.phone := REGEXP_REPLACE(NEW.phone, '[ \-\(\)]', '', 'g');

                        IF NEW.phone ~ '^[0-9]{9}$' THEN
                            NEW.phone := '+48' || NEW.phone;
                        END IF;

                        IF NEW.phone ~ '^00' THEN
                            NEW.phone := '+' || SUBSTRING(NEW.phone, 3);
                        END IF;

                        IF NEW.phone !~ '^\+[0-9]{7,15}$' THEN
                            RAISE EXCEPTION 'Nieprawidlowy format numeru telefonu. Wymagany E.164.';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_normalize_user_phone ON users;
                CREATE TRIGGER trg_normalize_user_phone
                BEFORE INSERT OR UPDATE OF phone ON users
                FOR EACH ROW
                WHEN (NEW.phone IS NOT NULL)
                EXECUTE FUNCTION normalize_phone_number();

                DROP TRIGGER IF EXISTS trg_normalize_restaurant_phone ON restaurants;
                CREATE TRIGGER trg_normalize_restaurant_phone
                BEFORE INSERT OR UPDATE OF phone ON restaurants
                FOR EACH ROW
                WHEN (NEW.phone IS NOT NULL)
                EXECUTE FUNCTION normalize_phone_number();
            ");
        }
    }
}
