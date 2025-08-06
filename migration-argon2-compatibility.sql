-- ========================================
-- Argon2 Password Migration Script
-- ========================================
-- 게임 서버와 웹 애플리케이션 간 Argon2 호환성 확보를 위한 스크립트
-- 
-- 목적: 
-- 1. 기존 SHA256/bcrypt 해시를 가진 사용자 식별
-- 2. 해당 사용자들의 비밀번호 재설정 플래그 설정
-- 3. Argon2 호환성 검증
--
-- 작성일: 2025-01-29
-- 참고: 게임 서버와 웹앱 모두 동일한 Argon2id 파라미터 사용
--       - 2 iterations, 64MB memory, 1 thread
-- ========================================

-- 1. 기존 사용자 분석 (Argon2 해시 형식이 아닌 것들 확인)
-- Argon2 해시는 "$argon2id$v=19$m=65536,t=2,p=1$" 형태로 시작함
SELECT 
    user_id,
    username,
    LENGTH(password_hash) as hash_length,
    LEFT(password_hash, 20) as hash_prefix,
    CASE 
        WHEN password_hash LIKE '$argon2id$v=19$m=65536,t=2,p=1$%' THEN 'Argon2 (Compatible)'
        WHEN password_hash LIKE '$argon2%' THEN 'Argon2 (Other params)'
        WHEN LENGTH(password_hash) = 64 THEN 'Possibly SHA256'
        WHEN password_hash LIKE '$2b$%' OR password_hash LIKE '$2a$%' THEN 'bcrypt'
        ELSE 'Unknown format'
    END as hash_type,
    created_at,
    last_login_at
FROM users 
ORDER BY created_at DESC;

-- 2. 호환되지 않는 해시를 가진 사용자 수 확인
SELECT 
    CASE 
        WHEN password_hash LIKE '$argon2id$v=19$m=65536,t=2,p=1$%' THEN 'Argon2 (Compatible)'
        WHEN password_hash LIKE '$argon2%' THEN 'Argon2 (Other params)'
        WHEN LENGTH(password_hash) = 64 THEN 'Possibly SHA256'
        WHEN password_hash LIKE '$2b$%' OR password_hash LIKE '$2a$%' THEN 'bcrypt'
        ELSE 'Unknown format'
    END as hash_type,
    COUNT(*) as user_count
FROM users 
GROUP BY 
    CASE 
        WHEN password_hash LIKE '$argon2id$v=19$m=65536,t=2,p=1$%' THEN 'Argon2 (Compatible)'
        WHEN password_hash LIKE '$argon2%' THEN 'Argon2 (Other params)'
        WHEN LENGTH(password_hash) = 64 THEN 'Possibly SHA256'
        WHEN password_hash LIKE '$2b$%' OR password_hash LIKE '$2a$%' THEN 'bcrypt'
        ELSE 'Unknown format'
    END
ORDER BY user_count DESC;

-- 3. 비호환 해시를 가진 사용자를 위한 임시 테이블 생성
CREATE TABLE IF NOT EXISTS password_migration_log (
    user_id INT PRIMARY KEY,
    username VARCHAR(20) NOT NULL,
    old_hash_type VARCHAR(50) NOT NULL,
    migration_requested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    migration_completed_at TIMESTAMP NULL,
    notes TEXT,
    
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 4. 비호환 사용자들을 마이그레이션 로그에 추가
INSERT INTO password_migration_log (user_id, username, old_hash_type, notes)
SELECT 
    user_id,
    username,
    CASE 
        WHEN password_hash LIKE '$argon2%' AND NOT password_hash LIKE '$argon2id$v=19$m=65536,t=2,p=1$%' THEN 'Argon2 (Different params)'
        WHEN LENGTH(password_hash) = 64 THEN 'SHA256 (Legacy)'
        WHEN password_hash LIKE '$2b$%' OR password_hash LIKE '$2a$%' THEN 'bcrypt (Legacy)'
        ELSE 'Unknown format'
    END,
    'User needs password reset to ensure compatibility between game server and web app'
FROM users 
WHERE NOT password_hash LIKE '$argon2id$v=19$m=65536,t=2,p=1$%'
ON CONFLICT (user_id) DO NOTHING;

-- 5. 비호환 사용자에게 비밀번호 재설정 필요성 알림을 위한 플래그 추가
-- (이미 스키마에 있는 경우 스킵)
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'users' AND column_name = 'needs_password_reset') THEN
        ALTER TABLE users ADD COLUMN needs_password_reset BOOLEAN DEFAULT FALSE;
    END IF;
END $$;

-- 6. 비호환 사용자들에게 비밀번호 재설정 플래그 설정
UPDATE users 
SET needs_password_reset = TRUE,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id IN (SELECT user_id FROM password_migration_log WHERE migration_completed_at IS NULL);

-- 7. 결과 요약 출력
SELECT 
    'Migration Summary' as status,
    (SELECT COUNT(*) FROM users WHERE password_hash LIKE '$argon2id$v=19$m=65536,t=2,p=1$%') as compatible_users,
    (SELECT COUNT(*) FROM users WHERE needs_password_reset = TRUE) as users_needing_reset,
    (SELECT COUNT(*) FROM password_migration_log) as total_migration_entries;

-- 8. 호환성 테스트를 위한 샘플 데이터 확인
SELECT 
    'Compatibility Test' as test_type,
    user_id,
    username,
    LEFT(password_hash, 50) as hash_sample,
    needs_password_reset,
    created_at
FROM users 
WHERE password_hash LIKE '$argon2id$v=19$m=65536,t=2,p=1$%'
LIMIT 5;

-- ========================================
-- 정리 스크립트 (마이그레이션 완료 후 실행)
-- ========================================

-- 마이그레이션 완료 마킹 (사용자가 새 비밀번호 설정 후)
/*
UPDATE password_migration_log 
SET migration_completed_at = CURRENT_TIMESTAMP,
    notes = CONCAT(notes, ' - Migration completed by user')
WHERE user_id = ? AND migration_completed_at IS NULL;

UPDATE users 
SET needs_password_reset = FALSE,
    updated_at = CURRENT_TIMESTAMP
WHERE user_id = ?;
*/

-- 마이그레이션 로그 테이블 정리 (모든 마이그레이션 완료 후)
/*
DROP TABLE IF EXISTS password_migration_log;
ALTER TABLE users DROP COLUMN IF EXISTS needs_password_reset;
*/

-- ========================================
-- 모니터링 쿼리들
-- ========================================

-- 마이그레이션 진행 상황 체크
/*
SELECT 
    COUNT(*) FILTER (WHERE migration_completed_at IS NULL) as pending_migrations,
    COUNT(*) FILTER (WHERE migration_completed_at IS NOT NULL) as completed_migrations,
    COUNT(*) as total_migrations
FROM password_migration_log;
*/

-- 호환되지 않는 해시 타입별 분포
/*
SELECT 
    old_hash_type,
    COUNT(*) as count,
    COUNT(*) FILTER (WHERE migration_completed_at IS NOT NULL) as completed
FROM password_migration_log
GROUP BY old_hash_type;
*/