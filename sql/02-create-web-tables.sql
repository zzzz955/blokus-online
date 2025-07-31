-- ==================================================
-- Blokus Online Web Database Schema
-- Generated from prisma/schema.prisma
-- ==================================================

-- 데이터베이스 생성 (필요한 경우)
-- CREATE DATABASE blokus_web;

-- blokus_web 데이터베이스 사용
\c blokus_web;

-- ==================================================
-- Enum Types
-- ==================================================

-- SupportTicketStatus enum
CREATE TYPE "SupportTicketStatus" AS ENUM (
    'PENDING',
    'ANSWERED',
    'CLOSED'
);

-- AdminRole enum  
CREATE TYPE "AdminRole" AS ENUM (
    'ADMIN',
    'SUPER_ADMIN'
);

-- ==================================================
-- Tables
-- ==================================================

-- Announcements table
CREATE TABLE "announcements" (
    "id" SERIAL NOT NULL,
    "title" TEXT NOT NULL,
    "content" TEXT NOT NULL,
    "author" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,
    "isPinned" BOOLEAN NOT NULL DEFAULT false,
    "isPublished" BOOLEAN NOT NULL DEFAULT true,

    CONSTRAINT "announcements_pkey" PRIMARY KEY ("id")
);

-- Patch Notes table
CREATE TABLE "patch_notes" (
    "id" SERIAL NOT NULL,
    "version" TEXT NOT NULL,
    "title" TEXT NOT NULL,
    "content" TEXT NOT NULL,
    "releaseDate" TIMESTAMP(3) NOT NULL,
    "downloadUrl" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "patch_notes_pkey" PRIMARY KEY ("id")
);

-- Support Tickets table
CREATE TABLE "support_tickets" (
    "id" SERIAL NOT NULL,
    "email" TEXT NOT NULL,
    "subject" TEXT NOT NULL,
    "message" TEXT NOT NULL,
    "status" "SupportTicketStatus" NOT NULL DEFAULT 'PENDING',
    "adminReply" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "repliedAt" TIMESTAMP(3),

    CONSTRAINT "support_tickets_pkey" PRIMARY KEY ("id")
);

-- Admin Users table
CREATE TABLE "admin_users" (
    "id" SERIAL NOT NULL,
    "username" TEXT NOT NULL,
    "passwordHash" TEXT NOT NULL,
    "role" "AdminRole" NOT NULL DEFAULT 'ADMIN',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "admin_users_pkey" PRIMARY KEY ("id")
);

-- ==================================================
-- Unique Constraints
-- ==================================================

-- Patch Notes: version must be unique
CREATE UNIQUE INDEX "patch_notes_version_key" ON "patch_notes"("version");

-- Admin Users: username must be unique
CREATE UNIQUE INDEX "admin_users_username_key" ON "admin_users"("username");

-- ==================================================
-- Indexes for Performance
-- ==================================================

-- Announcements indexes
CREATE INDEX "announcements_createdAt_idx" ON "announcements"("createdAt" DESC);
CREATE INDEX "announcements_isPinned_idx" ON "announcements"("isPinned");
CREATE INDEX "announcements_isPublished_idx" ON "announcements"("isPublished");

-- Patch Notes indexes
CREATE INDEX "patch_notes_releaseDate_idx" ON "patch_notes"("releaseDate" DESC);
CREATE INDEX "patch_notes_createdAt_idx" ON "patch_notes"("createdAt" DESC);

-- Support Tickets indexes
CREATE INDEX "support_tickets_status_idx" ON "support_tickets"("status");
CREATE INDEX "support_tickets_createdAt_idx" ON "support_tickets"("createdAt" DESC);
CREATE INDEX "support_tickets_email_idx" ON "support_tickets"("email");

-- Admin Users indexes
CREATE INDEX "admin_users_role_idx" ON "admin_users"("role");

