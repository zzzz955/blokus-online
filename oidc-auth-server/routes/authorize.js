const express = require('express')
const router = express.Router()
const { body, query, validationResult } = require('express-validator')
const crypto = require('crypto')
const { v4: uuidv4 } = require('uuid')
const argon2 = require('argon2')

const oidcConfig = require('../config/oidc')
const dbService = require('../config/database')
const logger = require('../config/logger')
const { env } = require('../config/env')

/**
 * GET /authorize
 * OAuth 2.1/OIDC Authorization 엔드포인트
 * Authorization Code Flow 시작점
 */
router.get('/',
  [
    query('response_type').equals('code').withMessage('Only code response type is supported'),
    query('client_id').notEmpty().withMessage('client_id is required'),
    query('redirect_uri').custom((value) => {
      // HTTP/HTTPS URLs or custom schemes (blokus://)
      if (value.startsWith('http://') || value.startsWith('https://') || value.startsWith('blokus://')) {
        return true;
      }
      throw new Error('Valid redirect_uri is required (http, https, or blokus scheme)');
    }),
    query('scope').notEmpty().withMessage('scope is required'),
    query('state').optional().isLength({ max: 1024 }).withMessage('state is too long'),
    query('nonce').optional().isLength({ max: 1024 }).withMessage('nonce is too long'),
    query('code_challenge').optional().isLength({ min: 43, max: 128 }).withMessage('Invalid code_challenge'),
    query('code_challenge_method').optional().isIn(['S256']).withMessage('Only S256 code_challenge_method is supported')
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        logger.warn('Invalid authorization request', {
          errors: errors.array(),
          ip: req.ip,
          query: req.query
        })

        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid request parameters',
          details: errors.array()
        })
      }

      const {
        response_type,
        client_id,
        redirect_uri,
        scope,
        state,
        nonce,
        code_challenge,
        code_challenge_method
      } = req.query

      logger.info('Authorization request received', {
        client_id,
        redirect_uri,
        scope,
        has_pkce: !!code_challenge,
        ip: req.ip
      })

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id)
      if (!clientValidation.valid) {
        return res.status(400).json({
          error: clientValidation.error,
          error_description: 'Invalid client'
        })
      }

      const client = clientValidation.client

      // Redirect URI 검증
      if (!oidcConfig.validateRedirectUri(client_id, redirect_uri)) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid redirect_uri'
        })
      }

      // PKCE 검증 (public client는 필수)
      if (client.require_pkce && (!code_challenge || !code_challenge_method)) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'PKCE is required for this client'
        })
      }

      // Scope 검증
      const scopeValidation = oidcConfig.validateScope(scope)
      if (!scopeValidation.valid) {
        return res.status(400).json({
          error: scopeValidation.error,
          error_description: scopeValidation.description
        })
      }

      // Accept 헤더 확인 - API 요청인지 브라우저 요청인지 구분
      const acceptsJson = req.headers.accept && req.headers.accept.includes('application/json')
      const isApiRequest = acceptsJson && !req.headers.accept.includes('text/html')

      if (isApiRequest) {
        // API 요청 - JSON으로 파라미터 반환 (클라이언트에서 직접 로그인 처리)
        return res.json({
          message: 'Authorization parameters validated',
          authorization_params: {
            response_type,
            client_id,
            redirect_uri,
            scope,
            state,
            nonce,
            code_challenge,
            code_challenge_method
          },
          endpoints: {
            direct_login: '/api/auth/login',
            google_oauth: `/auth/google?${new URLSearchParams(req.query).toString()}`
          }
        })
      }

      // 브라우저 요청 - HTML 로그인 페이지 렌더링
      const authParams = {
        response_type,
        client_id,
        redirect_uri,
        scope,
        state,
        nonce,
        code_challenge,
        code_challenge_method
      }

      // Google OAuth 클라이언트 설정 확인
      const hasGoogleOAuth = env.GOOGLE_CLIENT_ID && 
                            env.GOOGLE_CLIENT_SECRET &&
                            env.GOOGLE_CLIENT_ID !== 'your-google-client-id.apps.googleusercontent.com' &&
                            env.GOOGLE_CLIENT_SECRET !== 'your-google-client-secret'

      // 로그인 선택 페이지 생성
      const loginSelectionForm = generateLoginSelectionForm(authParams, hasGoogleOAuth)
      res.set('Content-Type', 'text/html')
      res.send(loginSelectionForm)

    } catch (error) {
      logger.error('Authorization endpoint error', {
        error: error.message,
        stack: error.stack,
        ip: req.ip
      })

      res.status(500).json({
        error: 'server_error',
        error_description: 'Internal server error'
      })
    }
  }
)

