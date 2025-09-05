using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Features.Multi.Net;

namespace Features.Multi.UI
{
    /// <summary>
    /// 멀티플레이어 로비 UI 컨트롤러
    /// 방 목록, 방 생성, 방 참가 기능 관리
    /// </summary>
    public class MultiplayerLobbyController : MonoBehaviour
    {
        [Header("로비 UI")]
        [SerializeField] private Transform roomListParent;
        [SerializeField] private GameObject roomItemPrefab;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button backButton;
        
        [Header("방 생성 UI")]
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private InputField roomNameInput;
        [SerializeField] private Dropdown maxPlayersDropdown;
        [SerializeField] private Button confirmCreateButton;
        [SerializeField] private Button cancelCreateButton;
        
        [Header("상태 표시")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text playerCountText;
        
        private List<GameObject> roomItems = new List<GameObject>();
        
        void Start()
        {
            InitializeUI();
            SubscribeToNetworkEvents();
            EnterLobby();
        }
        
        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 버튼 이벤트 연결
            if (createRoomButton != null)
                createRoomButton.onClick.AddListener(OnCreateRoomButtonClicked);
                
            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshButtonClicked);
                
            if (backButton != null)
                backButton.onClick.AddListener(OnBackButtonClicked);
                
            if (confirmCreateButton != null)
                confirmCreateButton.onClick.AddListener(OnConfirmCreateRoomClicked);
                
            if (cancelCreateButton != null)
                cancelCreateButton.onClick.AddListener(OnCancelCreateRoomClicked);
            
            // 방 생성 패널 초기 비활성화
            if (createRoomPanel != null)
                createRoomPanel.SetActive(false);
                
            UpdateConnectionStatus();
        }
        
