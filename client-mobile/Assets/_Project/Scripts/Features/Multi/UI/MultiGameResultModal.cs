using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Features.Multi.Net;
using System.Collections.Generic;
using System.Linq;
using SharedModels = Shared.Models;
using MultiModels = Features.Multi.Models;
using NetModels = Features.Multi.Net;

namespace Features.Multi.UI
{
    /// <summary>
    /// 멀티플레이어 게임 결과 모달
    /// 게임 종료 시 최종 순위, 점수, 경험치 획득 등을 표시
    /// </summary>
    public class MultiGameResultModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button toLobbyButton;
        
        [Header("플레이어 순위 표시")]
        [SerializeField] private Transform playerRankingParent;
        [SerializeField] private GameObject playerRankingItemPrefab;
        
        [Header("내 결과 표시")]
        [SerializeField] private TextMeshProUGUI myRankText;
        [SerializeField] private TextMeshProUGUI myScoreText;
        [SerializeField] private TextMeshProUGUI expGainedText;
        [SerializeField] private TextMeshProUGUI newLevelText; // 레벨업 시 표시
        
        [Header("게임 통계")]
        [SerializeField] private TextMeshProUGUI gameDurationText;
        [SerializeField] private TextMeshProUGUI totalBlocksPlacedText;
        [SerializeField] private TextMeshProUGUI gameResultSummaryText;
        
        [Header("네트워크 참조")]
        [SerializeField] private NetworkManager networkManager;
        
        // 결과 데이터
        private List<PlayerGameResult> playerResults = new List<PlayerGameResult>();
        private NetModels.UserInfo myUpdatedStats = null;
        private MultiModels.PlayerColor winnerColor = MultiModels.PlayerColor.None;
        private bool isWaitingForStats = false;
        
        /// <summary>
        /// 플레이어 게임 결과 데이터
        /// </summary>
        [System.Serializable]
        public class PlayerGameResult
        {
            public string username;
            public string displayName;
            public MultiModels.PlayerColor playerColor;
            public int finalScore;
            public int blocksPlaced;
            public int rank;
            public bool isWinner;
            public bool isMe;
        }
        
