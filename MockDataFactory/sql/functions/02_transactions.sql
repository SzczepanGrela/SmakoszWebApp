-- ========================================
-- SCHEMA: TRANSACTIONAL PROCEDURES
-- ========================================
-- Contains stored procedures for complex business logic requiring atomicity.
-- Examples: User Registration, Review Submission, Moderation Actions.

-- ========================================
-- PROCEDURE 1: register_user
-- ========================================
CREATE OR REPLACE PROCEDURE register_user(
    p_username VARCHAR(100),
    p_email VARCHAR(100),
    p_password_hash VARCHAR(255),
    p_role VARCHAR(20) DEFAULT 'user'
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO users (username, email, password_hash, role)
    VALUES (p_username, p_email, p_password_hash, p_role);
EXCEPTION
    WHEN unique_violation THEN
        RAISE EXCEPTION 'Użytkownik o podanym emailu lub nazwie już istnieje.';
END;
$$;

-- ========================================
-- PROCEDURE 2: submit_review
-- ========================================
CREATE OR REPLACE PROCEDURE submit_review(
    p_user_id INT,
    p_restaurant_id INT,
    p_dish_id INT,
    p_dish_rating INT,
    p_comment TEXT,
    p_service_rating INT DEFAULT NULL,
    p_cleanliness_rating INT DEFAULT NULL,
    p_ambiance_rating INT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Validation logic handled by table constraints (1-10 rating)
    INSERT INTO reviews (
        user_id, restaurant_id, dish_id, 
        dish_rating, comment, 
        service_rating, cleanliness_rating, ambiance_rating,
        visit_date, created_at,
        comment_status -- Initial status set by trigger (pending)
    )
    VALUES (
        p_user_id, p_restaurant_id, p_dish_id, 
        p_dish_rating, p_comment, 
        p_service_rating, p_cleanliness_rating, p_ambiance_rating,
        CURRENT_DATE, NOW(),
        'pending'
    );
END;
$$;

-- ========================================
-- PROCEDURE 7: confirm_email_change (Secure)
-- ========================================
CREATE OR REPLACE PROCEDURE confirm_email_change(
    p_user_id INT,
    p_code_hash VARCHAR(255),
    p_ip_address VARCHAR(45) DEFAULT '0.0.0.0'
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_new_email VARCHAR(100);
    v_code_id INT;
    v_stored_hash VARCHAR(255);
    v_attempts INT;
    v_max_attempts INT;
    v_log_id INT;
BEGIN
    -- 0. Get Configured Limit
    SELECT COALESCE(value, '5')::INT INTO v_max_attempts 
    FROM system.config WHERE key = 'verification_code_max_attempts';

    -- 1. Find ACTIVE code
    SELECT verification_code_id, payload, code_hash, attempts_count
    INTO v_code_id, v_new_email, v_stored_hash, v_attempts
    FROM verification_codes
    WHERE user_id = p_user_id
      AND type = 'email_change'
      AND expires_at > NOW();

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Nieprawidłowy lub wygasły kod weryfikacyjny (lub kod został usunięty).';
    END IF;

    -- 2. Check Attempts Limit
    IF v_attempts >= v_max_attempts THEN
        DELETE FROM verification_codes WHERE verification_code_id = v_code_id;
        
        INSERT INTO system.security_logs (user_id, event_type, ip_address, details)
        VALUES (p_user_id, 'brute_force_attempt', p_ip_address, 'Email Change Code Invalidated')
        RETURNING security_log_id INTO v_log_id;
        
        PERFORM upsert_notification(
            p_user_id := p_user_id,
            p_type := 'security_alert',
            p_title := 'Próba przejęcia konta',
            p_message := 'Zablokowaliśmy kod zmiany email po 5 nieudanych próbach.',
            p_metadata := json_build_object(
                'target_type', 'security_log',
                'event', 'brute_force_email_change',
                'security_log_id', v_log_id
            ),
            p_severity := 'danger'
        );
        
        RAISE EXCEPTION 'Zbyt wiele nieudanych prób. Kod został unieważniony ze względów bezpieczeństwa.';
    END IF;

    -- 3. Verify Hash
    IF v_stored_hash != p_code_hash THEN
        UPDATE verification_codes 
        SET attempts_count = attempts_count + 1 
        WHERE verification_code_id = v_code_id;
        RAISE EXCEPTION 'Nieprawidłowy kod weryfikacyjny. Pozostało prób: %', (v_max_attempts - v_attempts - 1);
    END IF;

    -- 4. Valid Code: Proceed
    IF v_new_email IS NULL THEN
        RAISE EXCEPTION 'Błąd danych: Kod nie zawiera adresu docelowego.';
    END IF;

    IF EXISTS(SELECT 1 FROM users WHERE email = v_new_email AND user_id != p_user_id) THEN
        RAISE EXCEPTION 'Adres email % został w międzyczasie zajęty.', v_new_email;
    END IF;

    UPDATE users
    SET email = v_new_email,
        email_verified = TRUE,
        updated_at = NOW()
    WHERE user_id = p_user_id;

    DELETE FROM verification_codes WHERE verification_code_id = v_code_id;

    RAISE NOTICE 'Email for User % changed to %', p_user_id, v_new_email;
EXCEPTION
    WHEN OTHERS THEN
        IF SQLERRM LIKE 'Nieprawidłowy%' OR SQLERRM LIKE 'Zbyt wiele%' THEN RAISE;
        ELSE RAISE EXCEPTION 'Błąd zmiany emaila: %', SQLERRM; END IF;
END;
$$;

-- ========================================
-- PROCEDURE 10: complete_password_reset (Secure)
-- ========================================
CREATE OR REPLACE PROCEDURE complete_password_reset(
    p_user_id INT,
    p_new_hash VARCHAR(255),
    p_new_stamp VARCHAR(50),
    p_code_hash VARCHAR(255) DEFAULT NULL,
    p_ip_address VARCHAR(45) DEFAULT '0.0.0.0'
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_code_id INT;
    v_stored_hash VARCHAR(255);
    v_attempts INT;
    v_max_attempts INT;
    v_log_id INT;
BEGIN
    IF p_code_hash IS NOT NULL THEN
        SELECT COALESCE(value, '5')::INT INTO v_max_attempts 
        FROM system.config WHERE key = 'verification_code_max_attempts';

        SELECT verification_code_id, code_hash, attempts_count
        INTO v_code_id, v_stored_hash, v_attempts
        FROM verification_codes
        WHERE user_id = p_user_id
          AND type = 'reset_password'
          AND expires_at > NOW();

        IF NOT FOUND THEN
            RAISE EXCEPTION 'Nieprawidłowy lub wygasły kod resetujący.';
        END IF;

        IF v_attempts >= v_max_attempts THEN
            DELETE FROM verification_codes WHERE verification_code_id = v_code_id;
            
            INSERT INTO system.security_logs (user_id, event_type, ip_address, details)
            VALUES (p_user_id, 'brute_force_attempt', p_ip_address, 'Password Reset Code Invalidated')
            RETURNING security_log_id INTO v_log_id;
            
            PERFORM upsert_notification(
                p_user_id := p_user_id,
                p_type := 'security_alert',
                p_title := 'Próba resetu hasła',
                p_message := 'Zablokowaliśmy kod resetu hasła po 5 nieudanych próbach.',
                            p_metadata := json_build_object(
                                'target_type', 'security_log',
                                'event', 'brute_force_password_reset',
                                'security_log_id', v_log_id
                            ),
                            p_severity := 'danger'
                        );            
            RAISE EXCEPTION 'Zbyt wiele nieudanych prób. Kod unieważniony.';
        END IF;

        IF v_stored_hash != p_code_hash THEN
            UPDATE verification_codes SET attempts_count = attempts_count + 1 WHERE verification_code_id = v_code_id;
            RAISE EXCEPTION 'Nieprawidłowy kod. Pozostało prób: %', (v_max_attempts - v_attempts - 1);
        END IF;
    END IF;

    UPDATE users
    SET password_hash = p_new_hash,
        security_stamp = p_new_stamp,
        updated_at = NOW()
    WHERE user_id = p_user_id;

    DELETE FROM user_sessions WHERE user_id = p_user_id;

    IF v_code_id IS NOT NULL THEN
        DELETE FROM verification_codes WHERE verification_code_id = v_code_id;
    END IF;

    RAISE NOTICE 'Password reset complete for User %. All sessions revoked.', p_user_id;
EXCEPTION
    WHEN OTHERS THEN
        RAISE EXCEPTION 'Błąd resetowania hasła: %', SQLERRM;
END;
$$;

-- ========================================
-- PROCEDURE 11: system.acquire_ticket_lock
-- ========================================
CREATE OR REPLACE PROCEDURE system.acquire_ticket_lock(
    p_ticket_id INT,
    p_admin_id INT,
    OUT p_success BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE system.tickets
    SET 
        assigned_admin_id = p_admin_id,
        locked_at = NOW(),
        status = 'in_progress',
        version = version + 1
    WHERE ticket_id = p_ticket_id
      AND (
          locked_at IS NULL                      -- Not locked
          OR locked_at < NOW() - INTERVAL '15 minutes' -- Lock expired
          OR assigned_admin_id = p_admin_id      -- Already locked by me
      );

    IF FOUND THEN
        p_success := TRUE;
    ELSE
        p_success := FALSE;
    END IF;
END;
$$;

-- ========================================
-- PROCEDURE 12: system.release_ticket_lock
-- ========================================
CREATE OR REPLACE PROCEDURE system.release_ticket_lock(
    p_ticket_id INT,
    p_admin_id INT
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE system.tickets
    SET 
        locked_at = NULL,
        assigned_admin_id = NULL,
        status = 'open',
        version = version + 1
    WHERE ticket_id = p_ticket_id
      AND assigned_admin_id = p_admin_id;
END;
$$;

-- ========================================
-- PROCEDURE 13: restore_user_account (Undo Soft Delete)
-- ========================================
CREATE OR REPLACE PROCEDURE restore_user_account(
    p_user_id INT
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- 1. Check eligibility
    IF NOT EXISTS(SELECT 1 FROM users WHERE user_id = p_user_id AND is_deleted = TRUE) THEN
        RAISE EXCEPTION 'Użytkownik nie jest usunięty lub nie istnieje.';
    END IF;

    -- 2. Restore User Record
    UPDATE users 
    SET is_deleted = FALSE, 
        deleted_at = NULL,
        is_active = TRUE -- Re-enable login
    WHERE user_id = p_user_id;

    -- 3. Restore Content (Unhide reviews)
    UPDATE reviews 
    SET is_deleted = FALSE 
    WHERE user_id = p_user_id;

    -- 4. Social Data
    -- Automatically valid again (we didn't delete it).

    RAISE NOTICE 'User % restored successfully.', p_user_id;
END;
$$;