        /// <summary>
        /// 네트워크 이벤트 구독
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionChanged += OnConnectionChanged;
                NetworkManager.Instance.OnRoomListUpdated += OnRoomListUpdated;
                NetworkManager.Instance.OnRoomCreated += OnRoomCreated;
                NetworkManager.Instance.OnJoinRoomResponse += OnJoinRoomResponse;
                NetworkManager.Instance.OnErrorReceived += OnErrorReceived;
            }
        }
        
        /// <summary>
        /// 로비 입장
        /// </summary>
        private void EnterLobby()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected())
            {
                NetworkManager.Instance.EnterLobby();
                UpdateStatus("로비에 입장했습니다.");
            }
            else
            {
                UpdateStatus("서버에 연결되지 않았습니다.");
            }
        }
        
        /// <summary>
        /// 방 생성 버튼 클릭
        /// </summary>
        private void OnCreateRoomButtonClicked()
        {
            if (createRoomPanel != null)
            {
                createRoomPanel.SetActive(true);
                
                // 기본값 설정
                if (roomNameInput != null)
                    roomNameInput.text = $"방 {Random.Range(1000, 9999)}";
            }
        }
        
        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            // 로비 재입장으로 방 목록 새로고침
            EnterLobby();
        }
        
        /// <summary>
        /// 뒤로가기 버튼 클릭
        /// </summary>
        private void OnBackButtonClicked()
        {
            // 메인 씬으로 돌아가기
            if (App.Core.SceneFlowController.Instance != null)
            {
                App.Core.SceneFlowController.Instance.StartExitMultiToMain();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
            }
        }
        
        /// <summary>
        /// 방 생성 확인 버튼 클릭
        /// </summary>
        private void OnConfirmCreateRoomClicked()
        {
            string roomName = roomNameInput?.text ?? "";
            int maxPlayers = maxPlayersDropdown?.value + 2 ?? 4; // 2-4명
            
            if (string.IsNullOrEmpty(roomName))
            {
                UpdateStatus("방 이름을 입력하세요.");
                return;
            }
            
            if (NetworkManager.Instance != null)
            {
                bool success = NetworkManager.Instance.CreateRoom(roomName, maxPlayers);
                if (success)
                {
                    UpdateStatus($"방 생성 요청: {roomName}");
                    if (createRoomPanel != null)
                        createRoomPanel.SetActive(false);
                }
                else
                {
                    UpdateStatus("방 생성 실패");
                }
            }
        }
        
        /// <summary>
        /// 방 생성 취소 버튼 클릭
        /// </summary>
        private void OnCancelCreateRoomClicked()
        {
            if (createRoomPanel != null)
                createRoomPanel.SetActive(false);
        }
        
        /// <summary>
        /// 방 참가 (방 아이템에서 호출)
        /// </summary>
        public void JoinRoom(int roomId)
        {
            if (NetworkManager.Instance != null)
            {
                bool success = NetworkManager.Instance.JoinRoom(roomId);
                if (success)
                {
                    UpdateStatus($"방 참가 요청: {roomId}");
                }
                else
                {
                    UpdateStatus("방 참가 실패");
                }
            }
        }
        
        /// <summary>
        /// 연결 상태 변경 이벤트
        /// </summary>
        private void OnConnectionChanged(bool isConnected)
        {
            UpdateConnectionStatus();
            
            if (!isConnected)
            {
                UpdateStatus("서버 연결이 끊어졌습니다.");
                // 메인 씬으로 돌아가기
                OnBackButtonClicked();
            }
        }
        
        /// <summary>
        /// 방 목록 업데이트 이벤트
        /// </summary>
        private void OnRoomListUpdated(List<RoomInfo> rooms)
        {
            UpdateRoomList(rooms);
            UpdateStatus($"방 목록 업데이트: {rooms.Count}개 방");
        }
        
        /// <summary>
        /// 방 생성 완료 이벤트
        /// </summary>
        private void OnRoomCreated(RoomInfo room)
        {
            UpdateStatus($"방 생성됨: {room.roomName}");
        }
        
        /// <summary>
        /// 방 참가 응답 이벤트
        /// </summary>
        private void OnJoinRoomResponse(bool success, string message)
        {
            if (success)
            {
                UpdateStatus($"방 참가 성공: {message}");
                // 게임 씬으로 이동
                LoadGameplayScene();
            }
            else
            {
                UpdateStatus($"방 참가 실패: {message}");
            }
        }
        
        /// <summary>
        /// 에러 이벤트
        /// </summary>
        private void OnErrorReceived(string error)
        {
            UpdateStatus($"오류: {error}");
        }
        
        /// <summary>
        /// 방 목록 UI 업데이트
        /// </summary>
        private void UpdateRoomList(List<RoomInfo> rooms)
        {
            // 기존 방 아이템 제거
            foreach (GameObject item in roomItems)
            {
                if (item != null)
                    Destroy(item);
            }
            roomItems.Clear();
            
            // 새 방 아이템 생성
            foreach (RoomInfo room in rooms)
            {
                if (roomItemPrefab != null && roomListParent != null)
                {
                    GameObject roomItem = Instantiate(roomItemPrefab, roomListParent);
                    roomItems.Add(roomItem);
                    
                    // 방 아이템 정보 설정 (RoomItemUI 컴포넌트 필요)
                    var roomItemUI = roomItem.GetComponent<RoomItemUI>();
                    if (roomItemUI != null)
                    {
                        roomItemUI.SetupRoom(room, this);
                    }
                }
            }
            
            // 플레이어 수 업데이트
            if (playerCountText != null)
            {
                int totalPlayers = 0;
                foreach (var room in rooms)
                {
                    totalPlayers += room.currentPlayers;
                }
                playerCountText.text = $"총 플레이어: {totalPlayers}명";
            }
        }
        
        /// <summary>
        /// 연결 상태 업데이트
        /// </summary>
        private void UpdateConnectionStatus()
        {
            bool isConnected = NetworkManager.Instance?.IsConnected() ?? false;
            
            // UI 버튼 활성화/비활성화
            if (createRoomButton != null)
                createRoomButton.interactable = isConnected;
                
            if (refreshButton != null)
                refreshButton.interactable = isConnected;
        }
        
        /// <summary>
        /// 상태 텍스트 업데이트
        /// </summary>
        private void UpdateStatus(string message)
        {
            Debug.Log($"[MultiplayerLobbyController] {message}");
            
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
        
        /// <summary>
        /// 게임플레이 씬 로드
        /// </summary>
        private void LoadGameplayScene()
        {
            // 멀티플레이 게임씬으로 직접 전환 (SceneFlowController에 해당 메서드가 없음)
            UnityEngine.SceneManagement.SceneManager.LoadScene("MultiGameplayScene");
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionChanged -= OnConnectionChanged;
                NetworkManager.Instance.OnRoomListUpdated -= OnRoomListUpdated;
                NetworkManager.Instance.OnRoomCreated -= OnRoomCreated;
                NetworkManager.Instance.OnJoinRoomResponse -= OnJoinRoomResponse;
                NetworkManager.Instance.OnErrorReceived -= OnErrorReceived;
            }
        }
    }
}