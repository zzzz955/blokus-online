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

        [Header("로딩 상태 UI")]
        [SerializeField] private GameObject loadingPanel; // 로딩 패널 (Optional)
        [SerializeField] private TextMeshProUGUI loadingText; // 로딩 텍스트

        [Header("네트워크 참조")]
        [SerializeField] private NetworkManager networkManager;
        
        // 결과 데이터
        private List<PlayerGameResult> playerResults = new List<PlayerGameResult>();
        private NetModels.UserInfo myUpdatedStats = null;
        private MultiModels.PlayerColor winnerColor = MultiModels.PlayerColor.None;
        private bool isWaitingForStats = false;

        // 새로운 GAME_RESULT 데이터
        private GameResultData currentGameResult = null;

        // 통계 대기 코루틴 참조 (중복 실행 방지)
        private Coroutine waitForStatsCoroutine = null;
        
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
            public bool isDisconnected; // 게임 중 탈주 여부
        }
        
        private void Awake()
        {
            Debug.Log("[MultiGameResultModal] ===== Awake() 시작 =====");

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

            // 로딩 UI 초기화 (비활성화 상태로 시작)
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(false);
            }

            // ⭐ 이벤트 구독을 Awake()에서 실행 (GameObject가 비활성화되어도 실행됨)
            SubscribeToEvents();
        }

        /// <summary>
        /// 네트워크 이벤트 구독 (GameObject가 비활성화되어 있어도 실행되도록)
        /// </summary>
        private void SubscribeToEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnGameEnded += OnGameEnded;
                networkManager.OnMyStatsUpdated += OnMyStatsUpdated;
                networkManager.OnGameResultReceived += OnGameResultReceived;
                Debug.Log("[MultiGameResultModal] 게임 결과 이벤트 구독 완료 (Awake)");
            }
            else
            {
                Debug.LogWarning("[MultiGameResultModal] NetworkManager를 찾을 수 없습니다!");

                // NetworkManager를 다시 찾기 시도 (늦은 초기화 대비)
                StartCoroutineSafely(RetrySubscribeToEvents());
            }
        }

        /// <summary>
        /// GameObject 활성화 상태를 확인하고 안전하게 코루틴 시작
        /// </summary>
        private void StartCoroutineSafely(System.Collections.IEnumerator coroutine)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.Log("[MultiGameResultModal] GameObject가 비활성화 상태 - 코루틴 시작을 위해 활성화");
                gameObject.SetActive(true);
            }

            StartCoroutine(coroutine);
        }

        /// <summary>
        /// NetworkManager가 준비될 때까지 이벤트 구독 재시도
        /// </summary>
        private System.Collections.IEnumerator RetrySubscribeToEvents()
        {
            float retryTime = 0f;
            const float maxRetryTime = 5f; // 최대 5초 대기

            while (networkManager == null && retryTime < maxRetryTime)
            {
                yield return new WaitForSeconds(0.5f);
                retryTime += 0.5f;

                networkManager = FindObjectOfType<NetworkManager>();
                if (networkManager != null)
                {
                    Debug.Log("[MultiGameResultModal] NetworkManager 재탐색 성공, 이벤트 구독 중");
                    SubscribeToEvents();
                    break;
                }
            }

            if (networkManager == null)
            {
                Debug.LogError("[MultiGameResultModal] NetworkManager를 찾을 수 없습니다! 이벤트 구독 실패");
            }
        }
        
        private void OnEnable()
        {
            // 이벤트 구독은 이제 Awake()에서 처리됨
            Debug.Log("[MultiGameResultModal] 컴포넌트 활성화됨");
        }
        
        private void OnDisable()
        {
            // 이벤트 구독 해제는 OnDestroy에서 처리됨
            Debug.Log("[MultiGameResultModal] 컴포넌트 비활성화됨");
        }
        
        private void OnDestroy()
        {
            // 네트워크 이벤트 구독 해제
            if (networkManager != null)
            {
                networkManager.OnGameEnded -= OnGameEnded;
                networkManager.OnMyStatsUpdated -= OnMyStatsUpdated;
                networkManager.OnGameResultReceived -= OnGameResultReceived;
                Debug.Log("[MultiGameResultModal] 게임 결과 이벤트 구독 해제 완료");
            }

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
            Debug.Log($"[MultiGameResultModal] ===== OnGameEnded() 호출됨 - 승자: {winner} =====");

            // GameObject가 비활성화되어 있으면 활성화 (코루틴 시작을 위해)
            if (!gameObject.activeInHierarchy)
            {
                Debug.Log("[MultiGameResultModal] GameObject가 비활성화 상태 - 코루틴 시작을 위해 활성화");
                gameObject.SetActive(true);
            }

            // 기존 통계 대기 코루틴이 실행 중이면 중지 (중복 실행 방지)
            if (waitForStatsCoroutine != null)
            {
                Debug.Log("[MultiGameResultModal] 기존 통계 대기 코루틴 중지 - 중복 OnGameEnded 호출 감지");
                StopCoroutine(waitForStatsCoroutine);
                waitForStatsCoroutine = null;
            }

            winnerColor = winner;
            isWaitingForStats = true;

            // 현재 게임 상태에서 결과 데이터 수집
            CollectGameResults();

            // 즉시 모달을 표시하고 로딩 상태로 시작 (UX 개선)
            ShowLoadingState();

            // 통계 업데이트 대기 (3초 후에도 안 오면 그냥 표시)
            waitForStatsCoroutine = StartCoroutine(WaitForStatsUpdate());
        }
        
        /// <summary>
        /// 내 통계 업데이트 이벤트 처리
        /// </summary>
        private void OnMyStatsUpdated(NetModels.UserInfo updatedStats)
        {
            Debug.Log($"[MultiGameResultModal] 내 통계 업데이트 수신: 레벨 {updatedStats.level}");

            myUpdatedStats = updatedStats;

            // 게임 종료 후 통계 대기 중일 때만 모달 표시
            if (isWaitingForStats)
            {
                isWaitingForStats = false;
                Debug.Log("[MultiGameResultModal] 게임 종료 후 통계 업데이트 - 결과 모달 표시");
                ShowGameResultModal();
            }
            else
            {
                Debug.Log("[MultiGameResultModal] 로비 진입 시 통계 업데이트 - 모달 표시 안함");
            }
        }

        /// <summary>
        /// 새로운 GAME_RESULT 데이터 수신 이벤트 처리
        /// </summary>
        private void OnGameResultReceived(GameResultData gameResult)
        {
            Debug.Log($"[MultiGameResultModal] GAME_RESULT 데이터 수신: 순위={gameResult.myRank}, 점수={gameResult.myScore}, 경험치={gameResult.expGained}, 레벨업={gameResult.levelUp}");

            currentGameResult = gameResult;

            // GAME_RESULT 데이터를 받았으므로 기존 대기 상태 해제 (이중 처리 방지)
            isWaitingForStats = false;

            // 통계 대기 코루틴이 실행 중이면 중지 (GAME_RESULT 수신됨)
            if (waitForStatsCoroutine != null)
            {
                Debug.Log("[MultiGameResultModal] GAME_RESULT 수신으로 통계 대기 코루틴 중지");
                StopCoroutine(waitForStatsCoroutine);
                waitForStatsCoroutine = null;
            }

            // 새로운 데이터로 결과 모달 직접 표시
            ShowGameResultWithNewData();
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
                waitForStatsCoroutine = null; // 코루틴 참조 초기화
                ShowGameResultModal();
            }
            else
            {
                // 정상적으로 GAME_RESULT를 받아서 종료된 경우
                waitForStatsCoroutine = null; // 코루틴 참조 초기화
            }
        }
        
        /// <summary>
        /// 현재 게임 상태에서 결과 데이터 수집
        /// GAME_RESULT가 도착하지 않을 경우 fallback으로 사용
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

            // GameRoomPanel에서 실제 플레이어 데이터 가져오기
            var playerDataSnapshot = gameRoomPanel.GetPlayerDataSnapshot();

            // 빈 슬롯 제외하고 플레이어 결과 생성
            var validPlayers = new System.Collections.Generic.List<PlayerGameResult>();

            for (int i = 0; i < playerDataSnapshot.Length; i++)
            {
                var playerSlot = playerDataSnapshot[i];

                // 빈 슬롯은 제외
                if (playerSlot.isEmpty)
                    continue;

                // PlayerGameResult로 변환
                var result = new PlayerGameResult
                {
                    username = playerSlot.playerName,
                    displayName = playerSlot.playerName,
                    playerColor = ConvertColorIndexToPlayerColor(playerSlot.colorIndex),
                    finalScore = playerSlot.currentScore,
                    blocksPlaced = 21 - playerSlot.remainingBlocks, // 전체 블록 - 남은 블록
                    rank = 0, // 정렬 후 설정
                    isWinner = false, // 정렬 후 설정
                    isMe = false, // TODO: 본인 확인 필요
                    isDisconnected = playerSlot.isDisconnected // 탈주 여부
                };

                validPlayers.Add(result);
            }

            // 점수 순으로 정렬 및 순위 설정
            validPlayers = validPlayers.OrderByDescending(p => p.finalScore).ToList();

            for (int i = 0; i < validPlayers.Count; i++)
            {
                validPlayers[i].rank = i + 1;

                // 1등을 승자로 설정
                if (i == 0)
                {
                    validPlayers[i].isWinner = true;
                }
            }

            playerResults = validPlayers;

            Debug.Log($"[MultiGameResultModal] GameRoomPanel에서 {playerResults.Count}명의 플레이어 결과 수집 완료");
        }

        /// <summary>
        /// 탈주로 인한 게임 종료 여부 확인
        /// </summary>
        private bool IsGameEndedByDisconnection()
        {
            // playerResults에 탈주한 플레이어가 있는지 확인
            return playerResults.Any(p => p.isDisconnected);
        }

        /// <summary>
        /// 탈주로 인한 게임 종료 메시지 (단순화)
        /// </summary>
        private string GetDisconnectionMessage()
        {
            int disconnectedCount = playerResults.Count(p => p.isDisconnected);
            return $"모든 플레이어가 탈주하여 게임이 종료되었습니다. (탈주: {disconnectedCount}명)";
        }

        /// <summary>
        /// colorIndex를 PlayerColor로 변환
        /// </summary>
        private MultiModels.PlayerColor ConvertColorIndexToPlayerColor(int colorIndex)
        {
            return colorIndex switch
            {
                0 => MultiModels.PlayerColor.Blue,
                1 => MultiModels.PlayerColor.Yellow,
                2 => MultiModels.PlayerColor.Red,
                3 => MultiModels.PlayerColor.Green,
                _ => MultiModels.PlayerColor.None
            };
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
        /// 로딩 상태 표시 (결과 집계 중)
        /// </summary>
        private void ShowLoadingState()
        {
            Debug.Log("[MultiGameResultModal] 로딩 상태 표시");

            // 부모 GameObject 활성화
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            // 모달 패널 활성화
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
                EnsureModalOnTop();
            }

            // 로딩 UI 표시
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }

            if (loadingText != null)
            {
                loadingText.text = "결과 집계 중...";
                loadingText.gameObject.SetActive(true);
            }

            // 타이틀 임시 설정
            if (titleText != null)
            {
                titleText.text = "게임 종료";
                titleText.color = Color.white;
            }
        }

        /// <summary>
        /// 로딩 상태 해제
        /// </summary>
        private void HideLoadingState()
        {
            Debug.Log("[MultiGameResultModal] 로딩 상태 해제");

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(false);
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

            // 로딩 상태 해제
            HideLoadingState();

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
        /// 새로운 GAME_RESULT 데이터로 모달 표시
        /// </summary>
        private void ShowGameResultWithNewData()
        {
            Debug.Log("[MultiGameResultModal] 새로운 GAME_RESULT 데이터로 모달 표시");

            // 부모 GameObject 활성화
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
            }

            // 로딩 상태 해제
            HideLoadingState();

            // 새로운 데이터로 UI 업데이트
            UpdateResultUIWithNewData();

            // 모달 표시
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
                EnsureModalOnTop();
            }
        }
        
        /// <summary>
        /// 결과 UI 업데이트 (Fallback 경로 - GAME_RESULT 수신 안됨)
        /// 탈주로 인한 게임 종료로 간주
        /// </summary>
        private void UpdateResultUI()
        {
            // 탈주로 인한 게임 종료 여부 확인
            bool endedByDisconnection = IsGameEndedByDisconnection();

            // 타이틀 설정
            if (titleText != null)
            {
                if (endedByDisconnection)
                {
                    // 탈주로 인한 게임 종료
                    int disconnectedCount = playerResults.Count(p => p.isDisconnected);
                    titleText.text = $"게임 종료 ({disconnectedCount}명 탈주)";
                    titleText.color = new Color(0.9f, 0.9f, 0.9f, 1f); // 밝은 회색
                }
                else
                {
                    // 탈주 플레이어가 없는데 GAME_RESULT가 안 온 경우 (네트워크 이슈 등)
                    titleText.text = "게임 종료";
                    titleText.color = Color.white;
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
            
            // 플레이어 이름 (탈주 표시 포함)
            var nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = result.displayName;

                // 탈주 상태 표시
                if (result.isDisconnected)
                {
                    nameText.text += " (탈주)";
                    nameText.color = new Color(0.7f, 0.7f, 0.7f, 1f); // 회색
                }
                else if (result.isMe)
                {
                    nameText.text += " (나)";
                    nameText.color = Color.yellow;
                }
                else
                {
                    nameText.color = Color.white;
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
        /// 새로운 GAME_RESULT 데이터로 결과 UI 업데이트 (정상 게임 종료)
        /// </summary>
        private void UpdateResultUIWithNewData()
        {
            if (currentGameResult == null)
            {
                Debug.LogWarning("[MultiGameResultModal] currentGameResult가 null입니다. 기존 방식으로 처리");
                UpdateResultUI();
                return;
            }

            Debug.Log("[MultiGameResultModal] 새로운 GAME_RESULT 데이터로 UI 업데이트 (정상 종료)");

            // 타이틀 설정 (정상 게임 종료)
            if (titleText != null)
            {
                bool isWinner = currentGameResult.winners != null &&
                               currentGameResult.winners.Length > 0 &&
                               currentGameResult.myRank == 1;

                string title = isWinner ? "🏆 승리!" : $"게임 종료 - {currentGameResult.myRank}등";
                titleText.text = title;
                titleText.color = isWinner ? Color.yellow : Color.white;
            }

            // 내 결과 표시 (새로운 데이터 사용)
            UpdateMyResultsWithNewData();

            // 플레이어 순위 표시 (scores 데이터 사용)
            UpdatePlayerRankingsWithNewData();

            // 게임 통계 표시 (새로운 데이터 사용)
            UpdateGameStatsWithNewData();
        }

        /// <summary>
        /// 새로운 데이터로 내 결과 정보 업데이트
        /// </summary>
        private void UpdateMyResultsWithNewData()
        {
            if (currentGameResult == null) return;

            // 내 순위와 점수
            if (myRankText != null)
            {
                myRankText.text = $"{currentGameResult.myRank}등";
                myRankText.color = currentGameResult.myRank == 1 ? Color.yellow :
                                  currentGameResult.myRank == 2 ? Color.cyan :
                                  currentGameResult.myRank == 3 ? Color.green : Color.white;
            }

            if (myScoreText != null)
            {
                myScoreText.text = $"{currentGameResult.myScore}점";
            }

            // 경험치 및 레벨업 정보
            if (expGainedText != null)
            {
                expGainedText.text = $"+{currentGameResult.expGained} EXP";
                expGainedText.color = Color.cyan;
            }

            // 레벨업 확인
            if (newLevelText != null)
            {
                if (currentGameResult.levelUp)
                {
                    newLevelText.text = $"Level UP! → {currentGameResult.newLevel}";
                    newLevelText.gameObject.SetActive(true);
                    newLevelText.color = Color.yellow;
                    Debug.Log($"[MultiGameResultModal] 레벨업 표시: {currentGameResult.newLevel}");
                }
                else
                {
                    newLevelText.text = $"Level {currentGameResult.newLevel}";
                    newLevelText.gameObject.SetActive(true);
                    newLevelText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// 새로운 데이터로 플레이어 순위 목록 업데이트
        /// </summary>
        private void UpdatePlayerRankingsWithNewData()
        {
            if (currentGameResult == null || currentGameResult.scores == null) return;

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

            // scores 딕셔너리에서 빈 슬롯 플레이어만 필터링 및 점수 순 정렬
            // 개선: 0점 플레이어도 포함 (게임 중 탈주한 플레이어 데이터 보존)
            var validPlayers = currentGameResult.scores
                .Where(kvp => !networkManager.IsEmptySlotPlayer(kvp.Key)) // 빈 슬롯만 제외
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            Debug.Log($"[MultiGameResultModal] 유효한 플레이어 수: {validPlayers.Count} (전체: {currentGameResult.scores.Count})");

            // GameRoomPanel에서 플레이어 탈주 정보 가져오기
            var gameRoomPanel = FindObjectOfType<GameRoomPanel>();

            // 순위별로 아이템 생성
            for (int i = 0; i < validPlayers.Count; i++)
            {
                var playerScore = validPlayers[i];
                int rank = i + 1;
                bool isMe = (rank == currentGameResult.myRank); // 내 순위와 비교
                bool isWinner = currentGameResult.winners != null &&
                               currentGameResult.winners.Contains(playerScore.Key);

                // user_name을 display_name으로 변환
                string displayName = networkManager.GetPlayerDisplayName(playerScore.Key);

                // 탈주 여부 확인 (GameRoomPanel의 username 기반 확인)
                bool isDisconnected = false;
                if (gameRoomPanel != null)
                {
                    // GAME_RESULT의 username(playerScore.Key)으로 직접 확인
                    // 최대 4명이므로 선형 탐색 성능 문제 없음
                    isDisconnected = gameRoomPanel.IsPlayerDisconnectedByUsername(playerScore.Key);
                }

                Debug.Log($"[MultiGameResultModal] 플레이어 순위 생성: {rank}등 - {playerScore.Key} → {displayName} ({playerScore.Value}점, 탈주={isDisconnected})");

                CreatePlayerRankingItemWithNewData(rank, displayName, playerScore.Value, isMe, isWinner, isDisconnected);
            }
        }

        /// <summary>
        /// 새로운 데이터로 플레이어 순위 아이템 생성
        /// </summary>
        private void CreatePlayerRankingItemWithNewData(int rank, string playerName, int score, bool isMe, bool isWinner, bool isDisconnected = false)
        {
            var itemObj = Instantiate(playerRankingItemPrefab, playerRankingParent);
            itemObj.SetActive(true);

            // 순위 텍스트
            var rankText = itemObj.transform.Find("RankText")?.GetComponent<TextMeshProUGUI>();
            if (rankText != null)
            {
                rankText.text = $"{rank}";
                rankText.color = rank == 1 ? Color.yellow :
                                rank == 2 ? Color.cyan :
                                rank == 3 ? Color.green : Color.white;
            }

            // 플레이어 이름 (탈주 표시 포함)
            var nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = playerName;

                // 탈주 상태 표시
                if (isDisconnected)
                {
                    nameText.text += " (탈주)";
                    nameText.color = new Color(0.7f, 0.7f, 0.7f, 1f); // 회색
                }
                else if (isMe)
                {
                    nameText.text += " (나)";
                    nameText.color = Color.yellow;
                }
                else
                {
                    nameText.color = Color.white;
                }
            }

            // 점수
            var scoreText = itemObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            if (scoreText != null)
            {
                scoreText.text = $"{score}점";
            }

            // 플레이어 색상 표시 (기본 색상으로 설정)
            var colorImage = itemObj.transform.Find("ColorImage")?.GetComponent<Image>();
            if (colorImage != null)
            {
                // rank에 따라 색상 할당 (임시)
                MultiModels.PlayerColor playerColor = (MultiModels.PlayerColor)(rank % 4 + 1);
                colorImage.color = GetPlayerColor(playerColor);
            }
        }

        /// <summary>
        /// 새로운 데이터로 게임 통계 정보 업데이트 (정상 게임 종료)
        /// </summary>
        private void UpdateGameStatsWithNewData()
        {
            if (currentGameResult == null) return;

            if (gameDurationText != null)
            {
                int minutes = currentGameResult.gameTime / 60;
                int seconds = currentGameResult.gameTime % 60;
                gameDurationText.text = $"게임 시간: {minutes:D2}:{seconds:D2}";
            }

            if (totalBlocksPlacedText != null)
            {
                // 유효한 플레이어들의 점수 합계만 계산 (빈 슬롯 제외)
                int totalScore = 0;
                if (currentGameResult.scores != null)
                {
                    totalScore = currentGameResult.scores
                        .Where(kvp => !networkManager.IsEmptySlotPlayer(kvp.Key) && kvp.Value > 0)
                        .Sum(kvp => kvp.Value);
                }
                totalBlocksPlacedText.text = $"전체 점수 합계: {totalScore}점";
            }

            if (gameResultSummaryText != null)
            {
                // 정상 게임 종료
                if (currentGameResult.winners != null && currentGameResult.winners.Length > 0)
                {
                    string winnerUserName = currentGameResult.winners[0];
                    string winnerDisplayName = networkManager.GetPlayerDisplayName(winnerUserName);
                    gameResultSummaryText.text = $"🏆 {winnerDisplayName}님이 승리했습니다!";
                    gameResultSummaryText.color = Color.white;
                }
                else
                {
                    gameResultSummaryText.text = "게임이 종료되었습니다.";
                    gameResultSummaryText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// 게임 통계 정보 업데이트 (Fallback - 탈주로 인한 종료)
        /// </summary>
        private void UpdateGameStats()
        {
            // 탈주로 인한 게임 종료 여부 확인
            bool endedByDisconnection = IsGameEndedByDisconnection();

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
                if (endedByDisconnection)
                {
                    // 탈주로 인한 게임 종료
                    gameResultSummaryText.text = GetDisconnectionMessage();
                    gameResultSummaryText.color = new Color(0.9f, 0.7f, 0.3f, 1f); // 주황색
                }
                else
                {
                    // 탈주 없는데 GAME_RESULT가 안 온 경우
                    gameResultSummaryText.text = "게임이 종료되었습니다.";
                    gameResultSummaryText.color = Color.white;
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
            currentGameResult = null;
        }
        
        /// <summary>
        /// 로비로 돌아가기 버튼 클릭
        /// </summary>
        private void OnToLobbyClicked()
        {
            Debug.Log("[MultiGameResultModal] 로비로 돌아가기 요청");

            // 모달 닫기
            HideModal();

            // GameRoomPanel을 통해 적절한 정리 과정을 거쳐 방 나가기
            var gameRoomPanel = FindObjectOfType<GameRoomPanel>();
            if (gameRoomPanel != null)
            {
                Debug.Log("[MultiGameResultModal] GameRoomPanel을 통해 방 나가기 처리");
                // GameRoomPanel의 비공개 메서드를 호출할 수 없으므로 리플렉션 사용
                var method = gameRoomPanel.GetType().GetMethod("OnLeaveRoomConfirmed",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(gameRoomPanel, null);
                    Debug.Log("[MultiGameResultModal] GameRoomPanel.OnLeaveRoomConfirmed() 호출 완료");
                }
                else
                {
                    Debug.LogError("[MultiGameResultModal] GameRoomPanel.OnLeaveRoomConfirmed() 메서드를 찾을 수 없습니다");
                    // 폴백: 직접 방 나가기
                    FallbackLeaveRoom();
                }
            }
            else
            {
                Debug.LogWarning("[MultiGameResultModal] GameRoomPanel을 찾을 수 없습니다. 직접 방 나가기 처리");
                // 폴백: 직접 방 나가기
                FallbackLeaveRoom();
            }
        }

        /// <summary>
        /// 폴백: 직접 방 나가기 (GameRoomPanel을 찾을 수 없을 때)
        /// </summary>
        private void FallbackLeaveRoom()
        {
            Debug.Log("[MultiGameResultModal] 폴백: 직접 방 나가기 처리");
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