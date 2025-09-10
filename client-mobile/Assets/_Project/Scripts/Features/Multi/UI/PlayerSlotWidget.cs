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
        
        [Header("게임 정보 UI")]
        [SerializeField] private Image hostIndicator;              // 호스트 표시 이미지
        [SerializeField] private TextMeshProUGUI scoreText;       // 현재 점수
        [SerializeField] private TextMeshProUGUI remainingBlocksText; // 남은 블록 개수
        
        [Header("상태 색상")]
        [SerializeField] private Color emptySlotColor = Color.white;
        
        [Header("준비 상태 스프라이트")]
        [SerializeField] private Sprite readySprite;    // isReady = true 시 사용할 스프라이트
        [SerializeField] private Sprite notReadySprite; // isReady = false 시 사용할 스프라이트
        
        [Header("호스트 표시")]
        [SerializeField] private Sprite hostCrownSprite; // 호스트 표시용 왕관 스프라이트
        
        [Header("본인 식별 표시")]
        [SerializeField] private Image currentPlayerIndicator; // 본인 식별용 이미지 (나일 경우에만 표시)
        
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
            Debug.Log($"[PlayerSlotWidget] SetPlayerData 호출: {data.playerName} (Host: {data.isHost}, Ready: {data.isReady}, isCurrentUserHost: {isCurrentUserHost})");
            
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
                readyIndicator.sprite = null;
                readyIndicator.gameObject.SetActive(false);
            }
            
            // 호스트 표시 숨김
            if (hostIndicator != null)
                hostIndicator.gameObject.SetActive(false);
                
            // 점수 및 블록 정보 초기화
            if (scoreText != null)
                scoreText.text = "";
                
            if (remainingBlocksText != null)
                remainingBlocksText.text = "";
            
            if (playerAvatar != null)
            {
                // Stub: 기본 아바타 또는 빈 이미지
                playerAvatar.sprite = null;
                playerAvatar.color = emptySlotColor;
            }
            
            if (kickButton != null)
                kickButton.gameObject.SetActive(false);
                
            // 본인 식별 표시도 해제
            if (currentPlayerIndicator != null)
                currentPlayerIndicator.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            Debug.Log($"[PlayerSlotWidget] UpdateUI 시작: {playerData.playerName} (isEmpty: {playerData.isEmpty})");
            
            if (playerData.isEmpty)
            {
                Debug.Log($"[PlayerSlotWidget] 빈 슬롯으로 설정");
                SetEmptySlot();
                return;
            }
            
            // 플레이어 이름
            if (playerNameText != null)
            {
                playerNameText.text = playerData.playerName;
                Debug.Log($"[PlayerSlotWidget] 플레이어 이름 설정: {playerData.playerName}");
            }
            else
            {
                Debug.LogError($"[PlayerSlotWidget] playerNameText가 null입니다!");
            }
            
            // 준비 상태 표시 (스프라이트 기반)
            if (readyIndicator != null)
            {
                readyIndicator.gameObject.SetActive(true);
                readyIndicator.sprite = playerData.isReady ? readySprite : notReadySprite;
                Debug.Log($"[PlayerSlotWidget] 준비 상태 설정: {playerData.isReady}");
            }
            else
            {
                Debug.LogError($"[PlayerSlotWidget] readyIndicator가 null입니다!");
            }
            
            // 호스트 표시
            if (hostIndicator != null)
            {
                hostIndicator.gameObject.SetActive(playerData.isHost);
                if (playerData.isHost && hostCrownSprite != null)
                {
                    hostIndicator.sprite = hostCrownSprite;
                }
                Debug.Log($"[PlayerSlotWidget] 호스트 표시 설정: {playerData.isHost}");
            }
            else if (playerData.isHost)
            {
                Debug.LogError($"[PlayerSlotWidget] hostIndicator가 null인데 플레이어가 호스트입니다!");
            }
            
            // 점수 표시
            if (scoreText != null)
            {
                scoreText.text = $"점수: {playerData.currentScore}";
            }
            
            // 남은 블록 개수 표시
            if (remainingBlocksText != null)
            {
                remainingBlocksText.text = $"블록: {playerData.remainingBlocks}";
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
            // 턴 하이라이트 효과 - 플레이어 이름 색상 변경으로 표시
            if (playerNameText != null)
            {
                playerNameText.color = highlight ? Color.yellow : Color.white;
            }
            
            // 또는 준비 상태 표시기에 노란색 테두리 등 추가 효과 가능
            // 현재는 이름 색상 변경으로 턴 표시
        }
        
        /// <summary>
        /// 내 슬롯으로 설정
        /// </summary>
        public void SetAsMySlot(bool isMySlot)
        {
            Debug.Log($"[PlayerSlotWidget] SetAsMySlot 호출: {isMySlot} for {playerData.playerName}");
            
            // 인스펙터 Bold 설정을 보존하고 본인일 때는 추가 강조 효과만 적용
            // fontStyle 변경하지 않음 - 인스펙터 설정 유지
            
            // 본인 식별 이미지 표시/숨김
            if (currentPlayerIndicator != null)
            {
                currentPlayerIndicator.gameObject.SetActive(isMySlot);
                Debug.Log($"[PlayerSlotWidget] 본인 식별 이미지 {(isMySlot ? "표시" : "숨김")}: {playerData.playerName}");
            }
            
            // 추가적인 본인 강조 효과 (색상 변경 등)는 여기서 구현 가능
            // 예: 텍스트 색상 변경 등
        }
        
        /// <summary>
        /// 준비 상태 업데이트
        /// </summary>
        public void UpdateReadyState(bool isReady)
        {
            playerData.isReady = isReady;
            if (readyIndicator != null)
            {
                readyIndicator.sprite = isReady ? readySprite : notReadySprite;
                readyIndicator.gameObject.SetActive(!playerData.isEmpty);
            }
        }
        
        /// <summary>
        /// 점수 업데이트 (서버 브로드캐스트 데이터 적용)
        /// </summary>
        public void UpdateScore(int newScore)
        {
            playerData.currentScore = newScore;
            if (scoreText != null)
            {
                scoreText.text = $"점수: {newScore}";
            }
        }
        
        /// <summary>
        /// 남은 블록 개수 업데이트 (서버 브로드캐스트 데이터 적용)
        /// </summary>
        public void UpdateRemainingBlocks(int blocksLeft)
        {
            playerData.remainingBlocks = blocksLeft;
            if (remainingBlocksText != null)
            {
                remainingBlocksText.text = $"블록: {blocksLeft}";
            }
        }
        
        /// <summary>
        /// 게임 정보 전체 업데이트 (서버 브로드캐스트 데이터 적용)
        /// </summary>
        public void UpdateGameInfo(int score, int blocksLeft)
        {
            UpdateScore(score);
            UpdateRemainingBlocks(blocksLeft);
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
        public int currentScore;      // 현재 점수
        public int remainingBlocks;   // 남은 블록 개수
        
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
            colorIndex = -1,
            currentScore = 0,
            remainingBlocks = 0
        };
    }
}