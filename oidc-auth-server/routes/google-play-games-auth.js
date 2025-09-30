/**
 * Google Play Games 인증 라우트
 *
 * Unity Mobile Client → Google Play Games SDK → Auth Code → Backend
 * Backend: Auth Code → Google API 검증 → JWT 토큰 발급
 *
 * 핵심: ID Token의 sub 필드 (Google Account ID)를 사용하여
 * Google OAuth와 Google Play Games 계정을 자동으로 연동
 */

const express = require('express');
const { body, validationResult } = require('express-validator');
const { OAuth2Client } = require('google-auth-library');
const crypto = require('crypto');
const logger = require('../config/logger');
const dbService = require('../config/database');
const { env } = require('../config/env');
const router = express.Router();

// Import generateTokens from direct-auth
const { generateTokens } = require('./token');

// Google OAuth2 Client (Android)
const androidOAuthClient = new OAuth2Client({
  clientId: env.GOOGLE_ANDROID_CLIENT_ID,
  clientSecret: env.GOOGLE_ANDROID_CLIENT_SECRET,
  redirectUri: 'com.madalang.bloblo:/oauth2redirect' // Android 리디렉션 (실제로는 사용 안 함)
});

// Rate Limiting (추후 Redis로 구현 예정)
const authAttempts = new Map(); // clientId -> { count, resetTime }

function checkRateLimit(clientId) {
  const now = Date.now();
  const attempt = authAttempts.get(clientId);

  if (!attempt || now > attempt.resetTime) {
    authAttempts.set(clientId, { count: 1, resetTime: now + 60000 }); // 1분
    return true;
  }

  if (attempt.count >= 5) {
    return false;
  }

  attempt.count++;
  return true;
}

/**
 * POST /auth/google-play-games
 *
 * Google Play Games 인증 코드를 받아서 JWT 토큰을 발급합니다.
 *
 * Request Body:
 * {
 *   "client_id": "unity-mobile-client",
 *   "auth_code": "4/0AY0e-g7..."  // Google Play Games에서 받은 서버 인증 코드
 * }
 *
 * Response:
 * {
 *   "access_token": "eyJhbGciOiJSUzI1NiIs...",
 *   "refresh_token": "...",
 *   "id_token": "...",
 *   "token_type": "Bearer",
 *   "expires_in": 3600
 * }
 */
router.post(
  '/google-play-games',
  [
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('auth_code').notEmpty().withMessage('auth_code is required')
  ],
  async (req, res) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      return res.status(400).json({
        error: 'invalid_request',
        error_description: 'Missing required parameters',
        details: errors.array()
      });
    }

    const { client_id, auth_code } = req.body;

    // Rate limiting 체크
    if (!checkRateLimit(client_id)) {
      logger.warn('Google Play Games auth rate limit exceeded', { client_id });
      return res.status(429).json({
        error: 'rate_limit_exceeded',
        error_description: 'Too many authentication attempts. Please try again later.'
      });
    }

    try {
      // Unity Editor 테스트 코드 감지
      const isEditorTestCode = auth_code.startsWith('editor_test_code_');

      let googleAccountId, email, emailVerified, displayName;

      if (isEditorTestCode) {
        // Unity Editor 테스트용 더미 데이터
        logger.info('Editor test auth code detected', { client_id, auth_code });

        googleAccountId = 'editor_test_' + Date.now();
        email = 'editor_test@example.com';
        emailVerified = true;
        displayName = 'Editor Test User';
      } else {
        // 1. Google API로 Auth Code 검증 및 토큰 교환
        logger.info('Exchanging Google Play Games auth code', { client_id });

        const { tokens } = await androidOAuthClient.getToken(auth_code);

        if (!tokens.id_token) {
          throw new Error('No ID token received from Google');
        }

        // 2. ID Token 검증 및 사용자 정보 추출
        const ticket = await androidOAuthClient.verifyIdToken({
          idToken: tokens.id_token,
          audience: env.GOOGLE_ANDROID_CLIENT_ID
        });

        const googleUser = ticket.getPayload();
        googleAccountId = googleUser.sub; // Google Account ID (OAuth와 동일)
        email = googleUser.email;
        emailVerified = googleUser.email_verified;
        displayName = googleUser.name || email.split('@')[0];
      }

      logger.info('Google Play Games auth code verified', {
        googleAccountId,
        email,
        emailVerified
      });

      // 3. Google Account ID 또는 이메일로 기존 계정 찾기
      let user = await findUserByGoogleAccountId(googleAccountId);

      // Google Account ID로 못 찾으면 이메일로 찾기 (Editor 테스트용)
      if (!user && isEditorTestCode) {
        user = await findUserByEmail(email);
        if (user) {
          logger.info('Found existing user by email (Editor test)', { userId: user.user_id, email });
        }
      }

      if (user) {
        // 기존 사용자: user_auth_providers에 Google Play Games 추가 (없으면)
        await linkGooglePlayGamesProvider(user.user_id, googleAccountId, {
          email,
          display_name: displayName,
          email_verified: emailVerified
        });

        logger.info('Existing user logged in via Google Play Games', {
          userId: user.user_id,
          googleAccountId
        });
      } else {
        // 신규 사용자: users 및 user_auth_providers 생성
        user = await createUserWithGooglePlayGames(googleAccountId, email, displayName, emailVerified);

        logger.info('New user created via Google Play Games', {
          userId: user.user_id,
          googleAccountId,
          email
        });
      }

      // 4. Device fingerprint 생성
      const deviceFingerprint = crypto.createHash('sha256')
        .update(`${client_id}-google-play-games-${googleAccountId}`)
        .digest('hex');

      // 5. Refresh Token Family 생성
      const tokenFamily = await dbService.createRefreshTokenFamily(
        user.user_id,
        client_id,
        deviceFingerprint
      );

      // 6. 토큰 생성 (direct-auth.js와 동일한 방식)
      const client = { client_id };
      const tokens = await generateTokens(
        user,
        client,
        'openid profile email',
        tokenFamily.family_id
      );

      logger.info('Google Play Games login successful - tokens issued', {
        userId: user.user_id,
        username: user.username,
        googleAccountId,
        client_id,
        familyId: tokenFamily.family_id
      });

      // 7. 응답 반환
      return res.status(200).json(tokens);

    } catch (error) {
      logger.error('Google Play Games authentication failed', {
        error: error.message,
        stack: error.stack,
        client_id
      });

      if (error.message.includes('invalid') || error.message.includes('expired')) {
        return res.status(401).json({
          error: 'invalid_grant',
          error_description: 'Invalid or expired authorization code'
        });
      }

      return res.status(500).json({
        error: 'server_error',
        error_description: 'Failed to process Google Play Games authentication'
      });
    }
  }
);

