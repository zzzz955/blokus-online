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

        // Null 체크 추가
        if (gameObject == null)
        {
            Debug.LogError($"BaseUIPanel Show 실패: gameObject가 null입니다! 클래스: {this.GetType().Name}");
            return;
        }

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