const express = require('express');
const router = express.Router();
const logger = require('../config/logger');
const dbService = require('../config/database');
const { authenticateToken } = require('../middleware/auth');
const { validateStageNumber, requireJson, validateStageCompletion } = require('../middleware/validation');
const { body, param, query, validationResult } = require('express-validator');

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

      // 데이터베이스에서 모든 활성 스테이지 조회
      const query = `
        SELECT 
          stage_number as n,
          CONCAT('스테이지 ', stage_number) as t,
          difficulty as d,
          optimal_score as o,
          time_limit as tl,
          thumbnail_url as tu,
          stage_description as desc,
          array_to_json(available_blocks) AS ab,
          max_undo_count as muc,
          initial_board_state as ibs,
          stage_hints as sh
        FROM stages
        WHERE is_active = true
        ORDER BY stage_number ASC
      `;

      const result = await dbService.query(query);
      const stageMetadata = result.rows;

      // 압축을 위해 작은 JSON 형태로 최적화
      const compactData = stageMetadata.map(stage => ({
        n: stage.n,          // number
        t: stage.t,                 // title  
        d: stage.d,            // difficulty
        o: stage.o,         // optimal_score
        tl: stage.tl,          // time_limit
        tu: stage.tu,
        desc: stage.desc || `${stage.n}번째 블로쿠스 퍼즐에 도전하세요!`,
        ab: stage.ab,
        muc: stage.muc,
        ibs: stage.ibs,
        sh: stage.sh
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

      // 데이터베이스에서 스테이지 데이터 조회
      const stageData = await dbService.getStageData(stageNumber);
      
      if (!stageData) {
        logger.warn('Stage not found', {
          stageNumber,
          username
        });
        
        return res.status(404).json({
          success: false,
          message: `Stage ${stageNumber} not found`,
          error: 'STAGE_NOT_FOUND'
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

      // 힌트 배열 처리 (텍스트를 배열로 변환)
      const hints = stageData.stage_hints ? 
        stageData.stage_hints.split('\n').filter(hint => hint.trim()) : 
        ['블록을 전략적으로 배치하세요.'];

      // 응답 데이터 구성 (게임 플레이에 필요한 상세 데이터)
      const responseData = {
        stage_number: stageData.stage_number,
        difficulty: stageData.difficulty,
        optimal_score: stageData.optimal_score,
        time_limit: stageData.time_limit,
        max_undo_count: stageData.max_undo_count,
        available_blocks: stageData.available_blocks,
        initial_board_state: stageData.initial_board_state,
        hints: hints,
        stage_description: stageData.stage_description,
        is_featured: stageData.is_featured,
        thumbnail_url: stageData.thumbnail_url
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

      // 데이터베이스에서 optimal_score 가져오기
      const optimalScore = stageData.optimal_score;
      
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
        },
        optimalScore  // 실제 스테이지의 optimal_score 전달
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
        errorName: error.name,
        requestBody: req.body,
        username: req.user?.username,
        userId: req.user?.userId,
        stack: error.stack,
        sqlState: error.code,
        sqlDetail: error.detail
      });

      res.status(500).json({
        success: false,
        message: 'Failed to process stage completion',
        error: 'INTERNAL_SERVER_ERROR',
        detail: process.env.NODE_ENV === 'development' ? error.message : undefined
      });
    }
  }
);

/**
 * GET /api/stages/batch
 * 관리자용: 여러 스테이지 메타데이터 일괄 조회
 */
router.get('/batch',
  authenticateToken,
  [
    query('start_stage').optional().isInt({ min: 1 }).withMessage('Start stage must be a positive integer'),
    query('count').optional().isInt({ min: 1, max: 100 }).withMessage('Count must be 1-100'),
    query('include_inactive').optional().isBoolean().withMessage('Include inactive must be boolean')
  ],
  async (req, res) => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        return res.status(400).json({
          success: false,
          message: 'Invalid query parameters',
          error: 'VALIDATION_ERROR',
          details: errors.array()
        });
      }

      const start_stage = parseInt(req.query.start_stage) || 1;
      const count = parseInt(req.query.count) || 50;
      const include_inactive = req.query.include_inactive === 'true';
      const { username } = req.user;

      logger.info('Batch stage data requested', {
        startStage: start_stage,
        count,
        includeInactive: include_inactive,
        username,
        ip: req.ip
      });

      // TODO: 관리자 권한 확인
      // if (!isAdmin(username)) { return 403; }

      // 데이터베이스에서 스테이지 배치 조회
      const query = `
        SELECT 
          stage_number,
          stage_name,
          difficulty,
          optimal_score,
          time_limit,
          max_undo_count,
          array_length(available_blocks, 1) as block_count,
          is_active,
          is_featured,
          created_at,
          updated_at
        FROM stages
        WHERE stage_number >= $1 
          AND stage_number < $2
          ${include_inactive ? '' : 'AND is_active = true'}
        ORDER BY stage_number ASC
      `;

      const result = await dbService.query(query, [start_stage, start_stage + count]);
      const stages = result.rows;

      // 통계 정보 계산
      const summary = {
        total_stages: stages.length,
        active_stages: stages.filter(s => s.is_active).length,
        inactive_stages: stages.filter(s => !s.is_active).length,
        featured_stages: stages.filter(s => s.is_featured).length,
        difficulty_distribution: {},
        avg_optimal_score: 0
      };

      // 난이도별 분포 계산
      stages.forEach(stage => {
        const diff = stage.difficulty;
        summary.difficulty_distribution[diff] = (summary.difficulty_distribution[diff] || 0) + 1;
      });

      // 평균 최적 점수 계산
      if (stages.length > 0) {
        summary.avg_optimal_score = Math.round(
          stages.reduce((sum, stage) => sum + stage.optimal_score, 0) / stages.length
        );
      }

      res.json({
        success: true,
        message: `Retrieved ${stages.length} stages from ${start_stage}`,
        data: {
          stages: stages.map(stage => ({
            stage_number: stage.stage_number,
            title: stage.stage_name || `스테이지 ${stage.stage_number}`,
            difficulty: stage.difficulty,
            optimal_score: stage.optimal_score,
            time_limit: stage.time_limit,
            max_undo_count: stage.max_undo_count,
            block_count: stage.block_count,
            is_active: stage.is_active,
            is_featured: stage.is_featured,
            created_at: stage.created_at,
            updated_at: stage.updated_at
          })),
          summary: summary,
          query_info: {
            start_stage,
            count_requested: count,
            count_returned: stages.length,
            include_inactive
          }
        }
      });

    } catch (error) {
      logger.error('Failed to retrieve batch stage data', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve batch stage data',
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

      // 데이터베이스에서 스테이지 데이터 조회
      const stageData = await dbService.getStageData(stageNumber);
      
      if (!stageData) {
        return res.status(404).json({
          success: false,
          message: `Stage ${stageNumber} not found`,
          error: 'STAGE_NOT_FOUND'
        });
      }

      // 힌트 배열 처리
      const hints = stageData.stage_hints ? 
        stageData.stage_hints.split('\n').filter(hint => hint.trim()).slice(0, 2) : 
        ['블록을 전략적으로 배치하세요.'];
      
      // 카테고리 결정
      const category = stageNumber <= 50 ? 'tutorial' :
                      stageNumber <= 200 ? 'basic' :
                      stageNumber <= 600 ? 'intermediate' : 'advanced';
      
      // 미리보기용 데이터만 추출
      const previewData = {
        stage_number: stageData.stage_number,
        title: stageData.stage_name || `스테이지 ${stageData.stage_number}`,
        difficulty: stageData.difficulty,
        optimal_score: stageData.optimal_score,
        time_limit: stageData.time_limit,
        preview_description: stageData.stage_description || `${stageNumber}번째 블로쿠스 퍼즐에 도전하세요!`,
        category: category,
        available_blocks: stageData.available_blocks,
        hints: hints,
        special_rules: {
          has_special_rules: stageData.max_undo_count !== 5 || stageData.time_limit !== 300,
          time_pressure: stageData.time_limit < 300,
          bonus_multiplier: 1.0 // TODO: 추후 구현 가능
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