-- MIGRATION: Worker API Tables
-- Version: 6.2
-- Date: 2026-01-01
-- Description: Wdrożenie tabel i kolumn dla architektury GPU Worker API (System Schema)

BEGIN;

-- ==========================================
-- 1. SERVICE ACCOUNTS (Machine-to-Machine Auth)
-- ==========================================
CREATE TABLE IF NOT EXISTS system.service_accounts (
    account_id SERIAL PRIMARY KEY,
    account_name VARCHAR(100) UNIQUE NOT NULL, -- np. 'gpu-worker-01'
    token_hash VARCHAR(255) NOT NULL,          -- Hash klucza API / JWT Secret
    role VARCHAR(50) NOT NULL DEFAULT 'worker',
    permissions JSONB DEFAULT '[]'::jsonb,     -- Granularne uprawnienia
    is_active BOOLEAN DEFAULT TRUE,
    last_used_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

COMMENT ON TABLE system.service_accounts IS 'Konta techniczne dla workerów i zewnętrznych API (M2M)';

-- ==========================================
-- 2. SYSTEM.NODES (Rozszerzenie)
-- ==========================================
-- Dodajemy kolumny do istniejącej tabeli, aby wspierać GPU Workery

ALTER TABLE system.nodes
    ADD COLUMN IF NOT EXISTS node_type VARCHAR(20) DEFAULT 'api', -- 'api', 'gpu', 'orchestrator'
    ADD COLUMN IF NOT EXISTS status VARCHAR(20) DEFAULT 'unknown', -- 'online', 'processing', 'idle', 'offline'
    ADD COLUMN IF NOT EXISTS hostname VARCHAR(255),
    ADD COLUMN IF NOT EXISTS gpu_name VARCHAR(255),
    ADD COLUMN IF NOT EXISTS gpu_memory_total INT, -- MB
    ADD COLUMN IF NOT EXISTS gpu_memory_used INT,  -- MB
    ADD COLUMN IF NOT EXISTS current_job_id INT,   -- Link do job_id
    ADD COLUMN IF NOT EXISTS metadata JSONB DEFAULT '{}'::jsonb;

-- Indeks do szybkiego znajdowania martwych workerów
CREATE INDEX IF NOT EXISTS idx_nodes_heartbeat_type
    ON system.nodes (node_type, status, last_heartbeat);

-- ==========================================
-- 3. SYSTEM.JOBS (Rozszerzenie)
-- ==========================================
-- Dodajemy obsługę Retry Logic, Priority i Wyników

ALTER TABLE system.jobs
    ADD COLUMN IF NOT EXISTS priority INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS started_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS result JSONB,       -- Wynik: model_url, metrics
    ADD COLUMN IF NOT EXISTS error_message TEXT, -- Stack trace błędu
    ADD COLUMN IF NOT EXISTS attempts INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS max_attempts INT DEFAULT 3,
    ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ DEFAULT NOW();

-- Indeks do PULL MODEL (Kluczowy dla wydajności Workera)
-- Szybkie pobieranie: PENDING, Wysoki Priorytet, Najstarsze
CREATE INDEX IF NOT EXISTS idx_jobs_pull_queue
    ON system.jobs (status, priority DESC, created_at ASC)
    WHERE status = 'PENDING';

-- Monitoring wiszących zadań
CREATE INDEX IF NOT EXISTS idx_jobs_stuck_monitor
    ON system.jobs (status, started_at)
    WHERE status = 'PROCESSING';

-- ==========================================
-- 4. SYSTEM.JOB_PROGRESS (Nowa Tabela)
-- ==========================================
-- Time-series dla monitorowania postępu treningu ML

CREATE TABLE IF NOT EXISTS system.job_progress (
    progress_id BIGSERIAL PRIMARY KEY,
    job_id INT NOT NULL REFERENCES system.jobs(job_id) ON DELETE CASCADE,
    epoch INT,
    loss DOUBLE PRECISION,
    accuracy DOUBLE PRECISION,
    learning_rate DOUBLE PRECISION,
    current_step INT,
    total_steps INT,
    percentage DOUBLE PRECISION GENERATED ALWAYS AS (
        CASE WHEN total_steps > 0 THEN (current_step::double precision / total_steps) * 100 ELSE 0 END
    ) STORED,
    metadata JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Indeks do szybkiego pobierania "Ostatniego stanu"
CREATE INDEX IF NOT EXISTS idx_job_progress_latest
    ON system.job_progress (job_id, created_at DESC);

COMMENT ON TABLE system.job_progress IS 'Log postępów zadań długotrwałych (ML Training)';

COMMIT;
