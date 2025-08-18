using UnityEngine;

namespace BlokusUnity.UI
{
    /// <summary>
    /// Unity 블로쿠스 UI 아키텍처 설계 (Migration Plan)
    /// 씬 구조 변경: AppPersistent → MainScene(additive) → Single/Multi Scene flows
    /// </summary>
    public class UIArchitecture : MonoBehaviour
    {
        /*
        ==============================================
        Migration Plan: 하이브리드 씬 아키텍처
        ==============================================
        
        씬 구조(모두 Additive):
        📁 AppPersistent(전역): SceneFlowController, SessionManager, SystemMessageManager, HttpApiClient
        📁 MainScene: 로그인/모드 선택/설정 + UIArchitecture
        📁 SingleCore: 싱글 전용 매니저/캐시(StageDataManager, StageProgressManager, UserDataCache, SingleCoreBootstrap)
        📁 SingleGameplayScene: 싱글 게임 화면/로직
        📁 MultiGameplayScene(Stub): 멀티 진입 포인트(TCP 준비용)
        
        전환 규칙:
        - 부팅: AppPersistent → MainScene(additive 활성)
        - GoSingle: SingleCore(없으면 로드) → SingleGameplayScene 로드 → SingleGameplayScene 활성
        - ExitSingleToMain: SingleGameplayScene 언로드(코어 유지) → MainScene 활성
        - GoMulti: SingleGameplayScene 언로드 → SingleCore 언로드 → MultiGameplayScene 로드/활성
        - ExitMultiToMain: MultiGameplayScene 언로드 → MainScene 활성
        
        UI Architecture:
        - IPanel/PanelBase: Animator(Show/Hide 트리거) 기반 패널 시스템
        - LoadingOverlay: 최상위 UI로 스피너 표시
        - InputLocker: EventSystem/GraphicRaycaster 비활성화
        - SystemMessageManager: 3개 스택 토스트 시스템
        */
    }

    /// <summary>
    /// UI 화면 상태 정의 (Migration Plan 호환)
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

