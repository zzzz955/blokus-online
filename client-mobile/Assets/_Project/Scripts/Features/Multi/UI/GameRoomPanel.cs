using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Features.Multi.Net;
using Features.Multi.Core;
using GameResult = Features.Multi.Core.GameResult;
using Features.Multi.Models;
using NetRoomInfo = Features.Multi.Net.RoomInfo;
using TMPro;
using Shared.UI;

namespace Features.Multi.UI
{
    /// <summary>
    /// 게임방 패널 - Qt GameRoomWindow와 동일한 기능
    /// 4개 플레이어 슬롯, 게임보드, 채팅, 게임 진행 관리
    /// </summary>
    public class GameRoomPanel : MonoBehaviour
    {
        [Header("Room Info Panel")]
        [SerializeField] private TextMeshProUGUI roomNameLabel;
        [SerializeField] private TextMeshProUGUI roomStatusLabel;
        [SerializeField] private TextMeshProUGUI currentTurnLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaveRoomButton;

        [Header("Timer Panel")]
        [SerializeField] private GameObject timerPanel;
        [SerializeField] private TextMeshProUGUI timerLabel;
        [SerializeField] private Slider timerProgressBar;

        [Header("Player Slots (4개 고정)")]
        [SerializeField] private PlayerSlotWidget[] playerSlots = new PlayerSlotWidget[4];

        [Header("Game Area")]
        [SerializeField] private GameBoard gameBoard;
        [SerializeField] private MyBlockPalette blockPalette;

        [Header("Chat Panel")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private TextMeshProUGUI chatDisplay;
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private Button chatSendButton;

        [Header("Controls Panel")]
        [SerializeField] private Button gameStartButton;
        [SerializeField] private TextMeshProUGUI gameStatusLabel;
        [SerializeField] private TextMeshProUGUI coordinateLabel;
        
        [Header("Modals")]
        [SerializeField] private ConfirmModal leaveRoomConfirmModal;

        // Dependencies
        private NetworkManager networkManager;
        private MultiUserDataCache dataCache;
        private GameLogic gameLogic;

        // Game State
        private NetRoomInfo currentRoom;
        private PlayerSlot[] playerData = new PlayerSlot[4];
        private bool isGameStarted = false;
        private bool isMyTurn = false;
        private bool isReady = false;
        private PlayerColor myPlayerColor = PlayerColor.None;
        private int currentTurnPlayerId = -1;
        
        // Chat
        private List<ChatMessage> chatHistory = new List<ChatMessage>();
        
        // Timer
        private float turnTimeLimit = 60f;
        private float remainingTime = 0f;
        private bool isTimerActive = false;

        // ========================================
        // Lifecycle
        // ========================================

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            UpdateTurnTimer();
            
            // Android 뒤로가기 버튼 처리
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 모달이 활성화된 경우 무시 (모달에서 처리)
                if (leaveRoomConfirmModal != null && leaveRoomConfirmModal.gameObject.activeInHierarchy)
                    return;
                
                // 방 나가기 확인 모달 표시
                OnLeaveRoomButtonClicked();
            }
        }

        void OnDestroy()
        {
            Cleanup();
        }

        // ========================================
        // Initialization
        // ========================================

        private void Initialize()
        {
            FindDependencies();
            SetupUI();
            SubscribeToEvents();
            InitializePlayerSlots();
            
            Debug.Log("[GameRoomPanel] Initialized successfully");
        }

        private void FindDependencies()
        {
            networkManager = NetworkManager.Instance;
            dataCache = MultiUserDataCache.Instance;

            if (networkManager == null)
                Debug.LogError("[GameRoomPanel] NetworkManager not found!");

            if (dataCache == null)
                Debug.LogError("[GameRoomPanel] MultiUserDataCache not found!");
        }

        private void SetupUI()
        {
            // 타이머 패널 초기 비활성화
            if (timerPanel != null)
                timerPanel.SetActive(false);

            // 버튼 이벤트 연결
            if (leaveRoomButton != null)
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);

            if (gameStartButton != null)
                gameStartButton.onClick.AddListener(OnGameStartButtonClicked);

            if (chatSendButton != null)
                chatSendButton.onClick.AddListener(OnChatSendButtonClicked);