/**
 * POST /authorize
 * 사용자 로그인 처리 및 Authorization Code 발급
 */
router.post('/',
  [
    body('username').trim().isLength({ min: 3, max: 50 }).withMessage('Username must be 3-50 characters'),
    body('password').isLength({ min: 4, max: 100 }).withMessage('Password must be 4-100 characters'),
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('redirect_uri').isURL().withMessage('Valid redirect_uri is required'),
    body('scope').notEmpty().withMessage('scope is required'),
    body('state').optional(),
    body('nonce').optional(),
    body('code_challenge').optional(),
    body('code_challenge_method').optional()
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid request parameters'
        })
      }

      const {
        username,
        password,
        client_id,
        redirect_uri,
        scope,
        state,
        nonce,
        code_challenge,
        code_challenge_method
      } = req.body

      logger.info('Login attempt', {
        username,
        client_id,
        ip: req.ip
      })

      // 테스트 계정 (개발 환경에서만)
      const testAccounts = {
        'testuser': 'testpass123',
        'zzzz955': 'fostj137sw!@',
        'admin': 'admin123'
      }

      let user = null
      
      // 개발 환경에서 테스트 계정 체크
      if (env.NODE_ENV !== 'production' && testAccounts[username]) {
        if (password === testAccounts[username]) {
          // 모의 사용자 객체 생성
          user = {
            user_id: username === 'zzzz955' ? 1 : 999,
            username: username,
            email: `${username}@example.com`,
            display_name: username,
            password_hash: 'test-hash', // 사용되지 않음
            level: 1,
            experience_points: 0,
            single_player_level: 1,
            max_stage_completed: 0
          }
          logger.info('Test account login successful', { username, ip: req.ip })
        }
      }

      // 프로덕션이거나 테스트 계정이 아닌 경우 데이터베이스 조회
      if (!user) {
        try {
          const dbUser = await dbService.getUserByUsername(username)
          if (!dbUser) {
            logger.warn('Login failed - user not found', { username, ip: req.ip })
            return res.status(401).json({
              error: 'invalid_credentials',
              error_description: 'Invalid username or password'
            })
          }

          const isValidPassword = await argon2.verify(dbUser.password_hash, password)
          if (!isValidPassword) {
            logger.warn('Login failed - invalid password', { username, ip: req.ip })
            return res.status(401).json({
              error: 'invalid_credentials',
              error_description: 'Invalid username or password'
            })
          }
          
          user = dbUser
        } catch (dbError) {
          logger.error('Database authentication error', { 
            error: dbError.message, 
            username, 
            ip: req.ip 
          })
          
          // 데이터베이스 오류 시에도 테스트 계정 허용 (개발 환경)
          if (env.NODE_ENV !== 'production' && testAccounts[username] && password === testAccounts[username]) {
            user = {
              user_id: username === 'zzzz955' ? 1 : 999,
              username: username,
              email: `${username}@example.com`,
              display_name: username,
              password_hash: 'test-hash',
              level: 1,
              experience_points: 0,
              single_player_level: 1,
              max_stage_completed: 0
            }
            logger.info('Fallback to test account due to DB error', { username, ip: req.ip })
          } else {
            return res.status(500).json({
              error: 'server_error',
              error_description: 'Authentication service temporarily unavailable'
            })
          }
        }
      }

      if (!user) {
        logger.warn('Login failed - authentication failed', { username, ip: req.ip })
        return res.status(401).json({
          error: 'invalid_credentials', 
          error_description: 'Invalid username or password'
        })
      }

      // Authorization Code 생성
      const authCode = crypto.randomBytes(32).toString('base64url')
      const expiresAt = new Date(Date.now() + oidcConfig.getTokenLifetimeInSeconds('authorizationCode') * 1000)

      // Authorization Code 저장
      await dbService.storeAuthorizationCode(
        authCode,
        client_id,
        user.user_id,
        redirect_uri,
        scope,
        code_challenge,
        code_challenge_method,
        expiresAt
      )

      logger.info('Authorization code issued', {
        username,
        client_id,
        userId: user.user_id,
        ip: req.ip
      })

      // Redirect URI에 Authorization Code 전달
      const redirectUrl = new URL(redirect_uri)
      redirectUrl.searchParams.append('code', authCode)
      if (state) {
        redirectUrl.searchParams.append('state', state)
      }

      res.redirect(redirectUrl.toString())

    } catch (error) {
      logger.error('Login processing error', {
        error: error.message,
        stack: error.stack,
        username: req.body?.username,
        ip: req.ip
      })

      res.status(500).json({
        error: 'server_error',
        error_description: 'Internal server error during authentication'
      })
    }
  }
)

