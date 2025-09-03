// Assets/Scripts/Game/SingleGameManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Shared.Models;
using App.Core;                  // GameLogic
using Features.Single.UI.InGame; // GameResultModal

namespace Features.Single.Gameplay
{
    /// <summary>
    /// 싱글플레이 게임 세션 관리 + Undo 지원
    /// - StageSelectPanel의 시작 버튼 → GamePanel → SingleGameManager 로 진입
    /// - 데이터 적용(Init) 완료 시 OnGameReady 이벤트를 발생시켜 GamePanel이 UI를 활성화
    /// </summary>
    public class SingleGameManager : MonoBehaviour
    {
        // ====== 이벤트 (GamePanel이 구독) ======
        public static event Action OnGameReady; // ✅ 게임 준비 완료 브로드캐스트

        // ===== Singleton & Legacy statics =====
        public static SingleGameManager Instance { get; private set; }
        public static int CurrentStage { get; private set; } = 0;
        public static Features.Single.Core.StageDataManager StageManager { get; private set; }
        public static bool IsInGameplayMode { get; private set; } = false; // 🔥 게임플레이 모드 플래그

        public static void SetStageContext(int stageNumber, Features.Single.Core.StageDataManager stageManager)
        {
            CurrentStage = stageNumber;
            StageManager = stageManager;
            IsInGameplayMode = (stageNumber > 0);
        }

        /// <summary>
        /// 🔥 추가: IsInGameplayMode를 명시적으로 제어하는 SetStageContext 오버로드
        /// </summary>
        public static void SetStageContext(int stageNumber, Features.Single.Core.StageDataManager stageManager, bool gameplayMode)
        {
            CurrentStage = stageNumber;
            StageManager = stageManager;
            IsInGameplayMode = gameplayMode;
        }

        private int _currentScore;
        public int CurrentScore => _currentScore;
        public bool HasAnyPlacement => placements.Count > 0;

        // 총점이 바뀔 때 알림 (UI용)
        public System.Action<int> OnTotalScoreUpdated;

        // ===== Inspector refs =====
        [Header("References")]
        [SerializeField] private GameBoard gameBoard;
        [SerializeField] private BlockPalette blockPalette;
        [SerializeField] private GameResultModal gameResultModal;

        [Header("Undo Settings")]
        [SerializeField] private int maxUndo = 3;

        [Header("Player Settings")]
        [SerializeField] private PlayerColor playerColor = PlayerColor.Blue;

        [Header("Logging")]
        [SerializeField] private bool verboseLog = true;

        // ===== Runtime =====
        private GameLogic logic;
        private StagePayload payload;
        private List<BlockType> initialBlocks;         // 현재 세션의 팔레트 원본
        private bool _undoInProgress = false;
        private readonly List<BlockPlacement> placements = new(); // 최신이 끝
        private Block _currentSelectedBlock;
        public int RemainingUndo { get; private set; }
        public Action<int> OnUndoCountChanged;
        public Action<int /*score*/> OnGameFinished;
        public Action<int /*scoreChange*/, string /*reason*/> OnScoreChanged;
        private float startTimeRealtime;
        public bool IsInitialized { get; private set; }

        // 게임 완료 상태 추적 (중복 처리 방지)
        private bool isGameCompleted = false;
        public bool IsGameCompleted => isGameCompleted;

        public int ElapsedSeconds => !IsInitialized ? 0 :
            Mathf.Max(0, Mathf.FloorToInt(Time.realtimeSinceStartup - startTimeRealtime));

