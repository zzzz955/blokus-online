using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        [SerializeField] private GameLogic gameLogic;
        [SerializeField] private Transform gameBoard;
        [SerializeField] private Transform blockPalette;
        
        [Header("UI References")]
        [SerializeField] private TMPro.TextMeshProUGUI scoreText;
        [SerializeField] private TMPro.TextMeshProUGUI timeText;
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
        
        // 사용 가능한 블록 리스트
        private System.Collections.Generic.List<BlockType> availableBlocks;
        private System.Collections.Generic.List<BlockType> usedBlocks;
        
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
            if (CurrentStage != null)
            {
                InitializeStage(CurrentStage);
            }
            else
            {
                Debug.LogError("CurrentStage가 설정되지 않았습니다!");
                BackToStageSelect();
            }
        }
        
        void Update()
        {
            if (isGameActive && CurrentStage.timeLimit > 0)
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
        /// 스테이지 초기화
        /// </summary>
        private void InitializeStage(StageData stageData)
        {
            Debug.Log($"스테이지 {stageData.stageNumber} 시작: {stageData.stageName}");
            
            // 게임 로직 초기화
            gameLogic.initializeBoard();
            
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
            UpdateBlockPalette();
            
            // 게임 시작
            isGameActive = true;
            gameUI.SetActive(true);
            resultPanel.SetActive(false);
            
            Debug.Log($"사용 가능한 블록: {availableBlocks.Count}개");
        }
        
        /// <summary>
        /// 블록 배치 시도
        /// </summary>
        public bool TryPlaceBlock(BlockPlacement placement)
        {
            if (!isGameActive) return false;
            
            // 블록 사용 가능 여부 확인
            if (!availableBlocks.Contains(placement.type))
            {
                Debug.Log("사용할 수 없는 블록입니다.");
                return false;
            }
            
            // 게임 로직으로 배치 검증
            if (gameLogic.canPlaceBlock(placement))
            {
                // 블록 배치
                gameLogic.placeBlock(placement);
                
                // 블록을 사용된 목록으로 이동
                availableBlocks.Remove(placement.type);
                usedBlocks.Add(placement.type);
                
                // 점수 계산 (블록 크기만큼 점수 획득)
                int blockScore = CalculateBlockScore(placement.type);
                currentScore += blockScore;
                
                UpdateScoreUI();
                UpdateBlockPalette();
                
                // 게임 종료 조건 체크
                CheckGameEnd();
                
                Debug.Log($"블록 배치 성공! 점수: +{blockScore} (총합: {currentScore})");
                return true;
            }
            else
            {
                Debug.Log("블록을 배치할 수 없는 위치입니다.");
                return false;
            }
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
        
        private void UpdateBlockPalette()
        {
            // TODO: 사용 가능한 블록 팔레트 UI 업데이트
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