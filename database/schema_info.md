📑 스키마 구조 문서 (정리본)

Table: users
| Column                    | Type        | Constraints                                                   |
| ------------------------- | ----------- | ------------------------------------------------------------- |
| user\_id                  | integer     | PK, AUTO INCREMENT, NOT NULL                                  |
| username                  | varchar(20) | UNIQUE, NOT NULL, CHECK length 4–20, regex ^\[a-zA-Z0-9\_]+\$ |
| password\_hash            | text        | NOT NULL                                                      |
| email                     | text        | UNIQUE, optional                                              |
| oauth\_provider           | text        | optional                                                      |
| oauth\_id                 | text        | optional                                                      |
| last\_login\_at           | timestamp   | optional                                                      |
| updated\_at               | timestamp   | NOT NULL                                                      |
| progress\_version         | integer     | DEFAULT 1, NOT NULL                                           |
| progress\_updated\_at     | timestamp   | DEFAULT now(), NOT NULL                                       |
| last\_sync\_at            | timestamp   | DEFAULT now(), NOT NULL                                       |
| last\_metadata\_check\_at | timestamp   | DEFAULT now(), NOT NULL                                       |

Indexes: idx_users_email, idx_users_oauth
Relations:

Referenced by user_stats.user_id, user_settings.user_id, user_stage_progress.user_id, support_tickets.user_id, testimonials.user_id, posts.author_id, comments.author_id, replies.author_id

Table: user_stats
| Column                | Type      | Constraints              |
| --------------------- | --------- | ------------------------ |
| user\_id              | integer   | PK, FK → users(user\_id) |
| total\_games          | integer   | DEFAULT 0                |
| wins                  | integer   | DEFAULT 0                |
| losses                | integer   | DEFAULT 0                |
| draws                 | integer   | DEFAULT 0                |
| best\_score           | integer   | DEFAULT 0                |
| total\_score          | integer   | DEFAULT 0                |
| longest\_win\_streak  | integer   | DEFAULT 0                |
| current\_win\_streak  | integer   | DEFAULT 0                |
| level                 | integer   | DEFAULT 1                |
| experience\_points    | integer   | DEFAULT 0                |
| last\_played          | timestamp | optional                 |
| updated\_at           | timestamp | NOT NULL                 |
| single\_player\_level | integer   | DEFAULT 1                |
| max\_stage\_completed | integer   | DEFAULT 0, CHECK ≥ 0     |
| total\_single\_games  | integer   | DEFAULT 0                |
| single\_player\_score | bigint    | DEFAULT 0                |

Relations: user_id → users.user_id (ON DELETE CASCADE)

Table: user_friends
| Column              | Type        | Constraints                                                        |
| ------------------- | ----------- | ------------------------------------------------------------------ |
| friendship\_id      | integer     | PK, AUTO INCREMENT                                                 |
| requester\_user\_id | integer     | NOT NULL, FK → users(user\_id)                                     |
| addressee\_user\_id | integer     | NOT NULL, FK → users(user\_id)                                     |
| status              | varchar(20) | DEFAULT 'pending', CHECK in (pending, accepted, blocked, declined) |
| requested\_at       | timestamp   | DEFAULT now(), NOT NULL                                            |
| responded\_at       | timestamp   | optional                                                           |

Unique: (requester_user_id, addressee_user_id)
Check: requester_user_id ≠ addressee_user_id

Table: user_settings
| Column                        | Type        | Constraints                                |
| ----------------------------- | ----------- | ------------------------------------------ |
| user\_id                      | integer     | PK, FK → users(user\_id) ON DELETE CASCADE |
| theme                         | varchar(20) | DEFAULT 'dark', NOT NULL                   |
| language                      | varchar(20) | DEFAULT 'korean', NOT NULL                 |
| game\_invite\_notifications   | boolean     | DEFAULT true                               |
| friend\_online\_notifications | boolean     | DEFAULT true                               |
| system\_notifications         | boolean     | DEFAULT true                               |
| bgm\_mute                     | boolean     | DEFAULT false                              |
| bgm\_volume                   | integer     | DEFAULT 50, CHECK 0–100                    |
| effect\_mute                  | boolean     | DEFAULT false                              |
| effect\_volume                | integer     | DEFAULT 50, CHECK 0–100                    |
| created\_at                   | timestamp   | DEFAULT now(), NOT NULL                    |
| updated\_at                   | timestamp   | DEFAULT now(), NOT NULL                    |

Table: user_stage_progress
| Column                 | Type      | Constraints                |
| ---------------------- | --------- | -------------------------- |
| user\_id               | integer   | PK, FK → users(user\_id)   |
| stage\_id              | integer   | PK, FK → stages(stage\_id) |
| is\_completed          | boolean   | DEFAULT false              |
| stars\_earned          | integer   | DEFAULT 0, CHECK 0–3       |
| best\_score            | integer   | DEFAULT 0                  |
| best\_completion\_time | integer   | optional                   |
| total\_attempts        | integer   | DEFAULT 0                  |
| successful\_attempts   | integer   | DEFAULT 0                  |
| first\_played\_at      | timestamp | DEFAULT now()              |
| first\_completed\_at   | timestamp | optional                   |
| last\_played\_at       | timestamp | DEFAULT now()              |
| updated\_at            | timestamp | DEFAULT now()              |