        // ===== Unity lifecycle =====
        private void Awake()
        {
            if (verboseLog)
            {
                Debug.Log("=== [SingleGameManager] Awake() 시작 ===");
                Debug.Log($"[SingleGameManager] 기존 Instance: {Instance}");
                Debug.Log($"[SingleGameManager] 현재 GameObject: {gameObject.name}");
                Debug.Log($"[SingleGameManager] 현재 Scene: {gameObject.scene.name}");
            }

            // 싱글톤 보정
            if (Instance == null)
            {
                Instance = this;
                if (verboseLog) Debug.Log("[SingleGameManager] ✅ 첫 번째 싱글톤 인스턴스 설정 완료");
            }
            else if (Instance != this)
            {
                // 기존 인스턴스 유효성 검사 후 교체 판단
                bool replace = false;

                if (Instance.gameObject == null || !Instance.gameObject.activeInHierarchy)
                {
                    replace = true;
                    if (verboseLog) Debug.Log("[SingleGameManager] 기존 인스턴스가 null/비활성. 교체.");
                }
                else if (Instance.gameObject.scene.name != "SingleGameplayScene")
                {
                    replace = true;
                    if (verboseLog) Debug.Log("[SingleGameManager] 기존 인스턴스가 다른 씬. 교체.");
                }
                else if (Instance.gameObject.GetInstanceID() != gameObject.GetInstanceID())
                {
                    replace = true;
                    if (verboseLog)
                    {
                        Debug.Log("[SingleGameManager] Scene 재로딩으로 인한 새 인스턴스 감지 → 교체");
                        Debug.Log($"  기존 InstanceID: {Instance.gameObject.GetInstanceID()} / 현재: {gameObject.GetInstanceID()}");
                    }
                    Destroy(Instance.gameObject);
                }

                if (replace)
                {
                    Instance = this;
                    if (verboseLog) Debug.Log("[SingleGameManager] ✅ 기존 인스턴스 교체 완료");
                }
                else
                {
                    if (verboseLog) Debug.Log("[SingleGameManager] ❌ 유효한 기존 인스턴스 존재 → 현재 인스턴스 제거");
                    Destroy(gameObject);
                    return;
                }
            }

            // 기본 참조 보정
            if (!gameBoard) gameBoard = FindObjectOfType<GameBoard>(true);
            if (!blockPalette) blockPalette = FindObjectOfType<BlockPalette>(true);
            if (!gameResultModal) gameResultModal = FindObjectOfType<GameResultModal>(true);
            if (!StageManager) StageManager = FindObjectOfType<Features.Single.Core.StageDataManager>(true);

            if (verboseLog)
            {
                Debug.Log($"[SingleGameManager] 컴포넌트 참조 확인:");
                Debug.Log($"  gameBoard: {gameBoard}");
                Debug.Log($"  blockPalette: {blockPalette}");
                Debug.Log($"  gameResultModal: {gameResultModal}");
                Debug.Log($"  StageManager: {StageManager}");
                Debug.Log("=== [SingleGameManager] Awake() 완료 ===");
            }
        }

        private void Start()
        {
            if (verboseLog)
            {
                Debug.Log("=== [SingleGameManager] Start() 시작 ===");
                Debug.Log($"[SingleGameManager] IsInitialized: {IsInitialized}");
                Debug.Log($"[SingleGameManager] StageManager: {StageManager}");
                Debug.Log($"[SingleGameManager] CurrentStage: {CurrentStage}");
                Debug.Log($"[SingleGameManager] gameBoard: {gameBoard}");
                Debug.Log($"[SingleGameManager] blockPalette: {blockPalette}");
            }

            // 자동 초기화는 "현재 스테이지가 명확하고 데이터가 있는 경우"에만 수행
            if (!IsInitialized && StageManager != null)
            {
                var stageData = StageManager.GetCurrentStageData();
                if (stageData != null)
                {
                    if (verboseLog) Debug.Log($"[SingleGameManager] ✅ 기존 CurrentStageData로 즉시 초기화");
                    Init(ConvertStageDataToPayload(stageData), emitReadyEvent: true);
                }
                else
                {
                    // 🔥 수정: CurrentStage가 지정되어 있어도 GameplayMode일 때만 자동 시작
                    if (CurrentStage > 0 && IsInGameplayMode)
                    {
                        if (verboseLog) Debug.Log($"[SingleGameManager] CurrentStage({CurrentStage}) + GameplayMode - SelectStage 시도");
                        StageManager.SelectStage(CurrentStage);
                        stageData = StageManager.GetCurrentStageData();
                        if (stageData != null)
                        {
                            Init(ConvertStageDataToPayload(stageData), emitReadyEvent: true);
                        }
                        else
                        {
                            // 자동 테스트 초기화는 하지 않음 (의도치 않은 "테스트 모드" 진입 방지)
                            if (verboseLog) Debug.Log("[SingleGameManager] 데이터 없음 - 스테이지 선택 대기");
                            IsInGameplayMode = false;
                        }
                    }
                    else if (CurrentStage > 0)
                    {
                        // 🔥 추가: CurrentStage는 있지만 GameplayMode가 아닌 경우 (스테이지 선택 모드)
                        if (verboseLog) Debug.Log($"[SingleGameManager] CurrentStage({CurrentStage}) 참조용 - 스테이지 선택 모드 대기");
                        IsInGameplayMode = false;
                    }
                    else
                    {
                        if (verboseLog) Debug.Log("[SingleGameManager] CurrentStage==0 - 스테이지 선택 대기");
                        IsInGameplayMode = false;
                    }
                }
            }

            if (verboseLog) Debug.Log("=== [SingleGameManager] Start() 완료 ===");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (verboseLog) Debug.Log("[SingleGameManager] OnDestroy - 싱글톤 참조 정리");
                Instance = null;
            }
        }

