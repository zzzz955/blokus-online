# Blokus Online - Project Structure Documentation

## Project Overview
멀티플레이어 블로쿠스 게임 프로젝트 - C++/Qt 클라이언트와 C++ 서버로 구성된 온라인 게임

## Build Commands
- **Windows Debug (CMake)**: `cmake --build build --config Debug` (콘솔 창 표시됨)
- **Windows Release (CMake)**: `cmake --build build --config Release` (콘솔 창 숨겨짐)
- **Test**: 테스트 스크립트는 아직 미정의
- **Lint/TypeCheck**: 아직 미정의

## Environment Configuration
서버 연결 설정은 환경변수를 통해 관리됩니다:
- `BLOKUS_SERVER_HOST`: 서버 호스트 (기본값: localhost)
- `BLOKUS_SERVER_PORT`: 서버 포트 (기본값: 9999)

### 개발 환경 설정
1. `.env.example`을 `.env`로 복사
2. 필요에 따라 서버 정보 수정
3. 배포 환경에서는 시스템 환경변수로 설정

## Project Architecture

### 1. Common Library (`/common/`)
**역할**: 클라이언트와 서버가 공유하는 핵심 게임 로직 및 타입 정의
**주요 파일**:
- `include/Types.h` - 기본 타입 정의 (PlayerColor, Position, GameState 등)
- `include/Block.h` - 블로쿠스 블록 클래스 정의
- `include/GameLogic.h` - 게임 룰과 로직 (블록 배치 검증, 턴 관리)
- `include/Utils.h` - 공통 유틸리티 함수들
- `src/` - 각 헤더파일의 구현체

### 2. Communication Protocol
**역할**: 클라이언트-서버 간 메시지 통신은 커스텀 문자열 기반 TCP 통신 사용
**통신 방식**: ':' 구분자를 기준으로 파싱하는 커스텀 문자열 프로토콜
**메시지 형식**: "MessageType:param1:param2:..." 형태의 구조화된 문자열

### 3. Server (`/server/`)
**역할**: 게임 서버 - 인증, 방 관리, 게임 로직 처리
**주요 파일**:

#### 헤더 파일 (`include/`)
- `GameServer.h` - 메인 서버 클래스, 전체 서버 관리
- `NetworkManager.h` - TCP 네트워크 연결 관리
- `MessageHandler.h` - 클라이언트 메시지 처리 및 라우팅
- `AuthenticationService.h` - 사용자 인증, 세션 관리
- `DatabaseManager.h` - PostgreSQL 데이터베이스 연동
- `RoomManager.h` - 게임 방 생성/관리
- `GameRoom.h` - 개별 게임 방 로직
- `Session.h` - 클라이언트 세션 정보
- `PlayerInfo.h` - 플레이어 정보 관리
- `ServerTypes.h` - 서버 전용 타입 정의
- `ConfigManager.h` - 서버 설정 관리

#### 구현 파일 (`src/`)
- 각 헤더파일에 대응하는 `.cpp` 구현체들
- `main.cpp` - 서버 진입점

### 4. Client (`/client/`)
**역할**: Qt 기반 GUI 클라이언트 애플리케이션
**주요 파일**:

#### 헤더 파일 (`include/`)
- `LoginWindow.h` - 로그인/회원가입 창
- `LobbyWindow.h` - 메인 로비 (방 목록, 사용자 목록, 채팅)
- `GameRoomWindow.h` - 게임 방 대기실
- `GameBoard.h` - 게임 보드 UI 및 렌더링
- `ImprovedBlockPalette.h` - 블록 선택 팔레트
- `UserInfoDialog.h` - 사용자 정보 모달 창
- `NetworkClient.h` - 서버 통신 클라이언트
- `ClientTypes.h` - 클라이언트 전용 타입 (Qt 래핑)
- `QtAdapter.h` - Common 라이브러리 Qt 어댑터
- `ClientBlock.h` - 클라이언트 전용 블록 클래스
- `ClientLogic.h` - 클라이언트 게임 로직

#### 구현 파일 (`src/`)
- 각 헤더파일에 대응하는 `.cpp` 구현체들
- `main.cpp` - 클라이언트 진입점

## Key Technical Details

### Database Schema
- `users` 테이블: 사용자 계정 정보 (user_id, username, password_hash, is_active)
- `user_stats` 테이블: 게임 통계 (total_games, wins, losses, level, experience_points, total_score, best_score)

### Message Flow
1. 클라이언트 이벤트 발생
2. NetworkClient가 커스텀 문자열 메시지로 직렬화 (':'로 구분)
3. TCP 소켓을 통해 서버로 전송
4. MessageHandler가 ':' 기준으로 파싱하여 메시지 타입별로 라우팅
5. 적절한 서비스 클래스에서 처리
6. 응답 메시지를 클라이언트로 전송
7. 클라이언트가 UI 업데이트

### Authentication System
- bcrypt 기반 패스워드 해싱 (salt + SHA256)
- 세션 토큰 기반 인증
- 게스트 로그인 지원
- 세션 만료 시간 관리

### User Info Modal System
- 더블클릭으로 사용자 정보 조회
- 서버에서 세션 기반 사용자 데이터 조회
- 실시간 통계 정보 표시 (레벨, 승률, 점수)
- 비모달 창으로 배경 클릭시 자동 닫기