            if (chatInput != null)
                chatInput.onEndEdit.AddListener(OnChatInputEndEdit);

            // 게임보드 초기 비활성화
            if (gameBoard != null)
            {
                gameBoard.SetInteractable(false);
            }

            if (blockPalette != null)
            {
                blockPalette.SetInteractable(false);
            }

            UpdateGameControlsState();
        }

        private void InitializePlayerSlots()
        {
            for (int i = 0; i < 4; i++)
            {
                if (playerSlots[i] != null)
                {
                    PlayerColor color = (PlayerColor)(i + 1);
                    PlayerSlot emptySlot = PlayerSlot.Empty;
                    playerSlots[i].Initialize(emptySlot);
                    
                    // 빈 슬롯으로 초기화 (Empty 사용)
                    playerData[i] = emptySlot;
                    playerSlots[i].UpdateSlot(emptySlot);
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnRoomInfoUpdated += OnRoomInfoUpdated;
                networkManager.OnPlayerJoined += OnPlayerJoined;
                networkManager.OnPlayerLeft += OnPlayerLeft;
                networkManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                networkManager.OnGameStarted += OnGameStarted;
                networkManager.OnTurnChanged += OnTurnChanged;
                networkManager.OnBlockPlaced += OnBlockPlaced;
                networkManager.OnGameEnded += OnGameEnded;
                networkManager.OnChatMessage += OnChatMessageReceived;
                networkManager.OnErrorReceived += OnErrorReceived;
            }

            // 게임보드 이벤트
            if (gameBoard != null)
            {
                gameBoard.OnCellClicked += OnGameBoardCellClicked;
                gameBoard.OnBlockPlaced += OnGameBoardBlockPlaced;
            }

            // 블록 팔레트 이벤트
            if (blockPalette != null)
            {
                blockPalette.OnBlockSelected += OnBlockSelected;
            }
        }

        private void Cleanup()
        {
            if (networkManager != null)
            {
                networkManager.OnRoomInfoUpdated -= OnRoomInfoUpdated;
                networkManager.OnPlayerJoined -= OnPlayerJoined;
                networkManager.OnPlayerLeft -= OnPlayerLeft;
                networkManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                networkManager.OnGameStarted -= OnGameStarted;
                networkManager.OnTurnChanged -= OnTurnChanged;
                networkManager.OnBlockPlaced -= OnBlockPlaced;
                networkManager.OnGameEnded -= OnGameEnded;
                networkManager.OnChatMessage -= OnChatMessageReceived;
                networkManager.OnErrorReceived -= OnErrorReceived;
            }

            if (gameBoard != null)
            {
                gameBoard.OnCellClicked -= OnGameBoardCellClicked;
                gameBoard.OnBlockPlaced -= OnGameBoardBlockPlaced;
            }

            if (blockPalette != null)
            {
                blockPalette.OnBlockSelected -= OnBlockSelected;
            }
        }

        // ========================================
        // UI Updates
        // ========================================

        private void UpdateRoomInfo()
        {
            if (currentRoom == null) return;

            if (roomNameLabel != null)
                roomNameLabel.text = $"🏠 {currentRoom.roomName}";

            if (roomStatusLabel != null)
            {
                string status = isGameStarted ? "게임 중" : "대기 중";
                roomStatusLabel.text = status;
            }
        }

        private void UpdateCurrentTurnDisplay()
        {
            if (currentTurnLabel == null) return;

            if (!isGameStarted)
            {
                currentTurnLabel.text = "";
                return;
            }

            if (currentTurnPlayerId >= 0 && currentTurnPlayerId < 4)
            {
                PlayerSlot currentPlayer = playerData[currentTurnPlayerId];
                if (!currentPlayer.isEmpty)
                {
                    string colorName = GetPlayerColorName((PlayerColor)(currentTurnPlayerId + 1));
                    currentTurnLabel.text = $"현재 턴: {colorName} ({currentPlayer.displayName})";
                    
                    if (isMyTurn)
                        currentTurnLabel.color = Color.yellow;
                    else
                        currentTurnLabel.color = Color.white;
                }
            }
        }

        private void UpdateGameControlsState()
        {
            if (gameStartButton == null) return;

            bool isHost = IsHost();
            bool canStart = isHost && !isGameStarted && AllPlayersReady();

            if (isGameStarted)
            {
                gameStartButton.gameObject.SetActive(false);
            }
            else if (isHost)
            {
                gameStartButton.gameObject.SetActive(true);
                gameStartButton.interactable = canStart;
                gameStartButton.GetComponentInChildren<TextMeshProUGUI>().text = "게임 시작";
            }
            else
            {
                gameStartButton.gameObject.SetActive(true);
                gameStartButton.interactable = true;
                gameStartButton.GetComponentInChildren<TextMeshProUGUI>().text = isReady ? "준비 해제" : "준비 완료";
            }

            // 게임 상태 라벨 업데이트
            if (gameStatusLabel != null)
            {
                if (isGameStarted)
                {
                    if (isMyTurn)
                        gameStatusLabel.text = "당신의 턴입니다";
                    else
                        gameStatusLabel.text = "상대방의 턴입니다";
                }
                else
                {
                    gameStatusLabel.text = "게임 대기 중";
                }
            }
        }

        private void UpdateTurnTimer()
        {
            if (!isTimerActive || !isGameStarted) return;

            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0)
            {
                remainingTime = 0;
                OnTurnTimeout();
            }

            // UI 업데이트
            if (timerLabel != null)
            {
                timerLabel.text = $"남은 시간: {Mathf.CeilToInt(remainingTime)}초";
            }

            if (timerProgressBar != null)
            {
                float progress = remainingTime / turnTimeLimit;
                timerProgressBar.value = progress;
            }
        }

        private void UpdatePlayerSlotHighlight()
        {
            for (int i = 0; i < 4; i++)
            {
                if (playerSlots[i] != null)
                {
                    bool isCurrentTurn = (i == currentTurnPlayerId);
                    playerSlots[i].SetTurnHighlight(isCurrentTurn);
                }
            }
        }

        // ========================================
        // Event Handlers
        // ========================================

        private void OnLeaveRoomButtonClicked()
        {
            Debug.Log("[GameRoomPanel] Leave room button clicked");
            
            // 방 나가기 확인 모달 표시
            if (leaveRoomConfirmModal != null)
            {
                string message = isGameStarted 
                    ? "게임이 진행 중입니다.\n정말로 방에서 나가시겠습니까?\n게임이 중단될 수 있습니다."
                    : "정말로 방에서 나가시겠습니까?";
                    
                leaveRoomConfirmModal.ShowModal(
                    "방 나가기",
                    message,
                    OnLeaveRoomConfirmed,
                    null
                );
            }
            else
            {
                Debug.LogError("[GameRoomPanel] leaveRoomConfirmModal이 설정되지 않았습니다!");
                OnLeaveRoomConfirmed(); // 폴백: 바로 방 나가기
            }
        }

        private void OnSettingsButtonClicked()
        {
            Debug.Log("[GameRoomPanel] Settings button clicked");
            // TODO: 설정 창 열기
        }

        private void OnGameStartButtonClicked()
        {
            if (networkManager == null) return;

            if (IsHost())
            {
                // 호스트: 게임 시작
                networkManager.StartGame();
            }
            else
            {
                // 플레이어: 준비 상태 토글
                isReady = !isReady;
                networkManager.SetPlayerReady(isReady);
                UpdateGameControlsState();
            }
        }

        private void OnChatSendButtonClicked()
        {
            SendChatMessage();
        }

        private void OnChatInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SendChatMessage();
            }
        }

        // ========================================
        // Network Event Handlers
        // ========================================

        private void OnRoomInfoUpdated(NetRoomInfo roomInfo)
        {
            currentRoom = roomInfo;
            UpdateRoomInfo();
        }

        private void OnPlayerJoined(UserInfo player)
        {
            // UserInfo를 PlayerSlot으로 변환 (Stub 구현)
            Debug.Log($"[GameRoomPanel] 플레이어 참가: {player.displayName}");
            
            // 빈 슬롯 찾아서 할당 (Stub)
            for (int i = 0; i < 4; i++)
            {
                if (playerData[i].isEmpty)
                {
                    PlayerSlot newSlot = new PlayerSlot
                    {
                        playerId = i + 1,
                        playerName = player.displayName,
                        isReady = false,
                        isHost = false,
                        colorIndex = i
                    };
                    
                    playerData[i] = newSlot;
                    if (playerSlots[i] != null)
                    {
                        playerSlots[i].UpdateSlot(newSlot);
                    }
                    break;
                }
            }

            UpdateGameControlsState();
        }

        private void OnPlayerLeft(int playerId)
        {
            int slotIndex = playerId - 1;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                // 빈 슬롯으로 설정 (Empty 사용)
                PlayerSlot emptySlot = PlayerSlot.Empty;
                playerData[slotIndex] = emptySlot;
                
                if (playerSlots[slotIndex] != null)
                {
                    playerSlots[slotIndex].UpdateSlot(emptySlot);
                    playerSlots[slotIndex].SetAsMySlot(false);
                }
            }

            UpdateGameControlsState();
        }

        private void OnPlayerReadyChanged(int playerId, bool isReady)
        {
            int slotIndex = playerId - 1;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                playerData[slotIndex].isReady = isReady;
                if (playerSlots[slotIndex] != null)
                {
                    playerSlots[slotIndex].UpdateReadyState(isReady);
                }
            }

            UpdateGameControlsState();
        }

        private void OnGameStarted()
        {
            isGameStarted = true;
            
            // 타이머 패널 활성화
            if (timerPanel != null)
                timerPanel.SetActive(true);

            // 게임보드 활성화
            if (gameBoard != null)
                gameBoard.SetInteractable(true);

            UpdateGameControlsState();
            ShowMessage("게임이 시작되었습니다!");
        }

        private void OnTurnChanged(PlayerColor currentPlayer)
        {
            currentTurnPlayerId = (int)currentPlayer;
            turnTimeLimit = 30.0f; // Default turn time limit
            remainingTime = turnTimeLimit;
            
            isMyTurn = (currentPlayer == myPlayerColor);
            isTimerActive = true;

            // 내 턴일 때만 블록 팔레트 활성화
            if (blockPalette != null)
                blockPalette.SetInteractable(isMyTurn);

            UpdateCurrentTurnDisplay();
            UpdatePlayerSlotHighlight();
        }

        private void OnBlockPlaced(BlockPlacement placement)
        {
            // 게임보드에 블록 배치 반영
            if (gameBoard != null)
            {
                gameBoard.PlaceBlock(placement.position, placement.playerId, placement.occupiedCells);
            }

            // 플레이어 점수 및 블록 수 업데이트
            int slotIndex = (int)placement.playerColor;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                // TODO: 점수 및 남은 블록 수 계산 로직
                // playerData[slotIndex].score = ...
                // playerData[slotIndex].remainingBlocks = ...
                
                if (playerSlots[slotIndex] != null)
                {
                    playerSlots[slotIndex].UpdateSlot(playerData[slotIndex]);
                }
            }
        }

        private void OnGameEnded(PlayerColor winner)
        {
            isGameStarted = false;
            isTimerActive = false;
            
            // 타이머 패널 비활성화
            if (timerPanel != null)
                timerPanel.SetActive(false);

            // 게임보드/팔레트 비활성화
            if (gameBoard != null)
                gameBoard.SetInteractable(false);

            if (blockPalette != null)
                blockPalette.SetInteractable(false);

            UpdateGameControlsState();
            
            // TODO: 게임 결과 다이얼로그 표시
            ShowMessage($"게임이 종료되었습니다. 승자: {winner}");
        }

        private void OnChatMessageReceived(string message)
        {
            // Convert string to ChatMessage object
            ChatMessage chatMsg = new ChatMessage("Unknown", message, "Unknown");
            chatHistory.Add(chatMsg);
            UpdateChatDisplay();
        }

        private void OnErrorReceived(string error)
        {
            ShowMessage($"오류: {error}");
        }

        // ========================================
        // Game Event Handlers
        // ========================================

        private void OnGameBoardCellClicked(Vector2Int position)
        {
            if (coordinateLabel != null)
                coordinateLabel.text = $"위치: ({position.x}, {position.y})";
        }

        private void OnGameBoardBlockPlaced(BlockPlacement placement)
        {
            // 서버에 블록 배치 전송
            if (networkManager != null)
            {
                networkManager.PlaceBlock(placement);
            }
        }

        private void OnBlockSelected(BlockItem blockItem)
        {
            // 선택된 블록을 게임보드에 표시
            if (gameBoard != null)
            {
                gameBoard.SetSelectedBlock((BlockType)blockItem.id);
            }
        }

        private void OnTurnTimeout()
        {
            isTimerActive = false;
            
            if (isMyTurn)
            {
                // 내 턴에서 시간 초과 - 패스 처리
                if (networkManager != null)
                {
                    networkManager.PassTurn();
                }
                ShowMessage("시간 초과로 턴을 넘깁니다.");
            }
        }

        // ========================================
        // Helper Methods
        // ========================================

        private void SendChatMessage()
        {
            if (chatInput == null || networkManager == null) return;

            string message = chatInput.text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            networkManager.SendChatMessage(message);
            chatInput.text = "";
        }

        private void UpdateChatDisplay()
        {
            if (chatDisplay == null) return;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (ChatMessage msg in chatHistory)
            {
                string timestamp = msg.timestamp.ToString("HH:mm");
                sb.AppendLine($"[{timestamp}] {msg.displayName}: {msg.message}");
            }

            chatDisplay.text = sb.ToString();

            // 스크롤을 맨 아래로
            if (chatScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void ShowMessage(string message)
        {
            Debug.Log($"[GameRoomPanel] {message}");
            // TODO: Toast 메시지 표시
        }

        private bool IsHost()
        {
            if (myPlayerColor == PlayerColor.None) return false;
            
            int mySlotIndex = (int)myPlayerColor;
            if (mySlotIndex >= 0 && mySlotIndex < 4)
            {
                return playerData[mySlotIndex].isHost;
            }
            
            return false;
        }

        private bool AllPlayersReady()
        {
            int playerCount = 0;
            int readyCount = 0;

            for (int i = 0; i < 4; i++)
            {
                if (!playerData[i].isEmpty)
                {
                    playerCount++;
                    if (playerData[i].isReady || playerData[i].isHost)
                    {
                        readyCount++;
                    }
                }
            }

            return playerCount >= 2 && playerCount == readyCount;
        }

        private string GetPlayerColorName(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Blue: return "파랑";
                case PlayerColor.Yellow: return "노랑";
                case PlayerColor.Red: return "빨강";
                case PlayerColor.Green: return "초록";
                default: return "없음";
            }
        }

        // ========================================
        // Public API (for PlayerSlotWidget)
        // ========================================

        public void KickPlayer(PlayerColor color)
        {
            if (IsHost() && networkManager != null)
            {
                networkManager.KickPlayer((int)color);
            }
        }
        
        // ========================================
        // Additional Helper Methods
        // ========================================
        
        /// <summary>
        /// 방 나가기 확인 후 실제 처리
        /// </summary>
        private void OnLeaveRoomConfirmed()
        {
            Debug.Log("[GameRoomPanel] 방 나가기 확인됨 - 방 퇴장 시작");
            
            // TCP 서버로 방 나가기 메시지 전송
            if (networkManager != null && currentRoom != null)
            {
                networkManager.LeaveRoom();
                Debug.Log("[GameRoomPanel] 방 나가기 메시지 전송 완료");
            }
            else
            {
                Debug.LogError("[GameRoomPanel] NetworkManager 또는 currentRoom이 null입니다!");
                // 폴백: 직접 로비로 이동
                ReturnToLobby();
            }
        }
        
        /// <summary>
        /// 로비로 복귀
        /// </summary>
        private void ReturnToLobby()
        {
            Debug.Log("[GameRoomPanel] 로비로 복귀");
            
            // 게임 상태 초기화
            isGameStarted = false;
            isMyTurn = false;
            isReady = false;
            myPlayerColor = PlayerColor.None;
            
            // 데이터 정리
            if (dataCache != null)
            {
                // 방 관련 데이터만 정리 (로그아웃이 아니므로)
                Debug.Log("[GameRoomPanel] 방 데이터 정리 완료");
            }
            
            // MultiGameplaySceneController를 통해 로비로 전환
            var sceneController = GetComponentInParent<MultiGameplaySceneController>();
            if (sceneController != null)
            {
                sceneController.ShowLobby();
                Debug.Log("[GameRoomPanel] 로비로 전환 요청 완료");
            }
            else
            {
                Debug.LogError("[GameRoomPanel] SceneController를 찾을 수 없습니다!");
            }
        }
        
    }
}