        // ===== Public API (StageSelectPanel/SceneFlowController/Modal에서 호출) =====

        /// <summary>
        /// 번호로 스테이지 시작(추천): StageSelectPanel의 Start 버튼에서 호출
        /// </summary>
        public void RequestStartByNumber(int stageNumber)
        {
            if (stageNumber <= 0)
            {
                Debug.LogError("[SingleGameManager] RequestStartByNumber 실패: stageNumber <= 0");
                return;
            }

            if (!StageManager)
                StageManager = FindObjectOfType<Features.Single.Core.StageDataManager>(true);

            if (!StageManager)
            {
                Debug.LogError("[SingleGameManager] StageDataManager를 찾을 수 없습니다.");
                return;
            }

            // StageManager를 통해 현재 스테이지 설정 + 데이터 확보
            StageManager.SelectStage(stageNumber);
            var data = StageManager.GetCurrentStageData();

            if (data == null)
            {
                Debug.LogError($"[SingleGameManager] Stage #{stageNumber} 데이터를 가져오지 못했습니다.");
                return;
            }

            // 컨텍스트 반영
            CurrentStage = stageNumber;
            IsInGameplayMode = true;

            Init(ConvertStageDataToPayload(data), emitReadyEvent: true);
        }

        /// <summary>
        /// 외부에서 이미 조립한 StageData를 직접 적용하고 시작
        /// </summary>
        public void ApplyStageData(Shared.Models.StageData data)
        {
            if (data == null)
            {
                Debug.LogError("[SingleGameManager] ApplyStageData 실패: data == null");
                return;
            }

            CurrentStage = data.stage_number;
            IsInGameplayMode = (CurrentStage > 0);

            Init(ConvertStageDataToPayload(data), emitReadyEvent: true);
        }

        /// <summary>
        /// (필요 시) 싱글톤 참조 클리어
        /// </summary>
        public static void ClearInstance()
        {
            Debug.Log("[SingleGameManager] 싱글톤 참조 강제 정리");
            Instance = null;
        }

        /// <summary>
        /// (필요 시) 재초기화를 위한 상태 리셋
        /// </summary>
        public void ResetForReinitialization()
        {
            if (verboseLog) Debug.Log("[SingleGameManager] 재초기화를 위한 상태 리셋");
            IsInitialized = false;
        }

        // ===== Init & 변환 =====

        public void Init(StagePayload p, bool emitReadyEvent)
        {
            // 이벤트 중복 연결 방지
            DetachHandlers();

            // 게임 완료 상태 초기화
            isGameCompleted = false;

            payload = p ?? new StagePayload();
            logic = new GameLogic();
            placements.Clear();

            _currentScore = 0;
            OnTotalScoreUpdated?.Invoke(_currentScore);    // UI 초기 표기 0

            RemainingUndo = Mathf.Max(0, payload.MaxUndoCount > 0 ? payload.MaxUndoCount : maxUndo);
            OnUndoCountChanged?.Invoke(RemainingUndo);

            // 보드 준비
            if (!gameBoard) 
            {
                gameBoard = FindObjectOfType<GameBoard>(true);
                if (verboseLog && gameBoard) Debug.Log("[SingleGameManager] GameBoard 컴포넌트 발견됨");
            }
            if (!blockPalette) 
            {
                blockPalette = FindObjectOfType<BlockPalette>(true);
                if (verboseLog && blockPalette) Debug.Log("[SingleGameManager] BlockPalette 컴포넌트 발견됨");
            }

            if (gameBoard == null)
            {
                Debug.LogError("[SingleGameManager] GameBoard를 찾을 수 없습니다! SingleGameplayScene에 GameBoard 컴포넌트가 있는지 확인하세요.");
                return;
            }
            
            if (blockPalette == null)
            {
                Debug.LogError("[SingleGameManager] BlockPalette를 찾을 수 없습니다! SingleGameplayScene에 BlockPalette 컴포넌트가 있는지 확인하세요.");
                return;
            }

            gameBoard.SetGameLogic(logic);
            gameBoard.ClearBoard();

            // 초기 보드 상태
            if (payload.InitialBoardPositions != null && payload.InitialBoardPositions.Length > 0)
            {
                if (verboseLog) Debug.Log($"[SingleGameManager] 원시 initial_board_state 적용: {payload.InitialBoardPositions.Length}개 위치");
                logic.SetInitialBoardState(payload.InitialBoardPositions);
                StartCoroutine(RefreshBoardNextFrame());
            }
            else if (payload.InitialBoard != null)
            {
                if (verboseLog) Debug.Log("[SingleGameManager] 파싱된 초기 보드 상태 적용");
                ApplyInitialBoardState(payload.InitialBoard);
            }
            else
            {
                if (verboseLog) Debug.Log("[SingleGameManager] 초기 보드 상태 없음 - 빈 보드");
            }

            // 팔레트
            var blocks = (payload.AvailableBlocks != null && payload.AvailableBlocks.Length > 0)
                ? new List<BlockType>(payload.AvailableBlocks)
                : new List<BlockType>(GetMinimalBlockSet());
            initialBlocks = blocks;

            blockPalette.InitializePalette(blocks, playerColor);

            // 이벤트 연결
            blockPalette.OnBlockSelected += OnBlockSelectedFromPalette;
            gameBoard.OnBlockPlaced += OnBlockPlacedToBoard;
            gameBoard.OnCellClicked += OnBoardCellClicked;

            if (verboseLog)
            {
                Debug.Log($"[SingleGame] Start - Stage: {payload.StageName ?? "Unknown"} (#{payload.StageNumber}), " +
                          $"Board: {payload.BoardSize}, Difficulty: {payload.Difficulty}, " +
                          $"TimeLimit: {(payload.TimeLimit > 0 ? payload.TimeLimit + "s" : "무제한")}, " +
                          $"MaxUndo: {RemainingUndo}");
            }

            startTimeRealtime = Time.realtimeSinceStartup;
            IsInitialized = true;

            // UI 활성화(핵심 2개 이상 존재하면 플레이 가능)
            ActivateGameUI();

            if (verboseLog) Debug.Log("[SingleGameManager] ✅ 게임 초기화 완전 완료 - 플레이 가능 상태");

            if (emitReadyEvent)
            {
                // ✅ GamePanel이 이 이벤트를 구독하여 StageSelectPanel을 숨기고 인터랙션을 열어준다
                OnGameReady?.Invoke();
            }
        }

