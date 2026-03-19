-- ========================================
-- AUDIT SYSTEM v4.1
-- ========================================
-- Universal audit logging system for tracking data changes across critical tables.
--
-- Purpose:
--   Track who changed what and when for restaurants, users, dishes, and reviews.
--   Store "Before" and "After" state using JSONB for flexible forensic analysis.
--
-- Key Features:
--   1. Universal audit logging (works with any table)
--   2. Captures full row state as JSONB
--   3. Tracks operation type (INSERT/UPDATE/DELETE)
--   4. Records user context (changed_by)
--   5. Optimized indexes for fast querying
--
-- Dependencies:
--   - sql/modules/01_tables.sql (core tables)
--
-- Usage:
--   Attach log_audit_event() trigger to any table requiring audit trail.
-- ========================================

-- ========================================
-- TABLE: audit_logs
-- ========================================
-- Stores complete audit trail of data changes

CREATE TABLE IF NOT EXISTS audit_logs (
    audit_log_id BIGSERIAL PRIMARY KEY,
    table_name VARCHAR(100) NOT NULL,
    record_id INT NOT NULL,
    operation VARCHAR(10) NOT NULL CHECK (operation IN ('INSERT', 'UPDATE', 'DELETE')),
    changed_by VARCHAR(100) DEFAULT 'system',
    changed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    old_values JSONB NULL,
    new_values JSONB NULL
);

COMMENT ON TABLE audit_logs IS
'Universal audit log tracking all data changes to critical tables.
Stores before/after state as JSONB for flexible querying and rollback analysis.';

COMMENT ON COLUMN audit_logs.table_name IS 'Name of the table where change occurred';
COMMENT ON COLUMN audit_logs.record_id IS 'Primary key value of the changed record';
COMMENT ON COLUMN audit_logs.operation IS 'Type of operation: INSERT, UPDATE, or DELETE';
COMMENT ON COLUMN audit_logs.changed_by IS 'User ID or username who made the change (from session or current_user)';
COMMENT ON COLUMN audit_logs.old_values IS 'JSONB snapshot of row state BEFORE the change (NULL for INSERT)';
COMMENT ON COLUMN audit_logs.new_values IS 'JSONB snapshot of row state AFTER the change (NULL for DELETE)';

-- ========================================
-- INDEXES: Optimized for Common Query Patterns
-- ========================================
-- Audit logs grow rapidly - these indexes are critical for performance

-- Index 1: Lookup by table and record (most common: "show me history of restaurant #42")
CREATE INDEX IF NOT EXISTS idx_audit_logs_table_record ON audit_logs(table_name, record_id);

-- Index 2: Time-based queries (recent changes, date range filters)
CREATE INDEX IF NOT EXISTS idx_audit_logs_changed_at ON audit_logs(changed_at DESC);

-- Index 3: Combined table + time (dashboard: "recent changes to restaurants")
CREATE INDEX IF NOT EXISTS idx_audit_logs_table_time ON audit_logs(table_name, changed_at DESC);

-- Index 4: User activity tracking (who changed what)
CREATE INDEX IF NOT EXISTS idx_audit_logs_changed_by ON audit_logs(changed_by);

COMMENT ON INDEX idx_audit_logs_table_record IS
'Primary lookup index for finding all changes to a specific record.
Example: SELECT * FROM audit_logs WHERE table_name = ''restaurants'' AND record_id = 42;';

COMMENT ON INDEX idx_audit_logs_changed_at IS
'Time-based queries for recent activity monitoring.
Example: SELECT * FROM audit_logs WHERE changed_at > NOW() - INTERVAL ''7 days'';';

-- ========================================
-- FUNCTION: log_audit_event()
-- ========================================
-- Generic trigger function that can be attached to any table
--
-- How to use:
--   CREATE TRIGGER trg_audit_restaurants
--   AFTER INSERT OR UPDATE OR DELETE ON restaurants
--   FOR EACH ROW
--   EXECUTE FUNCTION log_audit_event('restaurant_id');
--
-- Parameter: TG_ARGV[0] = primary key column name (e.g., 'restaurant_id')

CREATE OR REPLACE FUNCTION log_audit_event()
RETURNS TRIGGER AS $$
DECLARE
    pk_column_name VARCHAR(100);
    pk_value INT;
    user_context VARCHAR(100);
