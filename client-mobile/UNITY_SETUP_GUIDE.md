# Unity 블로쿠스 모바일 - 에디터 설정 가이드

Claude Code가 스크립트를 생성했으니, 이제 Unity 에디터에서 실제 씬과 GameObject를 구성해야 합니다.

## 📁 씬 구조 개요

```
📁 Scenes/
├── MainScene.unity          (메인 메뉴/로비 씬)
└── SingleGameplayScene.unity (싱글플레이 게임 씬)
```

## 🏠 1. MainScene.unity 구성

### 1.1 기본 GameObject 구조

```
MainScene
├── Main Camera
├── EventSystem
├── UI Canvas (Screen Space - Overlay)
│   ├── LoginPanel
│   ├── ModeSelectionPanel
│   ├── StageSelectPanel
│   ├── LobbyPanel (멀티플레이용)
│   ├── GameRoomPanel (멀티플레이용)
│   └── LoadingPanel
├── Managers
│   ├── UIManager
│   ├── StageDataManager
│   └── AudioManager (선택사항)
└── DontDestroyOnLoad (빈 GameObject)
    └── NetworkManager (나중에 추가)
```

### 1.2 UI Canvas 설정

1. **Canvas 컴포넌트**:
   - Render Mode: Screen Space - Overlay
   - UI Scale Mode: Scale With Screen Size
   - Reference Resolution: 1080 x 1920 (세로 모드)
   - Screen Match Mode: Match Width Or Height (0.5)

2. **CanvasScaler 설정**:
   - UI Scale Mode: Scale With Screen Size
   - Reference Resolution: 1080 x 1920
   - Match: 0.5 (Width/Height 균형)

### 1.3 UI 패널들 구성

#### LoginPanel (처음에 활성화)
```
LoginPanel
├── Background Image
├── Logo Image
├── Username InputField
├── Password InputField
├── LoginButton
├── RegisterButton
└── GuestLoginButton
```

#### ModeSelectionPanel (처음에 비활성화)
```
ModeSelectionPanel
├── Background Image
├── Title Text ("모드 선택")
├── SingleModeButton ("싱글플레이")
├── MultiModeButton ("멀티플레이")
└── SettingsButton
```

#### StageSelectPanel (처음에 비활성화)
```
StageSelectPanel
├── Background Image
├── Title Text ("스테이지 선택")
├── Scroll View
│   └── Content
│       ├── Stage1Button
│       ├── Stage2Button
│       └── ... (동적 생성 권장)
├── BackButton
└── StarsText ("총 별: 0/300")
```

#### LoadingPanel (처음에 비활성화)
```
LoadingPanel
├── Background Image (반투명)
├── LoadingSpinner (회전 이미지)
├── LoadingText ("로딩 중...")
└── ProgressBar (선택사항)
```

### 1.4 Manager GameObject들

#### UIManager
- GameObject 이름: "UIManager"
- 컴포넌트: UIManager.cs 스크립트 추가
- Inspector에서 UI 패널들 연결

#### StageDataManager
- GameObject 이름: "StageDataManager"
- 컴포넌트: StageDataManager.cs 스크립트 추가
- StageManager ScriptableObject 생성 후 연결

### 1.5 UI 버튼 이벤트 연결

각 버튼의 OnClick 이벤트에 UIManager 메서드 연결:

- **LoginButton** → UIManager.OnLoginSuccess()
- **SingleModeButton** → UIManager.OnSingleModeSelected()
- **MultiModeButton** → UIManager.OnMultiModeSelected()
- **Stage1Button** → UIManager.OnStageSelected(1)
- **BackButton** → UIManager.OnBackToMenu()

## 🎮 2. SingleGameplayScene.unity 구성

### 2.1 기본 GameObject 구조

```
SingleGameplayScene
├── Main Camera (3D View)
├── EventSystem
├── Directional Light
├── Game Canvas (World Space)
│   ├── ScoreText
│   ├── TimeText
│   ├── PauseButton
│   ├── RestartButton
│   └── UndoButton
├── Game Objects
│   ├── GameBoard (3D 보드)
│   ├── BlockPalette (사용 가능한 블록들)
│   └── CameraController
├── Managers
│   └── SingleGameManager
└── UI (Screen Space)
    └── ResultPanel (게임 결과)
```

### 2.2 카메라 설정

1. **Main Camera**:
   - Position: (10, 15, -10) 
   - Rotation: (45, -30, 0)
   - Projection: Perspective
   - FOV: 60

2. **카메라 컨트롤**:
   - 터치/마우스로 회전 가능하게 설정
   - 줌 인/아웃 지원

### 2.3 GameBoard 구성