        private void ApplyInitialBoardState(InitialBoardData initialBoard)
        {
            if (initialBoard == null)
            {
                Debug.Log("[SingleGame] 초기 보드 상태 데이터가 없습니다. 빈 보드로 시작합니다.");
                return;
            }

            // 1) 장애물 적용(게임 로직에 전용 API가 있다면 여기서 호출)
            if (initialBoard.obstacles != null && initialBoard.obstacles.Count > 0)
            {
                foreach (var obstaclePos in initialBoard.obstacles)
                {
                    // TODO: GameLogic에 장애물 설정 메서드가 있다면 사용
                    // logic.SetObstacle(obstaclePos);
                    // Debug.Log($"[SingleGame] 장애물: ({obstaclePos.row}, {obstaclePos.col})");
                }
                Debug.Log($"[SingleGame] 장애물 {initialBoard.obstacles.Count}개 적용됨");
            }

            // 2) 사전 배치 블록 적용
            int applied = 0;
            if (initialBoard.preplaced != null && initialBoard.preplaced.Count > 0)
            {
                foreach (var placement in initialBoard.preplaced)
                {
                    bool placed = logic.PlaceBlock(placement);
                    if (placed)
                    {
                        // Undo 히스토리에 포함 (원치 않으면 별도 리스트로 분리)
                        placements.Add(placement);
                        applied++;
                    }
                    else
                    {
                        // Debug.LogWarning($"[SingleGame] 사전 배치 실패: {placement.type} at ({placement.position.row},{placement.position.col})");
                    }
                }
                Debug.Log($"[SingleGame] 사전 배치 블록 {initialBoard.preplaced.Count}개 중 {applied}개 적용됨");
            }

            // 3) 보드 갱신
            gameBoard?.RefreshBoard();
        }

        private void DetachHandlers()
        {
            if (blockPalette != null)
                blockPalette.OnBlockSelected -= OnBlockSelectedFromPalette;
            if (gameBoard != null)
            {
                gameBoard.OnBlockPlaced -= OnBlockPlacedToBoard;
                gameBoard.OnCellClicked -= OnBoardCellClicked;
            }
        }

