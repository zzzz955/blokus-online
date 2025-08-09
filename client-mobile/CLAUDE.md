# Blokus Unity Mobile Client - Claude Code Integration

## Project Overview
Unity 2D 기반 모바일 블로커스 게임 클라이언트 - Android/iOS 플랫폼 지원

## Build Commands
- **Android APK/AAB**: Unity Menu → File → Build Settings → Build
- **Test**: Unity Test Runner 사용
- **Lint**: Unity 내장 코드 분석기 사용

## Unity Project Structure

### Core Folders
- **Assets/Scripts/**: C# 게임 로직
  - **Common/**: C++ Common 라이브러리 C# 포팅
  - **Game/**: 게임플레이 로직
  - **Network/**: 서버 통신
  - **UI/**: UI 시스템
  - **Audio/**: 사운드 관리
  - **Data/**: ScriptableObjects, 스테이지 데이터
- **Assets/Scenes/**: 게임 씬들
- **Assets/Prefabs/**: UI 프리팹, 게임 오브젝트
- **Assets/Resources/**: 런타임 로드 리소스

### Platform Settings
- **Target Platform**: Android (Primary), iOS (Future)
- **Scripting Backend**: IL2CPP
- **API Compatibility**: .NET Standard 2.1
- **Min Android Version**: API Level 21 (Android 5.0)

## Development Guidelines

### Common Library C# Porting Status
- [x] BlokusTypes.cs (Types.h 포팅) - 완료
- [x] GameLogic.cs (GameLogic.h 포팅) - 완료  
- [x] Block.cs (Block.h 포팅) - 완료
- [x] Network Layer (TCP + Custom Protocol) - 완료

### Key Features to Implement
1. **Single-Player Mode**: 1000+ 스테이지 퍼즐 시스템
2. **Multiplayer Mode**: 기존 서버와 TCP 소켓 통신
3. **UI System**: 모바일 최적화된 터치 UI
4. **Stage Management**: ScriptableObject 기반 스테이지 데이터
5. **Performance**: 모바일 기기 최적화

### Coding Standards
- **Namespace**: `BlokusUnity`
- **File Naming**: PascalCase (GameLogic.cs)
- **Method Naming**: PascalCase (PlaceBlock())
- **Field Naming**: camelCase (_gameBoard)
- **Constants**: UPPER_CASE (BOARD_SIZE)

### Unity-Specific Patterns
- **ScriptableObjects**: 스테이지 데이터, 설정
- **Singleton Pattern**: 매니저 클래스들
- **Observer Pattern**: 게임 이벤트 시스템
- **Object Pooling**: 성능 최적화

## Claude Code Integration

### Development Workflow
1. Claude가 Unity 프로젝트 구조 이해
2. C# 스크립트 생성/수정
3. Unity Inspector 설정 가이드
4. 빌드 및 테스트 자동화

### File Operations
- Unity .meta 파일은 자동 생성되므로 직접 편집 안함
- 스크립트는 Assets/Scripts/ 하위에 적절한 폴더 구조로 생성
- ScriptableObjects는 Assets/Data/ 폴더에 저장

### Testing Strategy
- Unity Test Framework 사용
- Edit Mode Tests: 로직 테스트
- Play Mode Tests: 게임플레이 테스트
- Device Testing: Android 실기기 테스트

## Common Issues & Solutions

### Unity-Claude Integration
- 스크립트 생성시 Unity가 자동으로 .meta 파일 생성
- 네임스페이스 일관성 유지
- Unity 생명주기 메서드 (Start, Update, etc.) 활용

### Performance Considerations
- Mobile-first 개발
- Texture streaming 및 압축
- Draw call 최적화
- Memory management

## Scene Architecture (현재 구조)

### 🏗️ 하이브리드 씬 아키텍처
- **MainScene.unity**: 메인 메뉴/로비 씬 (UI 패널 기반)
- **SingleGameplayScene.unity**: 싱글플레이 게임 씬 (완전 전환)
- **MultiGameplayScene.unity**: 멀티플레이 게임 씬 (나중에 구현)

### 📱 UI 플로우 시스템
```
Login → ModeSelection
     ├── SingleMode → StageSelect → LoadScene(SingleGameplay)  
     └── MultiMode → Lobby → GameRoom
                                ├── 대기중 (GameRoom UI)
                                ├── 게임중 (GameRoom 내부 전환)
                                ├── 게임종료 (결과 화면)
                                └── 다시 대기중 (순환)
```

### 🔄 GameRoom 상태 관리
- **대기 상태**: 플레이어 슬롯, 준비 버튼, 설정
- **게임 상태**: 3D 게임 보드, 블록 팔레트, 게임 UI 활성화
- **결과 상태**: 점수, 순위, 다시하기/나가기 버튼
- **상태 전환**: UI 패널 전환으로 구현 (씬 전환 없음)

### 🎮 싱글플레이 시스템
- **StageDataManager**: 스테이지 데이터 관리 및 씬 간 전달 (DontDestroyOnLoad)
- **SingleGameManager**: 게임 세션 관리 (점수, 시간, 완료 조건)
- **Scene 전환**: UIManager → StageDataManager → 데이터 전달 → Scene Load

### 🌐 네트워크 시스템
- **NetworkClient**: TCP 소켓 + 커스텀 문자열 프로토콜 (C++ 서버 호환)
  - 싱글플레이어 API: `SendStageDataRequest`, `SendStageCompleteRequest`, `SendBatchStageProgressRequest`
  - 멀티플레이어 API: `SendLoginRequest`, `SendCreateRoomRequest`, `SendPlaceBlockRequest`
- **MessageHandler**: `:` 구분자 메시지 파싱 및 이벤트 처리
  - 싱글플레이어 이벤트: `OnStageDataReceived`, `OnStageProgressReceived`, `OnMaxStageUpdated`
  - 멀티플레이어 이벤트: `OnAuthResponse`, `OnRoomListUpdated`, `OnGameStateUpdated`
- **NetworkManager**: 통합 네트워크 관리 파사드

### 📊 완성된 구조 요약
```
📁 MainScene (UI 중심)
├── UIManager (패널 전환)
├── StageDataManager (데이터 관리)
├── NetworkManager (서버 통신)
├── UI Panels (Login, ModeSelect, StageSelect, Lobby)
└── GameRoomPanel
    ├── 대기실 UI (플레이어 슬롯, 준비 버튼)
    ├── 게임 UI (3D 보드, 블록 팔레트) ⭐ 상태 전환
    └── 결과 UI (점수, 순위, 재시작)

📁 SingleGameplayScene (싱글플레이 전용)
├── SingleGameManager (게임 세션)
├── GameBoard (3D 보드)
├── BlockPalette (블록 선택)
└── Game UI (Score, Time, Controls)

📁 Tests (검증)
├── CommonLibraryTests (C++ vs C# 비교)
├── NetworkLayerTests (통신 계층)
└── CrossPlatformComparisonTests (크로스 플랫폼)
```

### 💾 데이터베이스 스키마 확장 (최적화됨)
- **기존 user_stats 확장**: single_player_level, max_stage_completed 등
- **stages 테이블**: 스테이지 마스터 데이터 (JSONB로 보드 상태, optimal_score)
- **user_stage_progress**: 플레이어별 진행도 (별점, 최고점수, 클리어시간)
- **클라이언트 계산**: 별점 시스템(90%/70%/50%), 언락 조건(순차적)

### 🔌 싱글플레이어 네트워크 프로토콜
**클라이언트 → 서버**:
- `STAGE_DATA_REQUEST:stageNumber` - 스테이지 데이터 요청
- `STAGE_PROGRESS_REQUEST:stageNumber` - 스테이지 진행도 요청
- `STAGE_PROGRESS_UPDATE:stageNumber:completed:stars:score:time` - 스테이지 완료 보고
- `UPDATE_MAX_STAGE:maxStageCompleted` - 최대 클리어 스테이지 업데이트
- `BATCH_STAGE_PROGRESS_REQUEST:startStage:endStage` - 일괄 진행도 요청

**서버 → 클라이언트**:
- `STAGE_DATA_RESPONSE:stageNumber:stageName:difficulty:optimalScore:timeLimit:maxUndoCount:availableBlocks:initialBoardState:stageDescription`
- `STAGE_PROGRESS_RESPONSE:stageNumber:isCompleted:starsEarned:bestScore:bestTime:totalAttempts:successfulAttempts`
- `STAGE_COMPLETE_RESPONSE:SUCCESS/FAILURE:메시지`
- `MAX_STAGE_UPDATED:maxStageCompleted`

### 🎯 단순화된 설계 결정
- **별점 계산**: DB 칼럼 제거 → 클라이언트에서 optimal_score 기반 실시간 계산
- **언락 시스템**: 복잡한 조건 제거 → 단순한 순차 언락 (1→2→3→...)
- **데이터 최소화**: 중복 정보 제거로 DB 부하 감소 및 유지보수성 향상

### 🔄 네트워크 아키텍처 최적화 결정

**기존 문제점**: 싱글플레이어에서 TCP 세션 유지는 리소스 낭비
- 실시간 상호작용 불필요한데 지속적 연결 유지
- 하트비트, 재연결 등 복잡한 로직 필요
- 모바일 배터리 소모 및 네트워크 비용

**새로운 하이브리드 아키텍처**:
- **멀티플레이어**: TCP Socket (실시간 게임 세션 필요)
- **싱글플레이어**: HTTP REST API (이벤트 기반 통신)
- **공통**: PostgreSQL 데이터베이스, JWT 기반 인증

### ✅ HTTP API 클라이언트 구현
- **✓ HttpApiClient.cs**: UnityWebRequest 기반 RESTful 클라이언트
- **✓ 오프라인 큐잉**: 네트워크 장애시 요청 저장 후 복구시 재시도
- **✓ JWT 토큰 인증**: TCP 로그인 후 토큰 받아서 HTTP API 사용
- **✓ 이벤트 기반 통신**: 스테이지 시작/완료시에만 서버 통신
- **✓ 자동 재연결**: 30초마다 연결 상태 확인 및 복구
- **✓ 요청 타임아웃**: 10초 타임아웃으로 응답성 보장

## References
- Unity 2022.3 LTS Documentation
- C# Common Library (../common/)
- Server Protocol Documentation (../server/)
- Mobile Optimization Best Practices
- Unity Test Framework Documentation