using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Features.Multi.Net;
using App.Network;
using System.Collections;

namespace Features.Multi.UI
{
    /// <summary>
    /// AFK 검증 모달 - 서버의 AFK_VERIFY 메시지에 대응
    /// 사용자가 응답하지 않으면 자동으로 연결이 끊어짐을 경고
    /// </summary>
    public class AFKModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private Button responseButton;
        [SerializeField] private Button backgroundButton;
        
        [Header("설정")]
        [SerializeField] private string modalTitle = "활동 확인";
        [SerializeField] private string modalMessage = "아직 계시나요?\n응답하지 않으면 연결이 끊어집니다.";
        [SerializeField] private string responseButtonText = "여기 있어요!";
        [SerializeField] private int countdownDuration = 30; // 30초 카운트다운
        
        [Header("네트워크 참조")]
        [SerializeField] private NetworkManager networkManager;
        
        // 내부 상태
        private bool isModalActive = false;
        private Coroutine countdownCoroutine = null;
        
        private void Awake()
        {
            Debug.Log("[AFKModal] ===== Awake() 시작 =====");

            // NetworkManager 참조 자동 탐색
            if (networkManager == null)
            {
                networkManager = FindObjectOfType<NetworkManager>();
            }

            // 버튼 이벤트 연결
            if (responseButton != null)
            {
                responseButton.onClick.AddListener(OnResponseClicked);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(OnResponseClicked); // 배경 클릭도 응답으로 처리
            }

            // UI 텍스트 설정
            SetupUI();

            // 초기에는 모달 숨김
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }

