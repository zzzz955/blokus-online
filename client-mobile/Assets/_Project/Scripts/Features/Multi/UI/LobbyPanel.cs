using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Features.Multi.Net;
using Features.Multi.Core;
using Features.Multi.Models;
using App.UI;
using TMPro;
using Shared.UI;
using NetRoomInfo = Features.Multi.Net.RoomInfo;
using NetUserInfo = Features.Multi.Net.UserInfo;
using SharedRoomInfo = Shared.Models.RoomInfo;
using SharedUserInfo = Shared.Models.UserInfo;

namespace Features.Multi.UI
{
    /// <summary>
    /// 로비 패널 - Qt LobbyWindow와 동일한 기능
    /// 접속자 목록, 방 목록, 채팅, 방 생성/참가 기능
    /// </summary>
    public class LobbyPanel : MonoBehaviour
    {
        [Header("Info Panel")]
        [SerializeField] private TextMeshProUGUI welcomeLabel;
        [SerializeField] private TextMeshProUGUI userStatsLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button logoutButton;

        [Header("Mobile Layout - Vertical Structure")]
        [SerializeField] private TextMeshProUGUI onlineCountLabel;
        [SerializeField] private ScrollRect userListScrollRect;
        [SerializeField] private Transform userListContent;
        [SerializeField] private GameObject userItemPrefab;
        
        // 모바일 최적화: Ranking 기능 제거, Tab Group 제거

        [Header("Mobile Section - Room List")]
        [SerializeField] private ScrollRect roomListScrollRect;
        [SerializeField] private Transform roomListContent;
        [SerializeField] private GameObject roomItemPrefab;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button refreshRoomButton;

        [Header("Mobile Section - Chat")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private TextMeshProUGUI chatDisplay;
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private Button chatSendButton;

        [Header("Create Room Panel")]
        [SerializeField] private CreateRoomPanel createRoomPanel;
        
        [Header("Modals")]
        [SerializeField] private ConfirmModal logoutConfirmModal;

        // Dependencies
        private NetworkManager networkManager;
        private MultiUserDataCache dataCache;

        // Data
        private List<NetUserInfo> onlineUsers = new List<NetUserInfo>();
        private List<NetRoomInfo> roomList = new List<NetRoomInfo>();
        // 모바일 최적화: rankingData 제거
        private List<ChatMessage> chatHistory = new List<ChatMessage>();
        
        // State
        private int selectedRoomId = -1;
        private bool isInitialized = false;

        // ========================================
        // Lifecycle
        // ========================================

        void Start()
        {
            Initialize();
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
            
            // 초기 데이터 로드
            RefreshAllData();
            
            isInitialized = true;
            Debug.Log("[LobbyPanel] Initialized successfully");
        }

        private void FindDependencies()
        {
            networkManager = NetworkManager.Instance;
            dataCache = MultiUserDataCache.Instance;

            if (networkManager == null)
                Debug.LogError("[LobbyPanel] NetworkManager not found!");

            if (dataCache == null)
                Debug.LogError("[LobbyPanel] MultiUserDataCache not found!");
        }

        private void SetupUI()
        {
            // CreateRoomPanel 이벤트 연결
            if (createRoomPanel != null)
            {
                createRoomPanel.OnRoomCreated += OnRoomCreated;
                createRoomPanel.OnCancelled += OnRoomCreationCancelled;
            }

            // 버튼 이벤트 연결
            if (createRoomButton != null)
                createRoomButton.onClick.AddListener(OnCreateRoomButtonClicked);

            if (joinRoomButton != null)
                joinRoomButton.onClick.AddListener(OnJoinRoomButtonClicked);

            if (refreshRoomButton != null)
                refreshRoomButton.onClick.AddListener(OnRefreshButtonClicked);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);

            if (logoutButton != null)
                logoutButton.onClick.AddListener(OnLogoutButtonClicked);

            if (chatSendButton != null)
                chatSendButton.onClick.AddListener(OnChatSendButtonClicked);

            if (chatInput != null)
                chatInput.onEndEdit.AddListener(OnChatInputEndEdit);

            // 방 생성 패널 이벤트
            if (createRoomPanel != null)
            {
                createRoomPanel.OnRoomCreated += OnRoomCreatedFromPanel;
                createRoomPanel.OnCancelled += OnCreateRoomCancelled;
            }

            // 초기 UI 상태 설정
            UpdateConnectionStatus();
            UpdateUserStatsDisplay();
        }