        private StagePayload ConvertStageDataToPayload(Shared.Models.StageData stageData)
        {
            var p = new StagePayload
            {
                StageNumber = stageData.stage_number,
                StageName = !string.IsNullOrEmpty(stageData.stage_description)
                                ? stageData.stage_description
                                : $"스테이지 {stageData.stage_number}",
                BoardSize = 20,
                Difficulty = stageData.difficulty,
                TimeLimit = stageData.time_limit,
                MaxUndoCount = stageData.max_undo_count > 0 ? stageData.max_undo_count : 3,
                ParScore = stageData.optimal_score
            };

            // 블록
            if (stageData.available_blocks != null && stageData.available_blocks.Length > 0)
            {
                var list = new List<BlockType>();
                foreach (var val in stageData.available_blocks)
                {
                    if (val >= 1 && val <= 21 && Enum.IsDefined(typeof(BlockType), (byte)val))
                        list.Add((BlockType)(byte)val);
                }
                p.AvailableBlocks = list.ToArray();
                if (verboseLog) Debug.Log($"[SingleGameManager] 스테이지 {p.StageNumber} 블록 {list.Count}개 설정");
            }
            else
            {
                p.AvailableBlocks = GetMinimalBlockSet();
                if (verboseLog) Debug.LogWarning($"[SingleGameManager] 스테이지 {p.StageNumber} blocks 미지정 → 최소 세트 사용");
            }

            // 초기 보드
            if (stageData.initial_board_state != null)
            {
                p.InitialBoard = ConvertInitialBoardState(stageData.initial_board_state);
                p.InitialBoardPositions = stageData.initial_board_state.boardPositions;
            }

            if (verboseLog)
            {
                Debug.Log($"[SingleGameManager] StagePayload 생성: " +
                          $"#{p.StageNumber}, Diff={p.Difficulty}, TL={p.TimeLimit}, " +
                          $"Undo={p.MaxUndoCount}, Blocks={p.AvailableBlocks?.Length ?? 0}");
            }

            return p;
        }

        private Shared.Models.InitialBoardData ConvertInitialBoardState(Shared.Models.InitialBoardState boardState)
        {
            if (boardState == null) return null;

            var placementsData = boardState.GetPlacements();
            if (placementsData == null || placementsData.Length == 0) return null;

            var result = new Shared.Models.InitialBoardData
            {
                obstacles = new List<Position>(),
                preplaced = new List<BlockPlacement>()
            };

            foreach (var pl in placementsData)
            {
                var bp = new BlockPlacement(
                    (BlockType)(byte)pl.block_type,
                    new Position(pl.row, pl.col),
                    (Rotation)(byte)pl.rotation,
                    (FlipState)(byte)pl.flip_state,
                    (PlayerColor)(byte)pl.color
                );
                result.preplaced.Add(bp);
            }

            if (verboseLog) Debug.Log($"[SingleGameManager] 초기 보드 변환 완료: 사전배치 {result.preplaced.Count}개");
            return result;
        }

        // ===== UI 활성/비활성 =====

        private void ActivateGameUI()
        {
            if (verboseLog) Debug.Log("[SingleGameManager] 게임 UI 패널들 활성화 시작");

            int ok = 0;

            // TopBarUI (있으면)
            var topBarUI = FindObjectOfType<Features.Single.UI.InGame.TopBarUI>(true);
            if (topBarUI)
            {
                if (!topBarUI.gameObject.activeInHierarchy)
                    topBarUI.gameObject.SetActive(true);
                if (verboseLog) Debug.Log("[SingleGameManager] TopBarUI 활성화");
                ok++;
            }
            else
            {
                if (verboseLog) Debug.Log("[SingleGameManager] TopBarUI 없음(선택사항)");
            }

            if (gameBoard)
            {
                if (!gameBoard.gameObject.activeInHierarchy)
                    gameBoard.gameObject.SetActive(true);
                ok++;
            }
            else Debug.LogError("[SingleGameManager] GameBoard 참조 null");

            if (blockPalette)
            {
                if (!blockPalette.gameObject.activeInHierarchy)
                    blockPalette.gameObject.SetActive(true);
                ok++;
            }
            else Debug.LogError("[SingleGameManager] BlockPalette 참조 null");

            // MainScene 일부 패널 숨김(정보성 로그만)
            HideMainScenePanels();

            if (ok >= 2)
            {
                if (verboseLog) Debug.Log("[SingleGameManager] 🎮 게임 플레이 준비 완료");
            }
            else
            {
                Debug.LogError("[SingleGameManager] ❌ 게임 UI 활성화 실패 - 필수 컴포넌트 부족");
            }
        }

        private void HideMainScenePanels()
        {
            if (verboseLog) Debug.Log("[SingleGameManager] MainScene UI 패널 상태 확인 중...");

            var uiManager = App.UI.UIManager.GetInstanceSafe();
            if (uiManager)
            {
                if (verboseLog) Debug.Log("[SingleGameManager] UIManager 통해 필요 패널 숨김(정보)");
            }

            // ModeSelectionPanel 숨김
            var modeSelectionPanel = GameObject.Find("ModeSelectionPanel");
            if (modeSelectionPanel && modeSelectionPanel.activeInHierarchy)
            {
                modeSelectionPanel.SetActive(false);
                if (verboseLog) Debug.Log("[SingleGameManager] ModeSelectionPanel 숨김");
            }

            // StageSelectPanel/GamePanel 가시성은 GamePanel 쪽에서 최종 제어
        }

