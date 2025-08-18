const express = require('express');
const router = express.Router();
const logger = require('../config/logger');
const dbService = require('../config/database');
const { authenticateToken } = require('../middleware/auth');
const { validateUsername, validatePagination } = require('../middleware/validation');
const { query } = require('express-validator');

/**
 * GET /api/user/profile
 * 현재 로그인한 사용자의 프로필 조회
 */
router.get('/profile',
  authenticateToken,
  async (req, res) => {
    try {
      const { username } = req.user;

      logger.info('User profile requested', {
        username,
        ip: req.ip
      });

      // 사용자 프로필 조회
      const userProfile = await dbService.getUserByUsername(username);
      
      if (!userProfile) {
        return res.status(404).json({
          success: false,
          message: 'User not found',
          error: 'USER_NOT_FOUND'
        });
      }

      // 응답 데이터 구성
      const responseData = {
        username: userProfile.username,
        single_player_level: userProfile.single_player_level || 1,
        max_stage_completed: userProfile.max_stage_completed || 0,
        total_single_games: userProfile.total_single_games || 0,
        single_player_score: userProfile.single_player_score || 0
      };

      logger.debug('User profile retrieved successfully', {
        username,
        level: responseData.single_player_level,
        maxStage: responseData.max_stage_completed
      });

      res.json({
        success: true,
        message: 'User profile retrieved successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to retrieve user profile', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve user profile',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/user/stats
 * 현재 로그인한 사용자의 상세 통계 조회
 */
router.get('/stats',
  authenticateToken,
  async (req, res) => {
    try {
      const { username, userId } = req.user;

      logger.info('User stats requested', {
        username,
        ip: req.ip
      });

      // 사용자 기본 정보 조회
      const userProfile = await dbService.getUserByUsername(username);
      if (!userProfile) {
        return res.status(404).json({
          success: false,
          message: 'User not found',
          error: 'USER_NOT_FOUND'
        });
      }

      // 추가 통계 조회
      const statsQuery = `
        SELECT 
          COUNT(*) as total_stages_played,
          COUNT(CASE WHEN is_completed = true THEN 1 END) as stages_completed,
          COUNT(CASE WHEN stars_earned = 3 THEN 1 END) as perfect_stages,
          AVG(CASE WHEN is_completed = true THEN best_score END) as average_score,
          SUM(total_attempts) as total_attempts,
          SUM(successful_attempts) as successful_attempts
        FROM user_stage_progress usp
        JOIN stages s ON usp.stage_id = s.stage_id
        WHERE usp.user_id = $1
      `;

      const statsResult = await dbService.query(statsQuery, [userId]);
      const stats = statsResult.rows[0] || {};

      // 완료율 계산
      const completionRate = stats.total_stages_played > 0 
        ? Math.round((stats.stages_completed / stats.total_stages_played) * 100) 
        : 0;

      // 성공률 계산
      const successRate = stats.total_attempts > 0
        ? Math.round((stats.successful_attempts / stats.total_attempts) * 100)
        : 0;

      const responseData = {
        // 기본 프로필
        username: userProfile.username,
        single_player_level: userProfile.single_player_level || 1,
        max_stage_completed: userProfile.max_stage_completed || 0,
        total_single_games: userProfile.total_single_games || 0,
        single_player_score: userProfile.single_player_score || 0,

        // 상세 통계
        total_stages_played: parseInt(stats.total_stages_played) || 0,
        stages_completed: parseInt(stats.stages_completed) || 0,
        perfect_stages: parseInt(stats.perfect_stages) || 0,
        average_score: Math.round(parseFloat(stats.average_score) || 0),
        completion_rate: completionRate,
        success_rate: successRate,
        total_attempts: parseInt(stats.total_attempts) || 0,
        successful_attempts: parseInt(stats.successful_attempts) || 0
      };

      logger.debug('User stats retrieved successfully', {
        username,
        totalGames: responseData.total_single_games,
        completionRate,
        successRate
      });

      res.json({
        success: true,
        message: 'User statistics retrieved successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to retrieve user stats', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve user statistics',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/user/progress/batch
 * 사용자의 전체 스테이지 진행도 일괄 조회 (스크롤 뷰용)
 * 스테이지 목록 화면에서 한 번만 호출하여 클라이언트에서 캐싱
 */
router.get('/progress/batch',
  authenticateToken,
  async (req, res) => {
    try {
      const { username, userId } = req.user;

      logger.debug('User progress batch request', {
        username,
        userId,
        ip: req.ip
      });

      // 사용자의 모든 스테이지 진행도 조회 (압축된 형태)
      const progressQuery = `
        SELECT 
          s.stage_number,
          COALESCE(usp.is_completed, false) as is_completed,
          COALESCE(usp.stars_earned, 0) as stars_earned,
          COALESCE(usp.best_score, 0) as best_score,
          COALESCE(usp.best_completion_time, 0) as best_time,
          COALESCE(usp.total_attempts, 0) as attempts
        FROM stages s
        LEFT JOIN user_stage_progress usp ON s.stage_id = usp.stage_id AND usp.user_id = $1
        WHERE s.stage_number <= 1000
        ORDER BY s.stage_number ASC
      `;

      const progressResult = await dbService.query(progressQuery, [userId]);

      // 데이터 압축 (네트워크 효율성을 위해)
      const compactProgress = progressResult.rows.map(row => ({
        n: row.stage_number,           // number
        c: row.is_completed,           // completed
        s: row.stars_earned,           // stars
        bs: row.best_score,            // best_score
        bt: row.best_completion_time,  // best_time
        a: row.total_attempts          // attempts
      }));

      // 사용자의 현재 상태 정보도 함께 제공
      const userStats = await dbService.getUserByUsername(username);
      const currentStatus = {
        max_stage_completed: userStats?.max_stage_completed || 0,
        single_player_level: userStats?.single_player_level || 1,
        total_stars: compactProgress.reduce((sum, stage) => sum + stage.s, 0),
        completion_count: compactProgress.filter(stage => stage.c).length
      };

      res.json({
        success: true,
        message: 'User progress batch retrieved successfully',
        data: {
          progress: compactProgress,
          current_status: currentStatus,
          total_count: compactProgress.length,
          last_updated: new Date().toISOString()
        }
      });

    } catch (error) {
      logger.error('Failed to retrieve user progress batch', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve user progress batch',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/user/progress
 * 사용자의 전체 스테이지 진행도 조회 (페이지네이션 지원)
 */
router.get('/progress',
  authenticateToken,
  [
    query('page').optional().isInt({ min: 1 }).withMessage('Page must be a positive integer'),
    query('limit').optional().isInt({ min: 1, max: 100 }).withMessage('Limit must be 1-100')
  ],
  async (req, res) => {
    try {
      const { username, userId } = req.user;
      const page = parseInt(req.query.page) || 1;
      const limit = parseInt(req.query.limit) || 20;
      const offset = (page - 1) * limit;

      logger.info('User progress list requested', {
        username,
        page,
        limit,
        ip: req.ip
      });

      // 총 진행도 수 조회
      const countQuery = `
        SELECT COUNT(*) as total
        FROM user_stage_progress usp
        JOIN stages s ON usp.stage_id = s.stage_id
        WHERE usp.user_id = $1
      `;
      const countResult = await dbService.query(countQuery, [userId]);
      const totalCount = parseInt(countResult.rows[0].total);

      // 진행도 목록 조회
      const progressQuery = `
        SELECT 
          s.stage_number,
          usp.is_completed,
          usp.stars_earned,
          usp.best_score,
          usp.best_completion_time,
          usp.total_attempts,
          usp.successful_attempts,
          usp.first_played_at,
          usp.first_completed_at,
          usp.last_played_at
        FROM user_stage_progress usp
        JOIN stages s ON usp.stage_id = s.stage_id
        WHERE usp.user_id = $1
        ORDER BY s.stage_number ASC
        LIMIT $2 OFFSET $3
      `;

      const progressResult = await dbService.query(progressQuery, [userId, limit, offset]);

      const responseData = {
        progress: progressResult.rows.map(row => ({
          stage_number: row.stage_number,
          is_completed: row.is_completed,
          stars_earned: row.stars_earned,
          best_score: row.best_score,
          best_completion_time: row.best_completion_time,
          total_attempts: row.total_attempts,
          successful_attempts: row.successful_attempts,
          first_played_at: row.first_played_at,
          first_completed_at: row.first_completed_at,
          last_played_at: row.last_played_at
        })),
        pagination: {
          page,
          limit,
          total: totalCount,
          pages: Math.ceil(totalCount / limit)
        }
      };

      logger.debug('User progress list retrieved successfully', {
        username,
        totalRecords: totalCount,
        page,
        recordsReturned: progressResult.rows.length
      });

      res.json({
        success: true,
        message: 'User progress retrieved successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to retrieve user progress', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve user progress',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/user/sync/light
 * 라이트 동기화 - 앱 시작시/포어그라운드 복귀시 사용
 * 사용자 프로필 요약 + 버전 정보만 조회
 */
router.get('/sync/light',
  authenticateToken,
  async (req, res) => {
    try {
      const { username, userId } = req.user;

      logger.debug('Light sync requested', {
        username,
        ip: req.ip
      });

      // 사용자 프로필과 버전 정보 조회
      const userQuery = `
        SELECT 
          username,
          single_player_level,
          max_stage_completed,
          total_single_games,
          single_player_score,
          progress_version,
          progress_updated_at,
          last_sync_at
        FROM users 
        WHERE user_id = $1
      `;

      // 스테이지 메타데이터 마지막 업데이트 시각 조회
      const metadataQuery = `
        SELECT MAX(updated_at) as stages_last_updated
        FROM stages
      `;

      const [userResult, metadataResult] = await Promise.all([
        dbService.query(userQuery, [userId]),
        dbService.query(metadataQuery, [])
      ]);

      const userProfile = userResult.rows[0];
      const stagesLastUpdated = metadataResult.rows[0]?.stages_last_updated;

      if (!userProfile) {
        return res.status(404).json({
          success: false,
          message: 'User not found',
          error: 'USER_NOT_FOUND'
        });
      }

      // last_sync_at 업데이트
      await dbService.query(
        'UPDATE users SET last_sync_at = CURRENT_TIMESTAMP WHERE user_id = $1',
        [userId]
      );

      const responseData = {
        user_profile: {
          username: userProfile.username,
          level: userProfile.single_player_level || 1,
          max_stage_completed: userProfile.max_stage_completed || 0,
          total_games: userProfile.total_single_games || 0,
          total_score: userProfile.single_player_score || 0,
          progress_version: userProfile.progress_version || 1,
          progress_updated_at: userProfile.progress_updated_at
        },
        stages_last_updated: stagesLastUpdated,
        server_time: new Date().toISOString(),
        sync_completed_at: new Date().toISOString()
      };

      logger.debug('Light sync completed', {
        username,
        progressVersion: responseData.user_profile.progress_version,
        stagesLastUpdated
      });

      res.json({
        success: true,
        message: 'Light sync completed successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to perform light sync', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to perform light sync',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/user/sync/progress
 * 전체 진행도 동기화 - 버전 불일치시 사용
 * 모든 스테이지 진행도 데이터 조회
 */
router.get('/sync/progress',
  authenticateToken,
  [
    query('from_stage').optional().isInt({ min: 1 }).withMessage('from_stage must be a positive integer'),
    query('to_stage').optional().isInt({ min: 1 }).withMessage('to_stage must be a positive integer')
  ],
  async (req, res) => {
    try {
      const { username, userId } = req.user;
      const fromStage = parseInt(req.query.from_stage) || 1;
      const toStage = parseInt(req.query.to_stage) || 1000;

      logger.debug('Progress sync requested', {
        username,
        fromStage,
        toStage,
        ip: req.ip
      });

      // 사용자의 현재 progress_version 조회
      const versionQuery = `
        SELECT progress_version, progress_updated_at
        FROM users 
        WHERE user_id = $1
      `;
      const versionResult = await dbService.query(versionQuery, [userId]);
      const currentVersion = versionResult.rows[0];

      // 지정된 범위의 스테이지 진행도 조회
      const progressQuery = `
        SELECT 
          s.stage_number,
          s.stage_id,
          COALESCE(usp.is_completed, false) as is_completed,
          COALESCE(usp.stars_earned, 0) as stars_earned,
          COALESCE(usp.best_score, 0) as best_score,
          COALESCE(usp.best_completion_time, 0) as best_completion_time,
          COALESCE(usp.total_attempts, 0) as total_attempts,
          COALESCE(usp.successful_attempts, 0) as successful_attempts,
          usp.first_completed_at,
          usp.last_played_at
        FROM stages s
        LEFT JOIN user_stage_progress usp ON s.stage_id = usp.stage_id AND usp.user_id = $1
        WHERE s.stage_number >= $2 AND s.stage_number <= $3 AND s.is_active = true
        ORDER BY s.stage_number ASC
      `;

      const progressResult = await dbService.query(progressQuery, [userId, fromStage, toStage]);

      const responseData = {
        progress_version: currentVersion?.progress_version || 1,
        progress_updated_at: currentVersion?.progress_updated_at,
        from_stage: fromStage,
        to_stage: toStage,
        progress_data: progressResult.rows.map(row => ({
          stage_number: row.stage_number,
          stage_id: row.stage_id,
          is_completed: row.is_completed,
          stars_earned: row.stars_earned,
          best_score: row.best_score,
          best_completion_time: row.best_completion_time,
          total_attempts: row.total_attempts,
          successful_attempts: row.successful_attempts,
          first_completed_at: row.first_completed_at,
          last_played_at: row.last_played_at
        })),
        total_count: progressResult.rows.length,
        sync_completed_at: new Date().toISOString()
      };

      logger.debug('Progress sync completed', {
        username,
        progressVersion: responseData.progress_version,
        recordCount: responseData.total_count,
        fromStage,
        toStage
      });

      res.json({
        success: true,
        message: 'Progress sync completed successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to sync progress', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to sync progress',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/user/sync/metadata
 * 스테이지 메타데이터 동기화 - TTL 만료시 사용
 * 스테이지 기본 정보만 조회 (게임 데이터 제외)
 */
router.get('/sync/metadata',
  authenticateToken,
  [
    query('version').optional().isString().withMessage('version must be a string')
  ],
  async (req, res) => {
    try {
      const { username, userId } = req.user;
      const clientVersion = req.query.version;

      logger.debug('Metadata sync requested', {
        username,
        clientVersion,
        ip: req.ip
      });

      // 현재 메타데이터 버전 (최신 업데이트 시각)
      const versionQuery = `
        SELECT MAX(updated_at) as current_version
        FROM stages
      `;
      const versionResult = await dbService.query(versionQuery, []);
      const currentVersion = versionResult.rows[0]?.current_version;

      // 클라이언트 버전과 동일하면 304 Not Modified 응답
      if (clientVersion && clientVersion === currentVersion?.toISOString()) {
        return res.status(304).json({
          success: true,
          message: 'Metadata not modified',
          data: {
            current_version: currentVersion?.toISOString(),
            not_modified: true
          }
        });
      }

      // 스테이지 메타데이터 조회 (게임 데이터 제외)
      const metadataQuery = `
        SELECT 
          stage_id,
          stage_number,
          difficulty,
          optimal_score,
          time_limit,
          max_undo_count,
          stage_description,
          stage_hints,
          available_blocks,
          is_active,
          is_featured,
          thumbnail_url,
          updated_at,
          initial_board_state
        FROM stages
        WHERE is_active = true
        ORDER BY stage_number ASC
      `;

      const metadataResult = await dbService.query(metadataQuery, []);

      // last_metadata_check_at 업데이트
      await dbService.query(
        'UPDATE users SET last_metadata_check_at = CURRENT_TIMESTAMP WHERE user_id = $1',
        [userId]
      );

      const responseData = {
        metadata_version: currentVersion?.toISOString(),
        stages: metadataResult.rows.map(row => ({
          stage_id: row.stage_id,
          stage_number: row.stage_number,
          difficulty: row.difficulty,
          optimal_score: row.optimal_score,
          time_limit: row.time_limit,
          max_undo_count: row.max_undo_count,
          description: row.stage_description,
          hints: row.stage_hints,
          available_blocks: row.available_blocks,
          is_featured: row.is_featured,
          thumbnail_url: row.thumbnail_url,
          initial_board_state: row.initial_board_state
        })),
        total_count: metadataResult.rows.length,
        sync_completed_at: new Date().toISOString()
      };

      logger.debug('Metadata sync completed', {
        username,
        metadataVersion: responseData.metadata_version,
        stageCount: responseData.total_count
      });

      res.json({
        success: true,
        message: 'Metadata sync completed successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to sync metadata', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to sync metadata',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

module.exports = router;