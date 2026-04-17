using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRejectionReasons : Migration
    {
        private static readonly string[] Columns =
        {
            "reason_code",
            "category",
            "admin_label",
            "user_message_template",
            "is_active",
            "created_at"
        };

        private static readonly string[] ReasonCodes =
        {
            "photo_nudity",
            "photo_violence",
            "photo_offtopic",
            "photo_poor_quality",
            "photo_duplicate",
            "photo_trademark",
            "text_profanity",
            "text_spam",
            "text_personal_attack",
            "text_offtopic",
            "text_fake_review",
            "text_pii"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var createdAt = new System.DateTime(2026, 4, 17, 0, 0, 0, System.DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "rejection_reasons",
                columns: Columns,
                values: new object[,]
                {
                    {
                        "photo_nudity",
                        "photo",
                        "Nagość lub treści erotyczne",
                        "Zdjęcie zostało odrzucone ponieważ zawiera nagość lub treści o charakterze erotycznym, które są niezgodne z regulaminem serwisu.",
                        true,
                        createdAt
                    },
                    {
                        "photo_violence",
                        "photo",
                        "Przemoc lub drastyczne treści",
                        "Zdjęcie zostało odrzucone ponieważ przedstawia treści brutalne lub drastyczne.",
                        true,
                        createdAt
                    },
                    {
                        "photo_offtopic",
                        "photo",
                        "Niezwiązane z jedzeniem",
                        "Zdjęcie nie przedstawia dania, restauracji ani wnętrza lokalu. Dodawaj zdjęcia związane z recenzją.",
                        true,
                        createdAt
                    },
                    {
                        "photo_poor_quality",
                        "photo",
                        "Niska jakość",
                        "Zdjęcie jest zbyt ciemne, rozmazane lub w złej rozdzielczości. Spróbuj przesłać ostrzejsze ujęcie.",
                        true,
                        createdAt
                    },
                    {
                        "photo_duplicate",
                        "photo",
                        "Duplikat",
                        "Identyczne zdjęcie zostało już dodane do tego dania lub restauracji.",
                        true,
                        createdAt
                    },
                    {
                        "photo_trademark",
                        "photo",
                        "Naruszenie znaku towarowego",
                        "Zdjęcie zawiera treści chronione prawem autorskim lub znaki towarowe, do których nie posiadasz praw.",
                        true,
                        createdAt
                    },
                    {
                        "text_profanity",
                        "text",
                        "Wulgaryzmy lub obraźliwy język",
                        "Recenzja została odrzucona ze względu na wulgaryzmy lub obraźliwy język niezgodny z regulaminem.",
                        true,
                        createdAt
                    },
                    {
                        "text_spam",
                        "text",
                        "Spam lub reklama",
                        "Recenzja ma charakter spamu lub niedozwolonej reklamy.",
                        true,
                        createdAt
                    },
                    {
                        "text_personal_attack",
                        "text",
                        "Atak osobisty",
                        "Recenzja zawiera atak osobisty na pracownika lub właściciela zamiast oceny jedzenia i obsługi.",
                        true,
                        createdAt
                    },
                    {
                        "text_offtopic",
                        "text",
                        "Niezwiązana z lokalem",
                        "Recenzja nie dotyczy faktycznej wizyty w restauracji ani dania.",
                        true,
                        createdAt
                    },
                    {
                        "text_fake_review",
                        "text",
                        "Podejrzenie fałszywej recenzji",
                        "Recenzja nosi znamiona fałszywej opinii (podejrzany wzorzec, brak szczegółów, powtarzająca się treść).",
                        true,
                        createdAt
                    },
                    {
                        "text_pii",
                        "text",
                        "Dane osobowe",
                        "Recenzja zawiera dane osobowe osób trzecich (imię i nazwisko, numer telefonu, adres), co narusza prywatność.",
                        true,
                        createdAt
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var reasonCode in ReasonCodes)
            {
                migrationBuilder.DeleteData(
                    table: "rejection_reasons",
                    keyColumn: "reason_code",
                    keyValue: reasonCode);
            }
        }
    }
}
