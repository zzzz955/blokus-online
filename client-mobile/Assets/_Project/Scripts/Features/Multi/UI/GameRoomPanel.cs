using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Features.Multi.Net;
using GameStateData = Features.Multi.Net.GameStateData;
using Features.Multi.Core;
using TurnChangeInfo = Features.Multi.Net.TurnChangeInfo;
using PlayerData = Features.Multi.Net.PlayerData;
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
        
        // Host Status Tracking
        private bool isCurrentUserRoomHost = false; // 방 생성 시 true, ROOM_INFO 수신 시 정확한 서버 데이터로 업데이트
        
        // Event Subscription Tracking (중복 구독 방지)
        private bool isEventsSubscribed = false;
        
        // Chat
        private List<MultiChatMessage> chatHistory = new List<MultiChatMessage>();
        
        // Timer
        private float turnTimeLimit = 60f;
        private float remainingTime = 0f;
        private bool isTimerActive = false;
        
        // Board State Synchronization
        private int[,] previousBoardState = null;

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
            
            // 방 입장 시 게임 보드 및 팔레트 초기 설정
            InitializeGameComponents();
            
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

            // 게임보드와 블록 팔레트 이벤트 연결 (초기화는 InitializeGameComponents에서 처리)
            if (gameBoard != null)
            {
                gameBoard.OnCellClicked += OnGameBoardCellClicked;
                gameBoard.OnBlockPlaced += OnGameBoardBlockPlaced;
            }

            if (blockPalette != null)
            {
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
            if (networkManager != null && !isEventsSubscribed)
            {
                networkManager.OnRoomCreated += OnRoomCreated; // 방 생성 시 호스트 상태 설정
                networkManager.OnRoomInfoUpdated += OnRoomInfoUpdated;
                networkManager.OnPlayerJoined += OnPlayerJoined;
                networkManager.OnPlayerLeft += OnPlayerLeft;
                networkManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                networkManager.OnGameStarted += OnGameStarted;
                networkManager.OnGameStateUpdate += OnGameStateUpdate;
                networkManager.OnTurnChanged += OnTurnChanged;
                networkManager.OnBlockPlaced += OnBlockPlaced;
                networkManager.OnGameEnded += OnGameEnded;
                networkManager.OnChatMessageReceived += OnChatMessageReceived;
                networkManager.OnErrorReceived += OnErrorReceived;
                
                isEventsSubscribed = true;
                Debug.Log("[GameRoomPanel] 이벤트 구독 완료 (중복 방지)");
            }
            else if (isEventsSubscribed)
            {
                Debug.Log("[GameRoomPanel] 이미 이벤트 구독됨 - 중복 방지");
            }

            // 게임보드와 블록 팔레트 이벤트는 SetupUI에서 연결됨
        }

        private void Cleanup()
        {
            // 상태 리셋
            isCurrentUserRoomHost = false;
            myPlayerColor = MultiPlayerColor.None;
            mySharedPlayerColor = SharedPlayerColor.None;
            isReady = false;
            isEventsSubscribed = false; // 이벤트 구독 상태 리셋
            
            if (networkManager != null)
            {
                networkManager.OnRoomCreated -= OnRoomCreated;
                networkManager.OnRoomInfoUpdated -= OnRoomInfoUpdated;
                networkManager.OnPlayerJoined -= OnPlayerJoined;
                networkManager.OnPlayerLeft -= OnPlayerLeft;
                networkManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                networkManager.OnGameStarted -= OnGameStarted;
                networkManager.OnGameStateUpdate -= OnGameStateUpdate;
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

        /// <summary>
        /// 방 입장 시 게임 컴포넌트 초기화 (1회)
        /// 게임 로직 설정, 보드 초기 상태 설정, 팔레트 초기화
        /// </summary>
        private void InitializeGameComponents()
        {
            Debug.Log("[GameRoomPanel] 게임 컴포넌트 초기화 시작 (방 입장)");
            
            // 게임보드 초기 설정
            if (gameBoard != null)
            {
                gameBoard.SetGameLogic(gameLogic);
                gameBoard.ResetBoard(); // 빈 보드로 초기화
                gameBoard.SetInteractable(false); // 게임 시작 전에는 비활성화
                Debug.Log("[GameRoomPanel] 게임보드 초기화 완료");
            }
            else
            {
                Debug.LogError("[GameRoomPanel] GameBoard가 할당되지 않았습니다!");
            }

            // 블록 팔레트 초기 설정 (색상 미정 상태)
            if (blockPalette != null)
            {
                blockPalette.SetInteractable(false); // 게임 시작 전에는 비활성화
                Debug.Log("[GameRoomPanel] 블록 팔레트 초기화 완료");
            }
            else
            {
                Debug.LogError("[GameRoomPanel] BlockPalette가 할당되지 않았습니다!");
            }

            // 타이머 패널 초기 상태
            if (timerPanel != null)
            {
                timerPanel.SetActive(false);
                isTimerActive = false;
                Debug.Log("[GameRoomPanel] 타이머 패널 초기화 완료");
            }

            Debug.Log("[GameRoomPanel] 게임 컴포넌트 초기화 완료");
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
            
            Debug.Log($"[GameRoomPanel] UpdateGameControlsState - isHost: {isHost}, isGameStarted: {isGameStarted}, canStart: {canStart}");

            if (isGameStarted)
            {
                gameStartButton.gameObject.SetActive(false);
                Debug.Log("[GameRoomPanel] 게임 시작됨 - GameStartButton 숨김");
            }
            else if (isHost)
            {
                gameStartButton.gameObject.SetActive(true);
                gameStartButton.interactable = canStart;
                gameStartButton.GetComponentInChildren<TextMeshProUGUI>().text = "게임 시작";
                Debug.Log($"[GameRoomPanel] 호스트 모드 - 버튼: '게임 시작', 활성화: {canStart}");
            }
            else
            {
                gameStartButton.gameObject.SetActive(true);
                gameStartButton.interactable = true;
                string buttonText = isReady ? "준비 해제" : "준비 완료";
                gameStartButton.GetComponentInChildren<TextMeshProUGUI>().text = buttonText;
                Debug.Log($"[GameRoomPanel] 플레이어 모드 - 버튼: '{buttonText}', 활성화: true");
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
        /// 플레이어 데이터 업데이트 (MessageHandler에서 호출)
        /// </summary>
        public void UpdatePlayerData(System.Collections.Generic.List<Features.Multi.Net.PlayerData> playerDataList)
        {
            Debug.Log($"[GameRoomPanel] 플레이어 데이터 업데이트: {playerDataList.Count}명");
            
            // 받은 플레이어 데이터 목록 출력
            for (int i = 0; i < playerDataList.Count; i++)
            {
                var player = playerDataList[i];
                Debug.Log($"[GameRoomPanel] 플레이어 {i}: {player.displayName} [{player.username}] (Host: {player.isHost}, Ready: {player.isReady}, ColorSlot: {player.colorSlot})");
            }
            
            // PlayerSlots 배열 상태 확인
            Debug.Log($"[GameRoomPanel] PlayerSlots 배열 상태:");
            for (int i = 0; i < 4; i++)
            {
                Debug.Log($"  - playerSlots[{i}]: {(playerSlots[i] != null ? "할당됨" : "NULL")}");
            }
            
            // 현재 사용자 정보 확인
            var currentUser = networkManager?.CurrentUserInfo;
            if (currentUser == null)
            {
                Debug.LogWarning("[GameRoomPanel] 현재 사용자 정보가 없습니다.");
                return;
            }
            
            // 현재 사용자가 호스트인지 확인
            bool isCurrentUserHost = false;
            foreach (var player in playerDataList)
            {
                if (player.username == currentUser.username)
                {
                    isCurrentUserHost = player.isHost;
                    break;
                }
            }
            
            // 모든 슬롯을 빈 상태로 초기화
            for (int i = 0; i < 4; i++)
            {
                playerData[i] = PlayerSlot.Empty;
                if (playerSlots[i] != null)
                {
                    playerSlots[i].UpdateSlot(PlayerSlot.Empty);
                    playerSlots[i].SetAsMySlot(false); // 초기화 시 본인 표시 해제
                }
                else
                {
                    Debug.LogError($"[GameRoomPanel] playerSlots[{i}]이 null입니다! Unity Inspector에서 PlayerSlotWidget을 할당하세요.");
                }
            }
            
            // 플레이어 데이터를 슬롯에 배치
            foreach (var player in playerDataList)
            {
                // 서버 색상 순서: 파(1), 노(2), 빨(3), 초(4)
                // 클라이언트 enum: Red(0), Blue(1), Yellow(2), Green(3)
                int slotIndex = ConvertServerColorSlotToClientIndex(player.colorSlot);
                Debug.Log($"[GameRoomPanel] 플레이어 '{player.displayName}' - 서버 colorSlot: {player.colorSlot} → 클라이언트 slotIndex: {slotIndex}");
                
                if (slotIndex >= 0 && slotIndex < 4)
                {
                    PlayerSlot slot = new PlayerSlot
                    {
                        playerId = player.playerId,
                        playerName = player.displayName,
                        isReady = player.isReady,
                        isHost = player.isHost,
                        colorIndex = slotIndex,
                        currentScore = 0,
                        remainingBlocks = 21
                    };
                    
                    playerData[slotIndex] = slot;
                    if (playerSlots[slotIndex] != null)
                    {
                        bool isCurrentUser = player.username == currentUser.username;
                        Debug.Log($"[GameRoomPanel] PlayerSlotWidget[{slotIndex}].SetPlayerData() 호출");
                        playerSlots[slotIndex].SetPlayerData(slot, isCurrentUserHost);
                        
                        // 본인 여부에 따라 Bold 처리 및 식별 이미지 설정
                        playerSlots[slotIndex].SetAsMySlot(isCurrentUser);
                        Debug.Log($"[GameRoomPanel] PlayerSlotWidget[{slotIndex}].SetAsMySlot({isCurrentUser}) 호출");
                    }
                    else
                    {
                        Debug.LogError($"[GameRoomPanel] playerSlots[{slotIndex}]이 null이므로 SetPlayerData() 호출 불가!");
                    }
                    
                    // 현재 사용자의 색상과 호스트 상태 업데이트
                    if (player.username == currentUser.username)
                    {
                        // slotIndex는 이미 올바른 클라이언트 배열 인덱스로 변환됨
                        myPlayerColor = (MultiPlayerColor)slotIndex; // MultiPlayerColor enum은 0-based
                        mySharedPlayerColor = ConvertServerColorSlotToSharedPlayerColor(player.colorSlot);
                        isReady = player.isReady;
                        
                        Debug.Log($"[GameRoomPanel] 내 정보: 슬롯={slotIndex}, 색상={myPlayerColor}, 호스트={player.isHost}, 레디={player.isReady}");
                        
                        // 플레이어 색상이 확정되면 바로 블록 팔레트 초기화
                        TryInitializeBlockPaletteOnRoomJoin();
                        
                        // ROOM_INFO 데이터가 있으면 서버 데이터를 우선하고 방 생성 플래그는 리셋
                        if (isCurrentUserRoomHost && !player.isHost)
                        {
                            Debug.Log("[GameRoomPanel] 서버 데이터에서 호스트가 아님을 확인 - 방 생성 플래그 리셋");
                            isCurrentUserRoomHost = false;
                        }
                    }
                    
                    Debug.Log($"[GameRoomPanel] 슬롯 {slotIndex} 업데이트 완료: {player.displayName} (Host: {player.isHost}, Ready: {player.isReady})");
                }
                else
                {
                    Debug.LogError($"[GameRoomPanel] 잘못된 colorSlot: {player.colorSlot} (slotIndex: {slotIndex})");
                }
            }
            
            // UI 상태 업데이트
            UpdateGameControlsState();
        }

        /// <summary>
        /// 플레이어 슬롯 UI 업데이트 (레거시 - 임시 구현 유지)
        /// </summary>
        private void UpdatePlayerSlots()
        {
            Debug.Log("[GameRoomPanel] 레거시 플레이어 슬롯 업데이트 (임시 구현)");
            // 이 메서드는 UpdatePlayerData()가 호출되면 더 이상 사용되지 않음
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

        /// <summary>
        /// 방 생성 시 호스트 상태 설정 및 플레이어 슬롯 할당
        /// </summary>
        private void OnRoomCreated(Features.Multi.Net.RoomInfo roomInfo)
        {
            Debug.Log($"[GameRoomPanel] 방 생성됨 - 호스트 상태 설정: {roomInfo.roomName}");
            isCurrentUserRoomHost = true; // 방 생성자는 호스트
            
            // 방 생성자를 첫 번째 슬롯(파란색)에 자동 할당
            AssignRoomCreatorToFirstSlot();
            
            UpdateGameControlsState(); // GameStart 버튼 상태 업데이트
        }

        private void OnRoomInfoUpdated(NetRoomInfo roomInfo, System.Collections.Generic.List<PlayerData> playerDataList)
        {
            Debug.Log($"[GameRoomPanel] 방 정보 업데이트 수신: {roomInfo.roomName}, 플레이어 {playerDataList?.Count ?? 0}명");
            
            currentRoom = roomInfo;
            UpdateRoomInfo();
            
            // 플레이어 슬롯 업데이트 (PlayerData 리스트 사용)
            UpdatePlayerSlotsWithServerData(playerDataList);
        }

        /// <summary>
        /// 서버에서 받은 PlayerData로 플레이어 슬롯 업데이트
        /// </summary>
        private void UpdatePlayerSlotsWithServerData(System.Collections.Generic.List<PlayerData> playerDataList)
        {
            if (playerDataList == null)
            {
                Debug.LogWarning("[GameRoomPanel] 플레이어 데이터 리스트가 null입니다.");
                return;
            }

            Debug.Log($"[GameRoomPanel] 플레이어 슬롯 업데이트 시작 - 총 {playerDataList.Count}명");

            // 모든 슬롯 초기화 (빈 슬롯으로 설정)
            for (int i = 0; i < 4; i++)
            {
                if (playerSlots[i] != null)
                {
                    var emptySlot = PlayerSlot.Empty;
                    this.playerData[i] = emptySlot;
                    playerSlots[i].SetPlayerData(emptySlot);
                }
            }

            // 서버 데이터로 슬롯 업데이트
            foreach (var playerData in playerDataList)
            {
                // 서버의 colorSlot이 1-4이면 0-3으로 변환
                int slotIndex = ConvertServerColorSlotToClientIndex(playerData.colorSlot);
                
                if (slotIndex >= 0 && slotIndex < 4 && playerSlots[slotIndex] != null)
                {
                    // PlayerData를 PlayerSlot으로 변환
                    PlayerSlot slot = new PlayerSlot
                    {
                        playerId = playerData.playerId,
                        playerName = playerData.displayName,
                        isReady = playerData.isReady,
                        isHost = playerData.isHost,
                        colorIndex = slotIndex,
                        currentScore = 0,
                        remainingBlocks = 21
                    };
                    
                    // PlayerData 배열 업데이트
                    this.playerData[slotIndex] = slot;
                    
                    // UI 위젯 업데이트
                    var currentUser = networkManager?.CurrentUserInfo;
                    bool isCurrentUser = currentUser != null && playerData.username == currentUser.username;
                    
                    playerSlots[slotIndex].SetPlayerData(slot, playerData.isHost);
                    playerSlots[slotIndex].SetAsMySlot(isCurrentUser);
                    
                    // 내 플레이어 정보 업데이트 (색상, 준비 상태, 호스트 상태)
                    if (isCurrentUser)
                    {
                        myPlayerColor = (MultiPlayerColor)slotIndex;
                        mySharedPlayerColor = ConvertServerColorSlotToSharedPlayerColor(playerData.colorSlot);
                        isReady = playerData.isReady;
                        
                        // 플레이어 색상이 확정되면 바로 블록 팔레트 초기화
                        TryInitializeBlockPaletteOnRoomJoin();
                        
                        // 호스트 상태 업데이트 (중요: 서버 데이터가 최우선)
                        bool wasHost = isCurrentUserRoomHost;
                        isCurrentUserRoomHost = playerData.isHost;
                        
                        // 호스트 변경 감지 및 로깅
                        if (wasHost != isCurrentUserRoomHost)
                        {
                            Debug.Log($"[GameRoomPanel] 호스트 상태 변경 감지: {wasHost} → {isCurrentUserRoomHost}");
                            if (isCurrentUserRoomHost)
                            {
                                Debug.Log("[GameRoomPanel] 내가 새 호스트가 됨 - UI 강제 업데이트");
                            }
                        }
                        
                        Debug.Log($"[GameRoomPanel] 내 플레이어 정보 업데이트: 색상={myPlayerColor}→{mySharedPlayerColor}, 호스트={isCurrentUserRoomHost}, 준비={isReady}");
                    }
                    
                    Debug.Log($"[GameRoomPanel] 슬롯 {slotIndex} 업데이트: {playerData.displayName} [{playerData.username}] - 호스트={playerData.isHost}, 준비={playerData.isReady}");
                }
                else
                {
                    Debug.LogWarning($"[GameRoomPanel] 잘못된 슬롯 인덱스: {slotIndex} (서버 colorSlot: {playerData.colorSlot})");
                }
            }

            UpdateGameControlsState(); // 게임 시작 버튼 등 상태 업데이트
            Debug.Log($"[GameRoomPanel] 플레이어 슬롯 업데이트 완료");
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
                
                // 방 생성자 확인 및 자동 할당 (타이밍 이슈 해결)
                // NetworkManager에서 ROOM_INFO를 통해 처리하므로 여기서는 호스트 상태만 설정
                if (networkManager.IsCurrentUserRoomCreator)
                {
                    Debug.Log("[GameRoomPanel] 방 생성자 확인됨 - 호스트 상태 설정");
                    isCurrentUserRoomHost = true;
                    UpdateGameControlsState();
                }
                
                // 즉시 마지막 ROOM_INFO 데이터 요청 (이벤트 구독 전에 도착한 메시지 처리)
                Debug.Log("[GameRoomPanel] 마지막 ROOM_INFO 데이터 요청");
                networkManager.RequestLastRoomInfo();
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
            Debug.Log($"[GameRoomPanel] OnPlayerLeft 호출: playerId={playerId}");
            
            int slotIndex = playerId - 1;
            if (slotIndex >= 0 && slotIndex < 4)
            {
                // 나간 플레이어가 호스트였는지 확인
                bool wasHost = playerData[slotIndex].isHost;
                string leavingPlayerName = playerData[slotIndex].playerName;
                
                Debug.Log($"[GameRoomPanel] 플레이어 {leavingPlayerName} 퇴장 - 호스트 여부: {wasHost}");
                
                // 빈 슬롯으로 설정 (Empty 사용)
                PlayerSlot emptySlot = PlayerSlot.Empty;
                playerData[slotIndex] = emptySlot;
                
                if (playerSlots[slotIndex] != null)
                {
                    playerSlots[slotIndex].UpdateSlot(emptySlot);
                    playerSlots[slotIndex].SetAsMySlot(false);
                }
                
                // 호스트가 나간 경우 - 호스트 변경 처리를 위해 ROOM_INFO 재요청
                if (wasHost)
                {
                    Debug.Log("[GameRoomPanel] 호스트가 퇴장함 - ROOM_INFO 재동기화 시작");
                    StartCoroutine(RequestRoomInfoAfterHostLeft());
                }
            }

            UpdateGameControlsState();
        }
        
        /// <summary>
        /// 호스트 퇴장 후 ROOM_INFO 재동기화 (약간의 지연 후 요청)
        /// </summary>
        private System.Collections.IEnumerator RequestRoomInfoAfterHostLeft()
        {
            // 서버에서 호스트 변경 처리 시간 대기
            yield return new WaitForSeconds(0.5f);
            
            if (networkManager != null)
            {
                Debug.Log("[GameRoomPanel] 호스트 변경 후 ROOM_INFO 재요청");
                // NetworkManager에 ROOM_INFO 재요청 (서버 동기화)
                networkManager.RequestRoomInfoSync();
            }
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
            Debug.Log("[GameRoomPanel] 게임 시작됨 - 게임 컴포넌트 재초기화");
            isGameStarted = true;
            
            // 게임 시작 시 이전 상태 완전 클리어 및 재초기화
            ResetGameComponentsForNewGame();
            
            // 게임 시작 시 상호작용 제어 업데이트 (아직 첫 턴이 오기 전이므로 비활성화 상태)
            UpdateTurnBasedInteraction();
            
            UpdateGameControlsState();
            ShowMessage("게임이 시작되었습니다!");
        }

        /// <summary>
        /// 게임 상태 업데이트 처리
        /// 서버로부터 전체 게임 상태를 동기화
        /// </summary>
        private void OnGameStateUpdate(GameStateData gameState)
        {
            Debug.Log($"[GameRoomPanel] 게임 상태 업데이트 수신: currentPlayer={gameState.currentPlayer}, turnNumber={gameState.turnNumber}");
            
            try
            {
                // 현재 플레이어 정보 업데이트 (필요시)
                if (gameState.currentPlayer > 0)
                {
                    Debug.Log($"[GameRoomPanel] 현재 플레이어: {gameState.currentPlayer}, 턴 번호: {gameState.turnNumber}");
                }
                
                // 보드 상태 처리 (필요시)
                if (gameState.boardState != null && gameState.boardState.Length > 0)
                {
                    Debug.Log($"[GameRoomPanel] 보드 상태: {(gameState.boardState.Length == 0 ? "빈 상태 (게임 시작 초기)" : $"데이터 {gameState.boardState.Length}개")}");
                }
                else
                {
                    Debug.Log($"[GameRoomPanel] 보드 상태: 빈 상태 (게임 시작 초기)");
                }
                
                // PlayerSlots 점수 동기화
                UpdatePlayerSlotScores(gameState.scores);
                
                // PlayerSlots 남은 블록 개수 동기화  
                UpdatePlayerSlotRemainingBlocks(gameState.remainingBlocks);
                
                Debug.Log($"[GameRoomPanel] 점수 정보 동기화 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameRoomPanel] 게임 상태 업데이트 처리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 서버 보드 상태와 클라이언트 보드 동기화
        /// 변경된 셀만 찾아서 업데이트
        /// </summary>
        private void SynchronizeBoardState(int[,] serverBoardState)
        {
            try
            {
                if (gameBoard == null)
                {
                    Debug.LogWarning("[GameRoomPanel] GameBoard가 null입니다. 보드 동기화 건너뜀");
                    return;
                }

                const int BOARD_SIZE = 20;
                
                // 서버 보드 상태 유효성 검사
                if (serverBoardState.GetLength(0) != BOARD_SIZE || serverBoardState.GetLength(1) != BOARD_SIZE)
                {
                    Debug.LogError($"[GameRoomPanel] 잘못된 보드 크기: {serverBoardState.GetLength(0)}x{serverBoardState.GetLength(1)} (예상: {BOARD_SIZE}x{BOARD_SIZE})");
                    return;
                }

                List<BoardCellChange> changes = new List<BoardCellChange>();

                // 현재 게임보드 상태를 가져와서 서버 상태와 직접 비교
                for (int row = 0; row < BOARD_SIZE; row++)
                {
                    for (int col = 0; col < BOARD_SIZE; col++)
                    {
                        int serverValue = serverBoardState[row, col];
                        PlayerColor currentBoardValue = gameBoard.GetCellColor(row, col);
                        int currentValue = ConvertPlayerColorToServerValue(currentBoardValue);

                        if (serverValue != currentValue)
                        {
                            changes.Add(new BoardCellChange(row, col, currentValue, serverValue));
                        }
                    }
                }

                Debug.Log($"[GameRoomPanel] 보드 변경사항: {changes.Count}개 셀");

                // 변경된 셀들을 GameBoard에 적용 (변경사항이 없어도 강제 업데이트)
                UpdateBoardCells(changes);

                // 현재 상태를 이전 상태로 저장 (딥 카피)
                SaveCurrentBoardState(serverBoardState);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameRoomPanel] 보드 상태 동기화 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// PlayerColor를 서버 값으로 변환
        /// </summary>
        private int ConvertPlayerColorToServerValue(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.None: return 0;
                case PlayerColor.Blue: return 1;
                case PlayerColor.Yellow: return 2;
                case PlayerColor.Red: return 3;
                case PlayerColor.Green: return 4;
                default: return 0;
            }
        }

        /// <summary>
        /// 변경된 셀들을 GameBoard에 업데이트
        /// </summary>
        private void UpdateBoardCells(List<BoardCellChange> changes)
        {
            foreach (var change in changes)
            {
                // 서버 값을 PlayerColor로 변환 (0=빈 셀, 1-4=플레이어 색상)
                PlayerColor playerColor = ConvertServerValueToPlayerColor(change.newValue);
                
                Debug.Log($"[GameRoomPanel] 셀 업데이트: ({change.row},{change.col}) {change.oldValue}→{change.newValue} (PlayerColor: {playerColor})");
                
                // GameBoard의 public UpdateCell 메서드 호출로 개별 셀 업데이트
                gameBoard.UpdateCell(change.row, change.col, playerColor);
            }
        }

        /// <summary>
        /// 서버 값을 PlayerColor로 변환
        /// </summary>
        private PlayerColor ConvertServerValueToPlayerColor(int serverValue)
        {
            switch (serverValue)
            {
                case 0: return PlayerColor.None;
                case 1: return PlayerColor.Blue;
                case 2: return PlayerColor.Yellow;
                case 3: return PlayerColor.Red;
                case 4: return PlayerColor.Green;
                default:
                    Debug.LogWarning($"[GameRoomPanel] 알 수 없는 서버 값: {serverValue}");
                    return PlayerColor.None;
            }
        }

        /// <summary>
        /// 현재 보드 상태를 이전 상태로 저장 (딥 카피)
        /// </summary>
        private void SaveCurrentBoardState(int[,] currentState)
        {
            const int BOARD_SIZE = 20;
            previousBoardState = new int[BOARD_SIZE, BOARD_SIZE];
            
            for (int row = 0; row < BOARD_SIZE; row++)
            {
                for (int col = 0; col < BOARD_SIZE; col++)
                {
                    previousBoardState[row, col] = currentState[row, col];
                }
            }
        }

        /// <summary>
        /// 게임 시작 시 모든 게임 컴포넌트를 새 게임에 맞게 재초기화
        /// 이전 게임 상태 완전 클리어, 서버 동기화 준비
        /// </summary>
        private void ResetGameComponentsForNewGame()
        {
            Debug.Log("[GameRoomPanel] 새 게임을 위한 컴포넌트 재초기화 시작");

            // 게임 로직 완전 리셋
            if (gameLogic != null)
            {
                gameLogic.ClearBoard();
                Debug.Log("[GameRoomPanel] 게임 로직 클리어 완료");
            }

            // 게임보드 완전 리셋
            if (gameBoard != null)
            {
                gameBoard.ResetBoard(); // 이전 게임의 모든 블록 제거
                Debug.Log("[GameRoomPanel] 게임보드 재초기화 완료");
            }

            // 블록 팔레트 재초기화 (내 색상 확정 후)
            if (blockPalette != null && mySharedPlayerColor != SharedPlayerColor.None)
            {
                blockPalette.InitializePalette(mySharedPlayerColor);
                Debug.Log($"[GameRoomPanel] 블록 팔레트 재초기화 완료 - 색상: {mySharedPlayerColor}");
            }
            else if (mySharedPlayerColor == SharedPlayerColor.None)
            {
                Debug.LogWarning("[GameRoomPanel] 내 플레이어 색상이 설정되지 않아 팔레트 초기화 연기");
            }

            // 타이머 시스템 리셋 및 활성화
            if (timerPanel != null)
            {
                timerPanel.SetActive(true);
                isTimerActive = false; // 첫 턴 시작까지는 비활성화
                remainingTime = 0f;
                
                // 타이머 UI 초기화
                if (timerLabel != null)
                    timerLabel.text = "대기 중...";
                if (timerProgressBar != null)
                    timerProgressBar.value = 1f;
                    
                Debug.Log("[GameRoomPanel] 타이머 시스템 재초기화 완료");
            }

            // 게임 상태 변수 리셋
            isMyTurn = false;
            currentTurnPlayerId = -1;
            
            // 보드 상태 동기화용 이전 상태 리셋
            previousBoardState = null;
            Debug.Log("[GameRoomPanel] 이전 보드 상태 리셋 완료");
            
            Debug.Log("[GameRoomPanel] 새 게임을 위한 컴포넌트 재초기화 완료");
        }

        /// <summary>
        /// 로비 복귀 시 게임 컴포넌트 완전 정리
        /// 모든 게임 상태 리셋, 메모리 정리
        /// </summary>
        private void CleanupGameComponents()
        {
            Debug.Log("[GameRoomPanel] 게임 컴포넌트 정리 시작 (로비 복귀)");

            // 게임 로직 완전 정리
            if (gameLogic != null)
            {
                gameLogic.ClearBoard();
                // 새 게임을 위해 새 인스턴스로 교체
                gameLogic = new SharedGameLogic();
                Debug.Log("[GameRoomPanel] 게임 로직 정리 및 재생성 완료");
            }

            // 게임보드 완전 정리
            if (gameBoard != null)
            {
                gameBoard.ResetBoard();
                gameBoard.SetInteractable(false);
                gameBoard.SetMyTurn(false, SharedPlayerColor.None);
                Debug.Log("[GameRoomPanel] 게임보드 정리 완료");
            }

            // 블록 팔레트 완전 정리
            if (blockPalette != null)
            {
                blockPalette.ResetPalette();
                blockPalette.SetInteractable(false);
                Debug.Log("[GameRoomPanel] 블록 팔레트 정리 완료");
            }

            // 타이머 시스템 완전 정리
            if (timerPanel != null)
            {
                timerPanel.SetActive(false);
                isTimerActive = false;
                remainingTime = 0f;
                turnTimeLimit = 0f;
                Debug.Log("[GameRoomPanel] 타이머 시스템 정리 완료");
            }

            // 게임 상태 변수 완전 초기화
            isGameStarted = false;
            isMyTurn = false;
            currentTurnPlayerId = -1;
            
            // 보드 상태 동기화용 이전 상태 완전 리셋
            previousBoardState = null;
            Debug.Log("[GameRoomPanel] 이전 보드 상태 완전 리셋 완료");

            Debug.Log("[GameRoomPanel] 게임 컴포넌트 정리 완료");
        }

        /// <summary>
        /// 플레이어 색상이 확정된 후 블록 팔레트를 초기화
        /// 방 입장 시점에서 호출되어 21개 블록을 생성
        /// </summary>
        private void TryInitializeBlockPaletteOnRoomJoin()
        {
            // 블록 팔레트와 플레이어 색상이 모두 준비된 경우에만 초기화
            if (blockPalette != null && mySharedPlayerColor != SharedPlayerColor.None)
            {
                try
                {
                    // SharedPlayerColor는 Shared.Models.PlayerColor의 별칭이므로 직접 전달 가능
                    blockPalette.InitializePalette(mySharedPlayerColor);
                    blockPalette.SetInteractable(false); // 게임 시작 전에는 비활성화
                    Debug.Log($"[GameRoomPanel] 방 입장 시 블록 팔레트 초기화 완료 - 색상: {mySharedPlayerColor}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameRoomPanel] 블록 팔레트 초기화 중 오류 발생: {ex.Message}");
                }
            }
            else if (blockPalette == null)
            {
                Debug.LogWarning("[GameRoomPanel] blockPalette가 null이어서 초기화 연기");
            }
            else if (mySharedPlayerColor == SharedPlayerColor.None)
            {
                Debug.LogWarning("[GameRoomPanel] 플레이어 색상이 아직 설정되지 않아 팔레트 초기화 연기");
            }
        }

        private void OnTurnChanged(TurnChangeInfo turnInfo)
        {
            // 서버 색상을 클라이언트 색상으로 변환 (1-4 → 0-3)
            int clientColorIndex = ConvertServerColorSlotToClientIndex(turnInfo.playerColor);
            if (clientColorIndex < 0)
            {
                Debug.LogError($"[GameRoomPanel] 잘못된 서버 색상 값: {turnInfo.playerColor}");
                return;
            }
            
            currentTurnPlayerId = clientColorIndex;
            
            // 서버에서 받은 타이머 정보 사용
            turnTimeLimit = turnInfo.turnTimeSeconds;
            remainingTime = turnInfo.remainingTimeSeconds;
            
            // 내 턴인지 확인 (사용자명으로 비교)
            var currentUser = networkManager?.CurrentUserInfo;
            bool previousTurnState = isMyTurn;
            isMyTurn = currentUser != null && turnInfo.newPlayer == currentUser.username;
            isTimerActive = true;

            // 턴 기반 상호작용 제어 (게임 시작 후에만)
            UpdateTurnBasedInteraction();
            
            // 턴 변경 로그
            if (previousTurnState != isMyTurn)
            {
                Debug.Log($"[GameRoomPanel] 턴 변경: {(isMyTurn ? "내 턴 시작" : "상대 턴 시작")} - 플레이어: {turnInfo.newPlayer}");
            }

            // 이전 턴 타임아웃 알림 처리
            if (turnInfo.previousTurnTimedOut)
            {
                ShowMessage("이전 플레이어의 시간이 초과되어 턴이 넘어왔습니다.");
                Debug.Log("[GameRoomPanel] 이전 턴 타임아웃 알림 표시됨");
            }

            UpdateCurrentTurnDisplay();
            UpdatePlayerSlotHighlight();
            
            Debug.Log($"[GameRoomPanel] 턴 변경 완료: 플레이어={turnInfo.newPlayer}, " +
                     $"색상={turnInfo.playerColor}→{clientColorIndex}, 턴={turnInfo.turnNumber}, " +
                     $"내턴={isMyTurn}, 제한시간={turnTimeLimit}초, 남은시간={remainingTime}초");
        }

        /// <summary>
        /// 게임 상태와 턴 정보에 따른 상호작용 제어
        /// 요구사항 1,2: 게임 시작 전 & 내 턴이 아닐 때 상호작용 비활성화
        /// </summary>
        private void UpdateTurnBasedInteraction()
        {
            // 상호작용 가능 조건: 게임이 시작되었고 && 내 턴일 때
            bool canInteract = isGameStarted && isMyTurn;
            
            // 게임보드 상호작용 제어
            if (gameBoard != null)
            {
                gameBoard.SetInteractable(isGameStarted); // 게임 시작 후에만 보드 활성화
                gameBoard.SetMyTurn(isMyTurn, mySharedPlayerColor);
                Debug.Log($"[GameRoomPanel] 게임보드 상호작용 설정: 게임시작={isGameStarted}, 내턴={isMyTurn}");
            }

            // 블록 팔레트 상호작용 제어  
            if (blockPalette != null)
            {
                blockPalette.SetMyTurn(isMyTurn, mySharedPlayerColor);
                blockPalette.SetInteractable(canInteract); // 게임 시작 && 내 턴일 때만 활성화
                Debug.Log($"[GameRoomPanel] 블록 팔레트 상호작용 설정: 활성화={canInteract}");
            }

            // 상태 로그 출력
            string statusMsg = !isGameStarted ? "게임 시작 대기" : 
                              (isMyTurn ? "내 턴 - 상호작용 가능" : "상대 턴 - 상호작용 불가");
            Debug.Log($"[GameRoomPanel] 상호작용 상태: {statusMsg}");
        }

        private void OnBlockPlaced(MultiBlockPlacement placement)
        {
            // 요구사항 6: 상대방 블록 배치 브로드캐스트 및 보드 동기화 처리
            // placement.playerId는 0-3, mySharedPlayerColor는 1-4이므로 올바른 비교 필요
            bool isMyPlacement = placement.playerId == ((int)mySharedPlayerColor - 1);
            string playerType = isMyPlacement ? "본인" : "상대방";
            
            Debug.Log($"[GameRoomPanel] 블록 배치 확인: playerId={placement.playerId}, myColor={(int)mySharedPlayerColor-1}, isMyPlacement={isMyPlacement}");
            for (int i = 0; i < placement.occupiedCells.Count && i < 10; i++)
            {
                var cell = placement.occupiedCells[i];
                Debug.Log($"    [{i}] Vector2Int({cell.x},{cell.y})");
            }

            try
            {
                // 게임보드에 블록 배치 반영 (본인 및 상대방 모두)
                if (gameBoard != null)
                {
                    var position = new SharedPosition(placement.position.y, placement.position.x); // Vector2Int(x,y) → SharedPosition(y,x) 서버 row/col 매핑
                    var occupiedCells = new List<SharedPosition>();
                    
                    foreach (var cell in placement.occupiedCells)
                    {
                        // 서버 좌표 매핑 수정: Vector2Int(col,row) → SharedPosition(row,col) 
                        // cell.x는 서버의 col값, cell.y는 서버의 row값이므로 순서 바꿔야 함
                        var sharedPos = new SharedPosition(cell.y, cell.x); // row=cell.y, col=cell.x
                        occupiedCells.Add(sharedPos);
                    }
                    
                    gameBoard.PlaceBlock(position, placement.playerId, occupiedCells);
                    Debug.Log($"[GameRoomPanel] {playerType} 블록이 게임보드에 성공적으로 배치됨");

                    for (int i = 0; i < occupiedCells.Count && i < 10; i++)
                    {
                        var cell = occupiedCells[i];
                    }
                    
                    // 로컬 게임 로직 상태 동기화
                    if (gameLogic != null)
                    {
                        var blockPlacement = new SharedBlockPlacement(
                            ConvertToSharedBlockType(placement.blockType),
                            position,
                            (Shared.Models.Rotation)placement.rotation,
                            placement.isFlipped ? Shared.Models.FlipState.Horizontal : Shared.Models.FlipState.Normal,
                            ConvertToSharedPlayerColor(placement.playerColor)
                        );
                        
                        // [DEBUG] 게임 로직 상태 확인
                        var playerColor = ConvertToSharedPlayerColor(placement.playerColor);
                        bool hasPlacedFirstBlock = gameLogic.HasPlayerPlacedFirstBlock(playerColor);
                        Debug.Log($"[GameRoomPanel] 🔍 게임로직 배치 전 상태: {playerColor}, 첫블록배치여부: {hasPlacedFirstBlock}");
                        
                        bool placed = gameLogic.PlaceBlock(blockPlacement);
                        if (placed)
                        {
                            bool hasPlacedFirstBlockAfter = gameLogic.HasPlayerPlacedFirstBlock(playerColor);
                            Debug.Log($"[GameRoomPanel] ✅ 로컬 게임 로직 상태 동기화 완료: {placement.blockType} at ({position.row},{position.col})");
                            Debug.Log($"[GameRoomPanel] 🔍 게임로직 배치 후 상태: {playerColor}, 첫블록배치여부: {hasPlacedFirstBlockAfter}");
                        }
                        else
                        {
                            Debug.LogWarning($"[GameRoomPanel] ❌ 로컬 게임 로직 동기화 실패: {placement.blockType} at ({position.row},{position.col})");
                            Debug.LogWarning($"[GameRoomPanel] 🔍 실패 원인 분석 - 첫블록여부: {hasPlacedFirstBlock}, 플레이어: {playerColor}");
                            
                            // 배치 실패 원인 상세 분석
                            bool canPlace = gameLogic.CanPlaceBlock(blockPlacement);
                            Debug.LogWarning($"[GameRoomPanel] 🔍 CanPlaceBlock 결과: {canPlace}");
                        }
                    }
                }

                // 블록 팔레트에서 사용된 블록 표시 (본인인 경우만)
                if (blockPalette != null && isMyPlacement)
                {
                    var sharedBlockType = ConvertToSharedBlockType(placement.blockType);
                    Debug.Log($"[GameRoomPanel] 내 팔레트에서 블록 제거 시도: {placement.blockType} → {sharedBlockType}");
                    blockPalette.MarkBlockAsUsed(sharedBlockType);
                    Debug.Log($"[GameRoomPanel] 내 팔레트에서 사용된 블록 {placement.blockType} 제거 완료");
                }
                else if (blockPalette == null)
                {
                    Debug.LogWarning($"[GameRoomPanel] blockPalette가 null이어서 블록 제거 불가");
                }
                else if (!isMyPlacement)
                {
                    Debug.Log($"[GameRoomPanel] 상대방 블록 배치 - 내 팔레트는 업데이트하지 않음");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameRoomPanel] {playerType} 블록 배치 처리 중 오류 발생: {ex.Message}");
                ShowMessage($"블록 배치 처리 중 오류가 발생했습니다: {ex.Message}");
                return;
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
            // 요구사항 4: 서버 검증 실패에 대한 구체적인 오류 처리
            Debug.LogError($"[GameRoomPanel] 서버 오류 수신: {error}");
            
            // 블록 배치 관련 오류인 경우 미리보기 상태 정리
            if (error.Contains("placement") || error.Contains("배치") || error.Contains("block"))
            {
                if (gameBoard != null)
                {
                    gameBoard.ClearTouchPreview();
                }
                
                if (error.Contains("invalid") || error.Contains("불가능"))
                {
                    ShowMessage("블록을 해당 위치에 배치할 수 없습니다.");
                }
                else if (error.Contains("occupied") || error.Contains("이미"))
                {
                    ShowMessage("해당 위치는 이미 사용된 공간입니다.");
                }
                else if (error.Contains("turn") || error.Contains("턴"))
                {
                    ShowMessage("당신의 턴이 아닙니다.");
                }
                else
                {
                    ShowMessage($"블록 배치 오류: {error}");
                }
            }
            else
            {
                ShowMessage($"오류: {error}");
            }
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
            // 서버에 블록 배치 전송 - 요구사항 4: 서버 통신 및 검증
            if (networkManager != null && isMyTurn && isGameStarted)
            {
                Debug.Log($"[GameRoomPanel] 블록 배치 시도: {block.Type} at ({position.row}, {position.col})");
                
                try
                {
                    // Shared.Models → Features.Multi.Models 변환
                    var placement = new MultiBlockPlacement(
                        (int)mySharedPlayerColor,
                        ConvertToMultiBlockType(block.Type),
                        new Vector2Int(position.col, position.row), // col=x, row=y로 데스크톱과 통일
                        (int)block.CurrentRotation,
                        block.CurrentFlipState == SharedFlipState.Horizontal
                    );
                    
                    // [DEBUG] 블록의 실제 점유 셀 확인
                    var blockCells = block.GetAbsolutePositions(position);
                    Debug.Log($"  - 클라이언트 블록 점유셀 ({blockCells.Count}개):");
                    for (int i = 0; i < blockCells.Count && i < 10; i++)
                    {
                        Debug.Log($"    [{i}] SharedPosition({blockCells[i].row},{blockCells[i].col})");
                    }
                    
                    // 서버에 배치 요청 전송 - 서버에서 검증 후 OnBlockPlaced로 응답
                    networkManager.PlaceBlock(placement);
                    Debug.Log($"[GameRoomPanel] 서버에 블록 배치 요청 전송: 플레이어={mySharedPlayerColor}, 블록={block.Type}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameRoomPanel] 블록 배치 요청 중 오류 발생: {ex.Message}");
                    ShowMessage("블록 배치 요청 처리 중 오류가 발생했습니다.");
                    
                    // 실패 시 게임보드의 미리보기 상태 정리
                    if (gameBoard != null)
                    {
                        gameBoard.ClearTouchPreview();
                    }
                }
            }
            else if (!isMyTurn)
            {
                Debug.LogWarning("[GameRoomPanel] 내 턴이 아닐 때 블록 배치 시도됨");
                ShowMessage("당신의 턴이 아닙니다.");
            }
            else if (!isGameStarted)
            {
                Debug.LogWarning("[GameRoomPanel] 게임이 시작되지 않았는데 블록 배치 시도됨");
                ShowMessage("게임이 아직 시작되지 않았습니다.");
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
            // 1. ROOM_INFO를 통해 정확한 호스트 정보가 있는 경우 해당 정보 사용
            if (myPlayerColor != MultiPlayerColor.None)
            {
                int mySlotIndex = (int)myPlayerColor;
                if (mySlotIndex >= 0 && mySlotIndex < 4 && !playerData[mySlotIndex].isEmpty)
                {
                    Debug.Log($"[GameRoomPanel] IsHost() - playerData 기반: {playerData[mySlotIndex].isHost} (슬롯: {mySlotIndex})");
                    return playerData[mySlotIndex].isHost;
                }
            }
            
            // 2. ROOM_INFO가 없는 경우 방 생성 상태 기반으로 판단
            Debug.Log($"[GameRoomPanel] IsHost() - 방 생성 상태 기반: {isCurrentUserRoomHost}");
            return isCurrentUserRoomHost;
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

        /// <summary>
        /// 서버 colorSlot을 클라이언트 슬롯 인덱스로 변환
        /// 서버: 파(1), 노(2), 빨(3), 초(4) → 클라이언트: Blue(0), Yellow(1), Red(2), Green(3)
        /// 기획 의도에 따라 파란색을 첫 번째 슬롯(인덱스 0)으로 매핑
        /// </summary>
        private int ConvertServerColorSlotToClientIndex(int serverColorSlot)
        {
            switch (serverColorSlot)
            {
                case 1: return 0; // 파(Blue) → 슬롯[0] (기획 의도: 방 생성자가 첫 번째 슬롯)
                case 2: return 1; // 노(Yellow) → 슬롯[1]  
                case 3: return 2; // 빨(Red) → 슬롯[2]
                case 4: return 3; // 초(Green) → 슬롯[3]
                default: 
                    Debug.LogError($"[GameRoomPanel] 잘못된 서버 colorSlot: {serverColorSlot}");
                    return -1;
            }
        }

        /// <summary>
        /// 서버 colorSlot을 SharedPlayerColor로 변환
        /// </summary>
        private SharedPlayerColor ConvertServerColorSlotToSharedPlayerColor(int serverColorSlot)
        {
            switch (serverColorSlot)
            {
                case 1: return SharedPlayerColor.Blue;   // 파(1) → Blue
                case 2: return SharedPlayerColor.Yellow; // 노(2) → Yellow
                case 3: return SharedPlayerColor.Red;    // 빨(3) → Red
                case 4: return SharedPlayerColor.Green;  // 초(4) → Green
                default: 
                    Debug.LogError($"[GameRoomPanel] 잘못된 서버 colorSlot: {serverColorSlot}");
                    return SharedPlayerColor.None;
            }
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

        /// <summary>
        /// 방 생성자를 첫 번째 슬롯(파란색)에 자동 할당
        /// </summary>
        private void AssignRoomCreatorToFirstSlot()
        {
            var currentUser = networkManager?.CurrentUserInfo;
            if (currentUser == null)
            {
                Debug.LogWarning("[GameRoomPanel] 현재 사용자 정보가 없어 방 생성자 슬롯 할당 불가");
                return;
            }

            // 첫 번째 슬롯(파란색, 인덱스 0)에 방 생성자 할당
            int slotIndex = 0; // Blue color slot
            PlayerSlot hostSlot = new PlayerSlot
            {
                playerId = 1, // 방 생성자는 ID 1
                playerName = currentUser.displayName,
                isReady = false, // 호스트는 초기에 준비 상태가 아님
                isHost = true,
                colorIndex = slotIndex,
                currentScore = 0,
                remainingBlocks = 21
            };

            // PlayerData 배열에 저장
            playerData[slotIndex] = hostSlot;
            
            // UI 위젯 업데이트
            if (playerSlots[slotIndex] != null)
            {
                playerSlots[slotIndex].SetPlayerData(hostSlot, true); // 본인 슬롯으로 설정
                Debug.Log($"[GameRoomPanel] 방 생성자 '{currentUser.displayName}' 슬롯 {slotIndex}(파란색)에 할당 완료");
            }
            else
            {
                Debug.LogError($"[GameRoomPanel] playerSlots[{slotIndex}]이 null이므로 방 생성자 슬롯 할당 실패");
            }

            // 내 플레이어 색상 정보 업데이트
            myPlayerColor = (MultiPlayerColor)slotIndex;
            mySharedPlayerColor = SharedPlayerColor.Blue;
            
            Debug.Log($"[GameRoomPanel] 방 생성자 색상 설정: myPlayerColor={myPlayerColor}, mySharedPlayerColor={mySharedPlayerColor}");
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
            if (networkManager != null)
            {
                // currentRoom이 null이어도 LeaveRoom 호출 가능 - NetworkManager가 연결 상태 관리
                networkManager.LeaveRoom();
                Debug.Log("[GameRoomPanel] 방 나가기 메시지 전송 완료");
                
                // 즉시 UI 정리 시작 (서버 응답 대기 안 함)
                ReturnToLobby();
            }
            else
            {
                Debug.LogError("[GameRoomPanel] NetworkManager가 null입니다!");
                // NetworkManager 없어도 UI는 정리
                ReturnToLobby();
            }
        }
        
        /// <summary>
        /// PlayerSlots 점수 동기화 (GAME_STATE_UPDATE 기반)
        /// </summary>
        private void UpdatePlayerSlotScores(object scoresObj)
        {
            if (scoresObj == null)
            {
                Debug.Log("[GameRoomPanel] 점수 정보 없음 - 스킵");
                return;
            }
            
            try
            {
                System.Collections.Generic.Dictionary<string, int> scoresDict;
                
                // Newtonsoft.Json.Linq.JObject인 경우 처리
                if (scoresObj is Newtonsoft.Json.Linq.JObject jObj)
                {
                    scoresDict = jObj.ToObject<System.Collections.Generic.Dictionary<string, int>>();
                }
                else
                {
                    // 다른 형태인 경우 JSON 문자열로 변환 후 파싱
                    var scoresJson = Newtonsoft.Json.JsonConvert.SerializeObject(scoresObj);
                    scoresDict = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, int>>(scoresJson);
                }
                
                if (scoresDict != null)
                {
                    Debug.Log($"[GameRoomPanel] 점수 동기화 시작: {scoresDict.Count}명");
                    
                    foreach (var scoreEntry in scoresDict)
                    {
                        // 서버 색상 (1-4) → 클라이언트 인덱스 (0-3) 변환
                        if (int.TryParse(scoreEntry.Key, out int serverColor) && serverColor >= 1 && serverColor <= 4)
                        {
                            int clientIndex = serverColor - 1;
                            int score = scoreEntry.Value;
                            
                            // 해당 슬롯이 비어있지 않고 PlayerSlotWidget이 존재하는 경우만 업데이트
                            if (clientIndex >= 0 && clientIndex < 4 && 
                                playerSlots[clientIndex] != null && 
                                !playerData[clientIndex].isEmpty)
                            {
                                playerSlots[clientIndex].UpdateScore(score);
                                Debug.Log($"[GameRoomPanel] 플레이어 슬롯 {clientIndex} 점수 업데이트: {score}점");
                            }
                            else if (playerData[clientIndex].isEmpty)
                            {
                                Debug.Log($"[GameRoomPanel] 슬롯 {clientIndex} 빈 슬롯이므로 점수 업데이트 스킵");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[GameRoomPanel] 점수 Dictionary 파싱 실패");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameRoomPanel] 점수 동기화 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// PlayerSlots 남은 블록 개수 동기화 (GAME_STATE_UPDATE 기반)
        /// </summary>
        private void UpdatePlayerSlotRemainingBlocks(object remainingBlocksObj)
        {
            if (remainingBlocksObj == null)
            {
                Debug.Log("[GameRoomPanel] 남은 블록 정보 없음 - 스킵");
                return;
            }
            
            try
            {
                System.Collections.Generic.Dictionary<string, int> blocksDict;
                
                // Newtonsoft.Json.Linq.JObject인 경우 처리
                if (remainingBlocksObj is Newtonsoft.Json.Linq.JObject jObj)
                {
                    blocksDict = jObj.ToObject<System.Collections.Generic.Dictionary<string, int>>();
                }
                else
                {
                    // 다른 형태인 경우 JSON 문자열로 변환 후 파싱
                    var blocksJson = Newtonsoft.Json.JsonConvert.SerializeObject(remainingBlocksObj);
                    blocksDict = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, int>>(blocksJson);
                }
                
                if (blocksDict != null)
                {
                    Debug.Log($"[GameRoomPanel] 남은 블록 동기화 시작: {blocksDict.Count}명");
                    
                    foreach (var blockEntry in blocksDict)
                    {
                        // 서버 색상 (1-4) → 클라이언트 인덱스 (0-3) 변환
                        if (int.TryParse(blockEntry.Key, out int serverColor) && serverColor >= 1 && serverColor <= 4)
                        {
                            int clientIndex = serverColor - 1;
                            int remainingBlocks = blockEntry.Value;
                            
                            // 해당 슬롯이 비어있지 않고 PlayerSlotWidget이 존재하는 경우만 업데이트
                            if (clientIndex >= 0 && clientIndex < 4 && 
                                playerSlots[clientIndex] != null && 
                                !playerData[clientIndex].isEmpty)
                            {
                                playerSlots[clientIndex].UpdateRemainingBlocks(remainingBlocks);
                                Debug.Log($"[GameRoomPanel] 플레이어 슬롯 {clientIndex} 남은 블록 업데이트: {remainingBlocks}개");
                            }
                            else if (playerData[clientIndex].isEmpty)
                            {
                                Debug.Log($"[GameRoomPanel] 슬롯 {clientIndex} 빈 슬롯이므로 남은 블록 업데이트 스킵");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[GameRoomPanel] 남은 블록 Dictionary 파싱 실패");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameRoomPanel] 남은 블록 동기화 중 오류: {ex.Message}");
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
            
            // 게임 컴포넌트 완전 정리
            CleanupGameComponents();
            
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
                // 상위 컴포넌트에서 찾을 수 없으면 전체 씬에서 검색
                sceneController = FindObjectOfType<MultiGameplaySceneController>();
                if (sceneController != null)
                {
                    sceneController.ShowLobby();
                    Debug.Log("[GameRoomPanel] 전체 씬 검색으로 SceneController 찾음 - 로비로 전환 완료");
                }
                else
                {
                    Debug.LogError("[GameRoomPanel] SceneController를 찾을 수 없습니다!");
                    Debug.Log("[GameRoomPanel] 수동으로 GameRoom 패널을 비활성화하고 UI 정리");
                    
                    // 폴백: 최소한 현재 GameRoom 패널 비활성화
                    if (gameObject != null)
                    {
                        gameObject.SetActive(false);
                        Debug.Log("[GameRoomPanel] GameRoom 패널 비활성화 완료");
                    }
                }
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
            // 요구사항 5: 완전한 블록 타입 매핑으로 정확한 블록 제거
            return sharedType switch
            {
                // 1-2칸 블록
                Shared.Models.BlockType.Single => Features.Multi.Models.BlockType.Single,
                Shared.Models.BlockType.Domino => Features.Multi.Models.BlockType.Domino,
                
                // 3칸 블록
                Shared.Models.BlockType.TrioLine => Features.Multi.Models.BlockType.TrioLine,
                Shared.Models.BlockType.TrioAngle => Features.Multi.Models.BlockType.TrioAngle,
                
                // 4칸 블록 (테트로미노)
                Shared.Models.BlockType.Tetro_I => Features.Multi.Models.BlockType.Tetro_I,
                Shared.Models.BlockType.Tetro_O => Features.Multi.Models.BlockType.Tetro_O,
                Shared.Models.BlockType.Tetro_T => Features.Multi.Models.BlockType.Tetro_T,
                Shared.Models.BlockType.Tetro_L => Features.Multi.Models.BlockType.Tetro_L,
                Shared.Models.BlockType.Tetro_S => Features.Multi.Models.BlockType.Tetro_S,
                
                // 5칸 블록 (펜토미노)
                Shared.Models.BlockType.Pento_F => Features.Multi.Models.BlockType.Pento_F,
                Shared.Models.BlockType.Pento_I => Features.Multi.Models.BlockType.Pento_I,
                Shared.Models.BlockType.Pento_L => Features.Multi.Models.BlockType.Pento_L,
                Shared.Models.BlockType.Pento_N => Features.Multi.Models.BlockType.Pento_N,
                Shared.Models.BlockType.Pento_P => Features.Multi.Models.BlockType.Pento_P,
                Shared.Models.BlockType.Pento_T => Features.Multi.Models.BlockType.Pento_T,
                Shared.Models.BlockType.Pento_U => Features.Multi.Models.BlockType.Pento_U,
                Shared.Models.BlockType.Pento_V => Features.Multi.Models.BlockType.Pento_V,
                Shared.Models.BlockType.Pento_W => Features.Multi.Models.BlockType.Pento_W,
                Shared.Models.BlockType.Pento_X => Features.Multi.Models.BlockType.Pento_X,
                Shared.Models.BlockType.Pento_Y => Features.Multi.Models.BlockType.Pento_Y,
                Shared.Models.BlockType.Pento_Z => Features.Multi.Models.BlockType.Pento_Z,
                
                _ => Features.Multi.Models.BlockType.Single
            };
        }

        /// <summary>
        /// Features.Multi.Models.BlockType → Shared.Models.BlockType 변환 (임시)
        /// </summary>
        private Shared.Models.BlockType ConvertToSharedBlockType(Features.Multi.Models.BlockType multiType)
        {
            // 요구사항 5: 완전한 블록 타입 매핑으로 정확한 블록 제거
            return multiType switch
            {
                // 1-2칸 블록
                Features.Multi.Models.BlockType.Single => Shared.Models.BlockType.Single,
                Features.Multi.Models.BlockType.Domino => Shared.Models.BlockType.Domino,
                
                // 3칸 블록
                Features.Multi.Models.BlockType.TrioLine => Shared.Models.BlockType.TrioLine,
                Features.Multi.Models.BlockType.TrioAngle => Shared.Models.BlockType.TrioAngle,
                
                // 4칸 블록 (테트로미노)
                Features.Multi.Models.BlockType.Tetro_I => Shared.Models.BlockType.Tetro_I,
                Features.Multi.Models.BlockType.Tetro_O => Shared.Models.BlockType.Tetro_O,
                Features.Multi.Models.BlockType.Tetro_T => Shared.Models.BlockType.Tetro_T,
                Features.Multi.Models.BlockType.Tetro_L => Shared.Models.BlockType.Tetro_L,
                Features.Multi.Models.BlockType.Tetro_S => Shared.Models.BlockType.Tetro_S,
                
                // 5칸 블록 (펜토미노)
                Features.Multi.Models.BlockType.Pento_F => Shared.Models.BlockType.Pento_F,
                Features.Multi.Models.BlockType.Pento_I => Shared.Models.BlockType.Pento_I,
                Features.Multi.Models.BlockType.Pento_L => Shared.Models.BlockType.Pento_L,
                Features.Multi.Models.BlockType.Pento_N => Shared.Models.BlockType.Pento_N,
                Features.Multi.Models.BlockType.Pento_P => Shared.Models.BlockType.Pento_P,
                Features.Multi.Models.BlockType.Pento_T => Shared.Models.BlockType.Pento_T,
                Features.Multi.Models.BlockType.Pento_U => Shared.Models.BlockType.Pento_U,
                Features.Multi.Models.BlockType.Pento_V => Shared.Models.BlockType.Pento_V,
                Features.Multi.Models.BlockType.Pento_W => Shared.Models.BlockType.Pento_W,
                Features.Multi.Models.BlockType.Pento_X => Shared.Models.BlockType.Pento_X,
                Features.Multi.Models.BlockType.Pento_Y => Shared.Models.BlockType.Pento_Y,
                Features.Multi.Models.BlockType.Pento_Z => Shared.Models.BlockType.Pento_Z,
                
                _ => Shared.Models.BlockType.Single
            };
        }
        
    }

    /// <summary>
    /// 보드 셀 변경사항을 나타내는 구조체
    /// </summary>
    public struct BoardCellChange
    {
        public int row;
        public int col;
        public int oldValue;
        public int newValue;

        public BoardCellChange(int row, int col, int oldValue, int newValue)
        {
            this.row = row;
            this.col = col;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }
    }
}