using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Features.Multi.Net;
using Features.Multi.Core;
using GameResult = Features.Multi.Core.GameResult;
using NetRoomInfo = Features.Multi.Net.RoomInfo;
using TMPro;
using Shared.UI;
using Shared.Models;
using SharedPlayerColor = Shared.Models.PlayerColor;
using SharedPosition = Shared.Models.Position;
using SharedBlock = Shared.Models.Block;
using SharedUserInfo = Shared.Models.UserInfo;
using SharedBlockPlacement = Shared.Models.BlockPlacement;
using MultiPlayerColor = Features.Multi.Models.PlayerColor;
using MultiBlockPlacement = Features.Multi.Models.BlockPlacement;
using MultiChatMessage = Features.Multi.Models.ChatMessage;
using SharedGameLogic = App.Core.GameLogic;
using SharedFlipState = Shared.Models.FlipState;
using NetUserInfo = Features.Multi.Net.UserInfo;

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
        // MultiUserDataCache 제거됨 - NetworkManager 직접 사용
        private SharedGameLogic gameLogic;

        // Game State
        private NetRoomInfo currentRoom;
        private PlayerSlot[] playerData = new PlayerSlot[4];
        private bool isGameStarted = false;
        private bool isMyTurn = false;
        private bool isReady = false;
        private MultiPlayerColor myPlayerColor = MultiPlayerColor.None;
        private SharedPlayerColor mySharedPlayerColor = SharedPlayerColor.None; // Shared.Models 버전
        private int currentTurnPlayerId = -1;
        
        // Chat
        private List<MultiChatMessage> chatHistory = new List<MultiChatMessage>();
        
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
            
            // 현재 방 정보 로드 (방에 이미 입장한 상태에서 패널이 활성화되는 경우)
            LoadCurrentRoomInfo();
            
            Debug.Log("[GameRoomPanel] Initialized successfully");
        }

        private void FindDependencies()
        {
            networkManager = NetworkManager.Instance;

            if (networkManager == null)
                Debug.LogError("[GameRoomPanel] NetworkManager not found!");

            // GameLogic 초기화
            if (gameLogic == null)
                gameLogic = new SharedGameLogic();
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

            // 게임보드와 블록 팔레트 초기화 및 이벤트 연결
            if (gameBoard != null)
            {
                gameBoard.SetInteractable(false);
                gameBoard.SetGameLogic(gameLogic);
                gameBoard.OnCellClicked += OnGameBoardCellClicked;
                gameBoard.OnBlockPlaced += OnGameBoardBlockPlaced;
            }

            if (blockPalette != null)
            {
                blockPalette.SetInteractable(false);
                blockPalette.OnBlockSelected += OnBlockSelected;
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
                networkManager.OnChatMessageReceived += OnChatMessageReceived;
                networkManager.OnErrorReceived += OnErrorReceived;
            }

            // 게임보드와 블록 팔레트 이벤트는 SetupUI에서 연결됨
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
                networkManager.OnChatMessageReceived -= OnChatMessageReceived;
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
                roomNameLabel.text = $"{currentRoom.roomName}";

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

        /// <summary>
        /// 플레이어 슬롯 UI 업데이트 (NetworkManager 데이터 기반)
        /// </summary>
        private void UpdatePlayerSlots()
        {
            Debug.Log("[GameRoomPanel] 플레이어 슬롯 업데이트 시작");
            
            // 현재 방 정보 가져오기
            if (networkManager?.CurrentRoomInfo != null)
            {
                var roomInfo = networkManager.CurrentRoomInfo;
                var currentUser = networkManager.CurrentUserInfo;
                
                Debug.Log($"[GameRoomPanel] 방 정보: {roomInfo.roomName}, 플레이어 수: {roomInfo.currentPlayers}/{roomInfo.maxPlayers}");
                
                // 기존 슬롯을 모두 빈 슬롯으로 초기화
                for (int i = 0; i < 4; i++)
                {
                    PlayerSlot emptySlot = PlayerSlot.Empty;
                    playerData[i] = emptySlot;
                    if (playerSlots[i] != null)
                    {
                        playerSlots[i].UpdateSlot(emptySlot);
                    }
                }
                
                // 현재 사용자 정보로 첫 번째 슬롯 설정 (임시 구현)
                if (currentUser != null)
                {
                    PlayerSlot userSlot = new PlayerSlot
                    {
                        playerId = 1, // 임시 ID
                        playerName = currentUser.displayName,
                        isReady = false,
                        isHost = true, // 현재 사용자를 호스트로 가정
                        colorIndex = 0,
                        currentScore = 0,
                        remainingBlocks = 21
                    };
                    
                    playerData[0] = userSlot;
                    if (playerSlots[0] != null)
                    {
                        playerSlots[0].SetPlayerData(userSlot, true);
                        Debug.Log($"[GameRoomPanel] 슬롯 0 업데이트: {userSlot.playerName} (Host: {userSlot.isHost})");
                    }
                    
                    // 호스트 이름 업데이트
                    if (roomInfo.hostName != currentUser.displayName)
                    {
                        roomInfo.hostName = currentUser.displayName;
                    }
                }
                
                // TODO: 실제 서버에서 플레이어 목록을 받아오면 여기서 처리
                // 현재는 방 정보에 플레이어 목록이 없으므로 임시로 현재 사용자만 표시
                
                Debug.Log($"[GameRoomPanel] 플레이어 슬롯 업데이트 완료 - 임시로 현재 사용자만 표시");
            }
            else
            {
                Debug.LogWarning("[GameRoomPanel] NetworkManager 방 정보가 null입니다.");
                
                // 폴백: 모든 슬롯을 빈 상태로 설정
                for (int i = 0; i < 4; i++)
                {
                    PlayerSlot emptySlot = PlayerSlot.Empty;
                    playerData[i] = emptySlot;
                    if (playerSlots[i] != null)
                    {
                        playerSlots[i].UpdateSlot(emptySlot);
                    }
                }
            }
        }

        /// <summary>
        /// 현재 사용자가 호스트인지 확인 (임시 구현)
        /// </summary>
        private bool IsCurrentUserHost()
        {
            // TODO: 실제 호스트 여부 확인 로직 구현
            // 현재는 방에 들어온 사용자를 호스트로 간주
            return true;
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
            Debug.Log($"[GameRoomPanel] leaveRoomConfirmModal null 여부: {leaveRoomConfirmModal == null}");
            
            // 방 나가기 확인 모달 표시
            if (leaveRoomConfirmModal != null)
            {
                string message = isGameStarted 
                    ? "게임이 진행 중입니다.\n정말로 방에서 나가시겠습니까?\n게임이 중단될 수 있습니다."
                    : "정말로 방에서 나가시겠습니까?";
                    
                Debug.Log($"[GameRoomPanel] 모달 표시 시도: {message}");
                leaveRoomConfirmModal.ShowModal(
                    "방 나가기",
                    message,
                    OnLeaveRoomConfirmed,
                    null
                );
                Debug.Log("[GameRoomPanel] ShowModal 호출 완료");
            }
            else
            {
                Debug.LogError("[GameRoomPanel] leaveRoomConfirmModal이 설정되지 않았습니다!");
                Debug.LogError("[GameRoomPanel] Inspector에서 Leave Room Confirm Modal을 할당해주세요.");
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

        /// <summary>
        /// NetworkManager에서 현재 방 정보를 가져와서 UI 업데이트
        /// </summary>
        private void LoadCurrentRoomInfo()
        {
            if (networkManager?.CurrentRoomInfo != null)
            {
                currentRoom = networkManager.CurrentRoomInfo;
                Debug.Log($"[GameRoomPanel] NetworkManager에서 방 정보 로드: {currentRoom.roomName} [ID: {currentRoom.roomId}]");
                UpdateRoomInfo();
                UpdatePlayerSlots(); // 플레이어 슬롯도 업데이트
            }
            else
            {
                Debug.LogWarning("[GameRoomPanel] NetworkManager에서 방 정보를 찾을 수 없습니다.");
            }
        }

        private void OnPlayerJoined(NetUserInfo player)
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
            {
                gameBoard.SetInteractable(true);
                gameBoard.ResetBoard(); // 게임 시작 시 보드 리셋
            }

            // 블록 팔레트 초기화 (내 플레이어 색상으로)
            if (blockPalette != null)
            {
                blockPalette.InitializePalette(mySharedPlayerColor);
                blockPalette.SetInteractable(false); // 첫 턴이 아니면 비활성화
            }

            UpdateGameControlsState();
            ShowMessage("게임이 시작되었습니다!");
        }

        private void OnTurnChanged(MultiPlayerColor currentPlayer)
        {
            currentTurnPlayerId = (int)currentPlayer;
            turnTimeLimit = 30.0f; // Default turn time limit
            remainingTime = turnTimeLimit;
            
            // MultiPlayerColor를 SharedPlayerColor로 변환
            SharedPlayerColor sharedCurrentPlayer = ConvertToSharedPlayerColor(currentPlayer);
            isMyTurn = (sharedCurrentPlayer == mySharedPlayerColor);
            isTimerActive = true;

            // 게임보드와 블록 팔레트 턴 상태 업데이트
            if (gameBoard != null)
                gameBoard.SetMyTurn(isMyTurn, mySharedPlayerColor);

            if (blockPalette != null)
            {
                blockPalette.SetMyTurn(isMyTurn, mySharedPlayerColor);
                blockPalette.SetInteractable(isMyTurn);
            }

            UpdateCurrentTurnDisplay();
            UpdatePlayerSlotHighlight();
            
            Debug.Log($"[GameRoomPanel] 턴 변경: Current={currentPlayer}, isMyTurn={isMyTurn}, myColor={mySharedPlayerColor}");
        }

        private void OnBlockPlaced(MultiBlockPlacement placement)
        {
            // TODO: BlockPlacement를 Shared.Models 구조로 변환 필요
            // 현재는 Features.Multi.Models.BlockPlacement를 사용하고 있음
            
            // 게임보드에 블록 배치 반영 - 임시 구현
            if (gameBoard != null)
            {
                var position = new SharedPosition(placement.position.x, placement.position.y);
                var occupiedCells = new List<SharedPosition>();
                
                foreach (var cell in placement.occupiedCells)
                {
                    occupiedCells.Add(new SharedPosition(cell.x, cell.y));
                }
                
                gameBoard.PlaceBlock(position, placement.playerId, occupiedCells);
            }

            // 블록 팔레트에서 사용된 블록 표시 (내 플레이어인 경우)
            if (blockPalette != null && placement.playerId == (int)mySharedPlayerColor)
            {
                blockPalette.MarkBlockAsUsed(ConvertToSharedBlockType(placement.blockType));
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

        private void OnGameEnded(MultiPlayerColor winner)
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

        private void OnChatMessageReceived(string username, string displayName, string message)
        {
            // Convert to ChatMessage object
            MultiChatMessage chatMsg = new MultiChatMessage(username, message, displayName);
            chatHistory.Add(chatMsg);
            Debug.Log($"[GameRoomPanel] 채팅 메시지 수신: {displayName} [{username}]: {message}");
            UpdateChatDisplay();
        }

        private void OnErrorReceived(string error)
        {
            ShowMessage($"오류: {error}");
        }

        // ========================================
        // Game Event Handlers
        // ========================================

        private void OnGameBoardCellClicked(SharedPosition position)
        {
            if (coordinateLabel != null)
                coordinateLabel.text = $"위치: ({position.row}, {position.col})";

            // 블록이 선택된 상태에서 보드 클릭 시 미리보기 표시
            if (blockPalette != null && gameBoard != null)
            {
                var selectedBlock = blockPalette.GetSelectedBlock();
                if (selectedBlock != null && isMyTurn)
                {
                    gameBoard.SetTouchPreview(selectedBlock, position);
                }
            }
        }

        private void OnGameBoardBlockPlaced(SharedBlock block, SharedPosition position)
        {
            // 서버에 블록 배치 전송 - Shared.Models를 Features.Multi.Models로 변환 필요
            if (networkManager != null && isMyTurn)
            {
                Debug.Log($"[GameRoomPanel] 블록 배치 시도: {block.Type} at ({position.row}, {position.col})");
                
                // TODO: Shared.Models → Features.Multi.Models 변환 로직 구현
                // 현재는 간단한 구현으로 대체
                var placement = new MultiBlockPlacement(
                    (int)mySharedPlayerColor,
                    ConvertToMultiBlockType(block.Type),
                    new Vector2Int(position.row, position.col),
                    (int)block.CurrentRotation,
                    block.CurrentFlipState == SharedFlipState.Horizontal
                );
                
                networkManager.PlaceBlock(placement);
            }
        }

        private void OnBlockSelected(SharedBlock block)
        {
            Debug.Log($"[GameRoomPanel] 블록 선택됨: {block.Type}");
            // 선택된 블록을 게임보드에 알림 - 추가 처리는 OnGameBoardCellClicked에서 수행
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
            foreach (MultiChatMessage msg in chatHistory)
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
            if (myPlayerColor.Equals(SharedPlayerColor.None)) return false;
            
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

        public void KickPlayer(MultiPlayerColor color)
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
            myPlayerColor = MultiPlayerColor.None;
            mySharedPlayerColor = SharedPlayerColor.None;
            
            // 게임보드와 블록 팔레트 정리
            if (gameBoard != null)
            {
                gameBoard.ResetBoard();
                gameBoard.SetInteractable(false);
            }
            
            if (blockPalette != null)
            {
                blockPalette.ResetPalette();
                blockPalette.SetInteractable(false);
            }
            
            // 데이터 정리 - MultiUserDataCache 제거로 더 이상 필요 없음
            Debug.Log("[GameRoomPanel] 방 데이터 정리 완료 - NetworkManager 이벤트 기반으로 관리됨");
            
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

        // ========================================
        // Helper Methods - 타입 변환 유틸리티
        // ========================================

        /// <summary>
        /// Features.Multi.Models.PlayerColor → Shared.Models.PlayerColor 변환
        /// </summary>
        private SharedPlayerColor ConvertToSharedPlayerColor(MultiPlayerColor multiColor)
        {
            return multiColor switch
            {
                MultiPlayerColor.Red => SharedPlayerColor.Red,
                MultiPlayerColor.Blue => SharedPlayerColor.Blue,
                MultiPlayerColor.Yellow => SharedPlayerColor.Yellow,
                MultiPlayerColor.Green => SharedPlayerColor.Green,
                _ => SharedPlayerColor.None
            };
        }

        /// <summary>
        /// Shared.Models.BlockType → Features.Multi.Models.BlockType 변환 (임시)
        /// </summary>
        private Features.Multi.Models.BlockType ConvertToMultiBlockType(Shared.Models.BlockType sharedType)
        {
            // TODO: 실제 매핑 구현 - 현재는 이름 기반으로 변환
            return sharedType.ToString() switch
            {
                "Single" => Features.Multi.Models.BlockType.Single,
                "Domino" => Features.Multi.Models.BlockType.Domino,
                "TriominoI" => Features.Multi.Models.BlockType.TriominoI,
                "TriominoL" => Features.Multi.Models.BlockType.TriominoL,
                _ => Features.Multi.Models.BlockType.Single
            };
        }

        /// <summary>
        /// Features.Multi.Models.BlockType → Shared.Models.BlockType 변환 (임시)
        /// </summary>
        private Shared.Models.BlockType ConvertToSharedBlockType(Features.Multi.Models.BlockType multiType)
        {
            // TODO: 실제 매핑 구현 - 현재는 이름 기반으로 변환
            return multiType.ToString() switch
            {
                "Single" => Shared.Models.BlockType.Single,
                "Domino" => Shared.Models.BlockType.Domino,
                "TriominoI" => Shared.Models.BlockType.TrioLine,
                "TriominoL" => Shared.Models.BlockType.TrioAngle,
                _ => Shared.Models.BlockType.Single
            };
        }
        
    }
}