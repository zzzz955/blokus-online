# 📦 블로쿠스 온라인 통합 데이터베이스 스키마
**OAuth 회원가입 + ID/PW 로그인 지원 버전**  
**작성일:** 2025-08-09

---

## 1. ENUM 타입 정의

| ENUM 타입명 | 값 목록 |
|-------------|---------|
| `SupportTicketStatus` | `PENDING`, `ANSWERED`, `CLOSED` |
| `AdminRole` | `ADMIN`, `SUPER_ADMIN` |
| `post_category` | `QUESTION`, `GUIDE`, `GENERAL` |

---

## 2. users (사용자 계정)

| 컬럼명 | 타입 | 제약조건 / 설명 |
|--------|------|----------------|
| user_id | SERIAL | PK |
| username | VARCHAR(20) | NOT NULL, UNIQUE, 4~20자, 영문/숫자/언더스코어 |
| password_hash | VARCHAR(255) | NOT NULL |
| email | VARCHAR(100) | UNIQUE, NULL 허용 |
| oauth_provider | VARCHAR(20) | OAuth 공급자명 (`google`, `kakao`, `github`, `naver`) |
| oauth_id | VARCHAR(100) | 공급자별 ID |
| display_name | VARCHAR(30) | 표시명 |
| avatar_url | TEXT | 프로필 이미지 URL |
| is_active | BOOLEAN | 기본값 TRUE |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| last_login_at | TIMESTAMPTZ | NULL 허용 |
| updated_at | TIMESTAMPTZ | DEFAULT 현재시간 |

---

## 3. user_stats (게임 통계)

| 컬럼명 | 타입 | 제약조건 / 설명 |
|--------|------|----------------|
| user_id | INTEGER | PK, FK → users(user_id), ON DELETE CASCADE |
| total_games | INTEGER | DEFAULT 0, ≥ 0 |
| wins | INTEGER | DEFAULT 0, ≥ 0 |
| losses | INTEGER | DEFAULT 0, ≥ 0 |
| draws | INTEGER | DEFAULT 0, ≥ 0 |
| best_score | INTEGER | DEFAULT 0, ≥ 0 |
| total_score | BIGINT | DEFAULT 0, ≥ 0 |
| longest_win_streak | INTEGER | DEFAULT 0 |
| current_win_streak | INTEGER | DEFAULT 0 |
| level | INTEGER | DEFAULT 1, 1~100 |
| experience_points | INTEGER | DEFAULT 0 |
| last_played | TIMESTAMPTZ | NULL 허용 |
| updated_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| 제약조건 | 승/패/무 합계 = total_games, current_win_streak ≤ longest_win_streak |

---

## 4. user_friends (친구 관계)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| friendship_id | SERIAL | PK |
| requester_user_id | INTEGER | FK → users(user_id) |
| addressee_user_id | INTEGER | FK → users(user_id) |
| status | VARCHAR(20) | `pending`, `accepted`, `blocked`, `declined` |
| requested_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| responded_at | TIMESTAMPTZ | NULL 허용 |
| 제약조건 | 자기 자신 친구 불가, 중복 관계 불가 |

---

## 5. announcements (공지사항)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| id | SERIAL | PK |
| title | TEXT | NOT NULL |
| content | TEXT | NOT NULL |
| author | TEXT | NOT NULL |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| updated_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| is_pinned | BOOLEAN | DEFAULT false |
| is_published | BOOLEAN | DEFAULT true |

---

## 6. patch_notes (패치노트)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| id | SERIAL | PK |
| version | TEXT | NOT NULL, UNIQUE |
| title | TEXT | NOT NULL |
| content | TEXT | NOT NULL |
| release_date | TIMESTAMPTZ | NOT NULL |
| download_url | TEXT | NULL 허용 |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |

---

## 7. support_tickets (문의사항)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| id | SERIAL | PK |
| user_id | INTEGER | FK → users(user_id), ON DELETE SET NULL |
| email | TEXT | NOT NULL |
| subject | TEXT | NOT NULL |
| message | TEXT | NOT NULL |
| status | `SupportTicketStatus` | DEFAULT `PENDING` |
| admin_reply | TEXT | NULL 허용 |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| replied_at | TIMESTAMPTZ | NULL 허용 |

---

## 8. admin_users (관리자 계정)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| id | SERIAL | PK |
| username | TEXT | NOT NULL, UNIQUE |
| password_hash | TEXT | NOT NULL |
| role | `AdminRole` | DEFAULT `ADMIN` |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |

---

## 9. testimonials (게임 후기)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| id | SERIAL | PK |
| user_id | INTEGER | FK → users(user_id), ON DELETE SET NULL |
| name | TEXT | NOT NULL |
| rating | INTEGER | 1~5 |
| comment | TEXT | NULL 허용 |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| is_pinned | BOOLEAN | DEFAULT false |
| is_published | BOOLEAN | DEFAULT true |

---

## 10. posts (게시판)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| id | SERIAL | PK |
| title | VARCHAR(200) | NOT NULL |
| content | TEXT | NOT NULL |
| category | `post_category` | NOT NULL |
| author_id | INTEGER | FK → users(user_id), ON DELETE CASCADE |
| is_hidden | BOOLEAN | DEFAULT false |
| is_deleted | BOOLEAN | DEFAULT false |
| view_count | INTEGER | DEFAULT 0 |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| updated_at | TIMESTAMPTZ | DEFAULT 현재시간 |

---

## 11. user_settings (유저 환경설정)

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| user_id | INTEGER | PK, FK → users(user_id), ON DELETE CASCADE |
| theme | VARCHAR(20) | DEFAULT `dark` |
| language | VARCHAR(20) | DEFAULT `korean` |
| game_invite_notifications | BOOLEAN | DEFAULT true |
| friend_online_notifications | BOOLEAN | DEFAULT true |
| system_notifications | BOOLEAN | DEFAULT true |
| bgm_mute | BOOLEAN | DEFAULT false |
| bgm_volume | INTEGER | 0~100, DEFAULT 50 |
| effect_mute | BOOLEAN | DEFAULT false |
| effect_volume | INTEGER | 0~100, DEFAULT 50 |
| created_at | TIMESTAMPTZ | DEFAULT 현재시간 |
| updated_at | TIMESTAMPTZ | DEFAULT 현재시간 |

---

## 12. 함수 및 트리거

### 12.1. updated_at 자동 갱신 함수
```sql
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
   NEW.updated_at = NOW();
   RETURN NEW;
END;
$$ LANGUAGE plpgsql;
```

### 12.2. user_settings.updated_at 자동 업데이트 트리거
```sql
CREATE OR REPLACE FUNCTION update_user_settings_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_user_settings_updated_at
    BEFORE UPDATE ON user_settings
    FOR EACH ROW
    EXECUTE FUNCTION update_user_settings_updated_at();
```