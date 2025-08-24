// Assets/Scripts/UI/GamePanel.cs
using UnityEngine;
using Shared.Models;
using Features.Single.Gameplay; // SingleGameManager

namespace Features.Single.UI.InGame
{
    public class GamePanel : MonoBehaviour
    {
        [Header("Visual Roots (게임 UI 루트들)")]
        [SerializeField] private GameObject[] gameplayRoots;   // GameBoard, HUD, InGame UIs 등
        [SerializeField] private GameObject topBarRoot;        // TopBarUI (선택)

        [Header("Selection Screen (스테이지 선택 루트)")]
        [SerializeField] private GameObject stageSelectPanelRoot;

        [Header("Behaviour")]
        [SerializeField] private CanvasGroup canvasGroup;      // 있으면 입력차단/허용에 사용
        [SerializeField] private bool lockBeforeReady = true;  // 준비 전 입력 차단
        [SerializeField] private bool showTopBarDuringSelection = false; // 선택 화면에서 TopBar 노출 여부
        [SerializeField] private bool verboseLog = true;

        private void OnEnable()
        {
            // 1) 항상 “프리-게임” 상태로 세팅해서 선택화면이 보이게 만든다
            EnsurePreGameplayState();

            // 2) 이벤트 구독
            SingleGameManager.OnGameReady += HandleGameReady;

            // 3) 레퍼런스 자동 보정(없을 때만)
            if (!topBarRoot)
            {
                var maybe = GameObject.Find("TopBarUI");
                if (maybe) topBarRoot = maybe;
            }
            if (!stageSelectPanelRoot)
            {
                var maybe = GameObject.Find("StageSelectPanel");
                if (maybe) stageSelectPanelRoot = maybe;
            }

            // 4) 만약 이미 게임이 시작되어 있었다면(늦게 합류) → 즉시 전환
            var gm = SingleGameManager.Instance;
            if (gm != null && gm.IsInitialized)
            {
                if (verboseLog) Debug.Log("[GamePanel] Late-join detected → activate gameplay now");
                HandleGameReady();
            }

            if (verboseLog) Debug.Log("[GamePanel] OnEnable done");
        }

        private void OnDisable()
        {
            SingleGameManager.OnGameReady -= HandleGameReady;
        }

        // ==== 버튼 바인딩용 API ====
        public void StartStageByNumber(int stageNumber)
        {
            if (verboseLog) Debug.Log($"[GamePanel] StartStageByNumber({stageNumber})");
            SetInteractable(false); // 시작 누르면 잠깐 잠금
            if (!SingleGameManager.Instance)
            {
                Debug.LogError("[GamePanel] SingleGameManager not found");
                return;
            }
            SingleGameManager.Instance.RequestStartByNumber(stageNumber);
        }

        public void StartStage(StageData data)
        {
            if (data == null) { Debug.LogError("[GamePanel] data == null"); return; }
            if (verboseLog) Debug.Log("[GamePanel] StartStage(StageData)");
            SetInteractable(false);
            if (!SingleGameManager.Instance)
            {
                Debug.LogError("[GamePanel] SingleGameManager not found");
                return;
            }
            SingleGameManager.Instance.ApplyStageData(data);
        }

        // ==== 이벤트 콜백 ====
        private void HandleGameReady()
        {
            if (verboseLog) Debug.Log("[GamePanel] ✅ OnGameReady → switch to gameplay UI");

            // 🔥 수정: 기획 의도에 따라 StageSelectPanel은 비활성화하지 않음 (위에 GamePanel이 레이어링됨)
            // StageSelectPanel 비활성화 제거

            // 🔥 추가: GamePanel이 StageSelectPanel 위에 레이어링되도록 Canvas 정렬 설정
            EnsureGamePanelOnTop();

            // 2) 게임 UI 표시
            if (gameplayRoots != null)
            {
                foreach (var go in gameplayRoots)
                    if (go && !go.activeSelf) go.SetActive(true);
            }

            if (topBarRoot && !topBarRoot.activeSelf)
                topBarRoot.SetActive(true);

            // 3) 입력 허용
            SetInteractable(true);
        }

        // ==== 프리-게임 상태 만들기 ====
        private void EnsurePreGameplayState()
        {
            // 게임 UI는 모두 숨김
            if (gameplayRoots != null)
            {
                foreach (var go in gameplayRoots)
                    if (go && go.activeSelf) go.SetActive(false);
            }

            // 선택 화면 보장
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
                stageSelectPanelRoot.SetActive(true);

            // TopBar는 선택 화면에서 숨기거나(기본), 노출할지 선택
            if (topBarRoot)
                topBarRoot.SetActive(showTopBarDuringSelection);

            // 입력 차단(겹쳐 있어도 클릭 안 가리게)
            SetInteractable(false);
        }

        /// <summary>
        /// 🔥 수정: GamePanel이 StageSelectPanel 위에 레이어링되도록 Canvas 정렬 설정
        /// 하위 오브젝트 순서는 에디터 설정 그대로 유지
        /// </summary>
        private void EnsureGamePanelOnTop()
        {
            if (verboseLog) Debug.Log("[GamePanel] GamePanel을 최상단으로 정렬 중...");
            
            // 🔥 수정: GamePanel 자체만 최상단으로 이동 (하위 오브젝트 순서는 건드리지 않음)
            this.transform.SetAsLastSibling();
            
            if (verboseLog) Debug.Log("[GamePanel] Canvas 정렬 완료 - GamePanel이 StageSelectPanel 위에 레이어링됨 (하위 오브젝트 순서 유지)");
        }

        private void SetInteractable(bool v)
        {
            if (canvasGroup)
            {
                canvasGroup.interactable = v;
                canvasGroup.blocksRaycasts = v;
                // alpha는 필요 시 연출용으로만
            }
            if (verboseLog) Debug.Log($"[GamePanel] Interactable = {v}");
        }
    }
}