        // ===== 게임 흐름 =====

        public void OnExitRequested()
        {
            if (verboseLog) Debug.Log($"[SingleGame] ExitRequested - elapsed={ElapsedSeconds}s");

            // 게임 완료 상태 설정 (Exit 시에도 중복 처리 방지)
            if (isGameCompleted)
            {
                Debug.LogWarning($"[SingleGame] 게임이 이미 완료됨 - Exit 중복 처리 방지");
                return;
            }
            isGameCompleted = true;
            
            // 즉시 이벤트 연결 해제 (추가 블록 배치 방지)
            DetachHandlers();

            var scores = logic?.CalculateScores();
            int currentScore = (scores != null && scores.ContainsKey(playerColor)) ? scores[playerColor] : 0;
            int optimalScore = payload?.ParScore ?? 0;

            // 🔥 Exit 시에도 GameEndResult 기반 처리 (stars 계산으로 정확한 실패/성공 판정)
            int stars = App.Services.ApiDataConverter.CalculateStars(currentScore, optimalScore);
            var gameResult = new GameEndResult(
                stageNumber: CurrentStage,
                stageName: payload?.StageName ?? $"Stage {CurrentStage}",
                finalScore: currentScore,
                optimalScore: optimalScore,
                elapsedTime: ElapsedSeconds,
                stars: stars,
                isNewBest: false,
                endReason: "Exit requested"
            );

            Debug.Log($"[SingleGame] Exit 처리: {gameResult}");
            ReportStageCompletion(gameResult);
        }

        /// <summary>
        /// 🔥 GameEndResult 기반 완료 보고 - 단일 진실원천 패턴
        /// </summary>
        private void ReportStageCompletion(GameEndResult gameResult)
        {
            if (gameResult.stageNumber <= 0)
            {
                Debug.LogWarning("[SingleGame] 유효하지 않은 스테이지 번호 → 보고 건너뜀");
                return;
            }

            if (StageManager == null)
            {
                Debug.LogWarning("[SingleGame] StageDataManager 미존재 → 보고 건너뜀");
                return;
            }

            // 🔥 GameEndResult 기반 올바른 API 호출 분리
            if (gameResult.isCleared) // stars >= 1
            {
                // ✅ 클리어 성공: 완료 API만 호출
                StageManager.CompleteStage(gameResult.stageNumber, gameResult.finalScore, 
                                         gameResult.stars, Mathf.FloorToInt(gameResult.elapsedTime));
                
                if (verboseLog) 
                    Debug.Log($"[SingleGame] ✅ 완료 보고: stage={gameResult.stageNumber}, " +
                             $"score={gameResult.finalScore}, stars={gameResult.stars}, " +
                             $"t={gameResult.elapsedTime:F1}s");
            }
            else // stars == 0
            {
                // ❌ 클리어 실패: 실패 처리 (완료 API 호출 금지)
                StageManager.FailStage(gameResult.stageNumber);
                
                if (verboseLog) 
                    Debug.Log($"[SingleGame] ❌ 실패 보고: stage={gameResult.stageNumber}, " +
                             $"score={gameResult.finalScore}, stars={gameResult.stars}, " +
                             $"t={gameResult.elapsedTime:F1}s");
                
                // 🚨 중요: 완료 API를 호출하지 않음으로써 서버에서 completed=true 응답 방지
                Debug.Log($"[SingleGame] 스테이지 {gameResult.stageNumber} 실패 처리: 완료 API 호출 금지됨 (0별)");
            }

            // 🚨 규칙 위반 재검증
            if (gameResult.stars == 0 && gameResult.isCleared)
            {
                Debug.LogError($"[SingleGame] 🚨 심각한 규칙 위반: GameEndResult가 0별인데 isCleared=true");
            }
        }

        // ===== Undo & 이벤트 핸들러 =====

        public bool CanUndo() => RemainingUndo > 0 && placements.Count > 0;

