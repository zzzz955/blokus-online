using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Features.Multi.Models;

namespace Features.Multi.UI
{
    /// <summary>
    /// 플레이어 슬롯 위젯 (Stub 구현)
    /// 게임방에서 각 플레이어 슬롯을 표시
    /// </summary>
    public class PlayerSlotWidget : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image playerAvatar; // Stub - 아바타는 구현하지 않음
        [SerializeField] private Image readyIndicator;
        [SerializeField] private Button kickButton;
        
        [Header("상태 색상")]
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.gray;
        [SerializeField] private Color emptySlotColor = Color.white;
        
        // 플레이어 데이터
        private PlayerSlot playerData;
        private bool isHost = false;
        
        public event System.Action<int> OnKickPlayer;
        
        private void Start()
        {
            SetupEventHandlers();
            SetEmptySlot(); // 초기에는 빈 슬롯으로 설정
        }
        
        /// <summary>
        /// 이벤트 핸들러 설정
        /// </summary>
        private void SetupEventHandlers()
        {
            if (kickButton != null)
            {
                kickButton.onClick.AddListener(OnKickButtonClicked);
            }
        }
        
        /// <summary>
        /// 플레이어 데이터 설정
        /// </summary>
        public void SetPlayerData(PlayerSlot data, bool isCurrentUserHost = false)
        {
            playerData = data;
            isHost = isCurrentUserHost;
            
            UpdateUI();
        }
        
        /// <summary>
        /// 빈 슬롯으로 설정
        /// </summary>
        public void SetEmptySlot()
        {
            playerData = PlayerSlot.Empty;
            
            if (playerNameText != null)
                playerNameText.text = "빈 슬롯";
                
            if (readyIndicator != null)
            {
                readyIndicator.color = emptySlotColor;
                readyIndicator.gameObject.SetActive(false);
            }
            
            if (playerAvatar != null)
            {
                // Stub: 기본 아바타 또는 빈 이미지
                playerAvatar.sprite = null;
                playerAvatar.color = emptySlotColor;
            }
            
            if (kickButton != null)
                kickButton.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (playerData.isEmpty)
            {
                SetEmptySlot();
                return;
            }
            
            // 플레이어 이름
            if (playerNameText != null)
                playerNameText.text = playerData.playerName;
            
            // 준비 상태 표시
            if (readyIndicator != null)
            {
                readyIndicator.gameObject.SetActive(true);
                readyIndicator.color = playerData.isReady ? readyColor : notReadyColor;
            }
            
            // 아바타 (Stub)
            if (playerAvatar != null)
            {
                // TODO: 실제 아바타 이미지 로드
                playerAvatar.color = Color.white; // 기본 색상
            }
            
            // 추방 버튼 (호스트일 때만 표시, 자신은 제외)
            if (kickButton != null)
            {
                bool canKick = isHost && !playerData.isHost && playerData.playerId != GetMyPlayerId();
                kickButton.gameObject.SetActive(canKick);
            }
        }
        
        /// <summary>
        /// 추방 버튼 클릭 처리
        /// </summary>
        private void OnKickButtonClicked()
        {
            if (!playerData.isEmpty)
            {
                Debug.Log($"[PlayerSlotWidget] 플레이어 추방 요청: {playerData.playerName}");
                OnKickPlayer?.Invoke(playerData.playerId);
            }
        }
        
        /// <summary>
        /// 내 플레이어 ID 가져오기 (임시 구현)
        /// </summary>
        private int GetMyPlayerId()
        {
            // TODO: 실제 내 플레이어 ID 가져오기
            return -1;
        }
        
        /// <summary>
        /// 플레이어 데이터 가져오기
        /// </summary>
        public PlayerSlot GetPlayerData()
        {
            return playerData;
        }
        
        /// <summary>
        /// 빈 슬롯 여부 확인
        /// </summary>
        public bool IsEmpty()
        {
            return playerData.isEmpty;
        }
        
        /// <summary>
        /// 플레이어 슬롯 초기화
        /// </summary>
        public void Initialize(PlayerSlot slot)
        {
            SetPlayerData(slot);
        }
        
        /// <summary>
        /// 슬롯 업데이트
        /// </summary>
        public void UpdateSlot(PlayerSlot slot)
        {
            SetPlayerData(slot);
        }
        
        /// <summary>
        /// 턴 하이라이트 설정
        /// </summary>
        public void SetTurnHighlight(bool highlight)
        {
            // Stub: 턴 하이라이트 효과 (추후 구현)
            if (readyIndicator != null)
            {
                readyIndicator.color = highlight ? Color.yellow : (playerData.isReady ? readyColor : notReadyColor);
            }
        }
        
        /// <summary>
        /// 내 슬롯으로 설정
        /// </summary>
        public void SetAsMySlot(bool isMySlot)
        {
            // Stub: 내 슬롯 강조 표시 (추후 구현)
            if (playerNameText != null)
            {
                playerNameText.fontStyle = isMySlot ? FontStyles.Bold : FontStyles.Normal;
            }
        }
        
        /// <summary>
        /// 준비 상태 업데이트
        /// </summary>
        public void UpdateReadyState(bool isReady)
        {
            playerData.isReady = isReady;
            if (readyIndicator != null)
            {
                readyIndicator.color = isReady ? readyColor : notReadyColor;
                readyIndicator.gameObject.SetActive(!playerData.isEmpty);
            }
        }
        
        private void OnDestroy()
        {
            if (kickButton != null)
                kickButton.onClick.RemoveAllListeners();
        }
    }
    
    /// <summary>
    /// 플레이어 슬롯 데이터 구조체
    /// </summary>
    [System.Serializable]
    public struct PlayerSlot
    {
        public int playerId;
        public string playerName;
        public bool isReady;
        public bool isHost;
        public int colorIndex; // 플레이어 색상 인덱스 (0=빨강, 1=파랑, 2=노랑, 3=초록)
        
        /// <summary>
        /// 빈 슬롯인지 확인
        /// </summary>
        public bool isEmpty => playerId <= 0 && string.IsNullOrEmpty(playerName);
        
        /// <summary>
        /// 플레이어 색상 반환
        /// </summary>
        public PlayerColor color => colorIndex >= 0 && colorIndex < 4 ? (PlayerColor)colorIndex : PlayerColor.None;
        
        /// <summary>
        /// 표시 이름 반환
        /// </summary>
        public string displayName => string.IsNullOrEmpty(playerName) ? "빈 슬롯" : playerName;
        
        /// <summary>
        /// 사용자명 (호환성을 위한 별칭)
        /// </summary>
        public string username => playerName;
        
        /// <summary>
        /// 빈 슬롯 생성
        /// </summary>
        public static PlayerSlot Empty => new PlayerSlot
        {
            playerId = 0,
            playerName = "",
            isReady = false,
            isHost = false,
            colorIndex = -1
        };
    }
}