/**
 * Google Account ID로 사용자 찾기
 */
async function findUserByGoogleAccountId(googleAccountId) {
  const query = `
    SELECT u.*
    FROM users u
    INNER JOIN user_auth_providers uap ON u.user_id = uap.user_id
    WHERE uap.provider_user_id = $1
    LIMIT 1
  `;

  const result = await dbService.query(query, [googleAccountId]);
  return result.rows[0] || null;
}

/**
 * 이메일로 사용자 찾기
 */
async function findUserByEmail(email) {
  const query = `
    SELECT * FROM users
    WHERE email = $1
    LIMIT 1
  `;

  const result = await dbService.query(query, [email]);
  return result.rows[0] || null;
}

/**
 * Google Play Games provider 연동 (기존 사용자)
 */
async function linkGooglePlayGamesProvider(userId, googleAccountId, metadata) {
  const query = `
    INSERT INTO user_auth_providers (user_id, provider_type, provider_user_id, metadata)
    VALUES ($1, $2, $3, $4)
    ON CONFLICT (user_id, provider_type)
    DO UPDATE SET
      last_verified_at = NOW(),
      metadata = $4
  `;

  await dbService.query(query, [
    userId,
    'google_play_games',
    googleAccountId,
    JSON.stringify(metadata)
  ]);
}

/**
 * 신규 사용자 생성 (Google Play Games)
 */
async function createUserWithGooglePlayGames(googleAccountId, email, displayName, emailVerified) {
  const client = await dbService.pool.connect();

  try {
    await client.query('BEGIN');

    // 1. users 테이블에 사용자 생성
    // username 20자 제한: email 앞부분 + 랜덤 숫자
    const emailPrefix = email.split('@')[0].substring(0, 10); // 최대 10자
    const randomSuffix = Math.floor(Math.random() * 100000000); // 8자리 숫자
    const username = `${emailPrefix}_${randomSuffix}`.substring(0, 20); // 최대 20자

    const insertUserQuery = `
      INSERT INTO users (username, email, display_name, password_hash, oauth_provider, oauth_id, updated_at)
      VALUES ($1, $2, $3, $4, $5, $6, NOW())
      RETURNING user_id, username, email, display_name
    `;

    const userResult = await client.query(insertUserQuery, [
      username,
      email,
      displayName,
      '', // password_hash는 빈 문자열 (OAuth 전용 계정)
      'google_play_games', // 기존 oauth_provider 컬럼 활용
      googleAccountId // 기존 oauth_id 컬럼 활용
    ]);

    const user = userResult.rows[0];

    // 2. user_auth_providers 테이블에 Google Play Games provider 추가
    const insertProviderQuery = `
      INSERT INTO user_auth_providers (user_id, provider_type, provider_user_id, is_primary, metadata)
      VALUES ($1, $2, $3, $4, $5)
    `;

    await client.query(insertProviderQuery, [
      user.user_id,
      'google_play_games',
      googleAccountId,
      true,
      JSON.stringify({
        email,
        display_name: displayName,
        email_verified: emailVerified
      })
    ]);

    await client.query('COMMIT');

    return user;
  } catch (error) {
    await client.query('ROLLBACK');
    throw error;
  } finally {
    client.release();
  }
}

module.exports = router;