/**
 * 로그인 선택 페이지 생성
 */
function generateLoginSelectionForm(authParams, hasGoogleOAuth) {
  const paramsQuery = new URLSearchParams(authParams).toString()
  
  return `
<!DOCTYPE html>
<html lang="ko">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Blokus Online - 로그인</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            margin: 0;
            padding: 20px;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .login-container {
            background: white;
            padding: 40px;
            border-radius: 12px;
            box-shadow: 0 8px 32px rgba(0,0,0,0.1);
            width: 100%;
            max-width: 400px;
            text-align: center;
        }
        .logo {
            color: #4a5568;
            margin-bottom: 30px;
        }
        .login-option {
            width: 100%;
            padding: 15px;
            margin: 10px 0;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            text-decoration: none;
            display: block;
            transition: transform 0.2s;
        }
        .login-option:hover {
            transform: translateY(-2px);
        }
        .google-login {
            background: #4285f4;
            color: white;
        }
        .google-login:hover {
            background: #3367d6;
            color: white;
            text-decoration: none;
        }
        .manual-login {
            background: #e2e8f0;
            color: #4a5568;
            border: 2px solid #cbd5e0;
        }
        .manual-login:hover {
            background: #cbd5e0;
            color: #4a5568;
            text-decoration: none;
        }
        .client-info {
            background: #f7fafc;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 30px;
            font-size: 14px;
            color: #4a5568;
        }
        .divider {
            margin: 20px 0;
            color: #999;
            position: relative;
        }
        .divider::before {
            content: '';
            position: absolute;
            top: 50%;
            left: 0;
            right: 0;
            height: 1px;
            background: #e2e8f0;
        }
        .divider span {
            background: white;
            padding: 0 15px;
        }
        .oauth-status {
            font-size: 13px;
            padding: 10px;
            border-radius: 6px;
            margin-bottom: 20px;
        }
        .oauth-enabled {
            background: #f0fff4;
            color: #22543d;
            border: 1px solid #c6f6d5;
        }
        .oauth-disabled {
            background: #fffaf0;
            color: #744210;
            border: 1px solid #fbb6ce;
        }
        .disabled {
            opacity: 0.6;
            cursor: not-allowed;
        }
    </style>
</head>
<body>
    <div class="login-container">
        <div class="logo">
            <h1>🎮 Blokus Online</h1>
            <p>로그인 방법을 선택하세요</p>
        </div>
        
        <div class="client-info">
            <strong>앱:</strong> ${authParams.client_id}<br>
            <strong>요청 권한:</strong> ${authParams.scope}
        </div>

        <div class="oauth-status ${hasGoogleOAuth ? 'oauth-enabled' : 'oauth-disabled'}">
            ${hasGoogleOAuth ? '✅ Google OAuth 사용 가능' : '⚠️ Google OAuth 미설정 - 개발 모드'}
        </div>
        
        <a href="/auth/google?${paramsQuery}" class="login-option google-login ${hasGoogleOAuth ? '' : 'disabled'}" 
           ${hasGoogleOAuth ? '' : 'onclick="alert(\'Google OAuth 클라이언트가 설정되지 않았습니다.\'); return false;"'}>
            🚀 Google 계정으로 로그인 ${hasGoogleOAuth ? '' : '(미설정)'}
        </a>
        
        <div class="divider">
            <span>${hasGoogleOAuth ? '또는' : '개발용'}</span>
        </div>
        
        <a href="/manual-auth?${paramsQuery}" class="login-option manual-login">
            📝 ${hasGoogleOAuth ? '아이디/비밀번호로 로그인' : '테스트 계정으로 로그인'}
        </a>
        
        <p style="font-size: 12px; color: #666; margin-top: 30px;">
            ${hasGoogleOAuth ? 
                'Google 로그인을 권장합니다. 안전하고 편리합니다.' : 
                '개발 환경입니다. Google OAuth 설정은 GOOGLE_OAUTH_SETUP.md를 참고하세요.'
            }
        </p>
    </div>
</body>
</html>
  `
}

