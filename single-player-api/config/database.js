const { Pool } = require('pg')
const logger = require('./logger')

class DatabaseService {
  constructor () {
    this.pool = null
  }

  async initialize () {
    try {
      // PostgreSQL 연결 풀 생성
      this.pool = new Pool({
        host: process.env.DB_HOST || 'localhost',
        port: parseInt(process.env.DB_PORT, 10) || 5432,
        database: process.env.DB_NAME || 'blokus_online',
        user: process.env.DB_USER || 'admin',
        password: process.env.DB_PASSWORD || 'admin',
        min: parseInt(process.env.DB_POOL_MIN, 10) || 2,
        max: parseInt(process.env.DB_POOL_MAX, 10) || 10,
        idleTimeoutMillis: 30000,
        connectionTimeoutMillis: 2000
      })

      // 연결 테스트
      const client = await this.pool.connect()
      const result = await client.query('SELECT NOW()')
      client.release()

      logger.info('Database connected successfully', {
        timestamp: result.rows[0].now,
        host: process.env.DB_HOST,
        database: process.env.DB_NAME
      })

      return true
    } catch (error) {
      logger.error('Database connection failed:', error)
      throw error
    }
  }

  async query (text, params = []) {
    try {
      const start = Date.now()
      const result = await this.pool.query(text, params)
      const duration = Date.now() - start

      logger.debug('Database query executed', {
        query: text.substring(0, 100),
        duration: `${duration}ms`,
        rows: result.rowCount
      })

      return result
    } catch (error) {
      logger.error('Database query error:', {
        query: text.substring(0, 100),
        error: error.message,
        params
      })
      throw error
    }
  }

  async getClient () {
    return this.pool.connect()
  }

  async close () {
    if (this.pool) {
      await this.pool.end()
      logger.info('Database connections closed')
    }
  }

  // Health check
  async healthCheck () {
    try {
      const result = await this.query('SELECT 1 as healthy')
      return result.rows[0].healthy === 1
    } catch (error) {
      logger.error('Database health check failed:', error)
      return false
    }
  }

  // 트랜잭션 헬퍼
  async transaction (callback) {
    const client = await this.getClient()

    try {
      await client.query('BEGIN')
      const result = await callback(client)
      await client.query('COMMIT')
      return result
    } catch (error) {
      await client.query('ROLLBACK')
      throw error
    } finally {
      client.release()
    }
  }

  // 싱글플레이 관련 쿼리들

  // 스테이지 데이터 조회
  async getStageData (stageNumber) {
    const query = `
      SELECT 
        stage_number,
        difficulty,
        optimal_score,
        time_limit,
        max_undo_count,
        available_blocks,
        initial_board_state,
        stage_description,
        stage_hints,
        is_active,
        is_featured,
        thumbnail_url
      FROM stages 
      WHERE stage_number = $1 AND is_active = true
    `

    const result = await this.query(query, [stageNumber])
    return result.rows[0] || null
  }

  // 사용자 스테이지 진행도 조회
  async getStageProgress (username, stageNumber) {
    const query = `
      SELECT 
        usp.stage_id,
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
      JOIN users u ON usp.user_id = u.user_id
      WHERE LOWER(u.username) = LOWER($1) 
        AND s.stage_number = $2
    `

    const result = await this.query(query, [username, stageNumber])
    return result.rows[0] || null
  }

  // 사용자 정보 조회
  async getUserByUsername (username) {
    const query = `
      SELECT 
        u.user_id,
        u.username,
        us.single_player_level,
        us.max_stage_completed,
        us.total_single_games,
        us.single_player_score
      FROM users u
      LEFT JOIN user_stats us ON u.user_id = us.user_id
      WHERE LOWER(u.username) = LOWER($1) AND u.is_active = true
    `

    const result = await this.query(query, [username])
    return result.rows[0] || null
  }

