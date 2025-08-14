using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Data;
using BlokusUnity.Game;

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
        [SerializeField] private TextMeshProUGUI stageNameText;
        [SerializeField] private TextMeshProUGUI stageDescriptionText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private Image[] starImages;
        [SerializeField] private TextMeshProUGUI bestScoreText;
        [SerializeField] private TextMeshProUGUI targetScoreText;
        
        [Header("게임 보드 미리보기")]
        [SerializeField] private RawImage boardThumbnail;
        [SerializeField] private GameObject thumbnailPlaceholder;
        
        [Header("제약 조건 UI")]
        [SerializeField] private TextMeshProUGUI maxUndoText;
        [SerializeField] private TextMeshProUGUI timeLimitText;
        [SerializeField] private Transform availableBlocksParent;
        [SerializeField] private GameObject blockIconPrefab;
        
        [Header("별 스프라이트 (StageButton과 동일)")]
        [SerializeField] private Sprite activeStar; // 활성화된 별 이미지
        [SerializeField] private Sprite inactiveStar; // 비활성화된 별 이미지
        
        [Header("블록 아이콘 스프라이트")]
        [SerializeField] private Sprite[] blockSprites = new Sprite[21]; // 21개 블록 타입별 스프라이트
        [SerializeField] private Sprite defaultBlockSprite; // 기본 블록 스프라이트 (폴백)
        
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
            
            // 한글 폰트 문제 확인 및 로깅
            CheckFontIssues();
            
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
            Debug.Log($"[DEBUG] ShowStageInfo 호출됨: stageData={stageData?.stageNumber}, userProgress={userProgress?.stageNumber}");
            
            if (stageData == null)
            {
                Debug.LogError("표시할 스테이지 데이터가 없습니다!");
                return;
            }
            
            currentStageData = stageData;
            currentStageNumber = stageData.stageNumber;
            
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
            
            // 점수 정보
            UpdateScoreInfo();
            
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
                stageNumberText.text = $"스테이지 {currentStageData.stageNumber}";
            }
            
            // 스테이지 이름
            if (stageNameText != null)
            {
                stageNameText.text = currentStageData.stageName;
            }
            
            // 설명
            if (stageDescriptionText != null)
            {
                stageDescriptionText.text = currentStageData.stageDescription;
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
        /// 점수 정보 업데이트
        /// </summary>
        private void UpdateScoreInfo()
        {
            // 최고 점수
            if (bestScoreText != null)
            {
                if (currentProgress != null && currentProgress.bestScore > 0)
                {
                    bestScoreText.text = $"최고 점수: {currentProgress.bestScore:N0}점";
                }
                else
                {
                    bestScoreText.text = "최고 점수: -";
                }
            }
            
            // 목표 점수 (별 조건)
            if (targetScoreText != null)
            {
                string targetInfo = $"★ {currentStageData.oneStar:N0}점";
                if (currentStageData.twoStar > 0)
                    targetInfo += $"  ★★ {currentStageData.twoStar:N0}점";
                if (currentStageData.threeStar > 0)
                    targetInfo += $"  ★★★ {currentStageData.threeStar:N0}점";
                
                targetScoreText.text = targetInfo;
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
                if (currentStageData.maxUndoCount > 0)
                {
                    maxUndoText.text = $"되돌리기: {currentStageData.maxUndoCount}회";
                }
                else
                {
                    maxUndoText.text = "되돌리기: 무제한";
                }
            }
            
            // 제한 시간
            if (timeLimitText != null)
            {
                if (currentStageData.timeLimit > 0)
                {
                    int minutes = currentStageData.timeLimit / 60;
                    int seconds = currentStageData.timeLimit % 60;
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
            
            // API에서 thumbnail_url이 제공되면 이미지 로딩 시도  
            if (currentStageData != null && !string.IsNullOrEmpty(currentStageData.thumbnail_url))
            {
                Debug.Log($"스테이지 {currentStageData.stageNumber}: 썸네일 URL 로딩 시도 - {currentStageData.thumbnail_url}");
                LoadThumbnailFromUrl(currentStageData.thumbnail_url);
                return;
            }
            
            // 초기 보드 상태가 있으면 썸네일 생성 시도
            if (currentStageData != null && currentStageData.initial_board_state?.placements != null && currentStageData.initial_board_state.placements.Length > 0)
            {
                Debug.Log($"스테이지 {currentStageData.stageNumber}: 초기 보드 상태 있음 ({currentStageData.initial_board_state.placements.Length}개 블록)");
                GenerateSimpleThumbnail();
            }
            else
            {
                // 초기 보드 상태가 없으면 플레이스홀더 표시
                Debug.Log($"스테이지 {currentStageData?.stageNumber}: 초기 보드 상태 없음 - 플레이스홀더 표시");
                boardThumbnail.gameObject.SetActive(false);
                thumbnailPlaceholder.SetActive(true);
                UpdateThumbnailPlaceholder();
            }
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
                        placeholderText.text = $"스테이지 {currentStageData.stageNumber}\n빈 보드";
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
            if (availableBlocksParent == null || blockIconPrefab == null) return;
            
            // 기존 블록 아이콘들 제거
            foreach (Transform child in availableBlocksParent)
            {
                DestroyImmediate(child.gameObject);
            }
            
            // 사용 가능한 블록들 표시
            if (currentStageData.availableBlocks != null && currentStageData.availableBlocks.Count > 0)
            {
                foreach (var blockType in currentStageData.availableBlocks)
                {
                    CreateBlockIcon(blockType);
                }
            }
            else
            {
                // 모든 블록 사용 가능
                CreateAllBlocksText();
            }
        }
        
        /// <summary>
        /// 개별 블록 아이콘 생성 (스프라이트 우선, 색상 폴백)
        /// </summary>
        private void CreateBlockIcon(BlokusUnity.Common.BlockType blockType)
        {
            GameObject iconObj = Instantiate(blockIconPrefab, availableBlocksParent);
            
            // 블록 스프라이트 적용 (1-based index를 0-based로 변환)
            int blockIndex = (int)blockType - 1; // BlockType은 1부터 시작
            
            // 블록 이미지 설정
            Image iconImage = iconObj.GetComponent<Image>();
            if (iconImage != null)
            {
                if (blockIndex >= 0 && blockIndex < blockSprites.Length && blockSprites[blockIndex] != null)
                {
                    // 블록별 전용 스프라이트 사용
                    iconImage.sprite = blockSprites[blockIndex];
                    iconImage.color = Color.white; // 스프라이트 원본 색상 사용
                    Debug.Log($"블록 {blockType}: 전용 스프라이트 적용 ({blockSprites[blockIndex].name})");
                }
                else if (defaultBlockSprite != null)
                {
                    // 기본 블록 스프라이트 사용하고 색상 변경
                    iconImage.sprite = defaultBlockSprite;
                    iconImage.color = GetBlockTypeColor(blockType);
                    Debug.Log($"블록 {blockType}: 기본 스프라이트 + 색상 적용");
                }
                else
                {
                    // 스프라이트가 없으면 색상만 설정 (Fallback)
                    iconImage.color = GetBlockTypeColor(blockType);
                    Debug.Log($"블록 {blockType}: 색상만 적용 (Fallback)");
                }
            }
            
            // 블록 번호 라벨 (선택사항)
            TextMeshProUGUI labelText = iconObj.GetComponentInChildren<TextMeshProUGUI>();
            if (labelText != null)
            {
                // 블록 번호 표시 또는 숨김 (스프라이트가 있으면 숨김)
                bool hasSprite = (blockIndex >= 0 && blockIndex < blockSprites.Length && blockSprites[blockIndex] != null) || defaultBlockSprite != null;
                
                if (hasSprite)
                {
                    labelText.gameObject.SetActive(false); // 스프라이트가 있으면 번호 숨김
                }
                else
                {
                    labelText.text = blockType.ToString().Replace("Type", ""); // "Type01" → "01"
                    labelText.gameObject.SetActive(true);
                }
            }
            
            // 블록 아이콘 크기 조정 (선택사항)
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.sizeDelta = new Vector2(32, 32); // 적당한 크기로 설정
            }
        }
        
        /// <summary>
        /// 모든 블록 사용 가능 텍스트 생성
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
        
        // ========================================
        // 폰트 문제 해결 관련
        // ========================================
        
        /// <summary>
        /// 한글 폰트 문제 확인 및 로깅
        /// </summary>
        private void CheckFontIssues()
        {
            // 모든 TextMeshProUGUI 컴포넌트 확인
            TextMeshProUGUI[] textComponents = {
                stageNumberText, stageNameText, stageDescriptionText,
                difficultyText, bestScoreText, targetScoreText,
                maxUndoText, timeLimitText
            };
            
            bool hasKoreanFontIssue = false;
            
            foreach (var textComponent in textComponents)
            {
                if (textComponent != null)
                {
                    var fontAsset = textComponent.font;
                    if (fontAsset != null)
                    {
                        // 폰트 에셋 정보 로깅
                        Debug.Log($"TextMeshPro Component: {textComponent.name}, Font: {fontAsset.name}, " +
                                 $"Character Count: {fontAsset.characterTable?.Count ?? 0}");
                        
                        // 한글 문자가 포함되어 있는지 확인 (간단한 검사)
                        bool hasKoreanChars = HasKoreanCharacters(fontAsset);
                        if (!hasKoreanChars)
                        {
                            Debug.LogWarning($"⚠️ {textComponent.name}: 한글 문자가 포함되지 않은 폰트를 사용하고 있습니다. " +
                                           $"폰트: {fontAsset.name}");
                            hasKoreanFontIssue = true;
                        }
                    }
                    else
                    {
                        Debug.LogError($"❌ {textComponent.name}: 폰트 에셋이 할당되지 않았습니다!");
                        hasKoreanFontIssue = true;
                    }
                }
            }
            
            if (hasKoreanFontIssue)
            {
                Debug.LogError("🚨 한글 폰트 문제 감지됨!\n" +
                              "해결 방법:\n" +
                              "1. Window → TextMeshPro → Font Asset Creator 열기\n" +
                              "2. 한글을 지원하는 폰트 선택 (예: NotoSansCJK)\n" +
                              "3. Character Set: Unicode Range (Hex)\n" +
                              "4. Character Sequence (Hex): AC00-D7AF (한글 완성형)\n" +
                              "5. Atlas Resolution: 2048x2048 또는 4096x4096\n" +
                              "6. Generate Font Atlas 클릭\n" +
                              "7. Save를 눌러 새 Font Asset 생성\n" +
                              "8. TextMeshPro 컴포넌트에 새 Font Asset 할당");
            }
            else
            {
                Debug.Log("✅ 폰트 확인 완료: 한글 지원 폰트가 설정되어 있습니다.");
            }
        }
        
        /// <summary>
        /// 폰트 에셋에 한글 문자가 포함되어 있는지 확인
        /// </summary>
        private bool HasKoreanCharacters(TMPro.TMP_FontAsset fontAsset)
        {
            if (fontAsset.characterTable == null || fontAsset.characterTable.Count == 0)
                return false;
                
            // 한글 완성형 범위 확인 (가-힣: U+AC00-U+D7AF)
            foreach (var character in fontAsset.characterTable)
            {
                uint unicode = character.unicode;
                if (unicode >= 0xAC00 && unicode <= 0xD7AF) // 한글 완성형 범위
                {
                    return true;
                }
            }
            
            // 한글 자모 범위도 확인 (ㄱ-ㅎ, ㅏ-ㅣ: U+3131-U+318E)
            foreach (var character in fontAsset.characterTable)
            {
                uint unicode = character.unicode;
                if (unicode >= 0x3131 && unicode <= 0x318E) // 한글 자모 범위
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}