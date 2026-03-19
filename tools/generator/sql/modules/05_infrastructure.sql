/*
    Moduł: 05_infrastructure.sql
    Opis: Infrastruktura systemowa dla mikroserwisów "Smakosz" (Engineering Thesis Infrastructure).
    Schemat: 'system' (izolowany od 'public').
    
    Elementy:
    1. Config: Hot Reload konfiguracji (LISTEN/NOTIFY).
    2. Nodes: Rejestr usług, status (Heartbeat) i konfiguracja sieciowa (IP/MAC).
    3. Jobs: Kolejka zadań z obsługą dużych plików przez R2 (JSONB payload/result).
    4. Logs: Centralny magazyn zdarzeń z metrykami w JSONB (pod wykresy Ops).
    5. Admin Tickets: Zunifikowany widok zadań dla moderatorów (Shared Inbox).
    6. Ops Views: Analityka i Health Check.
*/

-- 1. Utworzenie schematu systemowego
CREATE SCHEMA IF NOT EXISTS system;

-- =============================================================================
-- TABELA A: Konfiguracja Systemu (Hot Reload)
-- =============================================================================
CREATE TABLE system.config (
    key VARCHAR(50) PRIMARY KEY,
    value TEXT NOT NULL,
    description TEXT,
    is_secret BOOLEAN DEFAULT FALSE, -- For Admin UI (masking)
    is_public BOOLEAN DEFAULT FALSE, -- For Frontend API (exposed)
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    updated_by INT -- Admin ID
);
CREATE INDEX idx_config_public ON system.config(key, value) WHERE is_public = TRUE;

COMMENT ON TABLE system.config IS 'Konfiguracja dynamiczna. Zmiany wysyłają sygnał NOTIFY "system_config_change".';

-- =============================================================================
-- TABELA B: Rejestr Węzłów (Service Registry & Heartbeat)
-- =============================================================================
CREATE TABLE system.nodes (
    node_id VARCHAR(50) PRIMARY KEY, -- e.g. 'worker-gpu-01'
    ip_address VARCHAR(45),
    mac_address VARCHAR(17), -- Required for WoL
    wol_gateway_id VARCHAR(50) REFERENCES system.nodes(node_id) ON DELETE SET NULL, -- Gateway responsible for waking this node
    role VARCHAR(20) CHECK (role IN ('dispatcher', 'worker', 'gateway')),
    status VARCHAR(20) DEFAULT 'offline',
    gpu_name VARCHAR(100),
    last_heartbeat TIMESTAMPTZ
);

COMMENT ON TABLE system.nodes IS 'Rejestr usług. Służy do monitoringu (Heartbeat) i discovery (IP/MAC).';