        private void SubscribeToEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnConnectionChanged += OnConnectionChanged;
                networkManager.OnRoomListUpdated += OnRoomListUpdated;
                networkManager.OnRoomCreated += OnRoomCreated;
                networkManager.OnJoinRoomResponse += OnJoinRoomResponse;
                networkManager.OnChatMessage += OnChatMessageReceived;
                networkManager.OnErrorReceived += OnErrorReceived;
            }

            if (dataCache != null)
            {
                dataCache.OnOnlineUsersUpdated += OnOnlineUsersUpdated;
                // 모바일 최적화: OnRankingUpdated 제거
                dataCache.OnUserDataUpdated += OnUserDataUpdated;
            }
        }

        private void Cleanup()
        {
            if (networkManager != null)
            {
                networkManager.OnConnectionChanged -= OnConnectionChanged;
                networkManager.OnRoomListUpdated -= OnRoomListUpdated;
                networkManager.OnRoomCreated -= OnRoomCreated;
                networkManager.OnJoinRoomResponse -= OnJoinRoomResponse;
                networkManager.OnChatMessage -= OnChatMessageReceived;
                networkManager.OnErrorReceived -= OnErrorReceived;
            }

            if (dataCache != null)
            {
                dataCache.OnOnlineUsersUpdated -= OnOnlineUsersUpdated;
                // 모바일 최적화: OnRankingUpdated 제거
                dataCache.OnUserDataUpdated -= OnUserDataUpdated;
            }
        }

        // ========================================
        // UI Updates
        // ========================================

        private void RefreshAllData()
        {
            if (networkManager != null)
            {
                // 서버에 데이터 요청 (모바일: Ranking 제외)
                networkManager.EnterLobby();
                networkManager.RequestRoomList();
                networkManager.RequestOnlineUsers();
                // 모바일 최적화: Ranking 요청 제거
            }
        }

        private void UpdateConnectionStatus()
        {
            bool isConnected = networkManager?.IsConnected() ?? false;

            // 버튼 활성화/비활성화
            if (createRoomButton != null)
                createRoomButton.interactable = isConnected;

            if (joinRoomButton != null)
                joinRoomButton.interactable = isConnected && selectedRoomId != -1;

            if (refreshRoomButton != null)
                refreshRoomButton.interactable = isConnected;

            if (chatSendButton != null)
                chatSendButton.interactable = isConnected;
        }

        private void UpdateUserStatsDisplay()
        {
            if (dataCache == null) return;

            SharedUserInfo myInfo = dataCache.GetMyUserInfo();
            if (myInfo != null)
            {
                if (welcomeLabel != null)
                    welcomeLabel.text = $"🎮 {myInfo.display_name}님, 환영합니다!";

                if (userStatsLabel != null)
                {
                    userStatsLabel.text = $"레벨 {myInfo.level} | {myInfo.wins}승 {myInfo.losses}패 | " +
                                        $"승률 {myInfo.GetWinRate():F1}% | 게임 {myInfo.totalGames}회";
                }
            }
        }

        private void UpdateOnlineUsersList()
        {
            if (userListContent == null || userItemPrefab == null) return;

            // 기존 아이템 제거
            foreach (Transform child in userListContent)
            {
                Destroy(child.gameObject);
            }

            // 새 아이템 생성
            var users = dataCache?.GetOnlineUsers();
            if (users != null)
            {
                onlineUsers = new List<NetUserInfo>();
                // Shared.Models.UserInfo를 Features.Multi.Net.UserInfo로 변환
                foreach (var user in users)
                {
                    var netUser = new NetUserInfo
                    {
                        username = user.username,
                        displayName = user.display_name,
                        level = user.level,
                        wins = user.wins,
                        losses = user.losses,
                        totalGames = user.totalGames,
                        isOnline = true
                    };
                    onlineUsers.Add(netUser);
                }
            }
            else
            {
                onlineUsers = new List<NetUserInfo>();
            }
            foreach (NetUserInfo user in onlineUsers)
            {
                GameObject userItem = Instantiate(userItemPrefab, userListContent);
                
                // UserItemUI 컴포넌트에 데이터 설정 (구현 필요)
                var userItemUI = userItem.GetComponent<UserItemUI>();
                if (userItemUI != null)
                {
                    userItemUI.SetupUser(user);
                }
            }

            // 온라인 수 업데이트
            if (onlineCountLabel != null)
                onlineCountLabel.text = $"접속자 ({onlineUsers.Count}명)";
        }

        // 모바일 최적화: UpdateRankingList 제거 (Ranking 기능 제거)

        private void UpdateRoomList()
        {
            if (roomListContent == null || roomItemPrefab == null) return;

            // 기존 아이템 제거
            foreach (Transform child in roomListContent)
            {
                Destroy(child.gameObject);
            }

            // 새 아이템 생성
            var rooms = dataCache?.GetRoomList();
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    GameObject roomItem = Instantiate(roomItemPrefab, roomListContent);
                    
                    // RoomItemUI 컴포넌트에 데이터 설정 (구현 필요)
                    var roomItemUI = roomItem.GetComponent<RoomItemUI>();
                    if (roomItemUI != null)
                    {
                        roomItemUI.SetupRoom(room, null);
                    }
                }
            }
        }

        // ========================================
        // Event Handlers
        // ========================================

        private void OnCreateRoomButtonClicked()
        {
            Debug.Log("[LobbyPanel] 방 생성 버튼 클릭");
            
            if (createRoomPanel != null)
            {
                createRoomPanel.Show();
            }
            else
            {
                Debug.LogError("[LobbyPanel] CreateRoomPanel이 할당되지 않았습니다.");
            }
        }

        private void OnJoinRoomButtonClicked()
        {
            if (selectedRoomId != -1 && networkManager != null)
            {
                networkManager.JoinRoom(selectedRoomId);
            }
        }

        private void OnRefreshButtonClicked()
        {
            RefreshAllData();
        }

        private void OnSettingsButtonClicked()
        {
            // TODO: 설정 창 열기
            Debug.Log("[LobbyPanel] Settings button clicked");
        }

        private void OnLogoutButtonClicked()
        {
            Debug.Log("[LobbyPanel] Logout button clicked");
            
            // 로그아웃 확인 모달 표시
            if (logoutConfirmModal != null)
            {
                logoutConfirmModal.ShowModal(
                    "로그아웃",
                    "정말로 로그아웃 하시겠습니까?\n진행 중인 내용이 저장되지 않을 수 있습니다.",
                    OnLogoutConfirmed,
                    null
                );
            }
            else
            {
                Debug.LogError("[LobbyPanel] logoutConfirmModal이 설정되지 않았습니다!");
                OnLogoutConfirmed(); // 폴백: 바로 로그아웃
            }
        }
        
        /// <summary>
        /// 로그아웃 확인 후 실제 로그아웃 처리
        /// </summary>
        private void OnLogoutConfirmed()
        {
            Debug.Log("[LobbyPanel] 로그아웃 확인됨 - 세션 정리 시작");
            
            // 1. TCP 연결 해제 및 세션 정리
            if (networkManager != null)
            {
                networkManager.DisconnectFromServer();
                Debug.Log("[LobbyPanel] TCP 연결 해제 완료");
            }
            
            // 2. 데이터 캐시 정리 (로그아웃 시 자동으로 정리됨)
            Debug.Log("[LobbyPanel] 데이터 캐시는 연결 해제 시 자동 정리됨");
            
            // 3. 씬 정리 및 메인으로 복귀
            var sceneController = GetComponentInParent<MultiGameplaySceneController>();
            if (sceneController != null)
            {
                sceneController.ReturnToMainScene();
            }
            else
            {
                Debug.LogError("[LobbyPanel] SceneController를 찾을 수 없음 - 직접 씬 전환");
                // 폴백: 직접 씬 전환
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
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

        private void OnRoomCreatedFromPanel(RoomCreationInfo roomInfo)
        {
            if (string.IsNullOrEmpty(roomInfo.roomName))
            {
                ShowMessage("방 이름을 입력하세요.");
                return;
            }

            if (networkManager != null)
            {
                networkManager.CreateRoom(roomInfo.roomName, roomInfo.maxPlayers);
            }
        }

        private void OnCreateRoomCancelled()
        {
            // 방 생성 취소 시 아무 작업 없음 (CreateRoomPanel이 자체적으로 닫힘)
        }

        // ========================================
        // Network Event Handlers
        // ========================================

        private void OnConnectionChanged(bool isConnected)
        {
            UpdateConnectionStatus();
            
            if (!isConnected)
            {
                ShowMessage("서버 연결이 끊어졌습니다.");
            }
        }

        private void OnRoomListUpdated(List<NetRoomInfo> rooms)
        {
            UpdateRoomList();
        }

        private void OnRoomCreated(NetRoomInfo room)
        {
            ShowMessage($"방 '{room.roomName}'이 생성되었습니다.");
        }

        private void OnJoinRoomResponse(bool success, string message)
        {
            if (success)
            {
                ShowMessage("방에 참가했습니다.");
                // GameRoomPanel로 전환은 MultiGameplaySceneController가 처리
            }
            else
            {
                ShowMessage($"방 참가 실패: {message}");
            }
        }

        private void OnChatMessageReceived(string message)
        {
            // 메시지를 ChatMessage 객체로 변환
            ChatMessage chatMsg = new ChatMessage("Unknown", message, "Unknown");
            chatHistory.Add(chatMsg);
            UpdateChatDisplay();
        }

        private void OnErrorReceived(string error)
        {
            ShowMessage($"오류: {error}");
        }

        private void OnOnlineUsersUpdated()
        {
            UpdateOnlineUsersList();
        }

        // 모바일 최적화: OnRankingUpdated 제거

        private void OnUserDataUpdated()
        {
            UpdateUserStatsDisplay();
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
            Debug.Log($"[LobbyPanel] {message}");
            // TODO: Toast 메시지 표시
        }
        
        /// <summary>
        /// Android 뒤로가기 버튼 처리
        /// </summary>
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 모달이 활성화된 경우 무시 (모달에서 처리)
                if (logoutConfirmModal != null && logoutConfirmModal.gameObject.activeInHierarchy)
                    return;
                
                // 로그아웃 확인 모달 표시
                OnLogoutButtonClicked();
            }
        }

        // ========================================
        // Public API (for Room Item UI)
        // ========================================

        public void SelectRoom(int roomId)
        {
            selectedRoomId = roomId;
            UpdateConnectionStatus(); // joinRoomButton 상태 업데이트
        }

        public void JoinRoom(int roomId)
        {
            selectedRoomId = roomId;
            OnJoinRoomButtonClicked();
        }

        // ========================================
        // CreateRoomPanel Event Handlers
        // ========================================

        /// <summary>
        /// 방 생성 요청 처리
        /// </summary>
        private void OnRoomCreated(RoomCreationInfo roomInfo)
        {
            Debug.Log($"[LobbyPanel] 방 생성 요청: {roomInfo.roomName}");
            
            if (networkManager != null)
            {
                // TCP 프로토콜을 통해 방 생성 요청
                networkManager.CreateRoom(roomInfo.roomName, roomInfo.maxPlayers);
            }
            else
            {
                Debug.LogError("[LobbyPanel] NetworkManager not available for room creation");
                ShowMessage("네트워크 연결이 필요합니다.");
            }
        }

        /// <summary>
        /// 방 생성 취소 처리
        /// </summary>
        private void OnRoomCreationCancelled()
        {
            Debug.Log("[LobbyPanel] 방 생성 취소");
            // 특별한 처리 없이 로그만 출력
        }

        // ========================================
        // RoomItemUI Event Handlers
        // ========================================

        /// <summary>
        /// 방 선택 처리
        /// </summary>
        private void OnRoomItemSelected(NetRoomInfo roomInfo)
        {
            selectedRoomId = roomInfo.roomId;
            Debug.Log($"[LobbyPanel] 방 선택: {roomInfo.roomName} (ID: {roomInfo.roomId})");
            
            // 선택된 방 UI 업데이트
            UpdateRoomSelection();
        }

        /// <summary>
        /// 방 더블클릭 처리 (즉시 참가)
        /// </summary>
        private void OnRoomItemDoubleClicked(NetRoomInfo roomInfo)
        {
            Debug.Log($"[LobbyPanel] 방 더블클릭 참가: {roomInfo.roomName}");
            
            selectedRoomId = roomInfo.roomId;
            
            if (networkManager != null)
            {
                networkManager.JoinRoom(roomInfo.roomId);
            }
            else
            {
                Debug.LogError("[LobbyPanel] NetworkManager not available for room join");
                ShowMessage("네트워크 연결이 필요합니다.");
            }
        }

        /// <summary>
        /// 선택된 방 UI 업데이트
        /// </summary>
        private void UpdateRoomSelection()
        {
            // 방 목록에서 선택된 방을 하이라이트 처리
            if (roomListContent != null)
            {
                for (int i = 0; i < roomListContent.childCount; i++)
                {
                    var roomItem = roomListContent.GetChild(i).GetComponent<RoomItemUI>();
                    if (roomItem != null)
                    {
                        // 선택 상태는 각 RoomItemUI에서 관리됨
                        // 여기서는 selectedRoomId와 일치하는 항목만 선택 상태로 설정
                    }
                }
            }
        }
    }
}