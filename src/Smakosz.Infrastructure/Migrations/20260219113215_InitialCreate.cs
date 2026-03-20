using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "system");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateTable(
                name: "ai_logs",
                schema: "system",
                columns: table => new
                {
                    log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    model_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_id = table.Column<int>(type: "integer", nullable: true),
                    input_summary = table.Column<string>(type: "text", nullable: true),
                    scores = table.Column<string>(type: "jsonb", nullable: true),
                    verdict = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    processing_time_ms = table.Column<int>(type: "integer", nullable: true),
                    fallback = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_logs", x => x.log_id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    audit_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    table_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    record_id = table.Column<int>(type: "integer", nullable: false),
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "system"),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.audit_log_id);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    city_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    city_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_code_prefix = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.city_id);
                });

            migrationBuilder.CreateTable(
                name: "config",
                schema: "system",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_config", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "cuisine_types",
                columns: table => new
                {
                    cuisine_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuisine_types", x => x.cuisine_type_id);
                });

            migrationBuilder.CreateTable(
                name: "dish_archetypes",
                columns: table => new
                {
                    archetype_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    archetype_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dish_archetypes", x => x.archetype_id);
                });

            migrationBuilder.CreateTable(
                name: "email_logs",
                schema: "system",
                columns: table => new
                {
                    log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recipient = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_logs", x => x.log_id);
                });

            migrationBuilder.CreateTable(
                name: "files_to_delete",
                schema: "system",
                columns: table => new
                {
                    file_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    r2key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "smakosz-photos"),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_entity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_id = table.Column<int>(type: "integer", nullable: true),
                    queued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_files_to_delete", x => x.file_id);
                });

            migrationBuilder.CreateTable(
                name: "ingredients",
                columns: table => new
                {
                    ingredient_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ingredient_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    icon_blurhash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_allergen = table.Column<bool>(type: "boolean", nullable: false),
                    is_vegetarian = table.Column<bool>(type: "boolean", nullable: false),
                    is_vegan = table.Column<bool>(type: "boolean", nullable: false),
                    is_gluten_free = table.Column<bool>(type: "boolean", nullable: false),
                    is_lactose_free = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredients", x => x.ingredient_id);
                });

            migrationBuilder.CreateTable(
                name: "logs",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    context = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                schema: "system",
                columns: table => new
                {
                    node_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    mac_address = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    wol_gateway_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, defaultValue: "offline"),
                    node_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hostname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    gpu_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gpu_memory_total = table.Column<int>(type: "integer", nullable: true),
                    gpu_memory_used = table.Column<int>(type: "integer", nullable: true),
                    current_job_id = table.Column<int>(type: "integer", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    last_heartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nodes", x => x.node_id);
                    table.ForeignKey(
                        name: "fk_nodes_nodes_wol_gateway_id",
                        column: x => x.wol_gateway_id,
                        principalSchema: "system",
                        principalTable: "nodes",
                        principalColumn: "node_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rejection_reasons",
                columns: table => new
                {
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    admin_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_message_template = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rejection_reasons", x => x.reason_code);
                });

            migrationBuilder.CreateTable(
                name: "report_reason_definitions",
                columns: table => new
                {
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label_pl = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    severity_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_reason_definitions", x => x.reason_code);
                });

            migrationBuilder.CreateTable(
                name: "service_accounts",
                schema: "system",
                columns: table => new
                {
                    account_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    permissions = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_accounts", x => x.account_id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    tag_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tag_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_entity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    display_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.tag_id);
                });

            migrationBuilder.CreateTable(
                name: "dish_variants",
                columns: table => new
                {
                    variant_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    variant_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    archetype_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dish_variants", x => x.variant_id);
                    table.ForeignKey(
                        name: "fk_dish_variants_dish_archetypes_archetype_id",
                        column: x => x.archetype_id,
                        principalTable: "dish_archetypes",
                        principalColumn: "archetype_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "system",
                columns: table => new
                {
                    job_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    entity_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    worker_node = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    progress_message = table.Column<string>(type: "text", nullable: true),
                    error_log = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.job_id);
                    table.ForeignKey(
                        name: "fk_jobs_nodes_worker_node",
                        column: x => x.worker_node,
                        principalSchema: "system",
                        principalTable: "nodes",
                        principalColumn: "node_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "job_progress",
                schema: "system",
                columns: table => new
                {
                    progress_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<int>(type: "integer", nullable: false),
                    epoch = table.Column<int>(type: "integer", nullable: true),
                    loss = table.Column<double>(type: "double precision", nullable: true),
                    accuracy = table.Column<double>(type: "double precision", nullable: true),
                    learning_rate = table.Column<double>(type: "double precision", nullable: true),
                    current_step = table.Column<int>(type: "integer", nullable: true),
                    total_steps = table.Column<int>(type: "integer", nullable: true),
                    percentage = table.Column<double>(type: "double precision", nullable: true, computedColumnSql: "CASE WHEN total_steps > 0 THEN (current_step::double precision / total_steps) * 100 ELSE 0 END", stored: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_progress", x => x.progress_id);
                    table.ForeignKey(
                        name: "fk_job_progress_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "system",
                        principalTable: "jobs",
                        principalColumn: "job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "banned_identifiers",
                schema: "system",
                columns: table => new
                {
                    ban_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    banned_by = table.Column<int>(type: "integer", nullable: true),
                    banned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_banned_identifiers", x => x.ban_id);
                });

            migrationBuilder.CreateTable(
                name: "data_correction_requests",
                columns: table => new
                {
                    request_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    issue_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    proposed_value = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_correction_requests", x => x.request_id);
                });

            migrationBuilder.CreateTable(
                name: "dish_ingredients",
                columns: table => new
                {
                    dish_id = table.Column<int>(type: "integer", nullable: false),
                    ingredient_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dish_ingredients", x => new { x.dish_id, x.ingredient_id });
                    table.ForeignKey(
                        name: "fk_dish_ingredients_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "ingredient_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dish_section_assignments",
                columns: table => new
                {
                    dish_id = table.Column<int>(type: "integer", nullable: false),
                    section_id = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dish_section_assignments", x => new { x.dish_id, x.section_id });
                });

            migrationBuilder.CreateTable(
                name: "dish_tags",
                columns: table => new
                {
                    dish_id = table.Column<int>(type: "integer", nullable: false),
                    tag_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dish_tags", x => new { x.dish_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_dish_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dishes",
                columns: table => new
                {
                    dish_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<int>(type: "integer", nullable: true),
                    variant_id = table.Column<int>(type: "integer", nullable: true),
                    dish_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    trending_score = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    is_vegetarian = table.Column<bool>(type: "boolean", nullable: false),
                    is_vegan = table.Column<bool>(type: "boolean", nullable: false),
                    is_gluten_free = table.Column<bool>(type: "boolean", nullable: false),
                    is_lactose_free = table.Column<bool>(type: "boolean", nullable: false),
                    is_spicy = table.Column<bool>(type: "boolean", nullable: false),
                    ingredients_json = table.Column<string>(type: "jsonb", nullable: true),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    calories = table.Column<int>(type: "integer", nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_blurhash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    avg_rating = table.Column<double>(type: "double precision", nullable: true),
                    review_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    secret_base_price = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    secret_characteristics_vector = table.Column<string>(type: "jsonb", nullable: false),
                    secret_penalty_vector = table.Column<string>(type: "jsonb", nullable: true),
                    secret_quality = table.Column<double>(type: "double precision", nullable: true),
                    secret_popularity_factor = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dishes", x => x.dish_id);
                    table.ForeignKey(
                        name: "fk_dishes_dish_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "dish_variants",
                        principalColumn: "variant_id");
                });

            migrationBuilder.CreateTable(
                name: "favorite_restaurants",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_favorite_restaurants", x => new { x.user_id, x.restaurant_id });
                });

            migrationBuilder.CreateTable(
                name: "forbidden_words",
                schema: "system",
                columns: table => new
                {
                    word_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    word = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_regex = table.Column<bool>(type: "boolean", nullable: false),
                    added_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forbidden_words", x => x.word_id);
                });

            migrationBuilder.CreateTable(
                name: "ingredient_suggestions",
                columns: table => new
                {
                    suggestion_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    suggested_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    icon_blurhash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_allergen = table.Column<bool>(type: "boolean", nullable: false),
                    is_vegetarian = table.Column<bool>(type: "boolean", nullable: false),
                    is_vegan = table.Column<bool>(type: "boolean", nullable: false),
                    is_gluten_free = table.Column<bool>(type: "boolean", nullable: false),
                    is_lactose_free = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    admin_note = table.Column<string>(type: "text", nullable: true),
                    reviewed_by_admin_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    merged_ingredient_id = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredient_suggestions", x => x.suggestion_id);
                    table.ForeignKey(
                        name: "fk_ingredient_suggestions_ingredients_merged_ingredient_id",
                        column: x => x.merged_ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "ingredient_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    asset_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    blurhash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uploaded_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ai_nsfw_score = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    ai_on_topic_score = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    ai_verdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ai_model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    credit_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.asset_id);
                });

            migrationBuilder.CreateTable(
                name: "menu_sections",
                columns: table => new
                {
                    section_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    section_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menu_sections", x => x.section_id);
                });

            migrationBuilder.CreateTable(
                name: "moderation_logs",
                schema: "system",
                columns: table => new
                {
                    log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    actor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason_codes = table.Column<List<string>>(type: "text[]", nullable: false),
                    admin_note = table.Column<string>(type: "text", nullable: true),
                    processed_by = table.Column<int>(type: "integer", nullable: true),
                    ai_scores = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_logs", x => x.log_id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    group_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    counter = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    send_email = table.Column<bool>(type: "boolean", nullable: false),
                    email_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    send_push = table.Column<bool>(type: "boolean", nullable: false),
                    push_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.notification_id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "system",
                columns: table => new
                {
                    token_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    device_info = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.token_id);
                });

            migrationBuilder.CreateTable(
                name: "report_reason_assignments",
                columns: table => new
                {
                    report_id = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_reason_assignments", x => new { x.report_id, x.reason_code });
                    table.ForeignKey(
                        name: "fk_report_reason_assignments_report_reason_definitions_reason_",
                        column: x => x.reason_code,
                        principalTable: "report_reason_definitions",
                        principalColumn: "reason_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    report_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reporter_id = table.Column<int>(type: "integer", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by_admin_id = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.report_id);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_edit_requests",
                columns: table => new
                {
                    request_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    change_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    change_scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_entity_id = table.Column<int>(type: "integer", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    new_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    new_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    new_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    new_cuisine_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    new_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    new_website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    new_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    new_image_blurhash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_verdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ai_confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    ai_model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    auto_approved = table.Column<bool>(type: "boolean", nullable: false),
                    auto_approve_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewed_by = table.Column<int>(type: "integer", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    admin_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by_admin_id = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurant_edit_requests", x => x.request_id);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_opening_hours",
                columns: table => new
                {
                    hours_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    close_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurant_opening_hours", x => x.hours_id);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_tags",
                columns: table => new
                {
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    tag_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurant_tags", x => new { x.restaurant_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_restaurant_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurants",
                columns: table => new
                {
                    restaurant_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<int>(type: "integer", nullable: true),
                    restaurant_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    cuisine_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    price_level = table.Column<int>(type: "integer", nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    geocode_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    geocoded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_blurhash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    avg_service = table.Column<double>(type: "double precision", nullable: true),
                    avg_cleanliness = table.Column<double>(type: "double precision", nullable: true),
                    avg_ambiance = table.Column<double>(type: "double precision", nullable: true),
                    avg_food_score = table.Column<double>(type: "double precision", nullable: true),
                    trending_score = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_by = table.Column<int>(type: "integer", nullable: true),
                    secret_price_multiplier = table.Column<double>(type: "double precision", nullable: true),
                    secret_overall_food_quality = table.Column<double>(type: "double precision", nullable: true),
                    secret_service_quality = table.Column<double>(type: "double precision", nullable: true),
                    secret_cleanliness_score = table.Column<double>(type: "double precision", nullable: true),
                    secret_ambiance_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    secret_ambiance_quality = table.Column<double>(type: "double precision", nullable: true),
                    secret_archetype_modifiers = table.Column<string>(type: "jsonb", nullable: true),
                    secret_menu_blueprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurants", x => x.restaurant_id);
                    table.ForeignKey(
                        name: "fk_restaurants_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "city_id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    home_city_id = table.Column<int>(type: "integer", nullable: true),
                    restaurant_id = table.Column<int>(type: "integer", nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    newsletter_consent = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    security_stamp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    avatar_blurhash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_banned = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    followers_count = table.Column<int>(type: "integer", nullable: false),
                    following_count = table.Column<int>(type: "integer", nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is2fa_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    review_count = table.Column<int>(type: "integer", nullable: false),
                    photo_count = table.Column<int>(type: "integer", nullable: false),
                    secret_total_review_count = table.Column<int>(type: "integer", nullable: true),
                    secret_travel_propensity = table.Column<double>(type: "double precision", nullable: true),
                    secret_enjoyed_archetypes = table.Column<string>(type: "jsonb", nullable: true),
                    secret_chance_dine_random = table.Column<double>(type: "double precision", nullable: true),
                    secret_chance_pick_random_dish = table.Column<double>(type: "double precision", nullable: true),
                    secret_cross_impact_factor = table.Column<double>(type: "double precision", nullable: true),
                    secret_mood_propensity = table.Column<double>(type: "double precision", nullable: true),
                    secret_is_influencer = table.Column<bool>(type: "boolean", nullable: false),
                    secret_rating_baseline = table.Column<double>(type: "double precision", nullable: false, defaultValue: 6.0),
                    secret_characteristics_vector = table.Column<string>(type: "jsonb", nullable: false),
                    secret_ingredient_preferences = table.Column<string>(type: "jsonb", nullable: true),
                    secret_cleanliness_preference = table.Column<string>(type: "jsonb", nullable: true),
                    secret_preferred_ambiance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_users_cities_home_city_id",
                        column: x => x.home_city_id,
                        principalTable: "cities",
                        principalColumn: "city_id");
                    table.ForeignKey(
                        name: "fk_users_restaurants_restaurant_id",
                        column: x => x.restaurant_id,
                        principalTable: "restaurants",
                        principalColumn: "restaurant_id");
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    review_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    dish_id = table.Column<int>(type: "integer", nullable: false),
                    visit_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dish_rating = table.Column<int>(type: "integer", nullable: false),
                    service_rating = table.Column<int>(type: "integer", nullable: false),
                    cleanliness_rating = table.Column<int>(type: "integer", nullable: false),
                    ambiance_rating = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    content_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_rejection_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    helpful_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    ai_toxicity_score = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    ai_spam_score = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    ai_verdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ai_model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.review_id);
                    table.ForeignKey(
                        name: "fk_reviews_dishes_dish_id",
                        column: x => x.dish_id,
                        principalTable: "dishes",
                        principalColumn: "dish_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_restaurants_restaurant_id",
                        column: x => x.restaurant_id,
                        principalTable: "restaurants",
                        principalColumn: "restaurant_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_dishes",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    dish_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_dishes", x => new { x.user_id, x.dish_id });
                    table.ForeignKey(
                        name: "fk_saved_dishes_dishes_dish_id",
                        column: x => x.dish_id,
                        principalTable: "dishes",
                        principalColumn: "dish_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_saved_dishes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "search_histories",
                columns: table => new
                {
                    search_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    search_query = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_histories", x => x.search_id);
                    table.ForeignKey(
                        name: "fk_search_histories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_logs",
                schema: "system",
                columns: table => new
                {
                    log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_logs", x => x.log_id);
                    table.ForeignKey(
                        name: "fk_security_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "system",
                columns: table => new
                {
                    ticket_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    assigned_admin_id = table.Column<int>(type: "integer", nullable: true),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.ticket_id);
                    table.ForeignKey(
                        name: "fk_tickets_users_assigned_admin_id",
                        column: x => x.assigned_admin_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_follows",
                columns: table => new
                {
                    follower_id = table.Column<int>(type: "integer", nullable: false),
                    followed_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_follows", x => new { x.follower_id, x.followed_id });
                    table.ForeignKey(
                        name: "fk_user_follows_users_followed_id",
                        column: x => x.followed_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_follows_users_follower_id",
                        column: x => x.follower_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_settings",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    push_like = table.Column<bool>(type: "boolean", nullable: false),
                    push_follow = table.Column<bool>(type: "boolean", nullable: false),
                    push_system = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_notification_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_notification_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    user_session_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_active_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_sessions", x => x.user_session_id);
                    table.ForeignKey(
                        name: "fk_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verification_codes",
                columns: table => new
                {
                    verification_code_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payload = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempts_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verification_codes", x => x.verification_code_id);
                    table.ForeignKey(
                        name: "fk_verification_codes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_likes",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    review_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_likes", x => new { x.user_id, x.review_id });
                    table.ForeignKey(
                        name: "fk_review_likes_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "review_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_likes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_logs_entity_type_entity_id",
                schema: "system",
                table: "ai_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_logs_model_type_created_at",
                schema: "system",
                table: "ai_logs",
                columns: new[] { "model_type", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_changed_at",
                table: "audit_logs",
                column: "changed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_changed_by",
                table: "audit_logs",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_table_name_changed_at",
                table: "audit_logs",
                columns: new[] { "table_name", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_table_name_record_id",
                table: "audit_logs",
                columns: new[] { "table_name", "record_id" });

            migrationBuilder.CreateIndex(
                name: "ix_banned_identifiers_banned_by",
                schema: "system",
                table: "banned_identifiers",
                column: "banned_by");

            migrationBuilder.CreateIndex(
                name: "ix_banned_identifiers_type_value",
                schema: "system",
                table: "banned_identifiers",
                columns: new[] { "type", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cities_city_name",
                table: "cities",
                column: "city_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_config_public",
                schema: "system",
                table: "config",
                columns: new[] { "key", "value" },
                filter: "is_public = true");

            migrationBuilder.CreateIndex(
                name: "ix_cuisine_types_name",
                table: "cuisine_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_data_correction_requests_pending",
                table: "data_correction_requests",
                column: "restaurant_id",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_data_correction_requests_user_id",
                table: "data_correction_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dish_archetypes_archetype_name",
                table: "dish_archetypes",
                column: "archetype_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dish_ingredients_ingredient_id",
                table: "dish_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_dish_section_assignments_dish_id",
                table: "dish_section_assignments",
                column: "dish_id");

            migrationBuilder.CreateIndex(
                name: "ix_dish_section_assignments_section_id_display_order",
                table: "dish_section_assignments",
                columns: new[] { "section_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_dish_tags_tag_id",
                table: "dish_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_dish_variants_archetype_id",
                table: "dish_variants",
                column: "archetype_id");

            migrationBuilder.CreateIndex(
                name: "ix_dish_variants_variant_name_archetype_id",
                table: "dish_variants",
                columns: new[] { "variant_name", "archetype_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dishes_is_available",
                table: "dishes",
                column: "is_available");

            migrationBuilder.CreateIndex(
                name: "ix_dishes_price",
                table: "dishes",
                column: "price");

            migrationBuilder.CreateIndex(
                name: "ix_dishes_public_id",
                table: "dishes",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dishes_restaurant_id",
                table: "dishes",
                column: "restaurant_id");

            migrationBuilder.CreateIndex(
                name: "ix_dishes_slug",
                table: "dishes",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dishes_variant_id",
                table: "dishes",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_logs_recipient_created_at",
                schema: "system",
                table: "email_logs",
                columns: new[] { "recipient", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_favorite_restaurants_restaurant_id",
                table: "favorite_restaurants",
                column: "restaurant_id");

            migrationBuilder.CreateIndex(
                name: "ix_forbidden_words_added_by",
                schema: "system",
                table: "forbidden_words",
                column: "added_by");

            migrationBuilder.CreateIndex(
                name: "ix_forbidden_words_word",
                schema: "system",
                table: "forbidden_words",
                column: "word",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_suggestions_merged_ingredient_id",
                table: "ingredient_suggestions",
                column: "merged_ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_suggestions_restaurant_id",
                table: "ingredient_suggestions",
                column: "restaurant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_suggestions_reviewed_by_admin_id",
                table: "ingredient_suggestions",
                column: "reviewed_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_suggestions_status",
                table: "ingredient_suggestions",
                columns: new[] { "status", "created_at" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_suggestions_user_id",
                table: "ingredient_suggestions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ingredients_ingredient_name",
                table: "ingredients",
                column: "ingredient_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_progress_job_id_created_at",
                schema: "system",
                table: "job_progress",
                columns: new[] { "job_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_entity_type_entity_id",
                schema: "system",
                table: "jobs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_pull_queue",
                schema: "system",
                table: "jobs",
                columns: new[] { "status", "priority", "created_at" },
                descending: new[] { false, true, false },
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_stuck_monitor",
                schema: "system",
                table: "jobs",
                columns: new[] { "status", "started_at" },
                filter: "status = 'PROCESSING'");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_worker_node",
                schema: "system",
                table: "jobs",
                column: "worker_node");

            migrationBuilder.CreateIndex(
                name: "ix_logs_created_at",
                schema: "system",
                table: "logs",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_logs_level_created_at",
                schema: "system",
                table: "logs",
                columns: new[] { "level", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_logs_source_created_at",
                schema: "system",
                table: "logs",
                columns: new[] { "source", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_hero",
                table: "media_assets",
                column: "asset_id",
                filter: "entity_type = 'hero' AND status = 'approved'");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets",
                columns: new[] { "status", "created_at" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_primary",
                table: "media_assets",
                columns: new[] { "entity_type", "entity_id" },
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_public_id",
                table: "media_assets",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_review",
                table: "media_assets",
                column: "entity_id",
                filter: "entity_type = 'review'");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_uploaded_by",
                table: "media_assets",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_menu_sections_restaurant_id_display_order",
                table: "menu_sections",
                columns: new[] { "restaurant_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_menu_sections_restaurant_id_section_name",
                table: "menu_sections",
                columns: new[] { "restaurant_id", "section_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_logs_entity_type_entity_id_created_at",
                schema: "system",
                table: "moderation_logs",
                columns: new[] { "entity_type", "entity_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_logs_processed_by_created_at",
                schema: "system",
                table: "moderation_logs",
                columns: new[] { "processed_by", "created_at" },
                descending: new[] { false, true },
                filter: "processed_by IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_nodes_node_type_status_last_heartbeat",
                schema: "system",
                table: "nodes",
                columns: new[] { "node_type", "status", "last_heartbeat" });

            migrationBuilder.CreateIndex(
                name: "ix_nodes_wol_gateway_id",
                schema: "system",
                table: "nodes",
                column: "wol_gateway_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_actor_id",
                table: "notifications",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_badge",
                table: "notifications",
                column: "user_id",
                filter: "is_read = false AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_group_key_unique",
                table: "notifications",
                columns: new[] { "user_id", "group_key" },
                unique: true,
                filter: "is_read = false AND is_deleted = false AND group_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_public_id",
                table: "notifications",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_created_at",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                schema: "system",
                table: "refresh_tokens",
                column: "expires_at",
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "system",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_revoked_at",
                schema: "system",
                table: "refresh_tokens",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rejection_reasons_admin_label",
                table: "rejection_reasons",
                column: "admin_label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rejection_reasons_category_is_active",
                table: "rejection_reasons",
                columns: new[] { "category", "is_active" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_report_reason_assignments_reason_code",
                table: "report_reason_assignments",
                column: "reason_code");

            migrationBuilder.CreateIndex(
                name: "ix_reports_entity_type_entity_id",
                table: "reports",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id",
                table: "reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_resolved_by_admin_id",
                table: "reports",
                column: "resolved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_status_created_at",
                table: "reports",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_edit_requests_change_type_status",
                table: "restaurant_edit_requests",
                columns: new[] { "change_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_edit_requests_resolved_by_admin_id",
                table: "restaurant_edit_requests",
                column: "resolved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_edit_requests_restaurant_id_created_at",
                table: "restaurant_edit_requests",
                columns: new[] { "restaurant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_edit_requests_reviewed_by",
                table: "restaurant_edit_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_edit_requests_status",
                table: "restaurant_edit_requests",
                column: "status",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_edit_requests_user_id",
                table: "restaurant_edit_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_opening_hours_restaurant_id",
                table: "restaurant_opening_hours",
                column: "restaurant_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_tags_tag_id",
                table: "restaurant_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_city_id",
                table: "restaurants",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_cuisine_type",
                table: "restaurants",
                column: "cuisine_type");

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_is_verified_owner_id",
                table: "restaurants",
                columns: new[] { "is_verified", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_owner_id",
                table: "restaurants",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_public_id",
                table: "restaurants",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_restaurant_name",
                table: "restaurants",
                column: "restaurant_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_slug",
                table: "restaurants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_status",
                table: "restaurants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_review_likes_review_id",
                table: "review_likes",
                column: "review_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_likes_user_id_created_at",
                table: "review_likes",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_content_status",
                table: "reviews",
                columns: new[] { "content_status", "created_at" },
                filter: "content_status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_dish_id_created_at",
                table: "reviews",
                columns: new[] { "dish_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_public_id",
                table: "reviews",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_restaurant_id_created_at",
                table: "reviews",
                columns: new[] { "restaurant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_id_created_at",
                table: "reviews",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_saved_dishes_dish_id",
                table: "saved_dishes",
                column: "dish_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_histories_user_id_created_at",
                table: "search_histories",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_security_logs_event_type_created_at",
                schema: "system",
                table: "security_logs",
                columns: new[] { "event_type", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_security_logs_ip_address",
                schema: "system",
                table: "security_logs",
                column: "ip_address");

            migrationBuilder.CreateIndex(
                name: "ix_security_logs_user_id_created_at",
                schema: "system",
                table: "security_logs",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_service_accounts_service_name",
                schema: "system",
                table: "service_accounts",
                column: "service_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_category",
                table: "tags",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_tags_tag_name",
                table: "tags",
                column: "tag_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_assigned_admin_id",
                schema: "system",
                table: "tickets",
                column: "assigned_admin_id",
                filter: "assigned_admin_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_status_priority",
                schema: "system",
                table: "tickets",
                columns: new[] { "status", "priority" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_type_reference_id",
                schema: "system",
                table: "tickets",
                columns: new[] { "ticket_type", "reference_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_follows_followed_id_created_at",
                table: "user_follows",
                columns: new[] { "followed_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_user_follows_follower_id_created_at",
                table: "user_follows",
                columns: new[] { "follower_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_refresh_token_hash",
                table: "user_sessions",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_user_id",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_active_login",
                table: "users",
                column: "email",
                unique: true,
                filter: "is_active = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_users_home_city_id",
                table: "users",
                column: "home_city_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_public_id",
                table: "users",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_restaurant_id",
                table: "users",
                column: "restaurant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_role",
                table: "users",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "ix_users_secret_is_influencer",
                table: "users",
                column: "secret_is_influencer",
                filter: "secret_is_influencer = true");

            migrationBuilder.CreateIndex(
                name: "ix_users_slug",
                table: "users",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_verification_codes_code_hash",
                table: "verification_codes",
                column: "code_hash");

            migrationBuilder.CreateIndex(
                name: "ix_verification_codes_user_id",
                table: "verification_codes",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_banned_identifiers_users_banned_by",
                schema: "system",
                table: "banned_identifiers",
                column: "banned_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_data_correction_requests_restaurants_restaurant_id",
                table: "data_correction_requests",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_data_correction_requests_users_user_id",
                table: "data_correction_requests",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_dish_ingredients_dishes_dish_id",
                table: "dish_ingredients",
                column: "dish_id",
                principalTable: "dishes",
                principalColumn: "dish_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_dish_section_assignments_dishes_dish_id",
                table: "dish_section_assignments",
                column: "dish_id",
                principalTable: "dishes",
                principalColumn: "dish_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_dish_section_assignments_menu_sections_section_id",
                table: "dish_section_assignments",
                column: "section_id",
                principalTable: "menu_sections",
                principalColumn: "section_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_dish_tags_dishes_dish_id",
                table: "dish_tags",
                column: "dish_id",
                principalTable: "dishes",
                principalColumn: "dish_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_dishes_restaurants_restaurant_id",
                table: "dishes",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_favorite_restaurants_restaurants_restaurant_id",
                table: "favorite_restaurants",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_favorite_restaurants_users_user_id",
                table: "favorite_restaurants",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_forbidden_words_users_added_by",
                schema: "system",
                table: "forbidden_words",
                column: "added_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ingredient_suggestions_restaurants_restaurant_id",
                table: "ingredient_suggestions",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ingredient_suggestions_users_reviewed_by_admin_id",
                table: "ingredient_suggestions",
                column: "reviewed_by_admin_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ingredient_suggestions_users_user_id",
                table: "ingredient_suggestions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_media_assets_users_uploaded_by",
                table: "media_assets",
                column: "uploaded_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_sections_restaurants_restaurant_id",
                table: "menu_sections",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_moderation_logs_users_processed_by",
                schema: "system",
                table: "moderation_logs",
                column: "processed_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_users_actor_id",
                table: "notifications",
                column: "actor_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_users_user_id",
                table: "notifications",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_tokens_users_user_id",
                schema: "system",
                table: "refresh_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_report_reason_assignments_reports_report_id",
                table: "report_reason_assignments",
                column: "report_id",
                principalTable: "reports",
                principalColumn: "report_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reports_users_reporter_id",
                table: "reports",
                column: "reporter_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reports_users_resolved_by_admin_id",
                table: "reports",
                column: "resolved_by_admin_id",
                principalTable: "users",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_restaurant_edit_requests_restaurants_restaurant_id",
                table: "restaurant_edit_requests",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_restaurant_edit_requests_users_resolved_by_admin_id",
                table: "restaurant_edit_requests",
                column: "resolved_by_admin_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_restaurant_edit_requests_users_reviewed_by",
                table: "restaurant_edit_requests",
                column: "reviewed_by",
                principalTable: "users",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_restaurant_edit_requests_users_user_id",
                table: "restaurant_edit_requests",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_restaurant_opening_hours_restaurants_restaurant_id",
                table: "restaurant_opening_hours",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_restaurant_tags_restaurants_restaurant_id",
                table: "restaurant_tags",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_restaurants_users_owner_id",
                table: "restaurants",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            // ============================================================
            // SQL FUNCTIONS, TRIGGERS, AND VIEWS
            // ============================================================
            // Database objects that EF Core cannot generate automatically

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION f_unaccent(text)
  RETURNS text AS
$func$
SELECT public.unaccent('public.unaccent', $1)
$func$  LANGUAGE sql IMMUTABLE;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION generate_slug(input_text TEXT)
RETURNS TEXT AS $$
BEGIN
    RETURN LOWER(
        REGEXP_REPLACE(
            REGEXP_REPLACE(
                unaccent(TRIM(input_text)),
                '[^a-zA-Z0-9\s-]', '', 'g'
            ),
            '\s+', '-', 'g'
        )
    );
END;
$$ LANGUAGE plpgsql IMMUTABLE;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION log_audit_event(pk_column_name TEXT)
RETURNS TRIGGER AS $$
DECLARE
    pk_value TEXT;
    old_data JSONB;
    new_data JSONB;
BEGIN
    IF TG_OP = 'INSERT' THEN
        EXECUTE format('SELECT ($1).%I::TEXT', pk_column_name) INTO pk_value USING NEW;
        INSERT INTO audit_logs (table_name, operation, record_id, old_values, new_values)
        VALUES (TG_TABLE_NAME, 'INSERT', pk_value::INT, NULL, to_jsonb(NEW));
        RETURN NEW;
    ELSIF TG_OP = 'UPDATE' THEN
        EXECUTE format('SELECT ($1).%I::TEXT', pk_column_name) INTO pk_value USING NEW;
        INSERT INTO audit_logs (table_name, operation, record_id, old_values, new_values)
        VALUES (TG_TABLE_NAME, 'UPDATE', pk_value::INT, to_jsonb(OLD), to_jsonb(NEW));
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        EXECUTE format('SELECT ($1).%I::TEXT', pk_column_name) INTO pk_value USING OLD;
        INSERT INTO audit_logs (table_name, operation, record_id, old_values, new_values)
        VALUES (TG_TABLE_NAME, 'DELETE', pk_value::INT, to_jsonb(OLD), NULL);
        RETURN OLD;
    END IF;
END;
$$ LANGUAGE plpgsql;
");

            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_update_timestamp_users ON users;
CREATE TRIGGER trg_update_timestamp_users
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS trg_update_timestamp_restaurants ON restaurants;
CREATE TRIGGER trg_update_timestamp_restaurants
    BEFORE UPDATE ON restaurants
    FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS trg_update_timestamp_dishes ON dishes;
CREATE TRIGGER trg_update_timestamp_dishes
    BEFORE UPDATE ON dishes
    FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS trg_update_timestamp_reviews ON reviews;
CREATE TRIGGER trg_update_timestamp_reviews
    BEFORE UPDATE ON reviews
    FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS trg_update_timestamp_notifications ON notifications;
CREATE TRIGGER trg_update_timestamp_notifications
    BEFORE UPDATE ON notifications
    FOR EACH ROW EXECUTE FUNCTION update_timestamp();
");

            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_audit_restaurants ON restaurants;
CREATE TRIGGER trg_audit_restaurants
    AFTER INSERT OR UPDATE OR DELETE ON restaurants
    FOR EACH ROW EXECUTE FUNCTION log_audit_event('restaurant_id');

DROP TRIGGER IF EXISTS trg_audit_users ON users;
CREATE TRIGGER trg_audit_users
    AFTER INSERT OR UPDATE OR DELETE ON users
    FOR EACH ROW EXECUTE FUNCTION log_audit_event('user_id');

DROP TRIGGER IF EXISTS trg_audit_dishes ON dishes;
CREATE TRIGGER trg_audit_dishes
    AFTER INSERT OR UPDATE OR DELETE ON dishes
    FOR EACH ROW EXECUTE FUNCTION log_audit_event('dish_id');

DROP TRIGGER IF EXISTS trg_audit_reviews ON reviews;
CREATE TRIGGER trg_audit_reviews
    AFTER INSERT OR UPDATE OR DELETE ON reviews
    FOR EACH ROW EXECUTE FUNCTION log_audit_event('review_id');
");

            migrationBuilder.Sql(@"
CREATE OR REPLACE VIEW search_autocomplete AS
    SELECT DISTINCT
        'cuisine'::text AS type,
        0 AS id,
        cuisine_type AS name,
        'Kategoria'::text AS subtitle,
        NULL::text AS icon,
        f_unaccent(lower(cuisine_type)) AS name_normalized,
        1 AS priority
    FROM restaurants
    WHERE status = 'active' AND cuisine_type IS NOT NULL

    UNION ALL

    SELECT
        'restaurant'::text AS type,
        restaurant_id AS id,
        restaurant_name AS name,
        cuisine_type AS subtitle,
        image_url AS icon,
        f_unaccent(lower(restaurant_name || ' ' || COALESCE(cuisine_type, ''))) AS name_normalized,
        2 AS priority
    FROM restaurants
    WHERE status = 'active'

    UNION ALL

    SELECT
        'dish'::text AS type,
        d.dish_id AS id,
        d.dish_name AS name,
        r.restaurant_name AS subtitle,
        d.image_url AS icon,
        f_unaccent(lower(d.dish_name)) AS name_normalized,
        3 AS priority
    FROM dishes d
    JOIN restaurants r ON d.restaurant_id = r.restaurant_id
    WHERE d.is_available = TRUE AND r.status = 'active';
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS trgm_idx_restaurants_name
ON restaurants
USING GIN (f_unaccent(lower(restaurant_name)) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_restaurants_cuisine_btree
ON restaurants(cuisine_type) WHERE status = 'active';

CREATE INDEX IF NOT EXISTS trgm_idx_restaurants_full_search
ON restaurants
USING GIN (f_unaccent(lower(restaurant_name || ' ' || COALESCE(cuisine_type, ''))) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS trgm_idx_dishes_name
ON dishes
USING GIN (f_unaccent(lower(dish_name)) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS trgm_idx_users_username
ON users
USING GIN (f_unaccent(lower(username)) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_users_email_lower ON users (lower(email));
CREATE INDEX IF NOT EXISTS idx_users_username_lower ON users (lower(username));

CREATE INDEX IF NOT EXISTS idx_restaurants_geo
ON restaurants(latitude, longitude)
WHERE latitude IS NOT NULL AND longitude IS NOT NULL;
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS idx_restaurants_geo;
DROP INDEX IF EXISTS idx_users_username_lower;
DROP INDEX IF EXISTS idx_users_email_lower;
DROP INDEX IF EXISTS trgm_idx_users_username;
DROP INDEX IF EXISTS trgm_idx_dishes_name;
DROP INDEX IF EXISTS trgm_idx_restaurants_full_search;
DROP INDEX IF EXISTS idx_restaurants_cuisine_btree;
DROP INDEX IF EXISTS trgm_idx_restaurants_name;
");

            migrationBuilder.Sql(@"
DROP VIEW IF EXISTS search_autocomplete;
");

            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_audit_reviews ON reviews;
DROP TRIGGER IF EXISTS trg_audit_dishes ON dishes;
DROP TRIGGER IF EXISTS trg_audit_users ON users;
DROP TRIGGER IF EXISTS trg_audit_restaurants ON restaurants;

DROP TRIGGER IF EXISTS trg_update_timestamp_notifications ON notifications;
DROP TRIGGER IF EXISTS trg_update_timestamp_reviews ON reviews;
DROP TRIGGER IF EXISTS trg_update_timestamp_dishes ON dishes;
DROP TRIGGER IF EXISTS trg_update_timestamp_restaurants ON restaurants;
DROP TRIGGER IF EXISTS trg_update_timestamp_users ON users;
");

            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS log_audit_event(TEXT);
DROP FUNCTION IF EXISTS update_timestamp();
DROP FUNCTION IF EXISTS generate_slug(TEXT);
DROP FUNCTION IF EXISTS f_unaccent(text);
");

            migrationBuilder.DropForeignKey(
                name: "fk_restaurants_users_owner_id",
                table: "restaurants");

            migrationBuilder.DropTable(
                name: "ai_logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "banned_identifiers",
                schema: "system");

            migrationBuilder.DropTable(
                name: "config",
                schema: "system");

            migrationBuilder.DropTable(
                name: "cuisine_types");

            migrationBuilder.DropTable(
                name: "data_correction_requests");

            migrationBuilder.DropTable(
                name: "dish_ingredients");

            migrationBuilder.DropTable(
                name: "dish_section_assignments");

            migrationBuilder.DropTable(
                name: "dish_tags");

            migrationBuilder.DropTable(
                name: "email_logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "favorite_restaurants");

            migrationBuilder.DropTable(
                name: "files_to_delete",
                schema: "system");

            migrationBuilder.DropTable(
                name: "forbidden_words",
                schema: "system");

            migrationBuilder.DropTable(
                name: "ingredient_suggestions");

            migrationBuilder.DropTable(
                name: "job_progress",
                schema: "system");

            migrationBuilder.DropTable(
                name: "logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "moderation_logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "system");

            migrationBuilder.DropTable(
                name: "rejection_reasons");

            migrationBuilder.DropTable(
                name: "report_reason_assignments");

            migrationBuilder.DropTable(
                name: "restaurant_edit_requests");

            migrationBuilder.DropTable(
                name: "restaurant_opening_hours");

            migrationBuilder.DropTable(
                name: "restaurant_tags");

            migrationBuilder.DropTable(
                name: "review_likes");

            migrationBuilder.DropTable(
                name: "saved_dishes");

            migrationBuilder.DropTable(
                name: "search_histories");

            migrationBuilder.DropTable(
                name: "security_logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "service_accounts",
                schema: "system");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "system");

            migrationBuilder.DropTable(
                name: "user_follows");

            migrationBuilder.DropTable(
                name: "user_notification_settings");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "verification_codes");

            migrationBuilder.DropTable(
                name: "menu_sections");

            migrationBuilder.DropTable(
                name: "ingredients");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "report_reason_definitions");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "nodes",
                schema: "system");

            migrationBuilder.DropTable(
                name: "dishes");

            migrationBuilder.DropTable(
                name: "dish_variants");

            migrationBuilder.DropTable(
                name: "dish_archetypes");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "restaurants");

            migrationBuilder.DropTable(
                name: "cities");
        }
    }
}