        public void OnUndoMove()
        {
            if (_undoInProgress) { Debug.LogWarning("..."); return; }
            _undoInProgress = true;
            try
            {
                if (!CanUndo()) { Debug.LogWarning("..."); return; }

                var lastPlacement = placements[placements.Count - 1];
                int removedBlockScore = logic?.GetBlockScore(lastPlacement.type) ?? 0;

                placements.RemoveAt(placements.Count - 1);
                RebuildBoardOnlyFromPlacements();
                blockPalette?.RestoreBlock(lastPlacement.type);

                if (removedBlockScore > 0)
                {
                    _currentScore -= removedBlockScore; // 🔥 총점 차감
                    OnScoreChanged?.Invoke(-removedBlockScore, $"Undo {lastPlacement.type}");
                    OnTotalScoreUpdated?.Invoke(_currentScore);
                }

                RemainingUndo--;
                OnUndoCountChanged?.Invoke(RemainingUndo);
                Debug.Log($"[SingleGame] Undo 완료 - 남은 횟수: {RemainingUndo}");
            }
            finally { _undoInProgress = false; Canvas.ForceUpdateCanvases(); }
        }

        private void OnBlockSelectedFromPalette(Block block)
        {
            if (block != null)
            {
                _currentSelectedBlock = block;
            }
            else
            {
                if (!_undoInProgress)
                {
                    _currentSelectedBlock = null;
                    gameBoard?.ClearTouchPreview();
                }
            }
        }

        private void OnBoardCellClicked(Position pos)
        {
            if (_currentSelectedBlock != null)
            {
                if (gameBoard != null) gameBoard.SetTouchPreview(_currentSelectedBlock, pos);
                else Debug.LogError("[SingleGameManager] GameBoard가 null입니다!");
            }
            else
            {
                Debug.LogWarning("[SingleGameManager] 선택된 블록 없음. 팔레트에서 먼저 선택하세요.");
                gameBoard?.ClearTouchPreview();
            }
        }

        private void OnBlockPlacedToBoard(Block block, Position pos)
        {
            // 게임 완료 후 지연된 블록 배치 이벤트 무시 (서버 오류 방지)
            if (isGameCompleted)
            {
                Debug.LogWarning($"[SingleGame] 게임 완료 후 블록 배치 이벤트 무시: {block.Type} at ({pos.row},{pos.col})");
                return;
            }
            
            var placement = new BlockPlacement(
                block.Type, pos, block.CurrentRotation, block.CurrentFlipState, block.Player);

            bool dup = placements.Any(p =>
                p.type == placement.type &&
                p.position.row == placement.position.row &&
                p.position.col == placement.position.col);

            if (!dup)
            {
                placements.Add(placement);
                if (verboseLog) Debug.Log($"[SingleGame] 배치 기록: {placement.type} @ ({pos.row},{pos.col}) / 총 {placements.Count}");
            }

            blockPalette.MarkBlockAsUsed(block.Type);

            // 🔥 점수 가산
            int gain = logic?.GetBlockScore(block.Type) ?? 0;
            _currentScore += gain;
            OnScoreChanged?.Invoke(gain, $"Place {block.Type}");
            OnTotalScoreUpdated?.Invoke(_currentScore);

            // 🔥 Undo 버튼 상태 갱신 트리거 (값은 그대로여도 알림)
            OnUndoCountChanged?.Invoke(RemainingUndo);

            blockPalette.MarkBlockAsUsed(block.Type);
            CheckGameEndConditions();
        }

        private void CheckGameEndConditions()
        {
            if (blockPalette == null || logic == null) return;

            if (!blockPalette.HasAvailableBlocks())
            {
                if (verboseLog) Debug.Log("[SingleGame] 종료 - 사용 가능한 블록 없음");
                EndGame("모든 블록 사용 완료");
                return;
            }

            var available = blockPalette.GetAvailableBlocks();
            if (!logic.CanPlaceAnyBlock(playerColor, available))
            {
                if (verboseLog) Debug.Log($"[SingleGame] 종료 - 남은 {available.Count}개 모두 배치 불가");
                EndGame("더 이상 블록 배치 불가");
                return;
            }

            if (verboseLog) Debug.Log($"[SingleGame] 진행 - 사용 가능 블록: {blockPalette.GetAvailableBlockCount()}개");
        }

