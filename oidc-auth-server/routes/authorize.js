const express = require('express')
const router = express.Router()
const { body, query, validationResult } = require('express-validator')
const crypto = require('crypto')
const { v4: uuidv4 } = require('uuid')
const argon2 = require('argon2')

const oidcConfig = require('../config/oidc')
const dbService = require('../config/database')
const logger = require('../config/logger')

/**
 * GET /authorize
 * OAuth 2.1/OIDC Authorization 엔드포인트
 * Authorization Code Flow 시작점
 */
router.get('/',
  [
    query('response_type').equals('code').withMessage('Only code response type is supported'),
    query('client_id').notEmpty().withMessage('client_id is required'),
    query('redirect_uri').isURL().withMessage('Valid redirect_uri is required'),
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

      // 로그인 페이지 렌더링
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

      // 간단한 HTML 로그인 폼 반환
      const loginForm = generateLoginForm(authParams)
      res.set('Content-Type', 'text/html')
      res.send(loginForm)

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

      // 사용자 인증
      const user = await dbService.getUserByUsername(username)
      if (!user) {
        logger.warn('Login failed - user not found', { username, ip: req.ip })
        return res.status(401).json({
          error: 'invalid_credentials',
          error_description: 'Invalid username or password'
        })
      }

      const isValidPassword = await argon2.verify(user.password_hash, password)
      if (!isValidPassword) {
        logger.warn('Login failed - invalid password', { username, ip: req.ip })
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