Indexes: idx_user_stage_progress_user_id, idx_user_stage_progress_stage_id, idx_user_stage_progress_completed, idx_user_stage_progress_stars

Table: stages
| Column                | Type      | Constraints           |
| --------------------- | --------- | --------------------- |
| stage\_id             | integer   | PK, AUTO INCREMENT    |
| stage\_number         | integer   | UNIQUE                |
| difficulty            | integer   | CHECK 1–10            |
| available\_blocks     | int\[]    | NOT NULL              |
| optimal\_score        | integer   | NOT NULL              |
| time\_limit           | integer   | optional              |
| max\_undo\_count      | integer   | DEFAULT 3             |
| stage\_description    | text      | optional              |
| stage\_hints          | text      | optional              |
| is\_active            | boolean   | DEFAULT true          |
| is\_featured          | boolean   | DEFAULT false         |
| created\_at           | timestamp | DEFAULT now()         |
| updated\_at           | timestamp | DEFAULT now()         |
| thumbnail\_url        | text      | optional              |
| initial\_board\_state | int\[]    | optional              |

Indexes: idx_stages_number, idx_stages_active, idx_stages_difficulty, idx_stages_featured, idx_stages_initial_board_state

Table: posts
| Column      | Type                 | Constraints          |
| ----------- | -------------------- | -------------------- |
| id          | integer              | PK, AUTO INCREMENT   |
| title       | varchar(200)         | NOT NULL             |
| content     | text                 | NOT NULL             |
| category    | enum(post\_category) | NOT NULL             |
| author\_id  | integer              | FK → users(user\_id) |
| is\_hidden  | boolean              | DEFAULT false        |
| is\_deleted | boolean              | DEFAULT false        |
| view\_count | integer              | DEFAULT 0            |
| created\_at | timestamp            | DEFAULT now()        |
| updated\_at | timestamp            | NOT NULL             |

Table: comments
| Column           | Type      | Constraints            |
| ---------------- | --------- | ---------------------- |
| id               | integer   | PK, AUTO INCREMENT     |
| content          | text      | NOT NULL               |
| author\_id       | integer   | FK → users(user\_id)   |
| post\_id         | integer   | FK → posts(id)         |
| announcement\_id | integer   | FK → announcements(id) |
| patch\_note\_id  | integer   | FK → patch\_notes(id)  |
| is\_deleted      | boolean   | DEFAULT false          |
| created\_at      | timestamp | DEFAULT now()          |
| updated\_at      | timestamp | NOT NULL               |

Check: comment must reference exactly one of (post_id, announcement_id, patch_note_id)

Table: replies
| Column      | Type      | Constraints          |
| ----------- | --------- | -------------------- |
| id          | integer   | PK, AUTO INCREMENT   |
| content     | text      | NOT NULL             |
| author\_id  | integer   | FK → users(user\_id) |
| comment\_id | integer   | FK → comments(id)    |
| is\_deleted | boolean   | DEFAULT false        |
| created\_at | timestamp | DEFAULT now()        |
| updated\_at | timestamp | NOT NULL             |

Table: announcements
| Column        | Type      | Constraints        |
| ------------- | --------- | ------------------ |
| id            | integer   | PK, AUTO INCREMENT |
| title         | text      | NOT NULL           |
| content       | text      | NOT NULL           |
| author        | text      | NOT NULL           |
| created\_at   | timestamp | DEFAULT now()      |
| updated\_at   | timestamp | NOT NULL           |
| is\_pinned    | boolean   | DEFAULT false      |
| is\_published | boolean   | DEFAULT true       |

Table: patch_notes
| Column        | Type      | Constraints        |
| ------------- | --------- | ------------------ |
| id            | integer   | PK, AUTO INCREMENT |
| version       | text      | UNIQUE, NOT NULL   |
| title         | text      | NOT NULL           |
| content       | text      | NOT NULL           |
| release\_date | timestamp | NOT NULL           |
| download\_url | text      | optional           |
| created\_at   | timestamp | DEFAULT now()      |

Table: support_tickets
| Column       | Type                      | Constraints          |
| ------------ | ------------------------- | -------------------- |
| id           | integer                   | PK, AUTO INCREMENT   |
| user\_id     | integer                   | FK → users(user\_id) |
| email        | text                      | NOT NULL             |
| subject      | text                      | NOT NULL             |
| message      | text                      | NOT NULL             |
| status       | enum(SupportTicketStatus) | DEFAULT 'PENDING'    |
| admin\_reply | text                      | optional             |
| created\_at  | timestamp                 | DEFAULT now()        |
| replied\_at  | timestamp                 | optional             |

