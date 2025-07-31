# 데이터베이스 설정 가이드

## 개요
Blokus Online 프로젝트는 단일 PostgreSQL 컨테이너에 두 개의 데이터베이스를 사용합니다:

- **게임 서버 DB**: `${DB_NAME}` (환경변수로 설정, 예: `blokus_game`)
- **웹 애플리케이션 DB**: `blokus_web` (고정)

## 데이터베이스 생성 방법

### 1. Docker Compose 실행
```bash
docker-compose up -d postgres
```

### 2. 웹 애플리케이션 데이터베이스 생성

#### 방법 A: PostgreSQL 컨테이너 접속
```bash
# PostgreSQL 컨테이너에 접속
docker-compose exec postgres psql -U $DB_USER

# 웹 데이터베이스 생성
CREATE DATABASE blokus_web;
GRANT ALL PRIVILEGES ON DATABASE blokus_web TO your_db_user;

# 웹 데이터베이스로 전환 후 스키마 생성
\c blokus_web
\i /docker-entrypoint-initdb.d/02-create-web-tables.sql
```

#### 방법 B: 호스트에서 직접 실행
```bash
# 웹 데이터베이스 생성
docker-compose exec postgres createdb -U $DB_USER blokus_web

# 스키마 생성
docker-compose exec -T postgres psql -U $DB_USER -d blokus_web < sql/02-create-web-tables.sql
```

### 3. 데이터베이스 확인
```bash
# 데이터베이스 목록 확인
docker-compose exec postgres psql -U $DB_USER -l

# 웹 데이터베이스 테이블 확인
docker-compose exec postgres psql -U $DB_USER -d blokus_web -c "\dt"
```

## 스키마 구조

### 📋 웹 애플리케이션 테이블 (`blokus_web`)

#### `announcements` - 공지사항
- `id`: 기본키 (SERIAL)
- `title`: 제목 (TEXT)
- `content`: 내용, Markdown 형식 (TEXT)
- `author`: 작성자 (TEXT)
- `createdAt`: 생성일시 (TIMESTAMP)
- `updatedAt`: 수정일시 (TIMESTAMP)
- `isPinned`: 고정 여부 (BOOLEAN)
- `isPublished`: 게시 여부 (BOOLEAN)

#### `patch_notes` - 패치노트
- `id`: 기본키 (SERIAL)
- `version`: 버전, 유니크 (TEXT)
- `title`: 제목 (TEXT)
- `content`: 내용, Markdown 형식 (TEXT)
- `releaseDate`: 배포일 (TIMESTAMP)
- `downloadUrl`: 다운로드 링크 (TEXT, nullable)
- `createdAt`: 생성일시 (TIMESTAMP)

#### `support_tickets` - 고객지원 티켓
- `id`: 기본키 (SERIAL)
- `email`: 이메일 (TEXT)
- `subject`: 제목 (TEXT)
- `message`: 내용 (TEXT)
- `status`: 상태 (SupportTicketStatus enum)
- `adminReply`: 관리자 답변 (TEXT, nullable)
- `createdAt`: 생성일시 (TIMESTAMP)
- `repliedAt`: 답변일시 (TIMESTAMP, nullable)

#### `admin_users` - 관리자 계정
- `id`: 기본키 (SERIAL)
- `username`: 사용자명, 유니크 (TEXT)
- `passwordHash`: 비밀번호 해시 (TEXT)
- `role`: 권한 (AdminRole enum)
- `createdAt`: 생성일시 (TIMESTAMP)

### 🎯 Enum 타입

#### `SupportTicketStatus`
- `PENDING`: 대기중
- `ANSWERED`: 답변완료
- `CLOSED`: 종료

#### `AdminRole`
- `ADMIN`: 일반 관리자
- `SUPER_ADMIN`: 슈퍼 관리자

### 📊 성능 최적화 인덱스

주요 쿼리 패턴에 대한 인덱스가 자동으로 생성됩니다:
- 생성일시 내림차순 정렬
- 상태별 필터링
- 고정/게시 여부 필터링

## 샘플 데이터

`02-create-web-tables.sql` 실행 시 다음 샘플 데이터가 자동으로 생성됩니다:

### 기본 관리자 계정
- **사용자명**: `admin`
- **비밀번호**: `admin123`
- **권한**: `SUPER_ADMIN`

### 샘플 공지사항
- 서비스 오픈 공지 (고정)
- 서버 점검 안내

### 샘플 패치노트
- v1.0.0: 초기 버전
- v1.0.1: 버그 수정판

### 샘플 지원 티켓
- 게임 실행 문제
- 랭킹 시스템 문의

## 연결 설정

### 게임 서버 (.env)
```env
DB_HOST=postgres
DB_PORT=5432
DB_USER=your_db_user
DB_PASSWORD=your_db_password
DB_NAME=blokus_game  # 게임 서버용 DB명
```

### 웹 애플리케이션 (.env)
```env
DATABASE_URL=postgresql://your_db_user:your_db_password@postgres:5432/blokus_web
```

## 백업 및 복원

### 백업
```bash
# 전체 데이터베이스 백업
docker-compose exec postgres pg_dumpall -U $DB_USER > backup_all.sql

# 개별 데이터베이스 백업
docker-compose exec postgres pg_dump -U $DB_USER blokus_game > backup_game.sql
docker-compose exec postgres pg_dump -U $DB_USER blokus_web > backup_web.sql
```

### 복원
```bash
# 전체 복원
docker-compose exec -T postgres psql -U $DB_USER < backup_all.sql

# 개별 복원
docker-compose exec -T postgres psql -U $DB_USER -d blokus_game < backup_game.sql
docker-compose exec -T postgres psql -U $DB_USER -d blokus_web < backup_web.sql
```

## 문제해결

### 권한 오류
```bash
# 데이터베이스 권한 확인
docker-compose exec postgres psql -U $DB_USER -c "\l"

# 권한 부여
docker-compose exec postgres psql -U postgres -c "GRANT ALL PRIVILEGES ON DATABASE blokus_web TO $DB_USER;"
```

### 연결 테스트
```bash
# 게임 서버 DB 연결 테스트
docker-compose exec postgres psql -U $DB_USER -d $DB_NAME -c "SELECT 1;"

# 웹 애플리케이션 DB 연결 테스트  
docker-compose exec postgres psql -U $DB_USER -d blokus_web -c "SELECT 1;"
```