CREATE TABLE system.service_accounts (
    account_id SERIAL PRIMARY KEY,
    service_name VARCHAR(50) NOT NULL UNIQUE,
    token_hash VARCHAR(255) NOT NULL,
    permissions JSONB DEFAULT '[]',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
COMMENT ON TABLE system.service_accounts IS 'Konta M2M (Machine-to-Machine) dla wewnętrznych mikroserwisów.';

-- =============================================================================
-- TABELA C: Kolejka Zadań (Database-as-a-Queue)
-- =============================================================================
CREATE TABLE system.jobs (
    job_id SERIAL PRIMARY KEY,
    type VARCHAR(50) NOT NULL, -- np. 'TRAINING_NCF', 'GENERATE_REPORT', 'SYSTEM_MAINTENANCE'
    
    -- Zarządzanie stanem
    status VARCHAR(20) DEFAULT 'PENDING', -- 'PENDING', 'PROCESSING', 'COMPLETED', 'FAILED', 'CANCELLED'
    priority INT DEFAULT 0, -- Wyższy priorytet = pierwszeństwo pobrania
    
    -- Dane (Wzorzec Claim Check z R2)
    -- Input: Linki do danych treningowych w R2, parametry algorytmu
    payload JSONB DEFAULT '{}', 
    
    -- Output: Linki do gotowych modeli w R2, metryki końcowe
    result JSONB DEFAULT '{}', 
    
    -- Kontekst Biznesowy (Szybkie filtrowanie)
    entity_id VARCHAR(50), -- np. ID restauracji
    entity_type VARCHAR(30), -- np. 'RESTAURANT', 'USER'
    
    -- Przypisanie
    worker_node VARCHAR(50) REFERENCES system.nodes(node_id) ON DELETE SET NULL,
    
    -- Monitoring postępu
    progress INT DEFAULT 0 CHECK (progress BETWEEN 0 AND 100),
    progress_message TEXT,
    error_log TEXT,
    
    -- Audyt czasowy
    created_at TIMESTAMPTZ DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    finished_at TIMESTAMPTZ
);

-- Indeksy
CREATE INDEX idx_jobs_processing ON system.jobs(status, priority DESC, created_at ASC); -- Dla Dispatchera
CREATE INDEX idx_jobs_worker ON system.jobs(worker_node); -- Historia workera
CREATE INDEX idx_jobs_entity ON system.jobs(entity_type, entity_id); -- Historia obiektu

COMMENT ON TABLE system.jobs IS 'Kolejka zadań. Payload/Result przechowuje linki do R2 (duże pliki).';

-- =============================================================================
-- TABELA D: Logi Systemowe (Analityka Ops)
-- =============================================================================
CREATE TABLE system.logs (
    id BIGSERIAL PRIMARY KEY,
    source VARCHAR(50) NOT NULL, -- np. 'backend-api', 'gpu-worker'
    level VARCHAR(10) NOT NULL, -- 'INFO', 'WARNING', 'ERROR', 'CRITICAL'
    message TEXT NOT NULL,
    context JSONB DEFAULT '{}', -- Metrics/Data for Ops Dashboard
    created_at TIMESTAMPTZ DEFAULT NOW()
);
-- Optimized Indexes for Ops Dashboard
CREATE INDEX idx_system_logs_created_at ON system.logs(created_at DESC);
CREATE INDEX idx_system_logs_level ON system.logs(level, created_at DESC);
CREATE INDEX idx_system_logs_source ON system.logs(source, created_at DESC);

COMMENT ON TABLE system.logs IS 'Logi i metryki. Context zawiera dane do wykresów w panelu Ops.';

-- =============================================================================
-- TABELA D2: Logi Biznesowe (Security, Email, Moderation)
-- =============================================================================

CREATE TABLE system.security_logs (
    log_id BIGSERIAL PRIMARY KEY,
    event_type VARCHAR(50), -- 'failed_login', 'blocked_ip', 'suspicious_activity', 'password_reset'
    ip_address INET,
    user_agent TEXT,
    email VARCHAR(100),
    user_id INT REFERENCES public.users(user_id) ON DELETE SET NULL,
    details JSONB DEFAULT '{}',
    country_code VARCHAR(2),
    city VARCHAR(100),
    created_at TIMESTAMPTZ DEFAULT NOW()
);
-- Indexes for User Profile & Security Investigations
CREATE INDEX idx_security_logs_user ON system.security_logs(user_id, created_at DESC);
CREATE INDEX idx_security_logs_type ON system.security_logs(event_type, created_at DESC);
CREATE INDEX idx_security_logs_ip ON system.security_logs(ip_address);

CREATE TABLE system.email_logs (
    log_id BIGSERIAL PRIMARY KEY,
    type VARCHAR(50), -- 'verification', 'password_reset', 'notification', 'digest'
    recipient VARCHAR(100) NOT NULL,
    subject VARCHAR(200) NOT NULL,
    status VARCHAR(20) DEFAULT 'pending', -- 'pending', 'sent', 'failed', 'bounced'
    provider VARCHAR(50), -- 'sendgrid', 'ses'
    provider_message_id VARCHAR(100),
    error_message TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    sent_at TIMESTAMPTZ
);
CREATE INDEX idx_email_logs_user ON system.email_logs(recipient, created_at DESC);

CREATE TABLE system.moderation_logs (
    log_id BIGSERIAL PRIMARY KEY,
    entity_type VARCHAR(50) NOT NULL CHECK (entity_type IN ('photo', 'review', 'edit_request')),
    entity_id INT NOT NULL,
    actor VARCHAR(20) NOT NULL CHECK (actor IN ('admin', 'system', 'ai')),
    verdict VARCHAR(20) NOT NULL CHECK (verdict IN ('approve', 'reject')),
    reason_codes VARCHAR(50)[] DEFAULT ARRAY[]::VARCHAR[],
    admin_note TEXT,
    processed_by INT REFERENCES public.users(user_id) ON DELETE SET NULL, -- NULL = AI/system
    ai_scores JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_moderation_logs_entity ON system.moderation_logs(entity_type, entity_id, created_at DESC);
CREATE INDEX idx_moderation_logs_processed_by ON system.moderation_logs(processed_by, created_at DESC) WHERE processed_by IS NOT NULL;

COMMENT ON TABLE system.moderation_logs IS 'Historia decyzji moderacyjnych (AI/Admin). Przeniesiona do schematu system.';

CREATE TABLE system.ai_logs (
    log_id BIGSERIAL PRIMARY KEY,
    model_type VARCHAR(50), -- 'herbert', 'nsfw', 'clip', 'ncf'
    model_version VARCHAR(50), -- np. 'herbert-toxicity-v1'
    entity_type VARCHAR(50), -- 'review', 'media_asset', 'user', 'dish', 'restaurant'
    entity_id INT,
    input_summary TEXT,
    scores JSONB,
    verdict VARCHAR(50), -- 'approved', 'rejected', 'needs_review'
    processing_time_ms INT,
    fallback BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_ai_logs_entity ON system.ai_logs(entity_type, entity_id);
CREATE INDEX idx_ai_logs_model ON system.ai_logs(model_type, created_at DESC);
COMMENT ON TABLE system.ai_logs IS 'Logi inferencji AI.';

-- =============================================================================
-- TABELA H: Bilety Administracyjne (Admin Tickets & Concurrency)
-- =============================================================================
CREATE TABLE system.tickets (
    ticket_id SERIAL PRIMARY KEY,
    ticket_type VARCHAR(50) NOT NULL, -- 'review_content', 'photo', 'report', 'edit_request', 'ingredient_suggestion', 'data_correction'
    reference_id BIGINT NOT NULL,      -- ID in the source table
    
    status VARCHAR(20) DEFAULT 'open', -- 'open', 'in_progress', 'resolved', 'rejected', 'closed'
    priority INT DEFAULT 3,           -- 1 (Low) to 5 (Urgent)
    
    assigned_admin_id INT REFERENCES public.users(user_id) ON DELETE SET NULL,
    locked_at TIMESTAMPTZ,              -- Mutex for admin handling (expiration logic)
    
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    version INT DEFAULT 1,

    UNIQUE (ticket_type, reference_id)
);

CREATE INDEX idx_tickets_lookup ON system.tickets(ticket_type, reference_id);
CREATE INDEX idx_tickets_status_priority ON system.tickets(status, priority DESC);
CREATE INDEX idx_tickets_assigned ON system.tickets(assigned_admin_id) WHERE assigned_admin_id IS NOT NULL;

COMMENT ON TABLE system.tickets IS 
'Scentralizowana kolejka zadań dla adminów. Synchronizowana dwustronnie z tabelami źródłowymi.';

-- =============================================================================
-- FUNKCJE I TRIGGERY (Automatyzacja)
-- =============================================================================

-- 1. NOTIFY - Powiadamianie o zmianie konfiguracji
CREATE OR REPLACE FUNCTION system.notify_config_change()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM pg_notify(
        'system_config_channel',
        json_build_object(
            'operation', TG_OP,
            'key', NEW.key,
            'value', CASE WHEN NEW.is_secret THEN '******' ELSE NEW.value END
        )::text
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_config_change ON system.config;
CREATE TRIGGER trg_config_change
    AFTER INSERT OR UPDATE ON system.config
    FOR EACH ROW EXECUTE FUNCTION system.notify_config_change();

-- 2. JOB TIMESTAMPTZS - Automatyczne czasy start/stop
CREATE OR REPLACE FUNCTION system.update_job_timestamps()
RETURNS TRIGGER AS $$
BEGIN
    -- Start
    IF OLD.status != 'PROCESSING' AND NEW.status = 'PROCESSING' THEN
        NEW.started_at = NOW();
    -- Koniec
    ELSIF NEW.status IN ('COMPLETED', 'FAILED', 'CANCELLED') AND OLD.status != NEW.status THEN
        NEW.finished_at = NOW();
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_job_timestamps ON system.jobs;
CREATE TRIGGER trg_job_timestamps
    BEFORE UPDATE ON system.jobs
    FOR EACH ROW EXECUTE FUNCTION system.update_job_timestamps();

-- =============================================================================
-- VIEW: ADMIN TICKETS (Shared Inbox)
-- =============================================================================
-- Agreguje wszystkie sprawy wymagające uwagi moderatora w jedną listę.
-- Źródła: Zgłoszenia użytkowników, Edycje B2B, Sugestie danych (bez właściciela).

CREATE OR REPLACE VIEW system.admin_tickets AS
    -- 1. Zgłoszenia naruszeń (Reports)
    SELECT 
        'report'::VARCHAR(20) as ticket_type,
        report_id as ticket_id,
        status,
        created_at,
        'Zgłoszenie naruszenia'::TEXT as title,
        description as subtitle,
        NULL::INT as restaurant_id, -- Raporty mogą dotyczyć usera/opinii, nie zawsze restauracji
        reporter_id as user_id,
        version
    FROM reports 
    WHERE status = 'pending'

    UNION ALL

    -- 2. Prośby o edycję restauracji (B2B)
    SELECT 
        'restaurant_edit'::VARCHAR(20) as ticket_type,
        request_id as ticket_id,
        status,
        created_at,
        'Weryfikacja zmian w restauracji'::TEXT as title,
        'Prośba o edycję danych: ' || 
        CONCAT_WS(', ', 
            CASE WHEN new_name IS NOT NULL THEN 'Nazwa' END,
            CASE WHEN new_address IS NOT NULL THEN 'Adres' END
        ) as subtitle,
        restaurant_id,
        user_id,
        version
    FROM restaurant_edit_requests 
    WHERE status = 'pending'

    UNION ALL

    -- 3. Sugestie składników (Community)
    SELECT 
        'ingredient_proposal'::VARCHAR(20) as ticket_type,
        suggestion_id as ticket_id,
        status,
        created_at,
        'Nowy składnik do bazy'::TEXT as title,
        'Sugestia: ' || suggested_name as subtitle,
        restaurant_id,
        user_id,
        version
    FROM ingredient_suggestions
    WHERE status = 'pending'

    UNION ALL

    -- 4. Korekty danych (Orphans OR Escalated SLA Breach)
    SELECT 
        'data_correction'::VARCHAR(20) as ticket_type,
        request_id as ticket_id,
        dcr.status,
        dcr.created_at,
        CASE 
            WHEN r.owner_id IS NOT NULL THEN 'Korekta danych (ESKALACJA - Brak reakcji właściciela)'
            ELSE 'Korekta danych (bez właściciela)'
        END as title,
        'Typ problemu: ' || issue_type as subtitle,
        dcr.restaurant_id,
        dcr.user_id,
        dcr.version
    FROM data_correction_requests dcr
    JOIN restaurants r ON dcr.restaurant_id = r.restaurant_id
    WHERE dcr.status = 'pending' 
      AND (
          r.owner_id IS NULL -- Brak właściciela
          OR 
          dcr.created_at < NOW() - INTERVAL '7 days' -- SLA Breach (Eskalacja)
      );

COMMENT ON VIEW system.admin_tickets IS 
'Zunifikowana lista zadań dla administratorów. Uwzględnia automatyczną eskalację zgłoszeń po 7 dniach.';

-- =============================================================================
-- TABELA F: Czarne Listy (Security)
-- =============================================================================
CREATE TABLE system.banned_identifiers (
    ban_id SERIAL PRIMARY KEY,
    type VARCHAR(20) NOT NULL CHECK (type IN ('email', 'phone', 'ip', 'email_domain')),
    value VARCHAR(255) NOT NULL,
    reason TEXT,
    banned_by INT REFERENCES public.users(user_id) ON DELETE SET NULL,
    banned_at TIMESTAMPTZ DEFAULT NOW(),
    expires_at TIMESTAMPTZ,
    UNIQUE (type, value)
);

COMMENT ON TABLE system.banned_identifiers IS 
'Registry of banned emails/phones. 
Prevents deleted/banned users from re-registering with the same credentials.';

-- =============================================================================
-- TABELA F2: Zabronione Słowa (Profanity / Reserved Names)
-- =============================================================================
CREATE TABLE system.forbidden_words (
    word_id SERIAL PRIMARY KEY,
    word VARCHAR(100) NOT NULL UNIQUE,
    category VARCHAR(50) NOT NULL DEFAULT 'profanity'
        CHECK (category IN ('profanity', 'reserved', 'offensive', 'trademark')),
    is_regex BOOLEAN DEFAULT FALSE,
    added_by INT REFERENCES public.users(user_id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

COMMENT ON TABLE system.forbidden_words IS
'Słowa zabronione przy rejestracji i zmianie username.
Kategorie: profanity (wulgaryzmy), reserved (admin/system/api), offensive, trademark.
is_regex=true -> word traktowany jako wzorzec regex.';

-- =============================================================================
-- TABELA F3: Sesje JWT (Refresh Token Rotation)
-- =============================================================================
CREATE TABLE system.refresh_tokens (
    token_id BIGSERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES public.users(user_id) ON DELETE CASCADE,
    token_hash VARCHAR(128) NOT NULL UNIQUE, -- SHA-256 hash, NIE plaintext
    device_info VARCHAR(255),                -- Skrócony User-Agent
    ip_address INET,
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ,                  -- NULL = aktywny
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_refresh_tokens_user ON system.refresh_tokens(user_id, revoked_at);
CREATE INDEX idx_refresh_tokens_expiry ON system.refresh_tokens(expires_at) WHERE revoked_at IS NULL;

COMMENT ON TABLE system.refresh_tokens IS
'JWT refresh tokens z one-time use rotation.
Replay detection: użycie revoked tokenu -> revoke ALL dla usera.';

-- =============================================================================
-- TABELA G: Kolejka Usuwania Plików (R2 Reaper)
-- =============================================================================
CREATE TABLE system.files_to_delete (
    file_id BIGSERIAL PRIMARY KEY,
    r2_key VARCHAR(500) NOT NULL,
    bucket VARCHAR(100) DEFAULT 'smakosz-photos',
    reason VARCHAR(50), -- 'orphaned', 'rejected', 'user_deleted'
    source_entity VARCHAR(50), -- 'media_assets', 'users'
    source_id INT,
    queued_at TIMESTAMPTZ DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    error TEXT
);

COMMENT ON TABLE system.files_to_delete IS 
'Queue for external file cleanup script (R2/S3 Reaper).
Trigger populates this when media records are deleted from DB.';

-- =============================================================================
-- WIDOKI OPS & ANALITYKA (Panel Administratora)
-- =============================================================================

-- 1. Mapa Zagrożeń (Security Hotspots)
-- Agreguje nieudane logowania i ataki Brute Force po IP.
CREATE OR REPLACE VIEW system.vw_security_hotspots AS
SELECT 
    ip_address, 
    country_code, 
    COUNT(DISTINCT user_id) as affected_accounts, 
    COUNT(*) as total_attempts,
    MAX(created_at) as last_attempt_at
FROM system.security_logs
WHERE event_type IN ('login_failed', 'brute_force_attempt')
GROUP BY ip_address, country_code
ORDER BY total_attempts DESC;

COMMENT ON VIEW system.vw_security_hotspots IS 
'Lista podejrzanych adresów IP (nieudane logowania). Służy do banowania.';

-- 2. System Health Check (Traffic Light)
-- Sprawdza status węzłów i awaryjność zadań.
CREATE OR REPLACE VIEW system.vw_system_health AS
-- A. Martwe węzły (Brak Heartbeatu > 5 min)
SELECT 
    'CRITICAL'::TEXT as status, 
    'Node DOWN: ' || node_id as message,
    'infrastructure'::TEXT as category,
    last_heartbeat as timestamp
FROM system.nodes 
WHERE last_heartbeat < NOW() - INTERVAL '5 minutes'

UNION ALL

-- B. Błędy krytyczne w logach (Ostatnia godzina)
SELECT 
    'WARNING'::TEXT as status, 
    'High Error Rate: ' || source as message,
    'logs'::TEXT as category,
    MAX(created_at) as timestamp
FROM system.logs
WHERE level = 'ERROR' AND created_at > NOW() - INTERVAL '1 hour'
GROUP BY source
HAVING COUNT(*) > 10

UNION ALL

-- C. Zadania zakończone błędem (Ostatnie 24h)
SELECT 
    'WARNING'::TEXT as status, 
    'Job Failed: ' || type as message,
    'jobs'::TEXT as category,
    finished_at as timestamp
FROM system.jobs 
WHERE status = 'FAILED' AND finished_at > NOW() - INTERVAL '24 hours';

COMMENT ON VIEW system.vw_system_health IS 
'Raport stanu systemu. Pusty wynik = System zdrowy.';

-- 3. Ops Dashboard Metrics (Daily Stats)
-- Szybki podgląd trendów dla Admina.
CREATE OR REPLACE VIEW system.vw_ops_dashboard AS
SELECT
    DATE(created_at) as day,
    COUNT(*) FILTER (WHERE event_type = 'login_success') as successful_logins,
    COUNT(*) FILTER (WHERE event_type = 'login_failed') as failed_logins,
    COUNT(DISTINCT user_id) as active_users
FROM system.security_logs
WHERE created_at > NOW() - INTERVAL '30 days'
GROUP BY 1
ORDER BY 1 DESC;

COMMENT ON VIEW system.vw_ops_dashboard IS 
'Dzienne statystyki logowań i aktywności (ostatnie 30 dni).';

-- =============================================================================
-- MAINTENANCE: Scheduled Jobs (pg_cron)
-- =============================================================================

-- Przeliczanie trendów (Bayesian Average) - codziennie o 04:00
-- Uwaga: Wymaga rozszerzenia pg_cron zainstalowanego w bazie.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_cron') THEN
        -- Trending scores (daily at 04:00)
        PERFORM cron.schedule(
            'calculate_trending_scores',
            '0 4 * * *',
            'SELECT calculate_trending_scores();'
        );
        
        -- Average ratings (hourly)
        PERFORM cron.schedule(
            'update_average_ratings',
            '0 * * * *',
            'SELECT update_average_ratings();'
        );
        
        -- Notification pruning (daily at 03:00)
        PERFORM cron.schedule(
            'prune_notifications',
            '0 3 * * *',
            'SELECT prune_notifications();'
        );
    END IF;
END $$;

COMMENT ON SCHEMA system IS 'System infrastructure: config, nodes, jobs, logs, tickets. pg_cron jobs: trending (04:00), avg_ratings (hourly), prune (03:00).';
