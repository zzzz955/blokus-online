# We'll append UI prefab setup and Inspector connection examples to the existing guide, then save as file again.

extended_guide = """# Unity Blokus Mobile - Setup & Architecture Guide

이 문서는 현재까지 진행된 **프로젝트 구조, 씬 구성, 변경 내역**을 정리한 가이드입니다.

---

## 📁 프로젝트 디렉토리 구조

Assets/
└── Scripts/
    ├── Audio/
    ├── Common/       # 게임 공통 로직 (블록, 보드, 좌표, 유틸 등)
    ├── Data/         # 데이터 로딩 및 Stage 관리
    ├── Debug/
    ├── Game/         # GameBoard, BlockPalette, SingleGameManager 등 게임 플레이 로직
    ├── Network/      # HTTP API, TCP Client 등 네트워크 관련
    ├── UI/           # UI 전용 스크립트 (TopBarUI, StageSelectUI 등)
    └── Tests/        # 현재 사용 안 함 (삭제 예정)

---

## 🎮 씬 구성

### 1. MainScene (메인 메뉴/로비)
- 역할: 로그인, 모드 선택, 스테이지 선택 등
- 구성 요소:
  - Canvas (UIManager 연결)
  - EventSystem
  - StageDataManager
  - Main Camera
- 포함 UI:
  - LoginPanel, ModeSelectionPanel, StageSelectPanel
  - LoadingPanel

### 2. SingleGameplayScene (싱글 플레이)
- 역할: 실제 블로쿠스 게임 진행
- 구성 요소:
  - GameBoardRoot
    - GameBoard (20x20 셀)
    - ActionButtonPanel (Rotate, Flip, Place 버튼)
  - BlockPalettePanel
    - ScrollView (가로 스크롤)
    - Content (Horizontal Layout Group + ContentSizeFitter)
  - TopBar
    - Undo 버튼 (남은 횟수 표시)
    - Timer
    - Exit 버튼
  - SingleGameManager
  - TouchInputManager

### 3. (선택) LoadingScene
- 역할: 씬 전환 시 로딩 표시 (필요 시 추가)

---

## 🛠 주요 변경 내역

- **GameBoard**
  - ConfirmPlacementButton, ConfirmButtonPanel 제거 가능 (ActionButtonPanel로 대체)
  - 셀 클릭/호버 이벤트 유지

- **BlockPalette**
  - UI ScrollView 가로 배치 및 ContentSizeFitter 적용
  - BlockButton을 플레이어 색상으로 칠하고 라벨 표시
  - 버튼 클릭 → BlockPalette → SingleGameManager로 미리보기 호출

- **BlockButton**
  - 라벨 자동 생성
  - 클릭 시 콘솔 로그로 디버깅 가능

- **TopBarUI**
  - Pause 기능 제거
  - Score 표시 제거
  - Undo, Timer, Exit 기능 유지

- **TouchInputManager**
  - 스와이프 기반 회전/플립/Undo 기능 제거
  - ActionButtonPanel 버튼 클릭으로 회전/플립 수행

---

## 📋 씬 구성 가이드

### SingleGameplayScene Hierarchy 예시

SingleGameplayScene
├── Main Camera
├── EventSystem
├── Canvas (Screen Space - Overlay)
│   ├── TopBar
│   │   ├── UndoButton
│   │   ├── UndoCountText
│   │   ├── TimerText
│   │   └── ExitButton
│   ├── GameBoardRoot
│   │   ├── GameBoard
│   │   └── ActionButtonPanel
│   │       ├── RotateCWButton
│   │       ├── RotateCCWButton
│   │       ├── FlipHButton
│   │       ├── FlipVButton
│   │       └── PlaceButton
│   └── BlockPalettePanel
│       ├── ScrollView
│       │   ├── Viewport
│       │   │   └── Content (Horizontal Layout Group + ContentSizeFitter)
└── Managers
    ├── SingleGameManager
    └── TouchInputManager

---

## 🔍 UI 설정 팁

- **ScrollView Content**:
  - Horizontal Layout Group
  - Spacing: 16
  - Child Control Width/Height: ON
  - Force Expand Width/Height: OFF
  - Content Size Fitter: Horizontal Fit = Preferred Size
- **EventSystem** 필수
- **Canvas**에 GraphicRaycaster 유지

---

## 🧩 UI 프리팹 구성 예시

- **BlockButton.prefab**
  - Root: Button (Image)
    - Label: TextMeshProUGUI (Block 이름 표시)
  - Scripts:
    - BlockButton.cs
  - Button Component: OnClick → BlockPalette.NotifyButtonClicked(BlockType)

- **ActionButtonPanel.prefab**
  - Layout: Horizontal Layout Group
  - Buttons:
    - RotateCWButton → OnClick → BlockPalette.RotateSelectedBlock(true)
    - RotateCCWButton → OnClick → BlockPalette.RotateSelectedBlock(false)
    - FlipHButton → OnClick → BlockPalette.FlipSelectedBlock(false)
    - FlipVButton → OnClick → BlockPalette.FlipSelectedBlock(true)
    - PlaceButton → OnClick → SingleGameManager.TryPlaceSelectedBlock()

- **TopBar.prefab**
  - UndoButton → OnClick → SingleGameManager.OnUndoMove()
  - UndoCountText → SingleGameManager에서 갱신
  - TimerText → SingleGameManager에서 갱신
  - ExitButton → OnClick → SingleGameManager.OnExitRequested()

---

## 🔗 인스펙터 연결 예시

- **SingleGameManager**
  - GameBoard: GameBoard 컴포넌트 참조
  - BlockPalette: BlockPalette 컴포넌트 참조
  - TopBarUI: TopBarUI 스크립트 참조
  - ActionButtonPanel: Panel Transform 참조

- **BlockPalette**
  - BlockContainer: ScrollView/Viewport/Content
  - BlockButtonPrefab: BlockButton.prefab

- **GameBoard**
  - CellPrefab: UI Image 셀 또는 Sprite 셀
  - CellParent: GridContainer RectTransform
  - 색상 필드들: PlayerColor별 색상 지정

---

## 🧪 테스트 체크리스트

1. 팔레트 버튼이 플레이어 색상과 라벨로 표시되는지
2. 버튼 클릭 시 미리보기 표시되는지
3. Rotate/Flip 버튼 동작 후 미리보기 회전/반전 확인
4. Place 버튼으로 보드에 블록 배치
5. 배치 후 팔레트 버튼 비활성화
6. Undo 버튼 클릭 시 되돌리기 동작 및 횟수 감소
7. Exit 버튼 클릭 시 메인 메뉴로 복귀
8. Timer가 정상 동작하는지

---

이 가이드는 현재까지의 구현 사항을 반영한 것이며, 이후 네트워크 연동 및 스테이지 데이터 확장 시 추가 업데이트 예정입니다.
"""

file_path = "/mnt/data/UNITY_SETUP_GUIDE.md"
with open(file_path, "w", encoding="utf-8") as f:
    f.write(extended_guide)

file_path
