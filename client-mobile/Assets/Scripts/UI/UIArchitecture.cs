using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlokusUnity.Common;
using BlokusUnity.UI;

namespace BlokusUnity.UI
{
    /// <summary>
    /// Unity 블로쿠스 UI 아키텍처 설계
    /// 하이브리드 방식: 메인 씬 + UI 패널 시스템
    /// </summary>
    public class UIArchitecture : MonoBehaviour
    {
        /*
        ==============================================
        수정된 씬 구조 설계 (싱글플레이 중심)
        ==============================================
        
        📁 Main Scene (메뉴 및 로비)
        ├── UI Canvas
        │   ├── LoginPanel
        │   ├── ModeSelectionPanel  
        │   ├── StageSelectPanel (싱글플레이용)
        │   ├── LobbyPanel (멀티플레이용)
        │   ├── GameRoomPanel (멀티플레이용)
        │   └── LoadingPanel
        ├── AudioManager
        ├── NetworkManager  
        └── GameDataManager
        
        📁 SingleGameplay Scene (싱글플레이 전용)
        ├── GameBoard (3D 보드 + 초기 상태)
        ├── BlockPalette (사용 가능한 블록들)
        ├── SingleGameLogic (점수, 별점 계산)
        ├── GameUI (점수, 시간, 버튼들)
        ├── ResultPanel (별점, 다음 스테이지)
        └── CameraController
        
        📁 MultiGameplay Scene (멀티플레이 전용) - 나중에
        ├── GameBoard (실시간 동기화)
        ├── NetworkGameLogic
        └── MultiplayerUI
        
        ==============================================
        싱글플레이 게임 플로우
        ==============================================
        
        스테이지 선택 → SingleGameplay Scene 로드
        ├── 스테이지 데이터로 보드 초기화
        ├── 플레이어 블록 팔레트 설정  
        ├── 게임 진행 (블록 배치)
        ├── 점수 계산 및 게임 종료 판정
        ├── 결과 화면 (별점 부여)
        └── 스테이지 선택 화면으로 복귀
        */
    }

    /// <summary>
    /// UI 화면 상태 정의
    /// </summary>
    public enum UIState
    {
        Login,              // 로그인 화면
        ModeSelection,      // 모드 선택 (싱글/멀티)
        StageSelect,        // 스테이지 선택 (싱글 모드)
        Lobby,              // 로비 (멀티 모드)
        GameRoom,           // 게임 방
        Gameplay,           // 게임 플레이
        Settings,           // 설정
        Loading             // 로딩
    }

    /// <summary>
    /// 씬 관리 전략
    /// </summary>
    public static class SceneArchitecture
    {
        // 씬 이름 상수
        public const string MAIN_SCENE = "MainScene";
        public const string GAMEPLAY_SCENE = "GameplayScene";

        /// <summary>
        /// 권장 씬 분리 전략
        /// </summary>
        public static class RecommendedApproach
        {
            /*
            ✅ 메인 씬 (항상 로드)
            - UI Canvas 시스템
            - 네트워크 매니저  
            - 오디오 매니저
            - 사용자 데이터 매니저
            - 설정 매니저
            
            ✅ 게임플레이 씬 (게임시에만 Additive 로드)
            - 3D 게임 보드
            - 블록 렌더링 시스템
            - 카메라 컨트롤러
            - 파티클 이펙트
            
            📱 모바일 최적화 이유:
            1. 메모리 효율성: 게임 보드는 필요시에만 로드
            2. 빠른 UI 전환: 패널 show/hide로 즉시 전환
            3. 네트워크 연결 유지: 씬 전환시에도 연결 유지
            4. 데이터 보존: 사용자 정보, 설정 등 영구 보존
            */
        }