Table: testimonials
| Column        | Type      | Constraints          |
| ------------- | --------- | -------------------- |
| id            | integer   | PK, AUTO INCREMENT   |
| user\_id      | integer   | FK → users(user\_id) |
| name          | text      | optional             |
| rating        | integer   | CHECK 1–5, NOT NULL  |
| comment       | text      | optional             |
| created\_at   | timestamp | DEFAULT now()        |
| is\_pinned    | boolean   | DEFAULT false        |
| is\_published | boolean   | DEFAULT true         |

Table: admin_users
| Column         | Type            | Constraints               |
| -------------- | --------------- | ------------------------- |
| id             | integer         | PK, AUTO INCREMENT        |
| username       | text            | UNIQUE, NOT NULL          |
| password\_hash | text            | NOT NULL                  |
| role           | enum(AdminRole) | DEFAULT 'ADMIN', NOT NULL |
| created\_at    | timestamp       | DEFAULT now()             |

## 🔐 OIDC/OAuth 2.1 Authentication Tables

Table: authorization_codes
| Column                  | Type         | Constraints                                 |
| ----------------------- | ------------ | ------------------------------------------- |
| code\_id                | integer      | PK, AUTO INCREMENT                          |
| code                    | varchar(255) | UNIQUE, NOT NULL                            |
| client\_id              | varchar(255) | NOT NULL                                    |
| user\_id                | integer      | FK → users(user\_id), ON DELETE CASCADE     |
| redirect\_uri           | text         | NOT NULL                                    |
| scope                   | text         | NOT NULL                                    |
| code\_challenge         | varchar(255) | optional (PKCE)                             |
| code\_challenge\_method | varchar(10)  | CHECK in ('S256'), optional                 |
| expires\_at             | timestamp    | NOT NULL                                    |
| created\_at             | timestamp    | DEFAULT now()                               |

Indexes: idx_authorization_codes_code, idx_authorization_codes_expires, idx_authorization_codes_user

Table: refresh_token_families
| Column               | Type         | Constraints                               |
| -------------------- | ------------ | ----------------------------------------- |
| family\_id           | integer      | PK, AUTO INCREMENT                        |
| user\_id             | integer      | FK → users(user\_id), ON DELETE CASCADE   |
| client\_id           | varchar(255) | NOT NULL                                  |
| device\_fingerprint  | varchar(255) | optional                                  |
| status               | varchar(20)  | DEFAULT 'active', CHECK in ('active', 'revoked') |
| created\_at          | timestamp    | DEFAULT now()                             |
| last\_used\_at       | timestamp    | DEFAULT now()                             |
| max\_expires\_at     | timestamp    | NOT NULL (90-day absolute limit)          |

Indexes: idx_refresh_token_families_user, idx_refresh_token_families_client, idx_refresh_token_families_status, idx_refresh_token_families_max_expires

Table: refresh_tokens
| Column        | Type         | Constraints                                           |
| ------------- | ------------ | ----------------------------------------------------- |
| token\_id     | integer      | PK, AUTO INCREMENT                                    |
| family\_id    | integer      | FK → refresh\_token\_families(family\_id), ON DELETE CASCADE |
| jti           | varchar(255) | UNIQUE, NOT NULL (JWT ID)                             |
| prev\_jti     | varchar(255) | optional (previous token for rotation chain)          |
| status        | varchar(20)  | DEFAULT 'active', CHECK in ('active', 'used', 'revoked', 'expired') |
| expires\_at   | timestamp    | NOT NULL                                              |
| created\_at   | timestamp    | DEFAULT now()                                         |
| last\_used\_at| timestamp    | DEFAULT now()                                         |

Indexes: idx_refresh_tokens_jti, idx_refresh_tokens_family, idx_refresh_tokens_status, idx_refresh_tokens_expires, idx_refresh_tokens_prev_jti

##  OIDC Database Functions

**update_updated_at_column()**: Trigger function to auto-update last_used_at timestamps
**cleanup_expired_tokens()**: Utility function to clean expired authorization codes and refresh tokens

 관계 요약 (외래키)

**Core User Relations:**
- user_stats.user_id → users.user_id
- user_settings.user_id → users.user_id
- user_stage_progress.user_id → users.user_id
- user_stage_progress.stage_id → stages.stage_id
- support_tickets.user_id → users.user_id
- testimonials.user_id → users.user_id

**Community Relations:**
- posts.author_id → users.user_id
- comments.author_id → users.user_id
- comments.post_id → posts.id
- comments.announcement_id → announcements.id
- comments.patch_note_id → patch_notes.id
- replies.author_id → users.user_id
- replies.comment_id → comments.id

**OIDC/OAuth Relations:**
- authorization_codes.user_id → users.user_id (ON DELETE CASCADE)
- refresh_token_families.user_id → users.user_id (ON DELETE CASCADE)
- refresh_tokens.family_id → refresh_token_families.family_id (ON DELETE CASCADE)