  // 스테이지 진행도 업데이트/생성
  async updateStageProgress (userId, stageNumber, scoreData, optimalScore = 100) {
    const { score, completionTime, completed } = scoreData

    logger.info('Starting updateStageProgress', {
      userId, stageNumber, score, completionTime, completed, optimalScore
    })

    return await this.transaction(async (client) => {
      // 먼저 stage_id 조회
      const stageResult = await client.query(
        'SELECT stage_id FROM stages WHERE stage_number = $1',
        [stageNumber]
      )

      if (stageResult.rows.length === 0) {
        throw new Error(`Stage ${stageNumber} not found`)
      }

      const stageId = stageResult.rows[0].stage_id
      logger.info('Found stage_id', { stageId })

      // 기존 진행도 조회
      const existingResult = await client.query(
        'SELECT * FROM user_stage_progress WHERE user_id = $1 AND stage_id = $2',
        [userId, stageId]
      )

      logger.info('Existing progress check', {
        hasExisting: existingResult.rows.length > 0,
        existingData: existingResult.rows[0] || null
      })

      if (existingResult.rows.length === 0) {
        // 새로운 레코드 생성
        const insertQuery = `
          INSERT INTO user_stage_progress (
            user_id, stage_id, is_completed, stars_earned, best_score, 
            best_completion_time, total_attempts, successful_attempts,
            first_played_at, first_completed_at, last_played_at
          ) VALUES ($1, $2, $3, $4, $5, $6, 1, $7, NOW(), $8, NOW())
          RETURNING *
        `

        const values = [
          userId, stageId, completed, this.calculateStars(score, optimalScore),
          score, completionTime, completed ? 1 : 0,
          completed ? new Date() : null
        ]

        const result = await client.query(insertQuery, values)
        return result.rows[0]
      } else {
        // 기존 레코드 업데이트
        const existing = existingResult.rows[0]
        const isNewBest = score > existing.best_score
        const newStars = this.calculateStars(score, optimalScore)

        logger.info('Updating existing progress', {
          existingScore: existing.best_score,
          newScore: score,
          isNewBest,
          existingStars: existing.stars_earned,
          newStars,
          completionTime
        })

        const updateQuery = `
          UPDATE user_stage_progress SET
            is_completed = CASE WHEN $3 THEN true ELSE is_completed END,
            stars_earned = GREATEST(stars_earned, $4),
            best_score = GREATEST(best_score, $5),
            best_completion_time = CASE 
              WHEN $6::INTEGER IS NOT NULL AND (best_completion_time IS NULL OR $6::INTEGER < best_completion_time)
              THEN $6::INTEGER ELSE best_completion_time END,
            total_attempts = total_attempts + 1,
            successful_attempts = successful_attempts + CASE WHEN $3 THEN 1 ELSE 0 END,
            first_completed_at = CASE 
              WHEN $3 AND first_completed_at IS NULL THEN NOW() 
              ELSE first_completed_at END,
            last_played_at = NOW(),
            updated_at = NOW()
          WHERE user_id = $1 AND stage_id = $2
          RETURNING *, $7 as is_new_best
        `

        const updateParams = [userId, stageId, completed, newStars, score, completionTime, isNewBest]

        logger.info('Executing UPDATE query', {
          params: updateParams
        })

        const result = await client.query(updateQuery, updateParams)

        logger.info('UPDATE query completed', {
          rowsAffected: result.rowCount,
          updatedData: result.rows[0]
        })

        return result.rows[0]
      }
    })
  }

  // 사용자 통계 업데이트
  async updateUserStats (userId, scoreGained, completed) {
    const updateQuery = `
      UPDATE user_stats SET
        total_single_games = total_single_games + 1,
        single_player_score = single_player_score + $2,
        updated_at = NOW()
      WHERE user_id = $1
    `

    await this.query(updateQuery, [userId, scoreGained])
  }

  // 별점 계산 헬퍼 (클라이언트에서 계산하는 것과 동일한 로직)
  calculateStars (score, optimalScore = 100) {
    const percentage = (score / optimalScore) * 100

    if (percentage >= 90) return 3
    if (percentage >= 70) return 2
    if (percentage >= 50) return 1
    return 0
  }
}

// 싱글톤 인스턴스
const dbService = new DatabaseService()

module.exports = dbService