        /// <summary>
        /// UI 전환이 씬 전환보다 좋은 이유
        /// </summary>
        public static class WhyUITransition
        {
            /*
            🚀 성능상 이점:
            - 0.1~0.3초: UI 패널 전환
            - 1~3초: 씬 전환 (모바일에서)
            
            📱 모바일 특화:
            - 낮은 RAM에서도 안정적
            - 백그라운드 복귀시 빠른 복구
            - 네트워크 끊김 방지
            
            🎮 게임 경험:
            - 부드러운 화면 전환
            - 로딩 화면 최소화
            - 즉시 반응하는 UI
            */
        }

        /// <summary>
        /// 언제 씬을 분리할지 결정
        /// </summary>
        public static class WhenToUseSeparateScene
        {
            /*
            ✅ 별도 씬이 필요한 경우:
            - 게임플레이 (3D 보드, 복잡한 렌더링)
            - 스테이지 에디터 (개발 도구)
            - 설정 화면 (복잡한 옵션들)
            
            ❌ 씬 분리가 불필요한 경우:
            - 로그인 ↔ 로비 (간단한 UI)
            - 모드 선택 ↔ 스테이지 선택
            - 팝업, 다이얼로그
            */
        }
    }

    /// <summary>
    /// 구현 우선순위
    /// </summary>
    public static class ImplementationPriority
    {
        /*
        🎯 1단계: 메인 씬 + UI 매니저
        - UIManager.cs (패널 전환 시스템)
        - 기본 UI 패널들 (Login, ModeSelect, etc.)
        - SceneManager (씬 로드/언로드)
        
        🎯 2단계: 게임플레이 씬 분리
        - Additive Scene Loading
        - 3D 게임 보드 구현
        - 카메라 시스템
        
        🎯 3단계: 최적화 및 전환 애니메이션
        - UI 트랜지션 애니메이션
        - 메모리 최적화
        - 성능 프로파일링
        */
    }
}

/// <summary>
/// UI 패널 기본 클래스
/// </summary>
public abstract class BaseUIPanel : MonoBehaviour
{
    [Header("Panel Settings")]
    public UIState panelType;
    public bool startActive = false;

    protected CanvasGroup canvasGroup;
    protected bool isAnimating = false;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    protected virtual void Start()
    {
        if (!startActive)
            Hide(false);
    }

    /// <summary>
    /// 패널 표시 (애니메이션 옵션)
    /// </summary>
    public virtual void Show(bool animated = true)
    {
        if (isAnimating) return;

        Debug.Log($"=== BaseUIPanel Show 시작: {gameObject.name} ===");
        Debug.Log($"CanvasGroup null? {canvasGroup == null}");
        Debug.Log($"Animated: {animated}");

        gameObject.SetActive(true);

        if (animated)
        {
            Debug.Log("애니메이션 모드로 FadeIn 시작");
            StartCoroutine(FadeIn());
        }
        else
        {
            Debug.Log("즉시 표시 모드");
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                Debug.Log($"CanvasGroup 설정 완료: Alpha={canvasGroup.alpha}, Interactable={canvasGroup.interactable}");
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}에 CanvasGroup이 없습니다!");
            }
        }
        
        Debug.Log($"GameObject Active: {gameObject.activeInHierarchy}");
    }

    /// <summary>
    /// 패널 숨기기 (애니메이션 옵션)
    /// </summary>
    public virtual void Hide(bool animated = true)
    {
        if (isAnimating) return;

        if (animated)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
            }
            gameObject.SetActive(false);
        }
    }

    protected virtual IEnumerator FadeIn()
    {
        isAnimating = true;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;   // 보일 때는 차단 ON (패널 내부만 클릭)

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;     // 최종 인터랙션 ON
        isAnimating = false;
    }

    protected virtual IEnumerator FadeOut()
    {
        isAnimating = true;
        canvasGroup.interactable = false;    // 내부 버튼 비활성
        canvasGroup.blocksRaycasts = false;  // 🔑 외부 클릭 막지 않도록 즉시 OFF

        float duration = 0.3f;
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        isAnimating = false;
    }
}