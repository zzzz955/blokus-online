# Writing an updated schema_info.md based on the provided schema.sql

content = """# 📦 블로쿠스 온라인 통합 데이터베이스 스키마 (업데이트)
**기준 스키마:** `schema.sql` (PostgreSQL 17.4 덤프)  
**문서 갱신일:** 2025-08-14

> 본 문서는 최신 `schema.sql`을 정리/설명한 것입니다. (필요한 곳에 원시 타입·제약·인덱스·트리거를 명시)  
> 일부 테이블의 `updated_at`은 **INSERT 시 기본값이 없고**, **UPDATE 트리거로만 갱신**되니 주의하세요.

---

## 1) ENUM 타입

| 이름 | 값 |
|---|---|
| `public."AdminRole"` | `ADMIN`, `SUPER_ADMIN` |
| `public."SupportTicketStatus"` | `PENDING`, `ANSWERED`, `CLOSED` |
| `public.post_category` | `QUESTION`, `GUIDE`, `GENERAL` |

---

## 2) 함수(Functions)

- `ensure_user_stage_progress(p_user_id integer, p_stage_id integer) RETURNS void`  
  `user_stage_progress (user_id, stage_id)`에 존재하지 않으면 INSERT. (ON CONFLICT DO NOTHING)
- `update_updated_at_column() RETURNS trigger`  
  `NEW.updated_at = CURRENT_TIMESTAMP`로 갱신.
- `update_user_settings_updated_at() RETURNS trigger`  
  `NEW.updated_at = NOW()`로 갱신.

---

## 3) 테이블 정의

### 3.1 `users`
- 컬럼
  - `user_id SERIAL` (PK)
  - `username VARCHAR(20)` **NOT NULL, UNIQUE**, 체크: 길이 4~20, 정규식 `^[a-zA-Z0-9_]+$`
  - `password_hash VARCHAR(255)` **NOT NULL**
  - `email VARCHAR(100)` UNIQUE, NULL 허용
  - `oauth_provider VARCHAR(20)` / `oauth_id VARCHAR(100)`
  - `display_name VARCHAR(30)` / `avatar_url TEXT`
  - `is_active BOOLEAN` DEFAULT `true` **NOT NULL**
  - `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**
  - `last_login_at TIMESTAMP(3)` NULL
  - `updated_at TIMESTAMP(3)` **NOT NULL** (기본값 없음 → 앱/트리거로 설정 필요)
- 제약/인덱스/트리거
  - PK: `(user_id)` / UNIQUE: `(username)`, `(email)`
  - Partial Index: `idx_users_email` (WHERE `email IS NOT NULL`)
  - Partial Index: `idx_users_oauth` on (`oauth_provider`, `oauth_id`) WHERE `oauth_provider IS NOT NULL`
  - Trigger: `update_users_updated_at` (BEFORE UPDATE → `update_updated_at_column()`)

---

### 3.2 `user_stats`
- 컬럼(모두 기본값/체크 다수)
  - `user_id INTEGER` (PK, FK → `users(user_id)` ON UPDATE CASCADE ON DELETE CASCADE)
  - `total_games`, `wins`, `losses`, `draws`, `best_score`, `total_score(BIGINT)`, `longest_win_streak`, `current_win_streak`
  - `level`(1~100), `experience_points`, `last_played TIMESTAMP(3)`
  - `updated_at TIMESTAMP(3)` **NOT NULL**
- 체크
  - `(wins + losses + draws) = total_games`
  - `current_win_streak <= longest_win_streak`
  - 각 수치의 음수 금지 및 범위 체크 다수
- 트리거
  - `update_user_stats_updated_at` (BEFORE UPDATE)

---

### 3.3 `user_settings`
- 컬럼
  - `user_id INTEGER` (PK, FK → `users(user_id)` **ON DELETE CASCADE**)
  - `theme VARCHAR(20)` DEFAULT `dark`
  - `language VARCHAR(20)` DEFAULT `korean`
  - 알림/사운드 관련 BOOLEAN 다수
  - `bgm_volume`, `effect_volume` 각각 0~100, DEFAULT 50 (CHECK 포함)
  - `created_at TIMESTAMPTZ` DEFAULT `now()` **NOT NULL**
  - `updated_at TIMESTAMPTZ` DEFAULT `now()` **NOT NULL**
- 트리거
  - `trigger_user_settings_updated_at` (BEFORE UPDATE → `update_user_settings_updated_at()`)

---

### 3.4 `user_friends`
- 컬럼
  - `friendship_id SERIAL` (PK)
  - `requester_user_id INTEGER` **NOT NULL**
  - `addressee_user_id INTEGER` **NOT NULL**
  - `status VARCHAR(20)` DEFAULT `pending` (CHECK: `pending|accepted|blocked|declined`)
  - `requested_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**
  - `responded_at TIMESTAMP(3)` NULL
- 제약
  - `chk_no_self_friend`: 본인 친구 금지 (`requester_user_id <> addressee_user_id`)
  - UNIQUE `(requester_user_id, addressee_user_id)`
- 비고
  - **FK가 정의되어 있지 않습니다.**(애플리케이션 레벨에서 무결성 보장 필요)

---

### 3.5 `announcements`
- 컬럼: `id SERIAL`(PK), `title TEXT`, `content TEXT`, `author TEXT`,  
  `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**,  
  `updated_at TIMESTAMP(3)` **NOT NULL**(기본값 없음),  
  `is_pinned BOOLEAN` DEFAULT `false`, `is_published BOOLEAN` DEFAULT `true`
- 트리거: `update_announcements_updated_at` (BEFORE UPDATE)

---

### 3.6 `posts`
- 컬럼: `id SERIAL`(PK), `title VARCHAR(200)`, `content TEXT`,  
  `category post_category` **NOT NULL**,  
  `author_id INTEGER` **NOT NULL** (FK → `users(user_id)` ON UPDATE CASCADE ON DELETE CASCADE),  
  `is_hidden BOOLEAN` DEFAULT `false`, `is_deleted BOOLEAN` DEFAULT `false`, `view_count INTEGER` DEFAULT 0,  
  `created_at TIMESTAMP(3)` DEFAULT `now()` **NOT NULL**, `updated_at TIMESTAMP(3)` **NOT NULL**
- 트리거: `trigger_update_post_updated_at` (BEFORE UPDATE)

---

### 3.7 `comments`
- 컬럼: `id SERIAL`(PK), `content TEXT` **NOT NULL**, `author_id INTEGER` **NOT NULL**,  
  `post_id INTEGER` / `announcement_id INTEGER` / `patch_note_id INTEGER` (세 중 **정확히 하나만** 선택),  
  `is_deleted BOOLEAN` DEFAULT `false`,  
  `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**, `updated_at TIMESTAMP(3)` **NOT NULL**
- 체크: `chk_comment_single_target` (post/announcement/patch_note 중 하나만 NOT NULL)
- FK: 각각 `posts(id)`, `announcements(id)`, `patch_notes(id)`(모두 ON UPDATE CASCADE ON DELETE CASCADE), `author_id → users(user_id)`
- 인덱스(Partial): `idx_comments_post_id`, `idx_comments_announcement_id`, `idx_comments_patch_note_id`
- 트리거: `update_comments_updated_at` (BEFORE UPDATE)

---

### 3.8 `replies`
- 컬럼: `id SERIAL`(PK), `content TEXT` **NOT NULL**, `author_id INTEGER` **NOT NULL**, `comment_id INTEGER` **NOT NULL**,  
  `is_deleted BOOLEAN` DEFAULT `false`,  
  `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**, `updated_at TIMESTAMP(3)` **NOT NULL**
- FK: `author_id → users(user_id)`, `comment_id → comments(id)` (모두 ON UPDATE CASCADE ON DELETE CASCADE)
- 트리거: `update_replies_updated_at` (BEFORE UPDATE)

---

### 3.9 `patch_notes`
- 컬럼: `id SERIAL`(PK), `version TEXT` **UNIQUE NOT NULL**, `title TEXT` **NOT NULL**, `content TEXT` **NOT NULL**,  
  `release_date TIMESTAMP(3)` **NOT NULL**, `download_url TEXT` NULL,  
  `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**

---

### 3.10 `support_tickets`
- 컬럼: `id SERIAL`(PK), `user_id INTEGER`(FK → `users(user_id)` **ON DELETE SET NULL**),  
  `email TEXT` **NOT NULL**, `subject TEXT` **NOT NULL**, `message TEXT` **NOT NULL**,  
  `status SupportTicketStatus` DEFAULT `PENDING` **NOT NULL**, `admin_reply TEXT` NULL,  
  `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**, `replied_at TIMESTAMP(3)` NULL

---

### 3.11 `testimonials`
- 컬럼: `id SERIAL`(PK), `user_id INTEGER`(FK → `users(user_id)` **ON DELETE SET NULL**),  
  `name TEXT` NULL, `rating INTEGER` **NOT NULL (1~5)**, `comment TEXT` NULL,  
  `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**,  
  `is_pinned BOOLEAN` DEFAULT `false` **NOT NULL**, `is_published BOOLEAN` DEFAULT `true` **NOT NULL**

---

### 3.12 `stages`
- 컬럼: `stage_id SERIAL`(PK), `stage_number INTEGER` **UNIQUE NOT NULL**,  
  `difficulty INTEGER` (CHECK 1~10), `initial_board_state JSONB`, `available_blocks INTEGER[]` **NOT NULL**,  
  `optimal_score INTEGER` **NOT NULL**, `time_limit INTEGER` NULL, `max_undo_count INTEGER` DEFAULT 3,  
  `stage_description TEXT`, `stage_hints TEXT`,  
  `is_active BOOLEAN` DEFAULT `true`, `is_featured BOOLEAN` DEFAULT `false`,  
  `created_at TIMESTAMPTZ` DEFAULT `now()`, `updated_at TIMESTAMPTZ` DEFAULT `now()`,  
  `thumbnail_url TEXT`
- 인덱스
  - `idx_stages_number`(stage_number)
  - `idx_stages_difficulty`(difficulty)
  - Partial: `idx_stages_featured` (WHERE `is_featured = true`)

---

### 3.13 `user_stage_progress`
- PK: `(user_id, stage_id)`
- 컬럼: `is_completed BOOLEAN` DEFAULT `false`, `stars_earned INTEGER` DEFAULT 0 (CHECK 0~3),  
  `best_score INTEGER` DEFAULT 0, `best_completion_time INTEGER` NULL,  
  `total_attempts INTEGER` DEFAULT 0, `successful_attempts INTEGER` DEFAULT 0,  
  `first_played_at TIMESTAMPTZ` DEFAULT `now()`, `first_completed_at TIMESTAMPTZ` NULL,  
  `last_played_at TIMESTAMPTZ` DEFAULT `now()`, `updated_at TIMESTAMPTZ` DEFAULT `now()`
- FK: `user_id → users(user_id)` **ON DELETE CASCADE**, `stage_id → stages(stage_id)` **ON DELETE CASCADE**
- 인덱스: `idx_user_stage_progress_user_id`, `idx_user_stage_progress_stage_id`,  
  Partial: `idx_user_stage_progress_completed`(WHERE `is_completed = true`),  
  Partial: `idx_user_stage_progress_stars`(WHERE `stars_earned > 0`)

---

### 3.14 `admin_users`
- 컬럼: `id SERIAL`(PK), `username TEXT` **UNIQUE NOT NULL**, `password_hash TEXT` **NOT NULL**,  
  `role AdminRole` DEFAULT `ADMIN` **NOT NULL**, `created_at TIMESTAMP(3)` DEFAULT `CURRENT_TIMESTAMP` **NOT NULL**

---

## 4) 트리거 총괄
- BEFORE UPDATE 트리거(모두 `updated_at` 갱신):  
  `announcements`, `comments`, `posts`, `replies`, `user_stats`, `users` → `update_updated_at_column()`  
  `user_settings` → `update_user_settings_updated_at()`

> 주의: 위 테이블들의 `updated_at` 중 다수는 **INSERT 기본값이 없습니다.** 최초 INSERT 시 애플리케이션에서 값을 넣거나, 별도 BEFORE INSERT 트리거를 추가하세요.

---

## 5) 권한/스키마
- `REVOKE USAGE ON SCHEMA public FROM PUBLIC;` (기본 public 권한 제한)

---

## 6) 운영 팁
- `updated_at` 초기값: `users`, `posts`, `announcements`, `comments`, `replies`, `user_stats`는 **초기값이 없으므로** INSERT 시 명시 권장.
- `user_friends` FK 없음: 사용자 삭제 시 고아 레코드 가능 → 주기적 정리 또는 FK 추가 고려.
- `comments` 타깃 체크: 한 댓글은 **게시글/공지/패치노트 중 하나만** 가리킴.
- OAuth 로그인: `(oauth_provider, oauth_id)` 부분 인덱스로 조회 최적화.

---

## 7) 이전 문서 대비 주요 변경점 요약
- `users`, `posts`, `announcements` 등 다수 테이블의 `updated_at` **기본값 제거**를 반영. (UPDATE 트리거만 존재)  
- `testimonials.name`은 **NULL 허용**으로 수정.  
- `user_friends`에 **FK가 없음**을 명시(기존 문서엔 FK 표기).  
- 새로 누락 없이 정리: `comments`, `replies`, `stages`, `user_stage_progress`, 인덱스/트리거 섹션 추가.

"""

path = "/mnt/data/schema_info.md"
with open(path, "w", encoding="utf-8") as f:
    f.write(content)

path