            // ⭐ 이벤트 구독을 Awake()에서 실행 (GameObject가 비활성화되어도 실행됨)
            SubscribeToEvents();
        }

        /// <summary>
        /// 네트워크 이벤트 구독 (GameObject가 비활성화되어 있어도 실행되도록)
        /// </summary>
        private void SubscribeToEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnAfkVerifyReceived += ShowAFKModal;
                networkManager.OnAfkUnblockSuccess += OnAfkUnblockSuccess;
                Debug.Log("[AFKModal] AFK 이벤트 구독 완료 (Awake)");
            }
            else
            {
                Debug.LogWarning("[AFKModal] NetworkManager를 찾을 수 없습니다!");

                // NetworkManager를 다시 찾기 시도 (늦은 초기화 대비)
                StartCoroutineSafely(RetrySubscribeToEvents());
            }
        }

        /// <summary>
        /// GameObject 활성화 상태를 확인하고 안전하게 코루틴 시작
        /// </summary>
        private void StartCoroutineSafely(System.Collections.IEnumerator coroutine)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.Log("[AFKModal] GameObject가 비활성화 상태 - 코루틴 시작을 위해 활성화");
                gameObject.SetActive(true);
            }

            StartCoroutine(coroutine);
        }

        /// <summary>
        /// GameObject 활성화 상태를 확인하고 안전하게 코루틴 시작 (Coroutine 반환)
        /// </summary>
        private Coroutine StartCoroutineSafelyWithReturn(System.Collections.IEnumerator coroutine)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.Log("[AFKModal] GameObject가 비활성화 상태 - 코루틴 시작을 위해 활성화");
                gameObject.SetActive(true);
            }

            return StartCoroutine(coroutine);
        }

        /// <summary>
        /// NetworkManager가 준비될 때까지 이벤트 구독 재시도
        /// </summary>
        private System.Collections.IEnumerator RetrySubscribeToEvents()
        {
            float retryTime = 0f;
            const float maxRetryTime = 5f; // 최대 5초 대기

            while (networkManager == null && retryTime < maxRetryTime)
            {
                yield return new WaitForSeconds(0.5f);
                retryTime += 0.5f;

                networkManager = FindObjectOfType<NetworkManager>();
                if (networkManager != null)
                {
                    Debug.Log("[AFKModal] NetworkManager 재탐색 성공, 이벤트 구독 중");
                    SubscribeToEvents();
                    break;
                }
            }

            if (networkManager == null)
            {
                Debug.LogError("[AFKModal] NetworkManager를 찾을 수 없습니다! 이벤트 구독 실패");
            }
        }
        
        private void OnEnable()
        {
            // 이벤트 구독은 이제 Awake()에서 처리됨
            Debug.Log("[AFKModal] 컴포넌트 활성화됨");
        }
        
        private void OnDisable()
        {
            // 이벤트 구독 해제는 OnDestroy에서 처리됨
            Debug.Log("[AFKModal] 컴포넌트 비활성화됨");
        }
        
        private void OnDestroy()
        {
            // 네트워크 이벤트 구독 해제
            if (networkManager != null)
            {
                networkManager.OnAfkVerifyReceived -= ShowAFKModal;
                networkManager.OnAfkUnblockSuccess -= OnAfkUnblockSuccess;
                Debug.Log("[AFKModal] AFK 이벤트 구독 해제 완료");
            }

            // 버튼 이벤트 해제
            if (responseButton != null)
            {
                responseButton.onClick.RemoveListener(OnResponseClicked);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveListener(OnResponseClicked);
            }

            // 카운트다운 코루틴 정리
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }
        }
        
        /// <summary>
        /// UI 텍스트 초기 설정
        /// </summary>
        private void SetupUI()
        {
            if (titleText != null)
                titleText.text = modalTitle;
                
            if (messageText != null)
                messageText.text = modalMessage;
                
            if (responseButton != null)
            {
                var buttonText = responseButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = responseButtonText;
            }
        }
        
        /// <summary>
        /// AFK 검증 모달 표시 (서버 AFK_VERIFY 메시지 수신 시 호출)
        /// </summary>
        private void ShowAFKModal()
        {
            Debug.Log("[AFKModal] ===== ShowAFKModal() 호출됨 =====");

            if (isModalActive)
            {
                Debug.LogWarning("[AFKModal] AFK 모달이 이미 활성화되어 있습니다!");
                return;
            }

            Debug.Log("[AFKModal] AFK 검증 모달 표시 시작");
            
            // 부모 GameObject 활성화
            if (!gameObject.activeInHierarchy)
            {
                Debug.Log("[AFKModal] GameObject가 비활성화 상태였음 - 활성화 중");
                gameObject.SetActive(true);
            }
            else
            {
                Debug.Log("[AFKModal] GameObject는 이미 활성화 상태");
            }

            // 모달 표시
            if (modalPanel != null)
            {
                Debug.Log("[AFKModal] modalPanel 활성화 중");
                modalPanel.SetActive(true);
                EnsureModalOnTop();
            }
            else
            {
                Debug.LogError("[AFKModal] modalPanel이 null입니다! Inspector에서 할당되었는지 확인하세요.");
            }
            
            isModalActive = true;
            
            // 카운트다운 시작
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
            }
            countdownCoroutine = StartCoroutineSafelyWithReturn(CountdownCoroutine());
        }
        
        /// <summary>
        /// AFK 모달 숨김
        /// </summary>
        private void HideAFKModal()
        {
            Debug.Log("[AFKModal] AFK 검증 모달 숨김");
            
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
            
            isModalActive = false;
            
            // 카운트다운 중지
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }
        }
        
        /// <summary>
        /// 응답 버튼 클릭 처리
        /// </summary>
        private void OnResponseClicked()
        {
            Debug.Log("[AFKModal] 사용자 응답: AFK_UNBLOCK 메시지 전송");
            
            // 서버에 AFK_UNBLOCK 메시지 전송
            if (networkManager != null && networkManager.GetNetworkClient() != null)
            {
                networkManager.GetNetworkClient().SendCleanTCPMessage("AFK_UNBLOCK");
            }
            else
            {
                Debug.LogError("[AFKModal] NetworkManager 또는 NetworkClient를 찾을 수 없습니다!");
                // 오류 상황에서도 모달은 닫음
                HideAFKModal();
            }
        }
        
        /// <summary>
        /// AFK 해제 성공 응답 처리
        /// </summary>
        private void OnAfkUnblockSuccess()
        {
            Debug.Log("[AFKModal] AFK 해제 성공 - 모달 닫기");
            HideAFKModal();
        }
        
        /// <summary>
        /// 카운트다운 코루틴
        /// </summary>
        private System.Collections.IEnumerator CountdownCoroutine()
        {
            int remainingTime = countdownDuration;
            
            while (remainingTime > 0 && isModalActive)
            {
                // 카운트다운 텍스트 업데이트
                if (countdownText != null)
                {
                    countdownText.text = $"남은 시간: {remainingTime}초";
                    
                    // 시간이 얼마 안 남았을 때 색상 변경
                    if (remainingTime <= 10)
                    {
                        countdownText.color = Color.red;
                    }
                    else if (remainingTime <= 20)
                    {
                        countdownText.color = Color.yellow;
                    }
                    else
                    {
                        countdownText.color = Color.white;
                    }
                }
                
                yield return new WaitForSeconds(1f);
                remainingTime--;
            }
            
            // 시간 초과 - 연결 끊어짐 경고
            if (isModalActive)
            {
                Debug.LogWarning("[AFKModal] AFK 응답 시간 초과 - 연결이 끊어질 예정");
                
                if (countdownText != null)
                {
                    countdownText.text = "시간 초과 - 연결 종료 중...";
                    countdownText.color = Color.red;
                }
                
                // 추가 대기 후 모달 닫기 (서버에서 연결을 끊을 것으로 예상)
                yield return new WaitForSeconds(3f);
                HideAFKModal();
            }
        }
        
        /// <summary>
        /// 모달을 최상단에 표시되도록 보장
        /// </summary>
        private void EnsureModalOnTop()
        {
            if (modalPanel == null) return;
            
            // Transform을 최상단으로 이동
            modalPanel.transform.SetAsLastSibling();
            
            // Canvas 설정
            var canvas = modalPanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = modalPanel.AddComponent<Canvas>();
            }
            
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000; // AFK는 높은 우선순위
            
            // GraphicRaycaster 확인
            var raycaster = modalPanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = modalPanel.AddComponent<GraphicRaycaster>();
            }
            
            // CanvasGroup으로 입력 차단
            var canvasGroup = modalPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = modalPanel.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            
            Debug.Log("[AFKModal] 모달이 최상단에 배치되었습니다");
        }
        
        /// <summary>
        /// Android 뒤로가기 버튼 처리 (AFK는 취소 불가)
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && isModalActive)
            {
                // AFK 모달에서는 뒤로가기로 닫을 수 없음 - 응답 버튼만 사용
                Debug.Log("[AFKModal] AFK 모달은 뒤로가기로 닫을 수 없습니다. 응답 버튼을 사용하세요.");
            }
        }
    }
}