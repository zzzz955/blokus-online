using UnityEngine;
using UnityEngine.UI;
using App.UI;
using App.Core;

namespace Features.Multi.UI
{
    /// <summary>
    /// 방 생성 모달 패널
    /// Qt CreateRoomDialog와 동일한 기능 구현
    /// </summary>
    public class CreateRoomPanel : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private InputField roomNameInput;
        [SerializeField] private Dropdown gameModeDropdown;
        [SerializeField] private Dropdown maxPlayersDropdown;
        [SerializeField] private Toggle privateToggle;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Button createButton;
        [SerializeField] private Button cancelButton;
        
        [Header("패널 컨트롤")]
        [SerializeField] private GameObject modalBackground;
        
        // 생성된 방 정보를 전달하기 위한 이벤트
        public event System.Action<RoomCreationInfo> OnRoomCreated;
        public event System.Action OnCancelled;
        
        private void Start()
        {
            InitializeUI();
            SetupEventHandlers();
            Hide(); // 시작 시 숨김
        }
        
        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 방 이름 초기값 설정 (사용자명 + "님의 방")
            if (SessionManager.Instance != null)
            {
                string username = SessionManager.Instance.DisplayName ?? "플레이어";
                roomNameInput.text = $"{username}님의 방";
            }
            else
            {
                roomNameInput.text = "새로운 방";
            }
            
            roomNameInput.characterLimit = 30;
            
            // 게임 모드 설정 (클래식만)
            gameModeDropdown.ClearOptions();
            gameModeDropdown.AddOptions(new System.Collections.Generic.List<string> { "클래식 (4인, 20x20)" });
            gameModeDropdown.value = 0;
            gameModeDropdown.interactable = false; // 클래식 모드 고정
            
            // 최대 인원 설정 (2-4명)
            maxPlayersDropdown.ClearOptions();
            maxPlayersDropdown.AddOptions(new System.Collections.Generic.List<string> { "2명", "3명", "4명" });
            maxPlayersDropdown.value = 2; // 4명 선택
            maxPlayersDropdown.interactable = false; // 클래식 모드 고정
            
            // 비공개 방 설정
            privateToggle.isOn = false;
            privateToggle.interactable = false; // 클래식 모드에서는 비활성화
            
            // 패스워드 입력
            passwordInput.contentType = InputField.ContentType.Password;
            passwordInput.characterLimit = 20;
            passwordInput.interactable = false; // 초기에는 비활성화
            
            UpdatePasswordField();
        }
        
        /// <summary>
        /// 이벤트 핸들러 설정
        /// </summary>
        private void SetupEventHandlers()
        {
            if (createButton != null)
                createButton.onClick.AddListener(OnCreateButtonClicked);
                
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
                
            if (privateToggle != null)
                privateToggle.onValueChanged.AddListener(OnPrivateToggleChanged);
        }
        
        /// <summary>
        /// 방 생성 버튼 클릭 처리
        /// </summary>
        private void OnCreateButtonClicked()
        {
            string roomName = roomNameInput.text.Trim();
            
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = "새로운 방";
            }
            
            var roomInfo = new RoomCreationInfo
            {
                roomName = roomName,
                maxPlayers = maxPlayersDropdown.value + 2, // 0->2, 1->3, 2->4
                isPrivate = privateToggle.isOn,
                password = privateToggle.isOn ? passwordInput.text : "",
                gameMode = "classic"
            };
            
            Debug.Log($"[CreateRoomPanel] 방 생성 요청: {roomInfo.roomName}, 최대 {roomInfo.maxPlayers}명");
            
            OnRoomCreated?.Invoke(roomInfo);
            Hide();
        }
        
        /// <summary>
        /// 취소 버튼 클릭 처리
        /// </summary>
        private void OnCancelButtonClicked()
        {
            Debug.Log("[CreateRoomPanel] 방 생성 취소");
            OnCancelled?.Invoke();
            Hide();
        }
        
        /// <summary>
        /// 비공개 방 토글 변경 처리
        /// </summary>
        private void OnPrivateToggleChanged(bool isPrivate)
        {
            UpdatePasswordField();
        }
        
        /// <summary>
        /// 패스워드 입력 필드 활성화/비활성화
        /// </summary>
        private void UpdatePasswordField()
        {
            if (passwordInput != null)
            {
                passwordInput.interactable = privateToggle.isOn;
                if (!privateToggle.isOn)
                {
                    passwordInput.text = "";
                }
            }
        }
        
        /// <summary>
        /// 패널 표시
        /// </summary>
        public void Show()
        {
            if (modalBackground != null)
                modalBackground.SetActive(true);
                
            gameObject.SetActive(true);
            
            // 방 이름 입력 필드에 포커스
            if (roomNameInput != null)
            {
                roomNameInput.Select();
                roomNameInput.ActivateInputField();
            }
        }
        
        /// <summary>
        /// 패널 숨김
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            
            if (modalBackground != null)
                modalBackground.SetActive(false);
        }
        
        private void OnDestroy()
        {
            // 이벤트 핸들러 해제
            if (createButton != null)
                createButton.onClick.RemoveAllListeners();
                
            if (cancelButton != null)
                cancelButton.onClick.RemoveAllListeners();
                
            if (privateToggle != null)
                privateToggle.onValueChanged.RemoveAllListeners();
        }
    }
    
    /// <summary>
    /// 방 생성 정보 구조체
    /// </summary>
    [System.Serializable]
    public struct RoomCreationInfo
    {
        public string roomName;
        public int maxPlayers;
        public bool isPrivate;
        public string password;
        public string gameMode;
    }
}