        private void EndGame(string reason)
        {
            // 게임 완료 상태 설정 (중복 처리 방지)
            if (isGameCompleted)
            {
                Debug.LogWarning($"[SingleGame] 게임이 이미 완료됨 - 중복 종료 처리 방지");
                return;
            }
            isGameCompleted = true;
            
            // 즉시 이벤트 연결 해제 (추가 블록 배치 방지)
            DetachHandlers();
            
            var scores = logic.CalculateScores();
            int myScore = scores.ContainsKey(playerColor) ? scores[playerColor] : 0;
            int optimalScore = payload?.ParScore ?? 0;
            float elapsedTime = ElapsedSeconds;

            // 🔥 단일 진실원천: GameEndResult 생성 (별점 기반 클리어 판정)
            int stars = App.Services.ApiDataConverter.CalculateStars(myScore, optimalScore);
            var gameResult = new GameEndResult(
                stageNumber: CurrentStage,
                stageName: payload?.StageName ?? $"Stage {CurrentStage}",
                finalScore: myScore,
                optimalScore: optimalScore,
                elapsedTime: elapsedTime,
                stars: stars,
                isNewBest: false, // TODO: 최고점수 비교 로직 필요시 추가
                endReason: reason
            );

            Debug.Log($"[SingleGame] 게임 종료: {gameResult}");

            // 🚨 규칙 위반 검사: 0별인데 완료 처리하려는 경우 경고
            if (gameResult.stars == 0 && gameResult.isCleared)
            {
                Debug.LogError($"[SingleGame] 🚨 규칙 위반 감지: 0별인데 완료 처리 시도 - Stage {CurrentStage}");
            }

            // 🔥 (2) StageSelectPanel을 먼저 켜서, 비활성 코루틴 에러 방지
            EnsureStageSelectPanelActive();

            // 🔥 완료 보고: GameEndResult 기반으로 올바른 API 호출
            ReportStageCompletion(gameResult);

            // 🔥 (3) 결과 모달 표시: GameEndResult 전달
            ShowGameResult(gameResult,
                onClosed: () =>
                {
                    // (4) 모달 닫힐 때 GamePanel 닫고 (5) StageSelect가 드러나도록
                    var gamePanel = GameObject.Find("GamePanel");
                    if (gamePanel && gamePanel.activeSelf) gamePanel.SetActive(false);

                    var controller = FindObjectOfType<Features.Single.UI.Scene.SingleGameplayUIScreenController>(true);
                    controller?.ShowSelection(); // 보정 (StageSelect=ON)
                });

            // 레거시 이벤트(있으면 유지)
            OnGameFinished?.Invoke(myScore);
        }

        private void EnsureStageSelectPanelActive()
        {
            var stageSelect = GameObject.Find("StageSelectPanel");
            if (stageSelect && !stageSelect.activeSelf)
            {
                stageSelect.SetActive(true);
                Debug.Log("[SingleGame] StageSelectPanel 활성화(백그라운드)");
            }
        }

        /// <summary>
        /// 🔥 GameEndResult 기반 결과 모달 표시
        /// </summary>
        private void ShowGameResult(GameEndResult gameResult, System.Action onClosed = null)
        {
            if (gameResultModal != null)
            {
                // GameResultModal에 GameEndResult 전달 (단일 진실원천)
                gameResultModal.ShowResult(gameResult, onClosed);
            }
            else
            {
                Debug.LogWarning("[SingleGameManager] GameResultModal이 연결되지 않았습니다.");
                onClosed?.Invoke(); // 최소한 흐름은 유지
            }
        }

        public void UpdateStageProgress(int stageNumber, bool isCompleted, int stars, int score, float elapsedTime)
        {
            if (verboseLog) Debug.Log($"[SingleGameManager] 진행도 업데이트: Stage={stageNumber}, Done={isCompleted}, Stars={stars}, Score={score}, Time={elapsedTime:F1}s");

            if (StageManager != null)
            {
                if (isCompleted)
                    StageManager.CompleteStage(stageNumber, score, stars, Mathf.FloorToInt(elapsedTime));
                else
                    StageManager.FailStage(stageNumber);

                if (verboseLog) Debug.Log("[SingleGameManager] StageDataManager 업데이트 OK");
            }
            else
            {
                Debug.LogWarning("[SingleGameManager] StageDataManager 없음 → 진행도 저장 건너뜀");
            }
        }

        public void OnGameCompleted()
        {
            if (verboseLog) Debug.Log("[SingleGameManager] 게임 완료 정리");
        }

        // ===== Helpers =====

        private static BlockType[] GetMinimalBlockSet()
        {
            return new[]
            {
                BlockType.Single,
                BlockType.Domino,
                BlockType.TrioLine,
                BlockType.TrioAngle,
                BlockType.Tetro_I,
                BlockType.Tetro_O,
                BlockType.Tetro_T,
                BlockType.Tetro_L,
                BlockType.Pento_I,
                BlockType.Pento_L,
                BlockType.Pento_T
            };
        }

        private void RebuildBoardOnlyFromPlacements()
        {
            logic = new GameLogic();
            gameBoard.SetGameLogic(logic);

            foreach (var p in placements)
                logic.PlaceBlock(p);

            gameBoard.RefreshBoard();
        }

        private System.Collections.IEnumerator RefreshBoardNextFrame()
        {
            yield return null;
            if (verboseLog) Debug.Log("[SingleGameManager] 초기 보드 화면 새로고침");
            gameBoard?.RefreshBoard();
        }
    }
}