## Recent Features
- 사용자 정보 모달 구현 (UserInfoDialog)
- 데이터베이스 기반 점수 추적 시스템
- 클라이언트-서버 메시지 분리 (MY_STATS_UPDATE vs USER_STATS_RESPONSE)
- 새로고침 버튼 크래시 수정 (QTimer 안전성 개선)

## Dependencies
- **Qt 5.15+**: GUI 프레임워크
- **nlohmann_json**: JSON 설정 파일 처리 (통신은 커스텀 문자열)
- **PostgreSQL**: 데이터베이스
- **libpqxx**: PostgreSQL C++ 클라이언트
- **spdlog**: 로깅 라이브러리
- **OpenSSL**: 암호화
- **boost-system/boost-asio**: 네트워크 통신
- **vcpkg**: 패키지 관리자

## Detailed File Roles

### Server Core Components
1. **GameServer.cpp** (main.cpp 진입점)
   - 서버 초기화 및 종료 관리
   - 전체 서비스 컴포넌트 조립
   - 메인 이벤트 루프

2. **NetworkManager.cpp**
   - TCP 소켓 서버 운영
   - 클라이언트 연결 수락 및 관리
   - 메시지 송수신 버퍼링

3. **MessageHandler.cpp**
   - 커스텀 문자열 메시지 파싱 및 라우팅 (':' 구분자 기반)
   - 핸들러 함수: `handleGetUserStats`, `handleLogin`, `handleCreateRoom` 등
   - 클라이언트별 세션 상태 관리

4. **AuthenticationService.cpp**
   - 비밀번호 해싱/검증 (`hashPassword`, `verifyPassword`)
   - 세션 토큰 생성 및 관리
   - 게스트 로그인 처리

5. **DatabaseManager.cpp**
   - PostgreSQL 연결 풀 관리
   - 사용자 계정 CRUD: `createUser`, `getUserByUsername`
   - 게임 통계 업데이트: `updatePlayerStats`, `updatePlayerExperience`

### Client UI Components
1. **main.cpp** (클라이언트 진입점)
   - Qt 애플리케이션 초기화
   - 네트워크 클라이언트 생성
   - 메시지 핸들러 연결: `onUserStatsReceived`, `onMyStatsUpdated`

2. **LoginWindow.cpp**
   - 로그인/회원가입 폼
   - 입력 검증 및 서버 요청
   - 로그인 성공시 로비 창 전환

3. **LobbyWindow.cpp**
   - 방 목록 표시 및 관리
   - 사용자 목록 및 더블클릭 이벤트 처리
   - 채팅 기능
   - 사용자 정보 모달 생성: `showUserInfoDialog`

4. **UserInfoDialog.cpp**
   - 사용자 정보 모달 UI
   - 새로고침 버튼 및 안전한 타이머 처리
   - 통계 정보 표시: 승률, 점수, 레벨

5. **GameRoomWindow.cpp**
   - 게임 방 대기실
   - 플레이어 슬롯 관리
   - 준비 상태 토글

6. **GameBoard.cpp**
   - 게임 보드 렌더링
   - 블록 드래그 앤 드롭
   - 게임 상태 시각화

### Key Data Structures
```cpp
// 서버측 사용자 계정
struct UserAccount {
    uint32_t userId;
    std::string username;
    std::string passwordHash;  // salt:hash 형태
    int totalGames, wins, losses;
    int level, experiencePoints;
    int totalScore, bestScore;
    bool isActive;
};

// 클라이언트측 사용자 정보 (Qt 래핑)
struct UserInfo {
    QString username;
    int level, totalGames, wins, losses;
    int averageScore, totalScore, bestScore;
    bool isOnline;
    QString status;
    double getWinRate() const;  // 승률 계산
};
```

### Message Types
- `AUTH_REQUEST/AUTH_RESPONSE` - 로그인/회원가입
- `GET_USER_STATS_REQUEST/USER_STATS_RESPONSE` - 사용자 정보 조회
- `MY_STATS_UPDATE` - 자동 통계 업데이트 (로비 진입시)
- `CREATE_ROOM_REQUEST/ROOM_CREATED` - 방 생성
- `JOIN_ROOM_REQUEST/JOIN_ROOM_RESPONSE` - 방 참가

### Database Queries
```sql
-- 사용자 정보 조회 (비밀번호 포함)
SELECT u.user_id, u.username, u.password_hash,
       COALESCE(s.total_games, 0), COALESCE(s.wins, 0),
       COALESCE(s.total_score, 0), COALESCE(s.best_score, 0)
FROM users u LEFT JOIN user_stats s ON u.user_id = s.user_id
WHERE LOWER(u.username) = LOWER($1) AND u.is_active = true
```

## Common Patterns
1. **Qt 시그널-슬롯**: UI 이벤트 → 네트워크 요청 → 응답 처리 → UI 업데이트
2. **Protobuf 메시지**: C++ 객체 ↔ 바이너리 직렬화
3. **RAII**: 자동 리소스 관리 (DB 연결, 메모리)
4. **오류 처리**: `std::optional` 반환값, 예외 처리

## Development Notes
- 모든 UI 텍스트는 UTF-8 한국어
- 네트워크는 TCP 기반
- 게임 로직은 Common 라이브러리에서 공유
- Qt 시그널-슬롯 패턴 활용
- RAII 및 스마트 포인터 사용
- 크래시 방지: QTimer 안전성, 널 포인터 체크