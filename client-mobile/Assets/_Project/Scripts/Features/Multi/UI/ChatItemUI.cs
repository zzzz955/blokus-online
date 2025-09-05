using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Features.Multi.UI
{
    /// <summary>
    /// 개별 채팅 메시지 아이템 UI
    /// 스크롤뷰의 Content에 인스턴스화되어 표시됨
    /// 단일 텍스트로 {displayname}[{time}]: {message} 형식 표시
    /// </summary>
    public class ChatItemUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private TextMeshProUGUI chatMessageText;
        
        [Header("레이아웃 컴포넌트")]
        [SerializeField] private ContentSizeFitter contentSizeFitter;
        
        [Header("스타일 설정")]
        [SerializeField] private Color myMessageColor = new Color(0.8f, 0.9f, 1f, 1f);
        [SerializeField] private Color otherMessageColor = Color.white;
        [SerializeField] private Color systemMessageColor = Color.yellow;
        
        /// <summary>
        /// 채팅 메시지 데이터 설정 - {displayname}[{time}]: {message} 형식
        /// </summary>
        public void SetupMessage(string playerName, string message, bool isMyMessage = false)
        {
            if (chatMessageText != null)
            {
                string timeStamp = System.DateTime.Now.ToString("HH:mm");
                string formattedMessage = $"{playerName}[{timeStamp}]: {message}";
                
                chatMessageText.text = formattedMessage;
                chatMessageText.color = isMyMessage ? myMessageColor : otherMessageColor;
                
                Debug.Log($"[ChatItemUI] 메시지 설정 완료: {formattedMessage}");
            }
            else
            {
                Debug.LogError("[ChatItemUI] chatMessageText가 null입니다!");
            }
                
            // 동적 크기 조정
            RefreshLayout();
        }
        
        /// <summary>
        /// 시스템 메시지용 설정 - [시스템][{time}]: {message} 형식
        /// </summary>
        public void SetupSystemMessage(string message)
        {
            if (chatMessageText != null)
            {
                string timeStamp = System.DateTime.Now.ToString("HH:mm");
                string formattedMessage = $"[시스템][{timeStamp}]: {message}";
                
                chatMessageText.text = formattedMessage;
                chatMessageText.color = systemMessageColor;
            }
                
            // 동적 크기 조정
            RefreshLayout();
        }
        
        /// <summary>
        /// 레이아웃 새로고침 - Layout Group 환경에서 텍스트 변경 후 크기 재계산
        /// </summary>
        private void RefreshLayout()
        {
            var rectTransform = transform as RectTransform;
            
            // 텍스트 컴포넌트 강제 업데이트
            if (chatMessageText != null)
            {
                chatMessageText.ForceMeshUpdate();
                
                // LayoutElement의 preferredHeight를 텍스트의 preferredHeight로 설정
                var layoutElement = GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    float textPreferredHeight = chatMessageText.preferredHeight;
                    float finalHeight = Mathf.Max(textPreferredHeight + 10f, 40f); // 여백 + 최소 높이
                    
                    layoutElement.preferredHeight = finalHeight;
                    Debug.Log($"[ChatItemUI] LayoutElement 높이 설정: {finalHeight}px (텍스트: {textPreferredHeight}px)");
                }
            }
            
            // 부모 Layout Group이 레이아웃을 재계산하도록 요청
            var parent = transform.parent;
            if (parent != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(parent as RectTransform);
            }
            
            // 다음 프레임에 최종 확인
            StartCoroutine(RefreshLayoutNextFrame());
        }
        
        /// <summary>
        /// 다음 프레임에 레이아웃 새로고침 (안전장치)
        /// </summary>
        private System.Collections.IEnumerator RefreshLayoutNextFrame()
        {
            yield return null; // 한 프레임 대기
            
            // 부모 레이아웃 최종 업데이트
            var parent = transform.parent;
            if (parent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);
            }
            
            var rectTransform = transform as RectTransform;
            Debug.Log($"[ChatItemUI] 최종 크기: {rectTransform.sizeDelta.y}px");
        }
        
        /// <summary>
        /// 컴포넌트 초기화 시 자동 설정
        /// </summary>
        private void Awake()
        {
            SetupLayoutComponents();
        }
        
        /// <summary>
        /// 레이아웃 컴포넌트 설정 - Layout Group의 자식으로서 올바른 설정
        /// </summary>
        private void SetupLayoutComponents()
        {
            // Layout Group의 자식에서는 ContentSizeFitter 사용 금지
            // 대신 LayoutElement만 사용하여 크기 제어
            
            // 기존 ContentSizeFitter가 있다면 제거 (경고 방지)
            if (contentSizeFitter != null)
            {
                Debug.Log("[ChatItemUI] Layout Group 자식이므로 ContentSizeFitter 제거");
                DestroyImmediate(contentSizeFitter);
                contentSizeFitter = null;
            }
            
            // LayoutElement 추가 (Vertical Layout Group에서 크기 제어용)
            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = gameObject.AddComponent<LayoutElement>();
                Debug.Log("[ChatItemUI] LayoutElement 추가됨");
            }
            
            // LayoutElement 설정 - 텍스트 내용에 따른 동적 크기 조정
            layoutElement.minHeight = 40f;  // 최소 높이 (1줄 + 여백)
            layoutElement.preferredHeight = -1f;  // 자동 계산
            layoutElement.flexibleHeight = 1f;  // 필요에 따라 확장 가능
            
            // 채팅 메시지 텍스트 설정
            if (chatMessageText != null)
            {
                chatMessageText.enableWordWrapping = true;
                chatMessageText.overflowMode = TextOverflowModes.Overflow;
                
                // 텍스트의 preferredHeight를 LayoutElement가 참조하도록 설정
                var textRectTransform = chatMessageText.rectTransform;
                if (textRectTransform != null)
                {
                    // 텍스트가 자동으로 크기를 결정하도록 앵커 설정
                    textRectTransform.anchorMin = new Vector2(0, 0);
                    textRectTransform.anchorMax = new Vector2(1, 1);
                    textRectTransform.offsetMin = Vector2.zero;
                    textRectTransform.offsetMax = Vector2.zero;
                }
            }
        }
    }
}