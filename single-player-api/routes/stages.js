const express = require('express');
const router = express.Router();
const logger = require('../config/logger');
const dbService = require('../config/database');
const { authenticateToken } = require('../middleware/auth');
const { 
  validateStageNumber, 
  validateStageCompletion,
  requireJson 
} = require('../middleware/validation');

/**
 * GET /api/stages/:stageNumber
 * 스테이지 데이터 조회
 */
router.get('/:stageNumber', 
  authenticateToken,
  validateStageNumber,
  async (req, res) => {
    try {
      const stageNumber = parseInt(req.params.stageNumber);
      const { username } = req.user;

      logger.info('Stage data requested', {
        stageNumber,
        username,
        ip: req.ip
      });

      // 스테이지 데이터 조회
      const stageData = await dbService.getStageData(stageNumber);
      
      if (!stageData) {
        return res.status(404).json({
          success: false,
          message: `Stage ${stageNumber} not found`,
          error: 'STAGE_NOT_FOUND'
        });
      }

      // 사용자가 해당 스테이지에 접근 가능한지 확인 (선택적)
      // 예: 이전 스테이지를 완료해야 다음 스테이지 접근 가능
      const userProfile = await dbService.getUserByUsername(username);
      if (userProfile && stageNumber > userProfile.max_stage_completed + 1) {
        logger.warn('Unauthorized stage access attempt', {
          username,
          requestedStage: stageNumber,
          maxCompleted: userProfile.max_stage_completed
        });

        return res.status(403).json({
          success: false,
          message: `Stage ${stageNumber} is locked. Complete previous stages first.`,
          error: 'STAGE_LOCKED'
        });
      }

      // 응답 데이터 구성
      const responseData = {
        stage_number: stageData.stage_number,
        stage_name: stageData.stage_name || `Stage ${stageData.stage_number}`,
        difficulty: stageData.difficulty,
        optimal_score: stageData.optimal_score,
        time_limit: stageData.time_limit,
        max_undo_count: stageData.max_undo_count || 3,
        available_blocks: stageData.available_blocks || [],
        initial_board_state: stageData.initial_board_state,
        stage_description: stageData.stage_description,
        stage_hints: stageData.stage_hints,
        is_featured: stageData.is_featured || false
      };

      logger.debug('Stage data retrieved successfully', {
        stageNumber,
        username,
        difficulty: stageData.difficulty
      });

      res.json({
        success: true,
        message: 'Stage data retrieved successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to retrieve stage data', {
        error: error.message,
        stageNumber: req.params.stageNumber,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve stage data',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/stages/:stageNumber/progress
 * 스테이지 진행도 조회
 */
router.get('/:stageNumber/progress',
  authenticateToken,
  validateStageNumber,
  async (req, res) => {
    try {
      const stageNumber = parseInt(req.params.stageNumber);
      const { username } = req.user;

      logger.info('Stage progress requested', {
        stageNumber,
        username,
        ip: req.ip
      });

      // 스테이지 진행도 조회
      const progressData = await dbService.getStageProgress(username, stageNumber);
      
      // 진행도가 없으면 기본값 반환
      const responseData = progressData ? {
        stage_number: progressData.stage_number,
        is_completed: progressData.is_completed,
        stars_earned: progressData.stars_earned,
        best_score: progressData.best_score,
        best_completion_time: progressData.best_completion_time,
        total_attempts: progressData.total_attempts,
        successful_attempts: progressData.successful_attempts,
        first_played_at: progressData.first_played_at,
        first_completed_at: progressData.first_completed_at,
        last_played_at: progressData.last_played_at
      } : {
        stage_number: stageNumber,
        is_completed: false,
        stars_earned: 0,
        best_score: 0,
        best_completion_time: null,
        total_attempts: 0,
        successful_attempts: 0,
        first_played_at: null,
        first_completed_at: null,
        last_played_at: null
      };

      logger.debug('Stage progress retrieved successfully', {
        stageNumber,
        username,
        isCompleted: responseData.is_completed,
        starsEarned: responseData.stars_earned
      });

      res.json({
        success: true,
        message: 'Stage progress retrieved successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to retrieve stage progress', {
        error: error.message,
        stageNumber: req.params.stageNumber,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve stage progress',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * POST /api/stages/complete
 * 스테이지 완료 보고
 */
router.post('/complete',
  authenticateToken,
  requireJson,
  validateStageCompletion,
  async (req, res) => {
    try {
      const { stage_number, score, completion_time, completed } = req.body;
      const { username, userId } = req.user;

      logger.info('Stage completion reported', {
        stageNumber: stage_number,
        score,
        completionTime: completion_time,
        completed,
        username,
        ip: req.ip
      });

      // 스테이지가 존재하는지 확인
      const stageData = await dbService.getStageData(stage_number);
      if (!stageData) {
        return res.status(404).json({
          success: false,
          message: `Stage ${stage_number} not found`,
          error: 'STAGE_NOT_FOUND'
        });
      }

      // 별점 계산
      const starsEarned = dbService.calculateStars(score, stageData.optimal_score);

      // 기존 진행도 확인 (신기록 여부 판단용)
      const existingProgress = await dbService.getStageProgress(username, stage_number);
      const isNewBest = !existingProgress || score > existingProgress.best_score;

      // 스테이지 진행도 업데이트
      const updatedProgress = await dbService.updateStageProgress(
        userId, 
        stage_number, 
        {
          score,
          completionTime: completion_time,
          completed
        }
      );

      // 사용자 통계 업데이트
      await dbService.updateUserStats(userId, score, completed);

      // 최대 클리어 스테이지 업데이트 (완료한 경우)
      if (completed) {
        const userProfile = await dbService.getUserByUsername(username);
        if (userProfile && stage_number > userProfile.max_stage_completed) {
          await dbService.query(
            'UPDATE user_stats SET max_stage_completed = $1 WHERE user_id = $2',
            [stage_number, userId]
          );
        }
      }

      // 레벨업 계산 (추후 구현 가능)
      const levelUp = false; // TODO: 레벨업 로직 구현

      const responseData = {
        success: true,
        stars_earned: starsEarned,
        is_new_best: isNewBest,
        level_up: levelUp,
        message: completed ? 
          `Stage ${stage_number} completed successfully!` : 
          `Stage ${stage_number} attempt recorded.`
      };

      logger.info('Stage completion processed successfully', {
        stageNumber: stage_number,
        username,
        starsEarned,
        isNewBest,
        completed
      });

      res.json({
        success: true,
        message: 'Stage completion processed successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to process stage completion', {
        error: error.message,
        requestBody: req.body,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to process stage completion',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

module.exports = router;