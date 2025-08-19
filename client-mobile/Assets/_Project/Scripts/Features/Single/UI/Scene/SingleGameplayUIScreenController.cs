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
        /// 외부에서 '선택 화면'으로 복귀할 때 사용 (GamePanel만 OFF)
        /// </summary>
        public void ShowSelection()
        {
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf) stageSelectPanelRoot.SetActive(true);
            if (gamePanelRoot && gamePanelRoot.activeSelf) gamePanelRoot.SetActive(false);
            if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → GamePanel OFF, StageSelect ON");
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