BEGIN
    -- Get primary key column name from trigger argument
    -- Example: EXECUTE FUNCTION log_audit_event('restaurant_id')
    IF TG_NARGS < 1 THEN
        RAISE EXCEPTION 'log_audit_event() requires primary key column name as argument';
    END IF;
    pk_column_name := TG_ARGV[0];

    -- Extract primary key value from the appropriate row (OLD for DELETE, NEW for INSERT/UPDATE)
    IF TG_OP = 'DELETE' THEN
        -- For DELETE, use OLD row to get PK
        EXECUTE format('SELECT ($1).%I', pk_column_name) INTO pk_value USING OLD;
    ELSE
        -- For INSERT/UPDATE, use NEW row to get PK
        EXECUTE format('SELECT ($1).%I', pk_column_name) INTO pk_value USING NEW;
    END IF;

    -- Get user context (try session variable first, fallback to PostgreSQL user)
    BEGIN
        -- Try to get application user from session variable
        -- Application should: SET LOCAL app.current_user_id = '12345';
        user_context := current_setting('app.current_user_id', TRUE);

        -- If not set, use PostgreSQL username
        IF user_context IS NULL OR user_context = '' THEN
            user_context := current_user;
        END IF;
    EXCEPTION
        WHEN OTHERS THEN
            user_context := current_user;
    END;

    -- Insert audit record based on operation type
    IF TG_OP = 'INSERT' THEN
        INSERT INTO audit_logs (
            table_name,
            record_id,
            operation,
            changed_by,
            old_values,
            new_values
        ) VALUES (
            TG_TABLE_NAME,
            pk_value,
            'INSERT',
            user_context,
            NULL,  -- No old values for INSERT
            row_to_json(NEW)::jsonb
        );

    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO audit_logs (
            table_name,
            record_id,
            operation,
            changed_by,
            old_values,
            new_values
        ) VALUES (
            TG_TABLE_NAME,
            pk_value,
            'UPDATE',
            user_context,
            row_to_json(OLD)::jsonb,
            row_to_json(NEW)::jsonb
        );

    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO audit_logs (
            table_name,
            record_id,
            operation,
            changed_by,
            old_values,
            new_values
        ) VALUES (
            TG_TABLE_NAME,
            pk_value,
            'DELETE',
            user_context,
            row_to_json(OLD)::jsonb,
            NULL  -- No new values for DELETE
        );
    END IF;

    -- Return appropriate value based on trigger timing
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    ELSE
        RETURN NEW;
    END IF;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION log_audit_event() IS
'Universal audit logging function for any table.
Automatically captures INSERT/UPDATE/DELETE operations with before/after state.

Usage:
    CREATE TRIGGER trg_audit_tablename
    AFTER INSERT OR UPDATE OR DELETE ON tablename
    FOR EACH ROW
    EXECUTE FUNCTION log_audit_event(''primary_key_column_name'');

The function expects one argument: the name of the primary key column.
Example: log_audit_event(''restaurant_id'')

User Context:
    Tries to read from session variable app.current_user_id first.
    Application should set: SET LOCAL app.current_user_id = ''user_id'';
    Falls back to current_user (PostgreSQL username) if not set.';

-- ========================================
-- HELPER QUERY EXAMPLES
-- ========================================
-- Example 1: View all changes to a specific restaurant
-- SELECT * FROM audit_logs
-- WHERE table_name = 'restaurants' AND record_id = 42
-- ORDER BY changed_at DESC;

-- Example 2: Compare before/after state for an update
-- SELECT
--     changed_at,
--     old_values->>'restaurant_name' AS old_name,
--     new_values->>'restaurant_name' AS new_name
-- FROM audit_logs
-- WHERE table_name = 'restaurants' AND operation = 'UPDATE' AND record_id = 42;

-- Example 3: Recent deletions across all tables
-- SELECT table_name, record_id, changed_by, changed_at, old_values
-- FROM audit_logs
-- WHERE operation = 'DELETE' AND changed_at > NOW() - INTERVAL '7 days'
-- ORDER BY changed_at DESC;

-- Example 4: User activity summary
-- SELECT
--     changed_by,
--     table_name,
--     COUNT(*) AS change_count
-- FROM audit_logs
-- WHERE changed_at > NOW() - INTERVAL '30 days'
-- GROUP BY changed_by, table_name
-- ORDER BY change_count DESC;

-- ========================================
-- MAINTENANCE NOTES
-- ========================================
-- The audit_logs table grows indefinitely. Consider:
--   1. Partitioning by changed_at (monthly/yearly partitions)
--   2. Archiving old records to cold storage
--   3. Periodic VACUUM to reclaim space
--
-- Typical growth rate: ~200-500 bytes per audit record
-- For high-traffic systems: 100k changes/day = ~20-50 MB/day
-- ========================================