-- ==================================================
-- Sample Data (Optional)
-- ==================================================

-- 기본 관리자 계정 생성 (비밀번호: admin123)
-- bcrypt hash for 'admin123': $2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj7.kTXh9nYm
INSERT INTO "admin_users" ("username", "passwordHash", "role") VALUES 
('admin', '$2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj7.kTXh9nYm', 'SUPER_ADMIN');

-- 샘플 공지사항
INSERT INTO "announcements" ("title", "content", "author", "isPinned", "isPublished") VALUES 
(
    '블로쿠스 온라인 서비스 오픈!', 
    '# 블로쿠스 온라인에 오신 것을 환영합니다!

## 주요 기능
- 실시간 멀티플레이어 게임
- 랭킹 시스템
- 채팅 기능

게임을 즐겨보세요!', 
    'admin', 
    true, 
    true
),
(
    '서버 점검 안내',
    '정기 서버 점검이 예정되어 있습니다.

**점검 시간**: 매주 화요일 오전 2:00 - 4:00
**점검 내용**: 
- 서버 안정성 개선
- 버그 수정
- 성능 최적화

점검 중에는 게임 접속이 불가능합니다.',
    'admin',
    false,
    true
);

-- 샘플 패치노트
INSERT INTO "patch_notes" ("version", "title", "content", "releaseDate", "downloadUrl") VALUES 
(
    'v1.0.0',
    '블로쿠스 온라인 초기 버전',
    '# v1.0.0 Release Notes

## 새로운 기능
- 기본 블로쿠스 게임 플레이
- 멀티플레이어 지원 (최대 4명)
- 기본 UI/UX

## 알려진 이슈
- 일부 브라우저에서 렌더링 이슈
- 네트워크 지연 시 동기화 문제

다음 업데이트에서 개선될 예정입니다.',
    '2024-01-15 00:00:00',
    'https://blokus-online.mooo.com/downloads/BlokusClient-v1.0.0.zip'
),
(
    'v1.0.1',
    '버그 수정 및 안정성 개선',
    '# v1.0.1 Patch Notes

## 버그 수정
- 게임 종료 시 크래시 문제 해결
- 블록 배치 시 간헐적 오류 수정
- 채팅 메시지 깨짐 현상 해결

## 개선사항
- 게임 로딩 속도 향상
- UI 반응성 개선
- 서버 안정성 향상',
    '2024-01-20 00:00:00',
    'https://blokus-online.mooo.com/downloads/BlokusClient-v1.0.1.zip'
);

-- 샘플 지원 티켓
INSERT INTO "support_tickets" ("email", "subject", "message", "status") VALUES 
(
    'test@example.com',
    '게임 실행 문제',
    '게임을 실행하려고 하는데 "DLL을 찾을 수 없습니다" 오류가 발생합니다. Windows 10을 사용하고 있습니다.',
    'PENDING'
),
(
    'user@example.com',
    '랭킹 시스템 문의',
    '랭킹이 제대로 업데이트되지 않는 것 같습니다. 게임에서 이겼는데 점수가 반영되지 않았어요.',
    'ANSWERED'
);

-- ==================================================
-- Grants and Permissions
-- ==================================================

-- 웹 애플리케이션이 사용할 사용자에게 권한 부여
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO your_web_user;
-- GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO your_web_user;

-- ==================================================
-- Schema Creation Complete
-- ==================================================

-- 테이블 생성 확인
SELECT 
    schemaname,
    tablename,
    tableowner
FROM pg_tables 
WHERE schemaname = 'public'
ORDER BY tablename;

-- Enum 타입 확인
SELECT 
    t.typname AS enum_name,
    e.enumlabel AS enum_value
FROM pg_type t 
JOIN pg_enum e ON t.oid = e.enumtypid  
WHERE t.typname IN ('SupportTicketStatus', 'AdminRole')
ORDER BY t.typname, e.enumsortorder;

COMMIT;