```
GameBoard
├── Board (20x20 그리드)
│   ├── Cell_0_0 (Cube + BoxCollider)
│   ├── Cell_0_1
│   └── ... (400개 셀)
└── BoardLines (그리드 라인)
```

**셀 생성 방법**:
1. Cube Primitive 생성
2. Scale: (0.9, 0.1, 0.9) 
3. Material: 투명/반투명
4. BoxCollider 추가 (터치 감지용)
5. 스크립트로 20x20 격자 배치

### 2.4 BlockPalette 구성

```
BlockPalette
├── PaletteBackground
├── ScrollView
│   └── Content (Horizontal Layout Group)
│       ├── Block_Single (프리팹)
│       ├── Block_Domino (프리팹)
│       └── ... (21개 블록)
└── BlockPreview (선택된 블록 미리보기)
```

### 2.5 SingleGameManager 설정

1. **GameObject 이름**: "SingleGameManager"
2. **컴포넌트**: SingleGameManager.cs 스크립트 추가
3. **Inspector 연결**:
   - Game Board Transform
   - Block Palette Transform
   - UI Text 컴포넌트들 (Score, Time)
   - UI 패널들 (Game UI, Result Panel)

### 2.6 터치/마우스 입력 시스템

블록 드래그 앤 드롭을 위한 입력 처리:

1. **BlockPalette**: 블록 선택 시 드래그 시작
2. **GameBoard**: 드롭 위치 감지 및 배치
3. **InputManager**: 터치/마우스 이벤트 통합 처리

## 📦 3. ScriptableObject 에셋 생성

### 3.1 StageManager 생성

1. **Assets 폴더**에서 우클릭
2. **Create > Blokus > Stage Manager** 선택
3. 이름: "StageManager"
4. Inspector에서 스테이지 데이터 추가

### 3.2 StageData 생성

1. **Assets/Data** 폴더 생성
2. 우클릭 > **Create > Blokus > Stage Data**
3. 각 스테이지별로 생성 (Stage_001, Stage_002, ...)
4. Inspector에서 스테이지 정보 입력:
   - Stage Number: 1, 2, 3...
   - Stage Name: "첫 번째 스테이지"
   - Available Blocks: 사용 가능한 블록 타입들
   - Optimal Score: 최고 점수

## 🎨 4. UI 디자인 권장사항

### 4.1 색상 팔레트
- **Primary**: #2196F3 (블루)
- **Secondary**: #FFC107 (앰버)  
- **Background**: #F5F5F5 (라이트 그레이)
- **Text**: #212121 (다크 그레이)

### 4.2 폰트
- **제목**: 36-48pt, Bold
- **버튼**: 24-32pt, Medium
- **본문**: 16-20pt, Regular

### 4.3 버튼 스타일
- **둥근 모서리**: Corner Radius 8-12px
- **그림자**: Drop Shadow 효과
- **애니메이션**: Scale 1.0 → 1.1 (터치시)

## 🔧 5. 빌드 설정

### 5.1 Build Settings
1. **File > Build Settings**
2. **Add Open Scenes**: MainScene, SingleGameplayScene
3. **Platform**: Android
4. **Player Settings**:
   - Company Name: 본인 이름
   - Product Name: "Blokus Mobile"
   - Default Orientation: Portrait
   - Minimum API Level: 21 (Android 5.0)

### 5.2 Android 설정
- **Scripting Backend**: IL2CPP
- **Target Architectures**: ARM64
- **Bundle Version Code**: 1

## 🚀 6. 테스트 방법

### 6.1 에디터 테스트
1. MainScene에서 Play 버튼
2. UI 패널 전환 테스트
3. 스테이지 선택 → SingleGameplayScene 전환 확인

### 6.2 모바일 테스트
1. **Build and Run** → Android 기기 연결
2. 터치 입력 테스트
3. 화면 회전 테스트

## 📝 7. 주의사항

### 7.1 성능 최적화
- **Batching**: UI 요소들 Atlas 사용
- **Culling**: 카메라 시야 밖 오브젝트 비활성화
- **LOD**: 거리에 따른 모델 품질 조정

### 7.2 메모리 관리
- **Object Pooling**: 블록 오브젝트 재사용
- **Texture Compression**: ASTC 4x4 사용
- **Audio Compression**: OGG Vorbis 사용

### 7.3 디버깅
- **Console 창**: Debug.Log 메시지 확인
- **Scene 뷰**: 런타임 중 오브젝트 상태 확인
- **Profiler**: 성능 모니터링

---

이 가이드대로 Unity 에디터에서 씬을 구성하면 Claude Code가 생성한 스크립트들과 완벽히 연동되어 작동하는 블로쿠스 모바일 게임을 만들 수 있습니다!

**다음 단계**: Unity 에디터에서 실제 씬 구성 → 테스트 플레이 → 스테이지 데이터 추가