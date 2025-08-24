// Assets/_Project/Scripts/Features/Single/UI/Scene/SingleGameplayUIScreenController.cs
using UnityEngine;
using Features.Single.Gameplay; // SingleGameManager

namespace Features.Single.UI.Scene
{
    /// <summary>
    /// 싱글플레이 씬의 패널 전환 전담 컨트롤러
    /// - 진입 시: StageSelectPanel ON, GamePanel OFF
    /// - 게임 시작(OnGameReady): StageSelectPanel은 그대로 유지(ON), GamePanel만 ON
    /// - 게임 종료/나가기: GamePanel만 OFF → StageSelect 화면으로 복귀
    /// </summary>
    public class SingleGameplayUIScreenController : MonoBehaviour
    {
        [Header("Scene Panels")]
        [SerializeField] private GameObject stageSelectPanelRoot; // ex) "StageSelectPanel"
        [SerializeField] private GameObject gamePanelRoot;        // ex) "GamePanel"
        [SerializeField] private bool verboseLog = true;

        private void Awake()
        {
            if (!stageSelectPanelRoot) stageSelectPanelRoot = GameObject.Find("StageSelectPanel");
            if (!gamePanelRoot) gamePanelRoot = GameObject.Find("GamePanel");

            // ✅ 씬 진입 초기 상태 강제
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf) stageSelectPanelRoot.SetActive(true);
            if (gamePanelRoot && gamePanelRoot.activeSelf) gamePanelRoot.SetActive(false);

            if (verboseLog) Debug.Log("[UIScreenController] 초기 상태: StageSelect=ON, GamePanel=OFF");
        }

        private void OnEnable()
        {
            SingleGameManager.OnGameReady -= HandleGameReady;
            SingleGameManager.OnGameReady += HandleGameReady;

            // 늦게 들어와도 보정: 이미 초기화 끝난 상태면 즉시 전환
            var gm = SingleGameManager.Instance;
            if (gm != null && gm.IsInitialized) HandleGameReady();
        }

        private void OnDisable()
        {
            SingleGameManager.OnGameReady -= HandleGameReady;
        }

        /// <summary>
        /// 게임 시작 준비 완료 → GamePanel만 ON (StageSelect는 그대로 둠)
        /// </summary>
        private void HandleGameReady()
        {
            if (verboseLog) Debug.Log("[UIScreenController] OnGameReady → GamePanel ON (StageSelect KEEP)");

            // StageSelect는 절대 끄지 않는다
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
                stageSelectPanelRoot.SetActive(true);

            if (gamePanelRoot && !gamePanelRoot.activeSelf)
                gamePanelRoot.SetActive(true);
        }

        /// <summary>
        /// 외부에서 '게임 화면만' 켜고 싶을 때 사용 (StageSelect는 건드리지 않음)
        /// </summary>
        public void ShowGameplay()
        {
            if (gamePanelRoot && !gamePanelRoot.activeSelf) gamePanelRoot.SetActive(true);
            if (verboseLog) Debug.Log("[UIScreenController] ShowGameplay → GamePanel ON (StageSelect KEEP)");
        }

        /// <summary>
        /// 🔥 수정: 외부에서 '선택 화면'으로 복귀할 때 사용 (GamePanel만 OFF, UI 안정화 보장)
        /// </summary>
        public void ShowSelection()
        {
            // 🔥 수정: StageSelectPanel 강제 활성화 우선 처리
            if (stageSelectPanelRoot)
            {
                if (!stageSelectPanelRoot.activeSelf)
                {
                    stageSelectPanelRoot.SetActive(true);
                    if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → StageSelectPanel 강제 활성화");
                }
                
                // 🔥 추가: CandyCrushStageMapView 컴포넌트 즉시 활성화 확인
                var stageMapView = stageSelectPanelRoot.GetComponent<Features.Single.UI.StageSelect.CandyCrushStageMapView>();
                if (stageMapView != null && !stageMapView.gameObject.activeSelf)
                {
                    stageMapView.gameObject.SetActive(true);
                    if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → CandyCrushStageMapView 강제 활성화");
                }
            }
            
            // GamePanel 비활성화
            if (gamePanelRoot && gamePanelRoot.activeSelf)
            {
                gamePanelRoot.SetActive(false);
                if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → GamePanel 비활성화");
            }
            
            if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → GamePanel OFF, StageSelect ON");
            
            // 🔥 추가: UI 안정화를 위한 코루틴 시작
            StartCoroutine(EnsureUIStabilityAfterShowSelection());
        }
        
        /// <summary>
        /// 🔥 신규: ShowSelection 후 UI 안정화 보장 코루틴
        /// </summary>
        private System.Collections.IEnumerator EnsureUIStabilityAfterShowSelection()
        {
            // 1프레임 대기로 UI 업데이트 완료 보장
            yield return null;
            
            // StageSelectPanel 재확인
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
            {
                Debug.LogWarning("[UIScreenController] UI 안정화 - StageSelectPanel 재활성화");
                stageSelectPanelRoot.SetActive(true);
            }
            
            // 🔥 추가: CandyCrushStageMapView의 버튼 위치 재검증
            if (stageSelectPanelRoot)
            {
                var stageMapView = stageSelectPanelRoot.GetComponent<Features.Single.UI.StageSelect.CandyCrushStageMapView>();
                if (stageMapView != null)
                {
                    // ForceRefreshStageButtons 메서드 호출로 위치 재보정
                    stageMapView.SendMessage("ForceRefreshStageButtons", SendMessageOptions.DontRequireReceiver);
                    if (verboseLog) Debug.Log("[UIScreenController] UI 안정화 - CandyCrushStageMapView 강제 리프레시 완료");
                }
            }
        }

        /// <summary>
        /// 결과 모달/언락 코루틴 안전 보장을 위해 StageSelect를 반드시 켜두고 싶을 때 호출
        /// </summary>
        public void EnsureSelectionActive()
        {
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
            {
                stageSelectPanelRoot.SetActive(true);
                if (verboseLog) Debug.Log("[UIScreenController] EnsureSelectionActive → StageSelect forced ON");
            }
        }

        /// <summary>
        /// 결과 모달 '확인/배경 클릭' 등에서 호출: 선택화면 노출 + 게임화면 닫기
        /// </summary>
        public void ReturnToSelectionAndHideGame()
        {
            EnsureSelectionActive();
            if (gamePanelRoot && gamePanelRoot.activeSelf) gamePanelRoot.SetActive(false);
            if (verboseLog) Debug.Log("[UIScreenController] ReturnToSelectionAndHideGame → StageSelect ON, GamePanel OFF");
        }

        public void EnsureStageSelectVisible()
        {
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
                stageSelectPanelRoot.SetActive(true); // 🔥 강제로 켜기
        }
    }
}