        private void Awake()
        {
            // NetworkManager 참조 자동 탐색
            if (networkManager == null)
            {
                networkManager = FindObjectOfType<NetworkManager>();
            }
            
            // 버튼 이벤트 연결
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HideModal);
            }
            
            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(HideModal);
            }
            
            if (toLobbyButton != null)
            {
                toLobbyButton.onClick.AddListener(OnToLobbyClicked);
            }
            
            // 초기에는 모달 숨김
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
        }
        
        private void OnEnable()
        {
            // 게임 종료 관련 이벤트 구독
            if (networkManager != null)
            {
                networkManager.OnGameEnded += OnGameEnded;
                networkManager.OnMyStatsUpdated += OnMyStatsUpdated;
                Debug.Log("[MultiGameResultModal] 게임 결과 이벤트 구독 완료");
            }
            else
            {
                Debug.LogWarning("[MultiGameResultModal] NetworkManager를 찾을 수 없습니다!");
            }
        }
        
        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (networkManager != null)
            {
                networkManager.OnGameEnded -= OnGameEnded;
                networkManager.OnMyStatsUpdated -= OnMyStatsUpdated;
                Debug.Log("[MultiGameResultModal] 게임 결과 이벤트 구독 해제 완료");
            }
        }
        
        private void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HideModal);
            }
            
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveListener(HideModal);
            }
            
            if (toLobbyButton != null)
            {
                toLobbyButton.onClick.RemoveListener(OnToLobbyClicked);
            }
        }
        
        /// <summary>
        /// 게임 종료 이벤트 처리
        /// </summary>
        private void OnGameEnded(MultiModels.PlayerColor winner)
        {
            Debug.Log($"[MultiGameResultModal] 게임 종료 - 승자: {winner}");
            
            winnerColor = winner;
            isWaitingForStats = true;
            
            // 현재 게임 상태에서 결과 데이터 수집
            CollectGameResults();
            
            // 통계 업데이트 대기 (2초 후에도 안 오면 그냥 표시)
            StartCoroutine(WaitForStatsUpdate());
        }
        
        /// <summary>
        /// 내 통계 업데이트 이벤트 처리
        /// </summary>
        private void OnMyStatsUpdated(NetModels.UserInfo updatedStats)
        {
            Debug.Log($"[MultiGameResultModal] 내 통계 업데이트 수신: 레벨 {updatedStats.level}");
            
            myUpdatedStats = updatedStats;
            isWaitingForStats = false;
            
            // 모달 표시
            ShowGameResultModal();
        }
        
        /// <summary>
        /// 통계 업데이트 대기 (최대 3초)
        /// </summary>
        private System.Collections.IEnumerator WaitForStatsUpdate()
        {
            float waitTime = 3f;
            
            while (waitTime > 0 && isWaitingForStats)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime -= 0.1f;
            }
            
            if (isWaitingForStats)
            {
                Debug.LogWarning("[MultiGameResultModal] 통계 업데이트를 기다렸지만 수신하지 못했습니다. 결과 모달을 표시합니다.");
                isWaitingForStats = false;
                ShowGameResultModal();
            }
        }
        
        /// <summary>
        /// 현재 게임 상태에서 결과 데이터 수집
        /// </summary>
        private void CollectGameResults()
        {
            playerResults.Clear();
            
            // GameRoomPanel에서 현재 플레이어 정보와 점수를 수집
            var gameRoomPanel = FindObjectOfType<GameRoomPanel>();
            if (gameRoomPanel == null)
            {
                Debug.LogWarning("[MultiGameResultModal] GameRoomPanel을 찾을 수 없습니다!");
                return;
            }
            
            // TODO: GameRoomPanel에서 실제 최종 점수와 플레이어 정보를 가져오는 로직
            // 현재는 예시 데이터로 대체
            CreateSampleResults();
        }
        
        /// <summary>
        /// 샘플 결과 데이터 생성 (실제 구현에서는 게임 데이터에서 가져와야 함)
        /// </summary>
        private void CreateSampleResults()
        {
            // 실제 구현에서는 GameRoomPanel의 PlayerSlots와 점수 데이터에서 가져와야 함
            playerResults.Add(new PlayerGameResult
            {
                username = "player1",
                displayName = "플레이어1",
                playerColor = MultiModels.PlayerColor.Blue,
                finalScore = 89,
                blocksPlaced = 21,
                rank = 1,
                isWinner = winnerColor == MultiModels.PlayerColor.Blue,
                isMe = false
            });
            
            playerResults.Add(new PlayerGameResult
            {
                username = "player2",
                displayName = "플레이어2", 
                playerColor = MultiModels.PlayerColor.Yellow,
                finalScore = 76,
                blocksPlaced = 18,
                rank = 2,
                isWinner = winnerColor == MultiModels.PlayerColor.Yellow,
                isMe = true // 예시로 이 플레이어가 나라고 가정
            });
            
            playerResults.Add(new PlayerGameResult
            {
                username = "player3",
                displayName = "플레이어3",
                playerColor = MultiModels.PlayerColor.Red,
                finalScore = 65,
                blocksPlaced = 15,
                rank = 3,
                isWinner = winnerColor == MultiModels.PlayerColor.Red,
                isMe = false
            });
            
            // 점수 순으로 정렬
            playerResults = playerResults.OrderByDescending(p => p.finalScore).ToList();
            for (int i = 0; i < playerResults.Count; i++)
            {
                playerResults[i].rank = i + 1;
            }
        }
        
        /// <summary>
        /// 게임 결과 모달 표시
        /// </summary>
        private void ShowGameResultModal()
        {
            Debug.Log("[MultiGameResultModal] 게임 결과 모달 표시");
            
            // 부모 GameObject 활성화
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }
            
            // UI 업데이트
            UpdateResultUI();
            
            // 모달 표시
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
                EnsureModalOnTop();
            }
        }
        
        /// <summary>
        /// 결과 UI 업데이트
        /// </summary>
        private void UpdateResultUI()
        {
            // 타이틀 설정
            if (titleText != null)
            {
                var myResult = playerResults.FirstOrDefault(p => p.isMe);
                if (myResult != null)
                {
                    string title = myResult.isWinner ? "🏆 승리!" : $"게임 종료 - {myResult.rank}등";
                    titleText.text = title;
                    titleText.color = myResult.isWinner ? Color.yellow : Color.white;
                }
                else
                {
                    titleText.text = "게임 종료";
                }
            }
            
            // 내 결과 표시
            UpdateMyResults();
            
            // 플레이어 순위 표시
            UpdatePlayerRankings();
            
            // 게임 통계 표시
            UpdateGameStats();
        }
        
        /// <summary>
        /// 내 결과 정보 업데이트
        /// </summary>
        private void UpdateMyResults()
        {
            var myResult = playerResults.FirstOrDefault(p => p.isMe);
            if (myResult == null) return;
            
            // 내 순위와 점수
            if (myRankText != null)
            {
                myRankText.text = $"{myResult.rank}등";
                myRankText.color = myResult.rank == 1 ? Color.yellow : 
                                  myResult.rank == 2 ? Color.cyan : 
                                  myResult.rank == 3 ? Color.green : Color.white;
            }
            
            if (myScoreText != null)
            {
                myScoreText.text = $"{myResult.finalScore}점";
            }
            
            // 경험치 및 레벨업 정보
            if (myUpdatedStats != null)
            {
                if (expGainedText != null)
                {
                    // 획득한 경험치 계산 (실제로는 이전 경험치와 비교해야 함)
                    int expGained = myResult.finalScore; // 임시로 점수만큼 경험치를 얻는다고 가정
                    expGainedText.text = $"+{expGained} EXP";
                    expGainedText.color = Color.cyan;
                }
                
                // 레벨업 확인 (실제로는 이전 레벨과 비교해야 함)
                if (newLevelText != null && myUpdatedStats.level > 0) // 임시 조건
                {
                    newLevelText.text = $"Level {myUpdatedStats.level}!";
                    newLevelText.gameObject.SetActive(true);
                    newLevelText.color = Color.yellow;
                }
                else if (newLevelText != null)
                {
                    newLevelText.gameObject.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// 플레이어 순위 목록 업데이트
        /// </summary>
        private void UpdatePlayerRankings()
        {
            if (playerRankingParent == null || playerRankingItemPrefab == null)
            {
                Debug.LogWarning("[MultiGameResultModal] PlayerRanking UI가 설정되지 않았습니다!");
                return;
            }
            
            // 기존 아이템 제거
            foreach (Transform child in playerRankingParent)
            {
                if (child != playerRankingItemPrefab.transform)
                {
                    Destroy(child.gameObject);
                }
            }
            
            // 순위별로 아이템 생성
            foreach (var result in playerResults.OrderBy(p => p.rank))
            {
                CreatePlayerRankingItem(result);
            }
        }
        
        /// <summary>
        /// 플레이어 순위 아이템 생성
        /// </summary>
        private void CreatePlayerRankingItem(PlayerGameResult result)
        {
            var itemObj = Instantiate(playerRankingItemPrefab, playerRankingParent);
            itemObj.SetActive(true);
            
            // 순위 텍스트
            var rankText = itemObj.transform.Find("RankText")?.GetComponent<TextMeshProUGUI>();
            if (rankText != null)
            {
                rankText.text = $"{result.rank}";
                rankText.color = result.rank == 1 ? Color.yellow : 
                                result.rank == 2 ? Color.cyan : 
                                result.rank == 3 ? Color.green : Color.white;
            }
            
            // 플레이어 이름
            var nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = result.displayName;
                nameText.color = result.isMe ? Color.yellow : Color.white;
                if (result.isMe)
                {
                    nameText.text += " (나)";
                }
            }
            
            // 점수
            var scoreText = itemObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            if (scoreText != null)
            {
                scoreText.text = $"{result.finalScore}점";
            }
            
            // 플레이어 색상 표시
            var colorImage = itemObj.transform.Find("ColorImage")?.GetComponent<Image>();
            if (colorImage != null)
            {
                colorImage.color = GetPlayerColor(result.playerColor);
            }
        }
        
        /// <summary>
        /// 플레이어 색상 반환
        /// </summary>
        private Color GetPlayerColor(MultiModels.PlayerColor playerColor)
        {
            switch (playerColor)
            {
                case MultiModels.PlayerColor.Blue: return Color.blue;
                case MultiModels.PlayerColor.Yellow: return Color.yellow;
                case MultiModels.PlayerColor.Red: return Color.red;
                case MultiModels.PlayerColor.Green: return Color.green;
                default: return Color.white;
            }
        }
        
        /// <summary>
        /// 게임 통계 정보 업데이트
        /// </summary>
        private void UpdateGameStats()
        {
            if (gameDurationText != null)
            {
                // 실제로는 게임 시작 시간을 기록해서 계산해야 함
                gameDurationText.text = "게임 시간: 15:23";
            }
            
            if (totalBlocksPlacedText != null)
            {
                int totalBlocks = playerResults.Sum(p => p.blocksPlaced);
                totalBlocksPlacedText.text = $"총 배치된 블록: {totalBlocks}개";
            }
            
            if (gameResultSummaryText != null)
            {
                var winner = playerResults.FirstOrDefault(p => p.isWinner);
                if (winner != null)
                {
                    gameResultSummaryText.text = $"🏆 {winner.displayName}님이 승리했습니다!";
                }
                else
                {
                    gameResultSummaryText.text = "게임이 종료되었습니다.";
                }
            }
        }
        
        /// <summary>
        /// 모달 숨김
        /// </summary>
        private void HideModal()
        {
            Debug.Log("[MultiGameResultModal] 게임 결과 모달 숨김");
            
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
            
            // 데이터 초기화
            playerResults.Clear();
            myUpdatedStats = null;
            isWaitingForStats = false;
        }
        
        /// <summary>
        /// 로비로 돌아가기 버튼 클릭
        /// </summary>
        private void OnToLobbyClicked()
        {
            Debug.Log("[MultiGameResultModal] 로비로 돌아가기 요청");
            
            // 모달 닫기
            HideModal();
            
            // 방 나가기 요청
            if (networkManager != null && networkManager.GetNetworkClient() != null)
            {
                networkManager.GetNetworkClient().SendCleanTCPMessage("room:leave");
            }
        }
        
        /// <summary>
        /// 모달을 최상단에 표시되도록 보장
        /// </summary>
        private void EnsureModalOnTop()
        {
            if (modalPanel == null) return;
            
            // Transform을 최상단으로 이동
            modalPanel.transform.SetAsLastSibling();
            
            // Canvas 설정
            var canvas = modalPanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = modalPanel.AddComponent<Canvas>();
            }
            
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1500; // 게임 결과 모달
            
            // GraphicRaycaster 확인
            var raycaster = modalPanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = modalPanel.AddComponent<GraphicRaycaster>();
            }
            
            // CanvasGroup으로 입력 차단
            var canvasGroup = modalPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = modalPanel.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            
            Debug.Log("[MultiGameResultModal] 모달이 최상단에 배치되었습니다");
        }
        
        /// <summary>
        /// Android 뒤로가기 버튼 처리
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && modalPanel != null && modalPanel.activeSelf)
            {
                HideModal();
            }
        }
    }
}