/**
 * 간단한 HTML 로그인 폼 생성
 */
function generateLoginForm(authParams) {
  const paramsJson = JSON.stringify(authParams)
  
  return `
<!DOCTYPE html>
<html lang="ko">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Blokus Online - 로그인</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            margin: 0;
            padding: 20px;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .login-container {
            background: white;
            padding: 40px;
            border-radius: 12px;
            box-shadow: 0 8px 32px rgba(0,0,0,0.1);
            width: 100%;
            max-width: 400px;
        }
        .logo {
            text-align: center;
            color: #4a5568;
            margin-bottom: 30px;
        }
        .form-group {
            margin-bottom: 20px;
        }
        label {
            display: block;
            margin-bottom: 5px;
            color: #4a5568;
            font-weight: 500;
        }
        input[type="text"], input[type="password"] {
            width: 100%;
            padding: 12px;
            border: 2px solid #e2e8f0;
            border-radius: 6px;
            font-size: 16px;
            transition: border-color 0.3s;
            box-sizing: border-box;
        }
        input[type="text"]:focus, input[type="password"]:focus {
            outline: none;
            border-color: #667eea;
        }
        .login-btn {
            width: 100%;
            padding: 12px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            transition: transform 0.2s;
        }
        .login-btn:hover {
            transform: translateY(-1px);
        }
        .client-info {
            background: #f7fafc;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
            font-size: 14px;
            color: #4a5568;
        }
        .scope-list {
            margin-top: 10px;
        }
        .scope-item {
            display: inline-block;
            background: #e2e8f0;
            padding: 4px 8px;
            margin: 2px;
            border-radius: 4px;
            font-size: 12px;
        }
        .error {
            color: #e53e3e;
            font-size: 14px;
            margin-top: 10px;
        }
    </style>
</head>
<body>
    <div class="login-container">
        <div class="logo">
            <h1>🎮 Blokus Online</h1>
            <p>OAuth 2.1 인증</p>
        </div>
        
        <div class="client-info">
            <strong>앱 권한 요청:</strong> ${authParams.client_id}<br>
            <strong>요청 권한:</strong>
            <div class="scope-list">
                ${authParams.scope.split(' ').map(scope => `<span class="scope-item">${scope}</span>`).join('')}
            </div>
        </div>

        <form method="POST" action="/authorize">
            ${Object.entries(authParams).map(([key, value]) => 
                value ? `<input type="hidden" name="${key}" value="${value}">` : ''
            ).join('')}
            
            <div class="form-group">
                <label for="username">사용자명</label>
                <input type="text" id="username" name="username" required>
            </div>
            
            <div class="form-group">
                <label for="password">비밀번호</label>
                <input type="password" id="password" name="password" required>
            </div>
            
            <button type="submit" class="login-btn">로그인 및 인증</button>
            
            <div id="error" class="error" style="display: none;"></div>
        </form>
    </div>

    <script>
        // 간단한 클라이언트 사이드 검증
        document.querySelector('form').addEventListener('submit', function(e) {
            const username = document.getElementById('username').value.trim();
            const password = document.getElementById('password').value;
            const errorDiv = document.getElementById('error');
            
            if (!username || username.length < 3) {
                e.preventDefault();
                errorDiv.textContent = '사용자명은 3자 이상이어야 합니다.';
                errorDiv.style.display = 'block';
                return;
            }
            
            if (!password || password.length < 4) {
                e.preventDefault();
                errorDiv.textContent = '비밀번호는 4자 이상이어야 합니다.';
                errorDiv.style.display = 'block';
                return;
            }
            
            errorDiv.style.display = 'none';
        });
    </script>
</body>
</html>
  `
}

module.exports = router