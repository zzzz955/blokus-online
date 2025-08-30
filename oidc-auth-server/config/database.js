const { Pool } = require('pg')
const logger = require('./logger')
const { env } = require('./env')

class DatabaseService {
  constructor() {
    this.pool = null
  }

  async initialize() {
    try {
      // PostgreSQL 연결 풀 생성
      this.pool = new Pool({
        host: env.DB_HOST,
        port: env.DB_PORT,
        database: env.DB_NAME,
        user: env.DB_USER,
        password: env.DB_PASSWORD,
        min: env.DB_POOL_MIN,
        max: env.DB_POOL_MAX,
        idleTimeoutMillis: 30000,
        connectionTimeoutMillis: 2000
      })

      // 연결 테스트
      const client = await this.pool.connect()
      const result = await client.query('SELECT NOW()')
      client.release()

      logger.info('Database connected successfully', {
        timestamp: result.rows[0].now,
        host: env.DB_HOST,
        database: env.DB_NAME
      })

      return true
    } catch (error) {
      logger.error('Database connection failed:', error)
      throw error
    }
  }

  async query(text, params = []) {
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

  async getClient() {
    return this.pool.connect()
  }

  async close() {
    if (this.pool) {
      await this.pool.end()
      logger.info('Database connections closed')
    }
  }

  // Health check
  async healthCheck() {
    try {
      const result = await this.query('SELECT 1 as healthy')
      return result.rows[0].healthy === 1
    } catch (error) {
      logger.error('Database health check failed:', error)
      return false
    }
  }

  // 트랜잭션 헬퍼
  async transaction(callback) {
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

  // ==================== OIDC 관련 메서드들 ====================

  // 사용자 조회 (인증용)
  async getUserByUsername(username) {
    const query = `
      SELECT 
        u.user_id,
        u.username,
        u.display_name,
        u.password_hash,
        u.email,
        u.is_active,
        u.created_at,
        u.last_login_at
      FROM users u
      WHERE LOWER(u.username) = LOWER($1) AND u.is_active = true
    `

    const result = await this.query(query, [username])
    return result.rows[0] || null
  }

  // 사용자 ID로 조회
  async getUserById(userId) {
    const query = `
      SELECT 
        u.user_id,
        u.username,
        u.display_name,
        u.email,
        u.oauth_id,
        u.oauth_provider,
        u.is_active,
        u.created_at,
        u.last_login_at
      FROM users u
      WHERE u.user_id = $1 AND u.is_active = true
    `

    const result = await this.query(query, [userId])
    return result.rows[0] || null
  }

  // 이메일로 사용자 조회 (OAuth용)
  async getUserByEmail(email) {
    const query = `
      SELECT 
        u.user_id,
        u.username,
        u.display_name,
        u.email,
        u.oauth_id,
        u.oauth_provider,
        u.password_hash,
        u.is_active,
        u.created_at,
        u.last_login_at
      FROM users u
      WHERE LOWER(u.email) = LOWER($1) AND u.is_active = true
    `

    const result = await this.query(query, [email])
    return result.rows[0] || null
  }

  // OAuth 사용자 생성
  async createOAuthUser(userData) {
    return await this.transaction(async (client) => {
      // 사용자 생성
      const userQuery = `
        INSERT INTO users (
          username, 
          email, 
          display_name, 
          oauth_id,
          oauth_provider,
          password_hash,
          level,
          experience_points,
          single_player_level,
          max_stage_completed,
          created_at,
          updated_at
        ) VALUES ($1, $2, $3, $4, $5, $6, 1, 0, 1, 0, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
        RETURNING *
      `

      const userResult = await client.query(userQuery, [
        userData.username,
        userData.email,
        userData.display_name,
        userData.oauth_id,
        userData.oauth_provider,
        userData.password_hash // OAuth 사용자는 null
      ])

      const user = userResult.rows[0]

      // 기본 사용자 통계 생성 (멀티플레이 + 싱글플레이 통계 포함)
      const statsQuery = `
        INSERT INTO user_stats (
          user_id,
          total_games,
          wins,
          losses,
          total_score,
          best_score,
          single_player_level,
          max_stage_completed,
          total_single_games,
          single_player_score
        ) VALUES ($1, 0, 0, 0, 0, 0, 1, 0, 0, 0)
      `

      await client.query(statsQuery, [user.user_id])

      logger.info('OAuth user created successfully', {
        userId: user.user_id,
        username: user.username,
        email: user.email,
        oauthId: user.oauth_id,
        oauthProvider: user.oauth_provider
      })

      return user
    })
  }

  // 기존 사용자에 OAuth ID 추가
  async updateUserOAuthId(userId, oauthId, provider) {
    const query = `
      UPDATE users 
      SET oauth_id = $1, oauth_provider = $2, updated_at = CURRENT_TIMESTAMP
      WHERE user_id = $3
      RETURNING *
    `

    const result = await this.query(query, [oauthId, provider, userId])
    return result.rows[0] || null
  }

  // Authorization Code 저장
  async storeAuthorizationCode(code, clientId, userId, redirectUri, scope, codeChallenge, codeChallengeMethod, expiresAt) {
    const query = `
      INSERT INTO authorization_codes (
        code, client_id, user_id, redirect_uri, scope,
        code_challenge, code_challenge_method, expires_at, created_at
      ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, NOW())
      RETURNING *
    `

    const values = [code, clientId, userId, redirectUri, scope, codeChallenge, codeChallengeMethod, expiresAt]
    const result = await this.query(query, values)
    return result.rows[0]
  }

  // Authorization Code 조회 및 삭제
  async consumeAuthorizationCode(code) {
    return await this.transaction(async (client) => {
      // 코드 조회
      const selectQuery = `
        SELECT * FROM authorization_codes 
        WHERE code = $1 AND expires_at > NOW()
      `
      const selectResult = await client.query(selectQuery, [code])

      if (selectResult.rows.length === 0) {
        return null
      }

      // 코드 삭제 (one-time use)
      const deleteQuery = `DELETE FROM authorization_codes WHERE code = $1`
      await client.query(deleteQuery, [code])

      return selectResult.rows[0]
    })
  }

  // Refresh Token Family 생성
  async createRefreshTokenFamily(userId, clientId, deviceFingerprint) {
    const maxLifetime = new Date(Date.now() + 90 * 24 * 60 * 60 * 1000) // 90일 후
    
    const query = `
      INSERT INTO refresh_token_families (
        user_id, client_id, device_fingerprint, created_at, last_used_at, max_expires_at
      ) VALUES ($1, $2, $3, NOW(), NOW(), $4)
      RETURNING *
    `

    const result = await this.query(query, [userId, clientId, deviceFingerprint, maxLifetime])
    return result.rows[0]
  }

  // Refresh Token 저장
  async storeRefreshToken(familyId, jti, prevJti, expiresAt) {
    const query = `
      INSERT INTO refresh_tokens (
        family_id, jti, prev_jti, status, expires_at, created_at, last_used_at
      ) VALUES ($1, $2, $3, 'active', $4, NOW(), NOW())
      RETURNING *
    `

    const result = await this.query(query, [familyId, jti, prevJti, expiresAt])
    return result.rows[0]
  }

  // Refresh Token 조회
  async getRefreshToken(jti) {
    const query = `
      SELECT rt.*, rtf.user_id, rtf.client_id, rtf.device_fingerprint
      FROM refresh_tokens rt
      JOIN refresh_token_families rtf ON rt.family_id = rtf.family_id
      WHERE rt.jti = $1 AND rt.status = 'active'
    `

    const result = await this.query(query, [jti])
    return result.rows[0] || null
  }

  // Refresh Token 사용 (rotation with sliding window)
  async rotateRefreshToken(oldJti, newJti, expiresAt) {
    return await this.transaction(async (client) => {
      // 기존 토큰 및 패밀리 조회
      const oldTokenQuery = `
        SELECT rt.*, rtf.max_expires_at, rtf.created_at as family_created_at
        FROM refresh_tokens rt
        JOIN refresh_token_families rtf ON rt.family_id = rtf.family_id
        WHERE rt.jti = $1 AND rt.status = 'active'
      `
      const oldTokenResult = await client.query(oldTokenQuery, [oldJti])

      if (oldTokenResult.rows.length === 0) {
        throw new Error('Refresh token not found or already used')
      }

      const oldToken = oldTokenResult.rows[0]
      const now = new Date()

      // 최대 만료일 확인 (90일)
      if (now > oldToken.max_expires_at) {
        throw new Error('Refresh token family has reached maximum lifetime (90 days)')
      }

      // 슬라이딩 윈도우: 새 만료일은 현재시간 + 30일, 단 최대 만료일을 넘지 않음
      const slidingExpiresAt = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000) // 30일 후
      const finalExpiresAt = new Date(Math.min(slidingExpiresAt.getTime(), oldToken.max_expires_at.getTime()))

      // 기존 토큰을 used로 마킹
      const updateOldQuery = `
        UPDATE refresh_tokens 
        SET status = 'used', last_used_at = NOW()
        WHERE jti = $1
      `
      await client.query(updateOldQuery, [oldJti])

      // 새 토큰 생성 (슬라이딩 윈도우 적용)
      const newTokenQuery = `
        INSERT INTO refresh_tokens (
          family_id, jti, prev_jti, status, expires_at, created_at, last_used_at
        ) VALUES ($1, $2, $3, 'active', $4, NOW(), NOW())
        RETURNING *
      `
      const newTokenResult = await client.query(newTokenQuery, [
        oldToken.family_id, newJti, oldJti, finalExpiresAt
      ])

      // Family 마지막 사용 시간 업데이트
      const updateFamilyQuery = `
        UPDATE refresh_token_families 
        SET last_used_at = NOW()
        WHERE family_id = $1
      `
      await client.query(updateFamilyQuery, [oldToken.family_id])

      return newTokenResult.rows[0]
    })
  }

  // Refresh Token 재사용 감지 및 Family 무효화
  async detectTokenReuseAndRevoke(jti) {
    return await this.transaction(async (client) => {
      // 사용된 토큰인지 확인
      const tokenQuery = `
        SELECT * FROM refresh_tokens 
        WHERE jti = $1
      `
      const tokenResult = await client.query(tokenQuery, [jti])

      if (tokenResult.rows.length === 0) {
        return { detected: false, reason: 'token_not_found' }
      }

      const token = tokenResult.rows[0]

      if (token.status === 'used') {
        // 재사용 감지! Family 전체 무효화
        logger.warn('Refresh token reuse detected', {
          jti,
          familyId: token.family_id,
          status: token.status
        })

        // Family의 모든 토큰 무효화
        const revokeQuery = `
          UPDATE refresh_tokens 
          SET status = 'revoked', last_used_at = NOW()
          WHERE family_id = $1 AND status = 'active'
        `
        await client.query(revokeQuery, [token.family_id])

        // Family 무효화
        const revokeFamilyQuery = `
          UPDATE refresh_token_families 
          SET status = 'revoked', last_used_at = NOW()
          WHERE family_id = $1
        `
        await client.query(revokeFamilyQuery, [token.family_id])

        return { detected: true, reason: 'token_reuse', familyId: token.family_id }
      }

      return { detected: false, reason: 'token_valid' }
    })
  }

  // Refresh Token Family 무효화 (로그아웃)
  async revokeRefreshTokenFamily(familyId) {
    return await this.transaction(async (client) => {
      // Family의 모든 토큰 무효화
      const revokeTokensQuery = `
        UPDATE refresh_tokens 
        SET status = 'revoked', last_used_at = NOW()
        WHERE family_id = $1 AND status = 'active'
      `
      await client.query(revokeTokensQuery, [familyId])

      // Family 무효화
      const revokeFamilyQuery = `
        UPDATE refresh_token_families 
        SET status = 'revoked', last_used_at = NOW()
        WHERE family_id = $1
      `
      const result = await client.query(revokeFamilyQuery, [familyId])

      return result.rowCount > 0
    })
  }

  // 사용자의 모든 Refresh Token Family 무효화 (전체 로그아웃)
  async revokeAllUserRefreshTokens(userId) {
    return await this.transaction(async (client) => {
      // 사용자의 모든 활성 Family 조회
      const familiesQuery = `
        SELECT family_id FROM refresh_token_families 
        WHERE user_id = $1 AND status = 'active'
      `
      const familiesResult = await client.query(familiesQuery, [userId])

      if (familiesResult.rows.length === 0) {
        return 0
      }

      const familyIds = familiesResult.rows.map(row => row.family_id)

      // 모든 토큰 무효화
      const revokeTokensQuery = `
        UPDATE refresh_tokens 
        SET status = 'revoked', last_used_at = NOW()
        WHERE family_id = ANY($1) AND status = 'active'
      `
      await client.query(revokeTokensQuery, [familyIds])

      // 모든 Family 무효화
      const revokeFamiliesQuery = `
        UPDATE refresh_token_families 
        SET status = 'revoked', last_used_at = NOW()
        WHERE user_id = $1 AND status = 'active'
      `
      const result = await client.query(revokeFamiliesQuery, [userId])

      return result.rowCount
    })
  }

  // 만료된 토큰 정리 (주기적 실행용)
  async cleanupExpiredTokens() {
    return await this.transaction(async (client) => {
      // 만료된 Authorization Code 삭제
      const cleanupCodesQuery = `
        DELETE FROM authorization_codes 
        WHERE expires_at < NOW()
      `
      const codesResult = await client.query(cleanupCodesQuery)

      // 만료된 Refresh Token 무효화
      const cleanupTokensQuery = `
        UPDATE refresh_tokens 
        SET status = 'expired'
        WHERE expires_at < NOW() AND status = 'active'
      `
      const tokensResult = await client.query(cleanupTokensQuery)

      logger.info('Cleanup completed', {
        expiredCodes: codesResult.rowCount,
        expiredTokens: tokensResult.rowCount
      })

      return {
        expiredCodes: codesResult.rowCount,
        expiredTokens: tokensResult.rowCount
      }
    })
  }
}

// 싱글톤 인스턴스
const dbService = new DatabaseService()

// 초기화
dbService.initialize().catch(error => {
  logger.error('Failed to initialize database service', error)
  process.exit(1)
})

module.exports = dbService