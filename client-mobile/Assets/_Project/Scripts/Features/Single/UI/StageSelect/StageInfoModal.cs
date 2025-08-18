using System;
using System.Collections;
using UnityEngine.Networking;
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
        [SerializeField] private Image boardThumbnail;
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
        private static StageInfoModal _instance;

        // 비활성 시 코루틴 시작 에러 방지용 큐
        private string _pendingThumbnailUrl;

        // 싱글톤
        public static StageInfoModal Instance
        {
            get
            {
                // Unity에서 파괴된 객체는 == null 이 true
                if (_instance == null)
                {
#if UNITY_2020_1_OR_NEWER
                    _instance = UnityEngine.Object.FindObjectOfType<StageInfoModal>(true);
#else
            foreach (var m in Resources.FindObjectsOfTypeAll<StageInfoModal>())
            {
                if (m != null && m.gameObject.hideFlags == HideFlags.None)
                {
                    _instance = m;
                    break;
                }
            }
#endif
                }
                return _instance;
            }
            private set { _instance = value; }
        }

        void Awake()
        {
            // 중복 인스턴스가 있어도 파괴하지 않고, '더 적합한' 인스턴스를 고릅니다.
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                var existing = Instance; // 기존 참조(파괴되었거나 비활성일 수 있음)

                // 기존 인스턴스가 파괴되었거나 비활성/계층 비활성이라면 교체
                bool existingInvalid =
                    existing == null ||
                    !existing.isActiveAndEnabled ||
                    !existing.gameObject.activeInHierarchy;

                if (existingInvalid)
                {
                    Instance = this;
                }
                // else: 기존 인스턴스를 유지. 현재 오브젝트는 그대로 두되 파괴하지 않음.
                // (씬 구성에 따라 StageSelectPanel 하위/외부 여러 개가 공존해도 안전)
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

        private void OnEnable()
        {
            // 비활성 중 큐에 쌓인 썸네일 URL 처리
            if (!string.IsNullOrEmpty(_pendingThumbnailUrl))
            {
                BeginThumbnailLoad(_pendingThumbnailUrl);
                _pendingThumbnailUrl = null;
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
            else
            {
                Debug.Log("stageDescriptionText를 찾을 수 없음");
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
        /// 게임 보드 썸네일 업데이트 (PNG 전용)
        /// </summary>
        private void UpdateBoardThumbnail()
        {
            if (boardThumbnail == null || thumbnailPlaceholder == null) return;
            Debug.Log("이미지 URL : " + currentStageData.thumbnail_url);

            if (currentStageData != null && !string.IsNullOrEmpty(currentStageData.thumbnail_url))
            {
                // 썸네일은 웹 서버(3000 포트)의 stage-thumbnails 경로에서 제공
                string thumbnailBaseUrl = "http://localhost:3000";

                // DB의 thumbnail_url이 /stage-1-xxx.png 형태라면 앞의 '/' 제거
                string thumbnailPath = currentStageData.thumbnail_url;
                if (thumbnailPath.StartsWith("/"))
                {
                    thumbnailPath = thumbnailPath.Substring(1);
                }

                string absUrl = $"{thumbnailBaseUrl}/{thumbnailPath}";
                Debug.Log($"스테이지 {currentStageData.stage_number}: 썸네일 URL 로딩 시도 - {absUrl}");
                LoadThumbnailFromUrl(absUrl);
                return;
            }

            Debug.Log($"스테이지 {currentStageData?.stage_number}: 썸네일 없음 - 플레이스홀더 표시");
            boardThumbnail.gameObject.SetActive(false);
            thumbnailPlaceholder.gameObject.SetActive(true);
            UpdateThumbnailPlaceholder();
        }


        /// <summary>
        /// URL에서 썸네일 이미지 로딩
        /// </summary>
        private void LoadThumbnailFromUrl(string url)
        {
            string fullUrl = MakeAbsoluteUrl(url);

            if (!isActiveAndEnabled)
            {
                // 비활성 상태면 큐에 저장 → OnEnable에서 시작
                _pendingThumbnailUrl = fullUrl;
                Debug.Log($"[StageInfoModal] UI 비활성 상태 → 썸네일 대기열 저장: {fullUrl}");
                return;
            }

            BeginThumbnailLoad(fullUrl);
        }

        private void BeginThumbnailLoad(string fullUrl)
        {
            StartCoroutine(LoadPngThumbnailCoroutine(fullUrl));
        }

        /// <summary>
        /// 서버 상대경로를 절대 URL로 보정
        /// - /api/stage-thumbnails/..., /stage-thumbnails/... 모두 지원
        /// - 중복 /api 방지
        /// </summary>
        private static string MakeAbsoluteUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;

            const string ORIGIN = "http://localhost:8080";
            var path = url.StartsWith("/") ? url : "/" + url;

            // 이미 /api/로 시작하면 그대로 사용
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                return ORIGIN + path;
            }

            // /stage-thumbnails 로 시작하면 /api 안 붙임(정적 퍼블릭 경로)
            if (path.StartsWith("/stage-thumbnails/", StringComparison.OrdinalIgnoreCase))
            {
                return ORIGIN + path;
            }

            // 그 외 상대경로는 /api 접두어 부여
            return ORIGIN + "/api" + path;
        }

        /// <summary>
        /// PNG/JPG/WebP 등 래스터 이미지 로더 (서버는 PNG)
        /// </summary>
        private IEnumerator LoadPngThumbnailCoroutine(string url)
        {
            using (var www = UnityWebRequestTexture.GetTexture(url))
            {
                www.timeout = 10;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var tex = DownloadHandlerTexture.GetContent(www);
                    var spr = Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    ApplyThumbnailSprite(spr);
                    ShowPlaceholder(false);
                }
                else
                {
                    Debug.LogWarning($"[StageInfoModal] 썸네일 로딩 실패: {www.error} ({url})");
                    ShowPlaceholder(true);
                }
            }
        }


        private void ApplyThumbnailSprite(Sprite sprite)
        {
            if (boardThumbnail == null)
            {
                Debug.LogWarning("[StageInfoModal] boardThumbnail가 없습니다.");
                return;
            }

            boardThumbnail.sprite = sprite;
            boardThumbnail.preserveAspect = true;
            boardThumbnail.gameObject.SetActive(true);
        }

        private void ShowPlaceholder(bool show)
        {
            if (thumbnailPlaceholder != null)
                thumbnailPlaceholder.SetActive(show);

            if (boardThumbnail != null)
                boardThumbnail.gameObject.SetActive(!show);
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
        /// 사용 가능한 블록들 표시 업데이트
        /// </summary>
        private void UpdateAvailableBlocks()
        {
            if (availableBlocksParent == null || blockButtonPrefab == null)
            {
                Debug.LogWarning("StageInfoModal: availableBlocksParent 또는 blockButtonPrefab이 비어있습니다.");
                return;
            }

            // 🔥 수정: 기존 자식 제거 - 더 안전한 방법
            // Destroy 사용 (프레임 끝에서 삭제)
            int childCount = availableBlocksParent.childCount;
            var childrenToDestroy = new Transform[childCount];
            
            // 먼저 모든 자식을 배열에 저장
            for (int i = 0; i < childCount; i++)
            {
                childrenToDestroy[i] = availableBlocksParent.GetChild(i);
            }
            
            // 배열에서 삭제 (foreach 사용 가능)
            foreach (var child in childrenToDestroy)
            {
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }

            Debug.Log($"기존 블록 버튼 {childCount}개 제거 완료");

            // 데이터가 없으면 전체 블록을 보여주지 않고 끝
            if (currentStageData == null || currentStageData.available_blocks == null || currentStageData.available_blocks.Length == 0)
            {
                Debug.Log("StageInfoModal: availableBlocks 비어있음");
                return;
            }

            Debug.Log($"새로운 블록 버튼 {currentStageData.available_blocks.Length}개 생성 시작");

            // available_blocks 에 명시된 블록만 생성
            foreach (var blockType in currentStageData.available_blocks)
            {
                CreateBlockButton((BlokusUnity.Common.BlockType)blockType);
            }

            Debug.Log($"블록 버튼 생성 완료 - 현재 자식 수: {availableBlocksParent.childCount}");
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
        /// 플레이 버튼 클릭 이벤트
        /// </summary>
        private void OnPlayButtonClicked()
        {
            Debug.Log($"스테이지 {currentStageNumber} 게임 시작!");

            // 현재 스테이지 번호를 임시 변수에 저장 (HideModal()에서 초기화되기 전에)
            int selectedStageNumber = currentStageNumber;

            // 1. StageDataManager에 스테이지 데이터 설정 (가장 중요!)
            if (StageDataManager.Instance != null)
            {
                Debug.Log($"[StageInfoModal] StageDataManager에 스테이지 {selectedStageNumber} 선택 설정");
                StageDataManager.Instance.SelectStage(selectedStageNumber);
            }
            else
            {
                Debug.LogError("[StageInfoModal] StageDataManager.Instance가 null입니다!");
            }

            // 2. 모달 숨기기 (currentStageNumber가 0으로 초기화됨)
            HideModal();

            // 3. 게임 시작 (UI 전환) - 저장된 스테이지 번호 사용
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnStageSelected(selectedStageNumber);
            }
            else
            {
                Debug.LogError("[StageInfoModal] UIManager.Instance가 null입니다!");
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
    }
}