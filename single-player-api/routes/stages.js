const express = require('express');
const router = express.Router();
const logger = require('../config/logger');
const dbService = require('../config/database');
const { authenticateToken } = require('../middleware/auth');
const { body, param, query, validationResult } = require('express-validator');
const StageGenerator = require('../utils/stageGenerator');

/**
 * GET /api/stages/metadata
 * 모든 스테이지 메타데이터 일괄 조회 (목록용)
 * 로그인 성공 시 한 번만 호출하여 클라이언트에서 캐싱
 */
router.get('/metadata',
  authenticateToken,
  async (req, res) => {
    try {
      logger.debug('Stage metadata batch request', {
        userId: req.user.userId,
        username: req.user.username
      });

      // 프로시저럴 스테이지 생성기 사용
      const stageGenerator = new StageGenerator();
      const stageMetadata = [];
      
      // 1000개 스테이지 메타데이터 생성
      for (let i = 1; i <= 1000; i++) {
        const stageData = stageGenerator.generateStage(i);
        stageMetadata.push({
          stage_number: stageData.stage_number,
          title: stageData.title,
          difficulty: stageData.difficulty,
          optimal_score: stageData.optimal_score,
          time_limit: stageData.time_limit,
          thumbnail_url: stageData.thumbnail_url,
          preview_description: stageData.preview_description,
          category: stageData.category
        });
      }

      // 압축을 위해 작은 JSON 형태로 최적화
      const compactData = stageMetadata.map(stage => ({
        n: stage.stage_number,          // number
        t: stage.title,                 // title  
        d: stage.difficulty,            // difficulty
        o: stage.optimal_score,         // optimal_score
        tl: stage.time_limit,          // time_limit
        th: stage.thumbnail_url,        // thumbnail
        desc: stage.preview_description, // description
        cat: stage.category             // category
      }));

      res.json({
        success: true,
        message: 'Stage metadata retrieved successfully',
        data: {
          stages: compactData,
          total_count: stageMetadata.length,
          last_updated: new Date().toISOString()
        }
      });

    } catch (error) {
      logger.error('Failed to retrieve stage metadata', {
        error: error.message,
        userId: req.user?.userId,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve stage metadata',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/stages/:stageNumber
 * 특정 스테이지 상세 데이터 조회 (게임 플레이용)
 */
router.get('/:stageNumber', 
  authenticateToken,
  [
    param('stageNumber').isInt({ min: 1 }).withMessage('Stage number must be a positive integer')
  ],
  async (req, res) => {
    try {
      const stageNumber = parseInt(req.params.stageNumber);
      const { username } = req.user;

      logger.info('Stage data requested', {
        stageNumber,
        username,
        ip: req.ip
      });

      // 프로시저럴 스테이지 생성기로 데이터 생성
      const stageGenerator = new StageGenerator();
      const stageData = stageGenerator.generateStage(stageNumber);
      
      // 유효성 검증
      const validation = stageGenerator.validateStage(stageData);
      if (!validation.isValid) {
        logger.error('Generated stage validation failed', {
          stageNumber,
          issues: validation.issues
        });
        
        return res.status(500).json({
          success: false,
          message: `Stage ${stageNumber} generation failed`,
          error: 'STAGE_GENERATION_ERROR'
        });
      }

      // 사용자 접근 권한 확인
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

      // 응답 데이터 구성 (게임 플레이에 필요한 상세 데이터)
      const responseData = {
        stage_number: stageData.stage_number,
        title: stageData.title,
        difficulty: stageData.difficulty,
        optimal_score: stageData.optimal_score,
        time_limit: stageData.time_limit,
        max_undo_count: stageData.max_undo_count,
        available_blocks: stageData.available_blocks,
        initial_board_state: stageData.initial_board_state,
        hints: stageData.hints,
        special_rules: stageData.special_rules,
        generation_info: stageData.generation_info // 디버깅용
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

      // 프로시저럴 스테이지 생성으로 optimal_score 가져오기
      const stageGenerator = new StageGenerator();
      const generatedStage = stageGenerator.generateStage(stage_number);
      const optimalScore = generatedStage.optimal_score;
      
      // 별점 계산 (클라이언트와 동일한 로직)
      let starsEarned = 0;
      if (completed) {
        if (score >= optimalScore * 0.9) starsEarned = 3;      // 90% 이상: 3별
        else if (score >= optimalScore * 0.7) starsEarned = 2; // 70% 이상: 2별  
        else if (score >= optimalScore * 0.5) starsEarned = 1; // 50% 이상: 1별
        // 50% 미만: 0별
      }

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

/**
 * POST /api/stages/generate/batch
 * 관리자용: 여러 스테이지 일괄 생성 및 검증
 */
router.post('/generate/batch',
  authenticateToken,
  [
    body('start_stage').isInt({ min: 1 }).withMessage('Start stage must be a positive integer'),
    body('count').isInt({ min: 1, max: 100 }).withMessage('Count must be 1-100'),
    body('validate_only').optional().isBoolean().withMessage('Validate only must be boolean')
  ],
  async (req, res) => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        return res.status(400).json({
          success: false,
          message: 'Invalid input data',
          error: 'VALIDATION_ERROR',
          details: errors.array()
        });
      }

      const { start_stage, count, validate_only = false } = req.body;
      const { username } = req.user;

      logger.info('Batch stage generation requested', {
        startStage: start_stage,
        count,
        validateOnly: validate_only,
        username,
        ip: req.ip
      });

      // TODO: 관리자 권한 확인
      // if (!isAdmin(username)) { return 403; }

      const stageGenerator = new StageGenerator();
      const result = stageGenerator.generateMultipleStages(start_stage, count);

      // 검증만 요청한 경우
      if (validate_only) {
        res.json({
          success: true,
          message: `Validated ${count} stages`,
          data: {
            validation_results: result.validationResults,
            summary: result.summary
          }
        });
        return;
      }

      // 전체 생성 결과 반환
      res.json({
        success: true,
        message: `Generated ${count} stages starting from ${start_stage}`,
        data: {
          stages: result.stages.map(stage => ({
            stage_number: stage.stage_number,
            title: stage.title,
            difficulty: stage.difficulty,
            optimal_score: stage.optimal_score,
            generation_info: stage.generation_info
          })),
          validation_results: result.validationResults,
          summary: result.summary
        }
      });

    } catch (error) {
      logger.error('Failed to generate batch stages', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to generate batch stages',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/stages/:stageNumber/preview
 * 스테이지 미리보기 (로그인 불필요, 메타데이터만)
 */
router.get('/:stageNumber/preview',
  [
    param('stageNumber').isInt({ min: 1 }).withMessage('Stage number must be a positive integer')
  ],
  async (req, res) => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        return res.status(400).json({
          success: false,
          message: 'Invalid stage number',
          error: 'VALIDATION_ERROR',
          details: errors.array()
        });
      }

      const stageNumber = parseInt(req.params.stageNumber);
      
      logger.debug('Stage preview requested', {
        stageNumber,
        ip: req.ip
      });

      // 프로시저럴 생성으로 미리보기 데이터 생성
      const stageGenerator = new StageGenerator();
      const stageData = stageGenerator.generateStage(stageNumber);
      
      // 미리보기용 데이터만 추출
      const previewData = {
        stage_number: stageData.stage_number,
        title: stageData.title,
        difficulty: stageData.difficulty,
        optimal_score: stageData.optimal_score,
        time_limit: stageData.time_limit,
        preview_description: stageData.preview_description,
        category: stageData.category,
        available_blocks: stageData.available_blocks,
        hints: stageData.hints.slice(0, 2), // 처음 2개 힌트만
        special_rules: {
          has_special_rules: Object.values(stageData.special_rules).some(v => 
            v !== false && v !== 1.0 && v !== null
          ),
          time_pressure: stageData.special_rules.time_pressure,
          bonus_multiplier: stageData.special_rules.bonus_multiplier
        }
      };

      res.json({
        success: true,
        message: 'Stage preview retrieved successfully',
        data: previewData
      });

    } catch (error) {
      logger.error('Failed to retrieve stage preview', {
        error: error.message,
        stageNumber: req.params.stageNumber,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve stage preview',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

module.exports = router;