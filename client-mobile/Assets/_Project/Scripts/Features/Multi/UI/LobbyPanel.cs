using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Features.Multi.Net;
using Features.Multi.Core;
using Features.Multi.Models;
using App.UI;
using App.Core;
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
        [SerializeField] private Transform chatContent;
        [SerializeField] private GameObject chatItemPrefab;
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private Button chatSendButton;

        [Header("Create Room Panel")]
        [SerializeField] private CreateRoomPanel createRoomPanel;
        
        [Header("Modals")]
        [SerializeField] private ConfirmModal logoutConfirmModal;

        // Dependencies
        private NetworkManager networkManager;

        // Data
        private List<NetUserInfo> onlineUsers = new List<NetUserInfo>();
        private List<NetRoomInfo> roomList = new List<NetRoomInfo>();
        // 모바일 최적화: rankingData 제거
        private List<ChatMessage> chatHistory = new List<ChatMessage>();
        private List<GameObject> chatItemInstances = new List<GameObject>();
        private NetUserInfo myUserInfo; // 내 사용자 정보
        
        // 채팅 설정
        private const int MAX_CHAT_MESSAGES = 100;
        
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
            
            // NetworkManager에서 현재 사용자 정보 로드 (이미 인증된 경우)
            LoadCurrentUserInfo();
            
            isInitialized = true;
            Debug.Log("[LobbyPanel] Initialized successfully");
        }

        private void FindDependencies()
        {
            networkManager = NetworkManager.Instance;

            if (networkManager == null)
                Debug.LogError("[LobbyPanel] NetworkManager not found!");
            
            // CreateRoomPanel이 Inspector에서 할당되지 않은 경우 자동으로 찾기
            if (createRoomPanel == null)
            {
                createRoomPanel = FindObjectOfType<CreateRoomPanel>();
                Debug.Log($"[LobbyPanel] CreateRoomPanel 자동 검색: {(createRoomPanel != null ? "찾음" : "못 찾음")}");
            }
        }

        private void SetupUI()
        {
            // CreateRoomPanel 이벤트는 아래에서 한 번만 연결

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
                networkManager.OnMyStatsUpdated += OnMyStatsUpdated;
                networkManager.OnRoomListUpdated += OnRoomListUpdated;
                networkManager.OnUserListUpdated += OnUserListUpdated;
                networkManager.OnRoomCreated += OnRoomCreated;
                networkManager.OnJoinRoomResponse += OnJoinRoomResponse;
                networkManager.OnChatMessageReceived += OnChatMessageReceived;
                networkManager.OnErrorReceived += OnErrorReceived;
            }

        }

        private void Cleanup()
        {
            if (networkManager != null)
            {
                networkManager.OnConnectionChanged -= OnConnectionChanged;
                networkManager.OnMyStatsUpdated -= OnMyStatsUpdated;
                networkManager.OnRoomListUpdated -= OnRoomListUpdated;
                networkManager.OnUserListUpdated -= OnUserListUpdated;
                networkManager.OnRoomCreated -= OnRoomCreated;
                networkManager.OnJoinRoomResponse -= OnJoinRoomResponse;
                networkManager.OnChatMessageReceived -= OnChatMessageReceived;
                networkManager.OnErrorReceived -= OnErrorReceived;
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
            if (myUserInfo != null)
            {
                // 실제 사용자 정보 표시
                if (welcomeLabel != null)
                    welcomeLabel.text = $"{myUserInfo.displayName}님, 환영합니다!";

                if (userStatsLabel != null)
                {
                    string stats = $"레벨 {myUserInfo.level} | 승률 {GetWinRate():F1}% | 총 {myUserInfo.totalGames}게임";
                    userStatsLabel.text = stats;
                }
            }
            else
            {
                // 기본값으로 설정
                if (welcomeLabel != null)
                    welcomeLabel.text = "로비에 오신 것을 환영합니다!";

                if (userStatsLabel != null)
                    userStatsLabel.text = "사용자 정보 로딩 중...";
            }
        }

        private float GetWinRate()
        {
            if (myUserInfo == null || myUserInfo.totalGames == 0)
                return 0f;
            
            return ((float)myUserInfo.wins / myUserInfo.totalGames) * 100f;
        }

        private void UpdateOnlineUsersList()
        {
            if (userListContent == null || userItemPrefab == null) return;

            // 기존 아이템 제거
            foreach (Transform child in userListContent)
            {
                Destroy(child.gameObject);
            }

            // TODO: onlineUsers 리스트는 NetworkManager 이벤트를 통해 업데이트됨
            // 현재 저장된 데이터 사용
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

            // 새 아이템 생성 - NetworkManager 이벤트를 통해 업데이트된 roomList 사용
            foreach (NetRoomInfo room in roomList)
            {
                GameObject roomItem = Instantiate(roomItemPrefab, roomListContent);
                
                // RoomItemUI 컴포넌트에 데이터 설정 및 이벤트 연결
                var roomItemUI = roomItem.GetComponent<RoomItemUI>();
                if (roomItemUI != null)
                {
                    roomItemUI.SetupRoom(room);
                    
                    // 이벤트 연결
                    roomItemUI.OnRoomSelected += OnRoomItemSelected;
                    roomItemUI.OnRoomDoubleClicked += OnRoomItemDoubleClicked;
                }
            }
        }

        // ========================================
        // Event Handlers
        // ========================================

        private void OnCreateRoomButtonClicked()
        {
            Debug.Log("[LobbyPanel] 방 생성 버튼 클릭");
            
            // createRoomPanel이 null인 경우 다시 찾아보기
            if (createRoomPanel == null)
            {
                createRoomPanel = FindObjectOfType<CreateRoomPanel>();
                Debug.Log($"[LobbyPanel] CreateRoomPanel 다시 검색: {(createRoomPanel != null ? "찾음" : "못 찾음")}");
            }
            
            if (createRoomPanel != null)
            {
                // CreateRoomPanel GameObject가 비활성화되어 있으면 Show() 메서드가 실행되지 않으므로 먼저 활성화
                if (!createRoomPanel.gameObject.activeInHierarchy)
                {
                    createRoomPanel.gameObject.SetActive(true);
                    Debug.Log("[LobbyPanel] CreateRoomPanel GameObject 먼저 활성화");
                }
                
                createRoomPanel.Show();
            }
            else
            {
                Debug.LogError("[LobbyPanel] CreateRoomPanel을 찾을 수 없습니다. Inspector에서 할당하거나 씬에 CreateRoomPanel이 있는지 확인하세요.");
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
            
            // 1. 로그아웃 처리 (재연결 비활성화 포함)
            if (networkManager != null)
            {
                networkManager.LogoutFromServer();
                Debug.Log("[LobbyPanel] 로그아웃 처리 완료 (재연결 비활성화됨)");
            }
            
            // 2. 데이터 캐시 정리
            Debug.Log("[LobbyPanel] 멀티플레이 데이터 캐시 정리");
            
            // 3. ModeSelection으로 복귀 (SceneFlowController 사용)
            if (SceneFlowController.Instance != null)
            {
                Debug.Log("[LobbyPanel] SceneFlowController를 통해 ModeSelection으로 복귀");
                SceneFlowController.Instance.StartExitMultiToMain();
            }
            else
            {
                Debug.LogError("[LobbyPanel] SceneFlowController를 찾을 수 없음 - MultiGameplaySceneController 사용");
                
                // 폴백 1: MultiGameplaySceneController 사용
                var sceneController = GetComponentInParent<MultiGameplaySceneController>();
                if (sceneController != null)
                {
                    sceneController.ReturnToMainScene();
                }
                else
                {
                    Debug.LogError("[LobbyPanel] MultiGameplaySceneController도 찾을 수 없음 - 직접 씬 전환");
                    // 폴백 2: 직접 씬 전환
                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
                }
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
                networkManager.CreateRoom(roomInfo.roomName, roomInfo.isPrivate, roomInfo.password);
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

        private void OnMyStatsUpdated(NetUserInfo userInfo)
        {
            myUserInfo = userInfo;
            Debug.Log($"[LobbyPanel] 내 사용자 정보 업데이트: {userInfo.displayName} [{userInfo.username}]");
            UpdateUserStatsDisplay();
        }

        /// <summary>
        /// NetworkManager에서 현재 사용자 정보를 가져와서 UI 업데이트
        /// </summary>
        private void LoadCurrentUserInfo()
        {
            if (networkManager?.CurrentUserInfo != null)
            {
                myUserInfo = networkManager.CurrentUserInfo;
                Debug.Log($"[LobbyPanel] NetworkManager에서 사용자 정보 로드: {myUserInfo.displayName} [{myUserInfo.username}]");
                UpdateUserStatsDisplay();
            }
        }

        private void OnRoomListUpdated(List<NetRoomInfo> rooms)
        {
            roomList.Clear();
            roomList.AddRange(rooms);
            Debug.Log($"[LobbyPanel] 방 목록 업데이트: {rooms.Count}개");
            UpdateRoomList();
        }

        private void OnUserListUpdated(List<NetUserInfo> users)
        {
            onlineUsers.Clear();
            onlineUsers.AddRange(users);
            Debug.Log($"[LobbyPanel] 온라인 사용자 목록 업데이트: {users.Count}명");
            UpdateOnlineUsersList();
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

        private void OnChatMessageReceived(string username, string displayName, string message)
        {
            // 메시지를 ChatMessage 객체로 변환
            ChatMessage chatMsg = new ChatMessage(username, message, displayName);
            
            // 채팅 히스토리에 추가 (100개 제한)
            chatHistory.Add(chatMsg);
            if (chatHistory.Count > MAX_CHAT_MESSAGES)
            {
                chatHistory.RemoveAt(0);
            }
            
            Debug.Log($"[LobbyPanel] 채팅 메시지 수신: {displayName} [{username}]: {message}");
            UpdateChatDisplay();
        }

        private void OnErrorReceived(string error)
        {
            ShowMessage($"오류: {error}");
        }

        // 기존 MultiUserDataCache 이벤트 핸들러들 - 더 이상 사용되지 않음
        // TODO: NetworkManager 이벤트로 대체 필요
        /*
        private void OnOnlineUsersUpdated()
        {
            UpdateOnlineUsersList();
        }

        private void OnUserDataUpdated()
        {
            UpdateUserStatsDisplay();
        }
        */

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
            if (chatContent == null || chatItemPrefab == null) return;

            // 최신 메시지만 UI에 추가 (마지막 메시지)
            if (chatHistory.Count > 0)
            {
                var latestMessage = chatHistory[chatHistory.Count - 1];
                CreateChatItem(latestMessage);
            }

            // 스크롤을 맨 아래로 (다음 프레임에서 실행하여 레이아웃 업데이트 보장)
            StartCoroutine(ScrollToBottomNextFrame());
        }

        private System.Collections.IEnumerator ScrollToBottomNextFrame()
        {
            // 한 프레임 대기하여 레이아웃이 완전히 업데이트되도록 함
            yield return new WaitForEndOfFrame();
            
            if (chatScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }
        
        private void CreateChatItem(ChatMessage message)
        {
            // 채팅 아이템 인스턴스 생성
            GameObject chatItem = Instantiate(chatItemPrefab, chatContent);
            chatItemInstances.Add(chatItem);
            
            // ChatItemUI 컴포넌트 설정
            var chatItemUI = chatItem.GetComponent<ChatItemUI>();
            if (chatItemUI != null)
            {
                // 내 메시지인지 확인 (내 사용자 정보와 비교)
                bool isMyMessage = (myUserInfo != null && message.username == myUserInfo.username);
                chatItemUI.SetupMessage(message.displayName, message.message, isMyMessage);
            }
            
            // 메시지 수 제한 - 100개 초과 시 오래된 메시지 삭제
            if (chatItemInstances.Count > MAX_CHAT_MESSAGES)
            {
                var oldestItem = chatItemInstances[0];
                chatItemInstances.RemoveAt(0);
                if (oldestItem != null)
                {
                    DestroyImmediate(oldestItem);
                }
            }
        }

        private void ShowMessage(string message)
        {
            Debug.Log($"[LobbyPanel] {message}");
            // TODO: Toast 메시지 표시
        }
        
        // Android 뒤로가기 처리는 BackButtonManager에서 전역 관리

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

        // 중복된 OnRoomCreated 핸들러 제거됨 - OnRoomCreatedFromPanel 사용

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