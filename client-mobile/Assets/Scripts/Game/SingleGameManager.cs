using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Common;
using BlokusUnity.Data;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 싱글플레이 게임 매니저
    /// 스테이지 기반 퍼즐 게임 진행 관리
    /// </summary>
    public class SingleGameManager : MonoBehaviour
    {
        [Header("Game Components")]
        [SerializeField] private GameBoard gameBoard;
        [SerializeField] private BlockPalette blockPalette;
        
        [Header("UI References")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private GameObject gameUI;
        [SerializeField] private GameObject resultPanel;
        
        [Header("Game Settings")]
        [SerializeField] private float gameTime = 0f;
        
        // 현재 스테이지 정보
        public static StageData CurrentStage { get; set; }
        public static StageManager StageManager { get; set; }
        
        // 게임 상태
        private int currentScore = 0;
        private bool isGameActive = false;
        private float remainingTime = 0f;
        private int undoCount = 0;
        
        // 게임 로직
        private GameLogic gameLogic;
        
        // 사용 가능한 블록 리스트
        private System.Collections.Generic.List<BlockType> availableBlocks;
        private System.Collections.Generic.List<BlockType> usedBlocks;
        
        // 현재 선택된 블록
        private Block selectedBlock;
        
        public static SingleGameManager Instance { get; private set; }
        
        void Awake()
        {
            Instance = this;
            
            // GameLogic 초기화
            if (gameLogic == null)
                gameLogic = new GameLogic();
                
            availableBlocks = new System.Collections.Generic.List<BlockType>();
            usedBlocks = new System.Collections.Generic.List<BlockType>();
        }
        
        void Start()
        {
            // 컴포넌트 이벤트 연결
            SetupGameComponents();
            
            if (CurrentStage != null)
            {
                InitializeStage(CurrentStage);
            }
            else
            {
                Debug.LogWarning("CurrentStage가 설정되지 않음. 테스트 스테이지 생성중...");
                CreateTestStage();
            }
        }
        
        /// <summary>
        /// 게임 컴포넌트들 이벤트 연결
        /// </summary>
        private void SetupGameComponents()
        {
            // 게임보드 이벤트 연결
            if (gameBoard != null)
            {
                gameBoard.OnCellClicked += OnBoardCellClicked;
                gameBoard.OnCellHover += OnBoardCellHover;
                gameBoard.SetGameLogic(gameLogic);
            }
            
            // 블록 팔레트 이벤트 연결
            if (blockPalette != null)
            {
                blockPalette.OnBlockSelected += OnBlockSelected;
                blockPalette.OnBlockDeselected += OnBlockDeselected;
            }
        }
        
        void Update()
        {
            if (isGameActive && CurrentStage != null && CurrentStage.timeLimit > 0)
            {
                remainingTime -= Time.deltaTime;
                UpdateTimeUI();
                
                if (remainingTime <= 0)
                {
                    TimeUp();
                }
            }
        }
        
        /// <summary>
        /// 테스트용 스테이지 생성
        /// </summary>
        private void CreateTestStage()
        {
            // 임시 스테이지 데이터 생성
            var testStage = ScriptableObject.CreateInstance<StageData>();
            testStage.stageNumber = 1;
            testStage.stageName = "테스트 스테이지";
            testStage.difficulty = 1;
            testStage.optimalScore = 50;
            testStage.timeLimit = 300; // 5분
            testStage.maxUndoCount = 3;
            
            // 사용 가능한 블록 타입 설정 (처음 몇 개만)
            testStage.availableBlocks = new System.Collections.Generic.List<BlockType>
            {
                BlockType.Single,
                BlockType.Domino,
                BlockType.TrioLine,
                BlockType.TrioAngle,
                BlockType.Tetro_I,
                BlockType.Tetro_O,
                BlockType.Tetro_T,
                BlockType.Tetro_L
            };
            
            CurrentStage = testStage;
            InitializeStage(CurrentStage);
            
            Debug.Log($"테스트 스테이지 생성 완료: {testStage.availableBlocks.Count}개 블록");
        }
        
        /// <summary>
        /// 스테이지 초기화
        /// </summary>
        private void InitializeStage(StageData stageData)
        {
            Debug.Log($"스테이지 {stageData.stageNumber} 시작: {stageData.stageName}");
            
            // 게임 로직 초기화
            gameLogic.InitializeBoard();
            
            // 초기 보드 상태 설정
            stageData.ApplyInitialBoard(gameLogic);
            
            // 사용 가능한 블록 설정
            availableBlocks.Clear();
            availableBlocks.AddRange(stageData.availableBlocks);
            
            // 게임 UI 초기화
            currentScore = 0;
            remainingTime = stageData.timeLimit;
            undoCount = 0;
            
            UpdateScoreUI();
            UpdateTimeUI();
            
            // 블록 팔레트 초기화
            InitializeBlockPalette();
            
            // 게임 시작
            isGameActive = true;
            gameUI.SetActive(true);
            resultPanel.SetActive(false);
            
            Debug.Log($"사용 가능한 블록: {availableBlocks.Count}개");
        }
        
        /// <summary>
        /// 블록 배치 시도 (기존 메서드 - 사용 안함)
        /// </summary>
        public bool TryPlaceBlock(BlockPlacement placement)
        {
            // 이 메서드는 더 이상 사용하지 않음
            return false;
        }
        
        /// <summary>
        /// 블록 점수 계산
        /// </summary>
        private int CalculateBlockScore(BlockType blockType)
        {
            // 블록 크기에 따른 기본 점수
            return blockType switch
            {
                BlockType.Single => 1,
                BlockType.Domino => 2,
                BlockType.TrioLine => 3,
                BlockType.TrioAngle => 3,
                BlockType.Tetro_I => 4,
                BlockType.Tetro_O => 4,
                BlockType.Tetro_T => 4,
                BlockType.Tetro_L => 4,
                BlockType.Tetro_S => 4,
                _ => 5 // 펜토미노는 모두 5점
            };
        }
        
        /// <summary>
        /// 게임 종료 조건 체크
        /// </summary>
        private void CheckGameEnd()
        {
            // 더 이상 배치할 수 있는 블록이 없는지 확인
            bool canPlaceAnyBlock = false;
            
            foreach (var blockType in availableBlocks)
            {
                // TODO: 각 블록에 대해 배치 가능한 위치가 있는지 확인
                // 현재는 간단히 블록이 남아있으면 게임 계속
                canPlaceAnyBlock = true;
                break;
            }
            
            if (!canPlaceAnyBlock || availableBlocks.Count == 0)
            {
                GameEnd();
            }
        }
        
        /// <summary>
        /// 게임 종료 처리
        /// </summary>
        private void GameEnd()
        {
            isGameActive = false;
            
            // 별점 계산
            int starRating = CurrentStage.CalculateStarRating(currentScore);
            
            // 스테이지 진행 상황 업데이트
            var stageProgress = StageManager.GetStageProgress(CurrentStage.stageNumber);
            stageProgress.UpdateProgress(currentScore, starRating);
            
            // 다음 스테이지 언락
            if (starRating > 0)
            {
                StageManager.UnlockNextStage(CurrentStage.stageNumber);
            }
            
            // 결과 화면 표시
            ShowResult(starRating);
            
            Debug.Log($"게임 종료! 점수: {currentScore}, 별점: {starRating}");
        }
        
        /// <summary>
        /// 시간 초과 처리
        /// </summary>
        private void TimeUp()
        {
            Debug.Log("시간 초과!");
            GameEnd();
        }
        
        /// <summary>
        /// 결과 화면 표시
        /// </summary>
        private void ShowResult(int starRating)
        {
            gameUI.SetActive(false);
            resultPanel.SetActive(true);
            
            // TODO: 결과 UI 업데이트 (별점, 점수, 버튼들)
        }
        
        /// <summary>
        /// UI 업데이트 함수들
        /// </summary>
        private void UpdateScoreUI()
        {
            if (scoreText != null)
                scoreText.text = $"점수: {currentScore}";
        }
        
        private void UpdateTimeUI()
        {
            if (timeText != null && CurrentStage.timeLimit > 0)
            {
                int minutes = Mathf.FloorToInt(remainingTime / 60f);
                int seconds = Mathf.FloorToInt(remainingTime % 60f);
                timeText.text = $"{minutes:00}:{seconds:00}";
            }
        }
        
        /// <summary>
        /// 블록 팔레트 초기화
        /// </summary>
        private void InitializeBlockPalette()
        {
            if (blockPalette != null && availableBlocks != null)
            {
                blockPalette.InitializePalette(availableBlocks, PlayerColor.Blue);
            }
        }
        
        // ========================================
        // 이벤트 처리 메서드들
        // ========================================
        
        /// <summary>
        /// 블록 선택 이벤트
        /// </summary>
        private void OnBlockSelected(Block block)
        {
            selectedBlock = block;
            Debug.Log($"블록 선택됨: {BlockFactory.GetBlockName(block.Type)}");
        }
        
        /// <summary>
        /// 블록 선택 해제 이벤트
        /// </summary>
        private void OnBlockDeselected()
        {
            selectedBlock = null;
            if (gameBoard != null)
            {
                gameBoard.ClearPreview();
            }
        }
        
        /// <summary>
        /// 보드 셀 클릭 이벤트
        /// </summary>
        private void OnBoardCellClicked(Position position)
        {
            if (!isGameActive || selectedBlock == null) return;
            
            // 블록 배치 시도
            if (gameBoard.TryPlaceBlock(selectedBlock, position))
            {
                // 블록 사용 처리
                availableBlocks.Remove(selectedBlock.Type);
                usedBlocks.Add(selectedBlock.Type);
                
                // 점수 업데이트
                int blockScore = BlockFactory.GetBlockScore(selectedBlock.Type);
                currentScore += blockScore;
                UpdateScoreUI();
                
                // 팔레트에서 사용된 블록 표시
                if (blockPalette != null)
                {
                    blockPalette.MarkBlockAsUsed(selectedBlock.Type);
                }
                
                // 게임 종료 조건 체크
                CheckGameEnd();
                
                Debug.Log($"블록 배치 성공! 점수: +{blockScore} (총합: {currentScore})");
            }
        }
        
        /// <summary>
        /// 보드 셀 호버 이벤트
        /// </summary>
        private void OnBoardCellHover(Position position)
        {
            if (!isGameActive || selectedBlock == null) return;
            
            // 미리보기 표시
            if (gameBoard != null)
            {
                gameBoard.SetPreview(selectedBlock, position);
            }
        }
        
        /// <summary>
        /// 게임 버튼 이벤트들
        /// </summary>
        public void OnRestartStage()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        
        public void OnUndoMove()
        {
            if (undoCount < CurrentStage.maxUndoCount)
            {
                // TODO: 실행취소 로직 구현
                undoCount++;
            }
        }
        
        public void OnPauseGame()
        {
            isGameActive = !isGameActive;
            // TODO: 일시정지 UI 표시
        }
        
        public void OnBackToStageSelect()
        {
            BackToStageSelect();
        }
        
        /// <summary>
        /// 스테이지 선택 화면으로 돌아가기
        /// </summary>
        private void BackToStageSelect()
        {
            SceneManager.LoadScene("MainScene");
        }
        
        /// <summary>
        /// 다음 스테이지로 이동
        /// </summary>
        public void OnNextStage()
        {
            var nextStage = StageManager.GetStageData(CurrentStage.stageNumber + 1);
            if (nextStage != null)
            {
                CurrentStage = nextStage;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            else
            {
                BackToStageSelect();
            }
        }
    }
}