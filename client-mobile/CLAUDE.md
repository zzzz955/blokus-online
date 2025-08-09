# Blokus Unity Mobile Client - Claude Code Integration

## Project Overview
Unity 2D 기반 모바일 블로쿠스 게임 클라이언트 - Android/iOS 플랫폼 지원

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
- [ ] BlokusTypes.cs (Types.h 포팅)
- [ ] GameLogic.cs (GameLogic.h 포팅)  
- [ ] Block.cs (Block.h 포팅)
- [ ] Utils.cs (Utils.h 포팅)

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

## References
- Unity 2022.3 LTS Documentation
- C# Common Library (../common/)
- Server Protocol Documentation (../server/)
- Mobile Optimization Best Practices