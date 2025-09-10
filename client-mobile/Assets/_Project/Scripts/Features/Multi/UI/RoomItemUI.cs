using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Shared.Models;

namespace Features.Multi.UI
{
    /// <summary>
    /// 방 목록 아이템 UI
    /// 각 방의 정보를 표시하고 참가 버튼을 제공
    /// </summary>
    public class RoomItemUI : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button joinButton;
        
        private Features.Multi.Net.RoomInfo roomInfo;
        
        // 더블클릭 이벤트 처리  
        public event System.Action<Features.Multi.Net.RoomInfo> OnRoomSelected;
        public event System.Action<Features.Multi.Net.RoomInfo> OnRoomDoubleClicked;
        private float lastClickTime = 0f;
        private const float DOUBLE_CLICK_TIME = 0.5f;
        
        /// <summary>
        /// 방 정보 설정
        /// </summary>
        public void SetupRoom(RoomInfo room)
        {
            roomInfo = room;
            
            UpdateUI();
            
            // 참가 버튼 이벤트
            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(OnJoinButtonClicked);
            }
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (roomInfo == null) return;
            
            // 방 이름
            if (roomNameText != null)
                roomNameText.text = roomInfo.roomName;
            
            // 플레이어 수
            if (playerCountText != null)
                playerCountText.text = $"{roomInfo.currentPlayers}/{roomInfo.maxPlayers}";
            
            // 상태
            if (statusText != null)
            {
                if (roomInfo.isPlaying)
                {
                    statusText.text = "게임중";
                    statusText.color = Color.yellow;
                }
                else if (roomInfo.currentPlayers >= roomInfo.maxPlayers)
                {
                    statusText.text = "가득참";
                    statusText.color = Color.red;
                }
                else
                {
                    statusText.text = "대기중";
                    statusText.color = Color.green;
                }
            }
            
            // 참가 버튼 활성화/비활성화
            if (joinButton != null)
            {
                joinButton.interactable = !roomInfo.isPlaying && (roomInfo.currentPlayers < roomInfo.maxPlayers);
                
                // 버튼 텍스트 변경
                var buttonText = joinButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    if (roomInfo.isPlaying)
                        buttonText.text = "관전";
                    else if (roomInfo.currentPlayers >= roomInfo.maxPlayers)
                        buttonText.text = "가득참";
                    else
                        buttonText.text = "참가";
                }
            }
        }
        
        /// <summary>
        /// 참가 버튼 클릭 - 즉시 방 참가
        /// </summary>
        private void OnJoinButtonClicked()
        {
            if (roomInfo == null) return;
            
            // 참가 버튼 클릭 시 바로 방 참가 처리
            Debug.Log($"[RoomItemUI] 방 참가 버튼 클릭: {roomInfo.roomName}");
            OnRoomDoubleClicked?.Invoke(roomInfo);
        }
        
        /// <summary>
        /// 방 정보 업데이트 (외부에서 호출)
        /// </summary>
        public void UpdateRoom(RoomInfo updatedRoom)
        {
            roomInfo = updatedRoom;
            UpdateUI();
        }
    }
}