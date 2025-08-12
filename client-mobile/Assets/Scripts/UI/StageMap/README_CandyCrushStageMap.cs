using UnityEngine;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 캔디크러시 사가 스타일 스테이지 맵 구현 가이드
    /// 
    /// 🎯 구현 완료된 컴포넌트들:
    /// 1. StageFeed.cs - 뱀 모양 경로 생성기
    /// 2. StageNode.cs - 개별 스테이지 노드 UI
    /// 3. StageNodePool.cs - 성능 최적화용 오브젝트 풀링
    /// 4. CandyCrushStageMapView.cs - 메인 뷰 컨트롤러
    /// 5. StageInfoModal.cs - 스테이지 정보 표시 모달
    /// 
    /// 🚀 Unity 에디터 설정 방법:
    /// 
    /// 1단계: GameObject 준비
    /// ==========================================
    /// MainScene의 Canvas 하위에 다음과 같이 구성:
    /// 
    /// Canvas
    /// ├── StageSelectPanel (CandyCrushStageMapView 컴포넌트 추가)
    /// │   ├── ScrollView
    /// │   │   ├── Viewport
    /// │   │   └── Content (RectTransform)
    /// │   ├── UI Elements
    /// │   │   ├── BackButton
    /// │   │   ├── RefreshButton
    /// │   │   ├── ProgressText
    /// │   │   └── TotalStarsText
    /// │   └── StageFeed (빈 GameObject에 StageFeed 컴포넌트)
    /// ├── StageNodePool (빈 GameObject에 StageNodePool 컴포넌트)
    /// └── StageInfoModal (StageInfoModal 컴포넌트가 있는 모달 패널)
    /// 
    /// 2단계: CandyCrushStageMapView 설정
    /// ==========================================
    /// Inspector에서 다음 필드들을 드래그 앤 드롭으로 연결:
    /// 
    /// [스크롤 컴포넌트]
    /// - Scroll Rect: ScrollView의 ScrollRect 컴포넌트
    /// - Content Transform: ScrollView/Content의 RectTransform
    /// - Viewport Transform: ScrollView/Viewport의 RectTransform
    /// 
    /// [스테이지 시스템]
    /// - Stage Feed: StageFeed 컴포넌트가 있는 GameObject
    /// - Node Pool: StageNodePool 컴포넌트가 있는 GameObject
    /// 
    /// [UI 컴포넌트]
    /// - Progress Text: 진행률 표시할 Text 컴포넌트
    /// - Total Stars Text: 총 별 개수 표시할 Text 컴포넌트
    /// - Back Button: 뒤로가기 버튼
    /// - Refresh Button: 새로고침 버튼
    /// 
    /// [성능 설정]
    /// - Viewport Buffer: 200 (뷰포트 확장 영역)
    /// - Update Interval: 0.1 (업데이트 간격)
    /// 
    /// 3단계: StageFeed 설정
    /// ==========================================
    /// StageFeed 컴포넌트 Inspector 설정:
    /// 
    /// [레이아웃 설정]
    /// - Stages Per Row: 5 (한 줄당 스테이지 개수)
    /// - Stage Spacing: 120 (스테이지 간격)
    /// - Row Spacing: 150 (줄 간격)
    /// - Total Stages: 100 (총 스테이지 개수)
    /// 
    /// [경로 설정]
    /// - Draw Connection Lines: true (연결선 그리기)
    /// - Connection Line Prefab: 연결선 LineRenderer 프리팹
    /// - Connection Lines Parent: 연결선들의 부모 Transform
    /// 
    /// 4단계: StageNodePool 설정
    /// ==========================================
    /// StageNodePool 컴포넌트 Inspector 설정:
    /// 
    /// [풀링 설정]
    /// - Stage Node Prefab: StageNode 컴포넌트가 있는 프리팹
    /// - Initial Pool Size: 20 (초기 풀 크기)
    /// - Max Pool Size: 100 (최대 풀 크기)
    /// - Pool Parent: 풀 오브젝트들의 부모 Transform
    /// 
    /// 5단계: StageNode 프리팹 생성
    /// ==========================================
    /// Prefab 구조:
    /// 
    /// StageNodePrefab
    /// ├── Background (Image - 배경)
    /// ├── StageNumberText (Text - 스테이지 번호)
    /// ├── LockIcon (Image - 자물쇠 아이콘)
    /// ├── StarsParent (빈 GameObject)
    /// │   ├── Star1 (Image)
    /// │   ├── Star2 (Image)
    /// │   └── Star3 (Image)
    /// └── Button (전체 클릭 영역)
    /// 
    /// StageNode 컴포넌트 설정:
    /// - Stage Button: Button 컴포넌트
    /// - Stage Number Text: 번호 표시 Text
    /// - Background Image: 배경 Image
    /// - Lock Icon: 자물쇠 Image
    /// - Stars Parent: 별들의 부모 Transform
    /// - Star Images: 별 Image 배열 (3개)
    /// 
    /// 6단계: StageInfoModal 설정
    /// ==========================================
    /// 모달 구조:
    /// 
    /// StageInfoModal
    /// ├── Background (어두운 배경)
    /// ├── ModalPanel (실제 모달 창)
    /// │   ├── Header (제목 영역)
    /// │   ├── StageInfo (스테이지 정보)
    /// │   ├── Thumbnail (게임 보드 미리보기)
    /// │   ├── Constraints (제약 조건)
    /// │   └── Buttons (닫기/시작 버튼)
    /// 
    /// 7단계: UIManager 통합
    /// ==========================================
    /// UIManager Inspector에서:
    /// - Stage Select Panel: CandyCrushStageMapView가 있는 GameObject를 연결
    /// 
    /// 기존 StageMapView는 그대로 두고, 필요시 선택적으로 사용 가능
    /// 
    /// 8단계: 테스트 및 디버깅
    /// ==========================================
    /// 
    /// 개발자 모드 기능들:
    /// - StageFeed: Context Menu "Regenerate Path"로 경로 재생성
    /// - StageNodePool: Context Menu로 풀 상태 확인/정리
    /// - CandyCrushStageMapView: 런타임에서 스크롤 동작 테스트
    /// 
    /// Console 로그로 다음 사항들 확인:
    /// - "스테이지 경로 생성 완료: N개 스테이지"
    /// - "StageNodePool 초기화 완료: N개 노드 생성"
    /// - "CandyCrushStageMapView 초기화 완료"
    /// - "가시 노드 업데이트: 1-10 (N개 활성)"
    /// 
    /// 🎮 게임플레이 플로우:
    /// ==========================================
    /// 1. 유저가 로그인 → ModeSelection → StageSelect 진입
    /// 2. CandyCrushStageMapView가 활성화되면서 뱀 모양 스테이지들 표시
    /// 3. 유저가 스크롤하면 뷰포트 기반으로 동적 로딩
    /// 4. 스테이지 클릭 → StageInfoModal 표시 → 게임 시작 버튼
    /// 5. 게임 시작 → SingleGameplayScene으로 전환
    /// 
    /// ⚡ 성능 최적화 특징:
    /// ==========================================
    /// - 오브젝트 풀링으로 GC 최소화
    /// - 뷰포트 기반 컬링으로 메모리 효율성
    /// - 캔디크러시 사가 스타일의 부드러운 스크롤
    /// - 실시간 진행도 동기화
    /// 
    /// 🐛 문제 해결 가이드:
    /// ==========================================
    /// 
    /// Q: 스테이지가 표시되지 않아요
    /// A: StageFeed의 경로 생성이 완료되었는지 확인하고,
    ///    StageNodePool에 프리팹이 올바르게 설정되었는지 확인하세요.
    /// 
    /// Q: 스크롤이 부드럽지 않아요
    /// A: ScrollRect의 Inertia와 Deceleration Rate를 조정하고,
    ///    CandyCrushStageMapView의 Update Interval을 줄여보세요.
    /// 
    /// Q: 성능이 느려요
    /// A: StageNodePool의 크기를 조정하고,
    ///    Viewport Buffer를 줄여서 활성 노드 수를 제한하세요.
    /// 
    /// Q: 스테이지 진행도가 업데이트되지 않아요
    /// A: StageProgressManager가 올바르게 연결되어 있고,
    ///    서버 통신이 정상적으로 이루어지는지 확인하세요.
    /// 
    /// 📝 추가 개발 사항:
    /// ==========================================
    /// - 연결선 시각화 (LineRenderer 또는 UI Line)
    /// - 스테이지 완료/언락 애니메이션 효과
    /// - 게임 보드 썸네일 생성 시스템
    /// - 다양한 스테이지 테마 및 배경
    /// - 사운드 효과 및 햅틱 피드백
    /// </summary>
    public class README_CandyCrushStageMap
    {
        /*
        이 파일은 문서용 클래스입니다.
        실제 코드 실행에는 사용되지 않으며, 
        개발자들이 캔디크러시 스타일 스테이지 맵을 
        Unity에서 구현할 때 참고할 수 있도록 만들어졌습니다.
        
        모든 구현이 완료되면 이 파일은 삭제해도 됩니다.
        */
    }
}