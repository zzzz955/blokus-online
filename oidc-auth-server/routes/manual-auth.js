const express = require('express')
const router = express.Router()
const { body, query, validationResult } = require('express-validator')
const crypto = require('crypto')
const argon2 = require('argon2')

const oidcConfig = require('../config/oidc')
const dbService = require('../config/database')
const logger = require('../config/logger')

/**
 * Manual Authentication Routes
 * 개발/테스트용 내부 인증 시스템
 * 프로덕션에서는 Google OAuth를 사용하되, 
 * 개발 환경에서는 테스트 계정 또는 수동 인증 지원
 */

/**
 * GET /manual-auth
 * 수동 로그인 페이지 (개발/테스트용)
 */
router.get('/',
  [
    query('client_id').notEmpty().withMessage('client_id is required'),
    query('redirect_uri').custom((value) => {
      if (value.startsWith('http://') || value.startsWith('https://') || value.startsWith('blokus://')) {
        return true;
      }
      throw new Error('Valid redirect_uri is required');
    }),
    query('scope').notEmpty().withMessage('scope is required'),
    query('state').optional().isLength({ max: 1024 }).withMessage('state is too long'),
    query('nonce').optional().isLength({ max: 1024 }).withMessage('nonce is too long'),
    query('code_challenge').optional().isLength({ min: 43, max: 128 }).withMessage('Invalid code_challenge'),
    query('code_challenge_method').optional().isIn(['S256']).withMessage('Only S256 code_challenge_method is supported')
  ],
  async (req, res) => {
    try {
      // 개발 환경에서만 접근 허용
      if (process.env.NODE_ENV === 'production') {
        return res.status(404).json({
          error: 'not_found',
          error_description: 'Manual authentication not available in production'
        })
      }

      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        logger.warn('Invalid manual auth request', {
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
        client_id,
        redirect_uri,
        scope,
        state,
        nonce,
        code_challenge,
        code_challenge_method
      } = req.query

      // 클라이언트 및 redirect_uri 검증
      const clientValidation = oidcConfig.validateClient(client_id)
      if (!clientValidation.valid) {
        return res.status(400).json({
          error: clientValidation.error,
          error_description: 'Invalid client'
        })
      }

      if (!oidcConfig.validateRedirectUri(client_id, redirect_uri)) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid redirect_uri'
        })
      }

      // 수동 로그인 페이지 렌더링
      const authParams = {
        client_id,
        redirect_uri,
        scope,
        state,
        nonce,
        code_challenge,
        code_challenge_method
      }

      const loginForm = generateManualLoginForm(authParams)
      res.set('Content-Type', 'text/html')
      res.send(loginForm)

    } catch (error) {
      logger.error('Manual auth endpoint error', {
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
 * POST /manual-auth
 * 수동 로그인 처리 (개발/테스트용)
 */
router.post('/',
  [
    body('username').trim().isLength({ min: 3, max: 50 }).withMessage('Username must be 3-50 characters'),
    body('password').isLength({ min: 4, max: 100 }).withMessage('Password must be 4-100 characters'),
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('redirect_uri').notEmpty().withMessage('redirect_uri is required'),
    body('scope').notEmpty().withMessage('scope is required'),
    body('state').optional(),
    body('nonce').optional(),
    body('code_challenge').optional(),
    body('code_challenge_method').optional()
  ],
  async (req, res) => {
    try {
      // 개발 환경에서만 접근 허용
      if (process.env.NODE_ENV === 'production') {
        return res.status(404).json({
          error: 'not_found',
          error_description: 'Manual authentication not available in production'
        })
      }

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

      logger.info('Manual login attempt', {
        username,
        client_id,
        ip: req.ip
      })

      // 테스트 계정 (개발 환경)
      const testAccounts = {
        'testuser': 'testpass123',
        'zzzz955': 'fostj137sw!@',
        'admin': 'admin123'
      }

      let user = null
      
      // 테스트 계정 체크
      if (testAccounts[username] && password === testAccounts[username]) {
        user = {
          user_id: username === 'zzzz955' ? 1 : 999,
          username: username,
          email: `${username}@example.com`,
          display_name: username,
          level: 1,
          experience_points: 0,
          single_player_level: 1,
          max_stage_completed: 0
        }
        logger.info('Test account login successful', { username, ip: req.ip })
      } else {
        // 데이터베이스 조회
        try {
          const dbUser = await dbService.getUserByUsername(username)
          if (dbUser && dbUser.password_hash) {
            const isValidPassword = await argon2.verify(dbUser.password_hash, password)
            if (isValidPassword) {
              user = dbUser
            }
          }
        } catch (dbError) {
          logger.error('Database authentication error', { 
            error: dbError.message, 
            username, 
            ip: req.ip 
          })
        }
      }

      if (!user) {
        logger.warn('Manual login failed', { username, ip: req.ip })
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

      logger.info('Manual authorization code issued', {
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

      logger.info('Redirecting to client with authorization code', {
        redirect_uri,
        redirectUrl: redirectUrl.toString(),
        client_id,
        hasCode: !!authCode,
        hasState: !!state
      })

      res.redirect(redirectUrl.toString())

    } catch (error) {
      logger.error('Manual login processing error', {
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
 * 수동 로그인 HTML 폼 생성
 */
function generateManualLoginForm(authParams) {
  const paramsJson = JSON.stringify(authParams)
  
  return `
<!DOCTYPE html>
<html lang="ko">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Blokus Online - 개발자 로그인</title>
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
        .dev-warning {
            background: #fff3cd;
            border: 1px solid #ffeaa7;
            color: #856404;
            padding: 10px;
            border-radius: 6px;
            margin-bottom: 20px;
            font-size: 14px;
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
            margin-bottom: 10px;
        }
        .login-btn:hover {
            transform: translateY(-1px);
        }
        .google-btn {
            width: 100%;
            padding: 12px;
            background: #4285f4;
            color: white;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            text-decoration: none;
            display: block;
            text-align: center;
            transition: background-color 0.2s;
        }
        .google-btn:hover {
            background: #3367d6;
            text-decoration: none;
            color: white;
        }
        .client-info {
            background: #f7fafc;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
            font-size: 14px;
            color: #4a5568;
        }
        .test-accounts {
            background: #e6fffa;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
            font-size: 13px;
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
        <div class="dev-warning">
            ⚠️ 개발자 전용 로그인 페이지입니다. 프로덕션에서는 Google OAuth를 사용합니다.
        </div>
        
        <div class="logo">
            <h1>🎮 Blokus Online</h1>
            <p>개발자 인증</p>
        </div>
        
        <div class="client-info">
            <strong>클라이언트:</strong> ${authParams.client_id}<br>
            <strong>요청 권한:</strong> ${authParams.scope}
        </div>

        <a href="/authorize?${new URLSearchParams(authParams).toString()}" class="google-btn">
            🚀 Google OAuth로 로그인 (권장)
        </a>

        <div style="text-align: center; margin: 20px 0; color: #999;">또는</div>

        <div class="test-accounts">
            <strong>테스트 계정:</strong><br>
            • testuser / testpass123<br>
            • zzzz955 / fostj137sw!@<br>
            • admin / admin123
        </div>

        <form method="POST" action="/manual-auth">
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
            
            <button type="submit" class="login-btn">수동 로그인</button>
            
            <div id="error" class="error" style="display: none;"></div>
        </form>
    </div>

    <script>
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