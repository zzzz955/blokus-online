using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Data;
using UserStageProgress = BlokusUnity.Game.UserStageProgress;
using StageData = BlokusUnity.Data.StageData;
using BlokusUnity.Game;
using BlokusUnity.Network;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 스테이지 정보 표시 모달
    /// 스테이지 클릭 시 상세 정보와 게임 시작 버튼을 제공
    /// </summary>
    public class StageInfoModal : MonoBehaviour
    {
        [Header("모달 컴포넌트")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button playButton;

        [Header("스테이지 정보 UI")]
        [SerializeField] private TextMeshProUGUI stageNumberText;
        [SerializeField] private TextMeshProUGUI stageDescriptionText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private Image[] starImages;

        [Header("게임 보드 미리보기")]
        [SerializeField] private RawImage boardThumbnail;
        [SerializeField] private GameObject thumbnailPlaceholder;

        [Header("제약 조건 UI")]
        [SerializeField] private TextMeshProUGUI maxUndoText;
        [SerializeField] private TextMeshProUGUI timeLimitText;
        [SerializeField] private Transform availableBlocksParent;

        [Header("별 스프라이트 (StageButton과 동일)")]
        [SerializeField] private Sprite activeStar; // 활성화된 별 이미지
        [SerializeField] private Sprite inactiveStar; // 비활성화된 별 이미지

        [Header("블록 아이콘 스프라이트")]
        // 미리보기용 BlockButton 프리팹 (Assets/Prefabs/BlockButton.prefab)
        [SerializeField] private BlokusUnity.Game.BlockButton blockButtonPrefab;
        // 모달에서는 선택할 필요가 없으므로 미리보기용 플레이어 컬러(색상만 사용)
        [SerializeField] private BlokusUnity.Common.PlayerColor previewPlayerColor = BlokusUnity.Common.PlayerColor.Blue;
        [SerializeField] private BlockSkin previewSkin;

        [Header("색상 설정 (Fallback)")]
        [SerializeField] private Color activeStarColor = Color.yellow;
        [SerializeField] private Color inactiveStarColor = Color.gray;
        [SerializeField] private Color[] difficultyColors = { Color.green, Color.yellow, new Color(1f, 0.5f, 0f), Color.red };

        // 현재 표시 중인 스테이지 정보
        private StageData currentStageData;
        private StageProgress currentProgress;
        private int currentStageNumber;

        // 싱글톤
        public static StageInfoModal Instance { get; private set; }

        void Awake()
        {
            // 싱글톤 설정 (Scene 내에서만)
            if (Instance == null)
            {
                Instance = this;
                // UI 모달은 DontDestroyOnLoad 불필요
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 버튼 이벤트 연결
            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(HideModal);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HideModal);
            }

            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayButtonClicked);
            }

            // 초기 상태로 숨김 (Awake에서는 데이터 초기화하지 않음)
            // gameObject.SetActive(false)는 Awake에서 호출하면 위험하므로 생략
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 스테이지 정보 모달 표시 (UserStageProgress 오버로드)
        /// </summary>
        public void ShowStageInfo(StageData stageData, UserStageProgress userProgress)
        {
            Debug.Log($"[DEBUG] ShowStageInfo 호출됨: stageData={stageData?.stage_number}, userProgress={userProgress?.stageNumber}");

            if (stageData == null)
            {
                Debug.LogError("표시할 스테이지 데이터가 없습니다!");
                return;
            }

            currentStageData = stageData;
            currentStageNumber = stageData.stage_number;

            Debug.Log($"[DEBUG] currentStageNumber 설정됨: {currentStageNumber}");

            // UserStageProgress를 StageProgress로 변환
            if (userProgress != null)
            {
                currentProgress = new StageProgress
                {
                    stageNumber = userProgress.stageNumber,
                    isCompleted = userProgress.isCompleted,
                    bestScore = userProgress.bestScore
                };
            }
            else
            {
                currentProgress = null;
            }

            // UI 업데이트
            UpdateModalUI();

            // 로그 먼저 출력 (gameObject.SetActive 전에)
            Debug.Log($"스테이지 {currentStageNumber} 정보 모달 표시");

            // 모달 표시 - 전체 GameObject 활성화 (마지막에)
            gameObject.SetActive(true);
        }


        /// <summary>
        /// 모달 UI 업데이트
        /// </summary>
        private void UpdateModalUI()
        {
            // 기본 스테이지 정보
            UpdateBasicInfo();

            // 별점 표시
            UpdateStarDisplay();

            // 제약 조건
            UpdateConstraints();

            // 게임 보드 썸네일
            UpdateBoardThumbnail();

            // 사용 가능한 블록들
            UpdateAvailableBlocks();
        }

        /// <summary>
        /// 기본 스테이지 정보 업데이트
        /// </summary>
        private void UpdateBasicInfo()
        {
            // 스테이지 번호
            if (stageNumberText != null)
            {
                stageNumberText.text = $"스테이지 {currentStageData.stage_number}";
            }

            // 설명
            if (stageDescriptionText != null)
            {
                stageDescriptionText.text = currentStageData.stage_description;
            }

            // 난이도
            if (difficultyText != null)
            {
                string difficultyStr = GetDifficultyString(currentStageData.difficulty);
                difficultyText.text = $"난이도: {difficultyStr}";

                // 난이도별 색상 적용
                if (currentStageData.difficulty > 0 && currentStageData.difficulty <= difficultyColors.Length)
                {
                    difficultyText.color = difficultyColors[currentStageData.difficulty - 1];
                }
            }
        }

        /// <summary>
        /// 별점 표시 업데이트 (StageButton과 동일한 방식)
        /// </summary>
        private void UpdateStarDisplay()
        {
            if (starImages == null || starImages.Length == 0)
            {
                Debug.LogWarning("StageInfoModal: starImages 배열이 비어있습니다.");
                return;
            }

            int earnedStars = 0;
            if (currentProgress != null)
            {
                // StageProgress에서 별점 계산
                if (currentProgress.isCompleted)
                {
                    if (currentProgress.bestScore >= currentStageData.threeStar)
                        earnedStars = 3;
                    else if (currentProgress.bestScore >= currentStageData.twoStar)
                        earnedStars = 2;
                    else if (currentProgress.bestScore >= currentStageData.oneStar)
                        earnedStars = 1;
                }
            }

            Debug.Log($"StageInfoModal: 별점 업데이트 - 획득한 별: {earnedStars}/{starImages.Length}");

            // StageButton과 동일한 방식으로 별 스프라이트/색상 적용
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null)
                {
                    bool shouldActivate = i < earnedStars;

                    if (shouldActivate)
                    {
                        // 활성화된 별 - 스프라이트 우선, 색상 폴백
                        if (activeStar != null)
                        {
                            starImages[i].sprite = activeStar;
                            starImages[i].color = Color.white; // 스프라이트 사용시 색상 취소
                        }
                        else
                        {
                            // 스프라이트가 없으면 색상만 변경
                            starImages[i].color = activeStarColor;
                        }
                    }
                    else
                    {
                        // 비활성화된 별 - 스프라이트 우선, 색상 폴백
                        if (inactiveStar != null)
                        {
                            starImages[i].sprite = inactiveStar;
                            starImages[i].color = Color.white; // 스프라이트 사용시 색상 취소
                        }
                        else
                        {
                            // 스프라이트가 없으면 색상만 변경
                            starImages[i].color = inactiveStarColor;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 제약 조건 업데이트
        /// </summary>
        private void UpdateConstraints()
        {
            // 최대 되돌리기 횟수
            if (maxUndoText != null)
            {
                if (currentStageData.max_undo_count > 0)
                {
                    maxUndoText.text = $"되돌리기: {currentStageData.max_undo_count}회";
                }
                else
                {
                    maxUndoText.text = "되돌리기: 무제한";
                }
            }

            // 제한 시간
            if (timeLimitText != null)
            {
                if (currentStageData.time_limit > 0)
                {
                    int minutes = currentStageData.time_limit / 60;
                    int seconds = currentStageData.time_limit % 60;
                    timeLimitText.text = $"제한시간: {minutes:D2}:{seconds:D2}";
                }
                else
                {
                    timeLimitText.text = "제한시간: 없음";
                }
            }
        }

        /// <summary>
        /// 게임 보드 썸네일 업데이트
        /// </summary>
        private void UpdateBoardThumbnail()
        {
            if (boardThumbnail == null || thumbnailPlaceholder == null) return;

            // 1) 서버가 준 상대경로(/stage-thumbnails/...)를 절대경로로 변환해 로딩
            if (currentStageData != null && !string.IsNullOrEmpty(currentStageData.thumbnail_url))
            {
                // HttpApiClient의 베이스에서 호스트/포트만 뽑아 조립
                string baseUrl = (HttpApiClient.Instance != null && !string.IsNullOrEmpty(HttpApiClient.Instance.ApiBaseUrl))
                    ? HttpApiClient.Instance.ApiBaseUrl
                    : "http://localhost:3000";

                string absUrl = currentStageData.GetAbsoluteThumbnailUrl(baseUrl);
                Debug.Log($"스테이지 {currentStageData.stage_number}: 썸네일 URL 로딩 시도 - {absUrl}");
                LoadThumbnailFromUrl(absUrl);
                return;
            }

            Debug.Log($"스테이지 {currentStageData?.stage_number}: 초기 보드 상태 없음 - 플레이스홀더 표시");
            boardThumbnail.gameObject.SetActive(false);
            thumbnailPlaceholder.gameObject.SetActive(true);
            UpdateThumbnailPlaceholder();
        }

        /// <summary>
        /// URL에서 썸네일 이미지 로딩
        /// </summary>
        private void LoadThumbnailFromUrl(string url)
        {
            // 개발환경에서는 localhost, 프로덕션에서는 실제 호스트
            string fullUrl = url.StartsWith("http") ? url : $"http://localhost:3000{url}";

            Debug.Log($"[StageInfoModal] 썸네일 로딩 시작: {fullUrl}");
            StartCoroutine(LoadThumbnailCoroutine(fullUrl));
        }

        /// <summary>
        /// 썸네일 이미지 로딩 코루틴
        /// </summary>
        private System.Collections.IEnumerator LoadThumbnailCoroutine(string url)
        {
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                www.timeout = 10; // 10초 타임아웃
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Texture2D texture = ((UnityEngine.Networking.DownloadHandlerTexture)www.downloadHandler).texture;
                    if (texture != null)
                    {
                        boardThumbnail.texture = texture;
                        boardThumbnail.gameObject.SetActive(true);
                        thumbnailPlaceholder.SetActive(false);
                        Debug.Log($"썸네일 로딩 성공: {url}");
                    }
                }
                else
                {
                    Debug.LogWarning($"썸네일 로딩 실패: {url} - {www.error}");
                    // 실패시 플레이스홀더 표시
                    boardThumbnail.gameObject.SetActive(false);
                    thumbnailPlaceholder.SetActive(true);
                    UpdateThumbnailPlaceholder();
                }
            }
        }

        /// <summary>
        /// 간단한 썸네일 생성 (임시 구현)
        /// </summary>
        private void GenerateSimpleThumbnail()
        {
            // TODO: 실제 보드 렌더링 로직 구현
            // 현재는 단순히 보드가 있다는 표시만
            boardThumbnail.gameObject.SetActive(true);
            thumbnailPlaceholder.SetActive(false);

            // RawImage에 간단한 패턴 생성 (임시)
            if (boardThumbnail.texture == null)
            {
                // 기본 체크무늬 패턴 생성
                Texture2D simpleTexture = CreateSimpleBoardTexture();
                boardThumbnail.texture = simpleTexture;
            }
        }

        /// <summary>
        /// 썸네일 플레이스홀더 업데이트
        /// </summary>
        private void UpdateThumbnailPlaceholder()
        {
            if (thumbnailPlaceholder != null)
            {
                TextMeshProUGUI placeholderText = thumbnailPlaceholder.GetComponentInChildren<TextMeshProUGUI>();
                if (placeholderText != null)
                {
                    if (currentStageData != null)
                    {
                        placeholderText.text = $"스테이지 {currentStageData.stage_number}\n빈 보드";
                    }
                    else
                    {
                        placeholderText.text = "보드 미리보기\n준비 중...";
                    }
                }
            }
        }

        /// <summary>
        /// 간단한 보드 텍스처 생성 (임시)
        /// </summary>
        private Texture2D CreateSimpleBoardTexture()
        {
            int size = 64; // 작은 썸네일 크기
            Texture2D texture = new Texture2D(size, size);

            // 체크무늬 패턴
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    bool isEven = (x / 8 + y / 8) % 2 == 0;
                    Color color = isEven ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.7f, 0.7f, 0.7f);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 사용 가능한 블록들 표시 업데이트
        /// </summary>
        private void UpdateAvailableBlocks()
        {
            if (availableBlocksParent == null || blockButtonPrefab == null)
            {
                Debug.LogWarning("StageInfoModal: availableBlocksParent 또는 blockButtonPrefab이 비어있습니다.");
                return;
            }

            // 기존 자식 제거
            foreach (Transform child in availableBlocksParent)
                DestroyImmediate(child.gameObject);

            // 데이터가 없으면 전체 블록을 보여주지 않고 끝
            if (currentStageData == null || currentStageData.available_blocks == null || currentStageData.available_blocks.Length == 0)
            {
                Debug.Log("StageInfoModal: availableBlocks 비어있음");
                return;
            }

            // available_blocks 에 명시된 블록만 생성
            foreach (var blockType in currentStageData.available_blocks)
                CreateBlockButton((BlokusUnity.Common.BlockType)blockType);
        }

        private void CreateBlockButton(BlokusUnity.Common.BlockType blockType)
        {
            var btn = Instantiate(blockButtonPrefab, availableBlocksParent);

            // BlockButton은 팔레트 선택용 컴포넌트라서 클릭/선택이 가능하지만,
            // 모달에선 "미리보기"만 필요하므로 클릭 비활성화 + 하이라이트 없이 사용
            var uibutton = btn.GetComponent<UnityEngine.UI.Button>();
            if (uibutton != null) uibutton.interactable = false;

            // Init(owner, type, player, baseColor, title)
            // owner는 선택 로직에만 필요 -> null 전달해도 OK
            // baseColor는 미사용(스킨/기본색 내부 계산 사용)
            btn.Init(null, blockType, previewPlayerColor, Color.white, null);

            // (선택) 스킨 주입
            if (previewSkin != null)
            {
                var field = typeof(BlokusUnity.Game.BlockButton).GetField("skin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) field.SetValue(btn, previewSkin);
            }
        }

        /// <summary>
        /// 개별 블록 아이콘 생성 (스프라이트 우선, 색상 폴백)
        /// </summary>
        private void CreateAllBlocksText()
        {
            GameObject textObj = new GameObject("AllBlocksText");
            textObj.transform.SetParent(availableBlocksParent, false);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "모든 블록 사용 가능";
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            RectTransform rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 30);
        }

        /// <summary>
        /// 플레이 버튼 클릭 이벤트
        /// </summary>
        private void OnPlayButtonClicked()
        {
            Debug.Log($"스테이지 {currentStageNumber} 게임 시작!");

            // 모달 숨기기
            HideModal();

            // 게임 시작
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnStageSelected(currentStageNumber);
            }
        }


        /// <summary>
        /// 모달 숨기기
        /// </summary>
        public void HideModal()
        {
            // 전체 GameObject 비활성화
            gameObject.SetActive(false);

            // 현재 데이터 초기화
            currentStageData = null;
            currentProgress = null;
            currentStageNumber = 0;
        }

        // ========================================
        // 헬퍼 함수들
        // ========================================

        /// <summary>
        /// 난이도 문자열 반환
        /// </summary>
        private string GetDifficultyString(int difficulty)
        {
            switch (difficulty)
            {
                case 1: return "쉬움";
                case 2: return "보통";
                case 3: return "어려움";
                case 4: return "매우 어려움";
                default: return "알 수 없음";
            }
        }

        /// <summary>
        /// 블록 타입별 색상 반환 (임시)
        /// </summary>
        private Color GetBlockTypeColor(BlokusUnity.Common.BlockType blockType)
        {
            // 블록 타입에 따른 임시 색상 (나중에 실제 블록 색상으로 교체)
            int typeNumber = (int)blockType;
            float hue = (typeNumber * 0.1f) % 1f;
            return Color.HSVToRGB(hue, 0.8f, 0.9f);
        }

        /// <summary>
        /// 현재 표시 중인 스테이지 번호 반환
        /// </summary>
        public int GetCurrentStageNumber()
        {
            return currentStageNumber;
        }

        /// <summary>
        /// 모달이 표시 중인지 확인
        /// </summary>
        public bool IsShowing()
        {
            return gameObject.activeInHierarchy;
        }
    }
}