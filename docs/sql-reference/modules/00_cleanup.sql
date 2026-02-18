-- ========================================
-- CLEANUP: Drop existing objects
-- ========================================

-- Drop System Schema (Infrastructure)
DROP SCHEMA IF EXISTS system CASCADE;

-- Drop Views
DROP VIEW IF EXISTS moderation_queue_stats;
DROP VIEW IF EXISTS admin_review_pending;
DROP VIEW IF EXISTS ai_review_pending;
DROP VIEW IF EXISTS vw_active_dishes;
DROP VIEW IF EXISTS vw_user_stats;

-- Drop Tables (Order matters due to foreign keys)
-- Child tables first
DROP TABLE IF EXISTS audit_logs CASCADE;
DROP TABLE IF EXISTS system.tickets CASCADE;
DROP TABLE IF EXISTS ingredient_suggestions CASCADE;
DROP TABLE IF EXISTS restaurant_edit_requests CASCADE;
DROP TABLE IF EXISTS user_variant_preferences CASCADE;
DROP TABLE IF EXISTS verification_codes CASCADE;
DROP TABLE IF EXISTS user_sessions CASCADE;
DROP TABLE IF EXISTS user_push_subscriptions CASCADE; -- NEW v3.0
DROP TABLE IF EXISTS user_notification_settings CASCADE; -- NEW v3.0
DROP TABLE IF EXISTS security_logs CASCADE;
DROP TABLE IF EXISTS banned_identifiers CASCADE; -- NEW
DROP TABLE IF EXISTS files_to_delete CASCADE;    -- NEW
DROP TABLE IF EXISTS email_logs CASCADE;
DROP TABLE IF EXISTS search_history CASCADE;
DROP TABLE IF EXISTS data_correction_requests CASCADE;
DROP TABLE IF EXISTS user_follows CASCADE;
DROP TABLE IF EXISTS notifications CASCADE;
DROP TABLE IF EXISTS review_likes CASCADE;
DROP TABLE IF EXISTS restaurant_opening_hours CASCADE;
DROP TABLE IF EXISTS dish_section_assignments CASCADE;
DROP TABLE IF EXISTS menu_sections CASCADE;
-- State Machine Migration - All Moderation Tables
DROP TABLE IF EXISTS moderation_comments CASCADE;
DROP TABLE IF EXISTS moderation_photos CASCADE;
DROP TABLE IF EXISTS pending_comments CASCADE;
DROP TABLE IF EXISTS pending_user_photos CASCADE;
DROP TABLE IF EXISTS ai_review_photos CASCADE;
DROP TABLE IF EXISTS ai_review_comments CASCADE;
DROP TABLE IF EXISTS admin_review_photos CASCADE;
DROP TABLE IF EXISTS admin_review_comments CASCADE;
DROP TABLE IF EXISTS moderation_logs CASCADE;
DROP TABLE IF EXISTS rejection_reasons CASCADE;
DROP TABLE IF EXISTS report_reason_assignments CASCADE;
DROP TABLE IF EXISTS report_reason_definitions CASCADE;
DROP TABLE IF EXISTS reports CASCADE;
DROP TABLE IF EXISTS media_assets CASCADE;
DROP TABLE IF EXISTS saved_dishes CASCADE;
DROP TABLE IF EXISTS favorite_restaurants CASCADE;
DROP TABLE IF EXISTS restaurant_tags CASCADE;
DROP TABLE IF EXISTS dish_tags CASCADE;
DROP TABLE IF EXISTS tags CASCADE;
DROP TABLE IF EXISTS reviews CASCADE;
DROP TABLE IF EXISTS dish_ingredients CASCADE;
DROP TABLE IF EXISTS dishes CASCADE;
DROP TABLE IF EXISTS dish_variants CASCADE;
DROP TABLE IF EXISTS dish_archetypes CASCADE;
DROP TABLE IF EXISTS restaurants CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS system_settings CASCADE;
DROP TABLE IF EXISTS node_heartbeats CASCADE;
DROP TABLE IF EXISTS gpu_tasks CASCADE;
DROP TABLE IF EXISTS system_nodes CASCADE;
DROP TABLE IF EXISTS ingredients CASCADE;
DROP TABLE IF EXISTS cities CASCADE;

-- Functions and Triggers are dropped automatically if they are attached to tables,
-- but good practice to clean up if we use CREATE OR REPLACE.
