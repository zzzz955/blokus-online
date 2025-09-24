const express = require('express')
const router = express.Router()
const logger = require('../config/logger')

/**
 * Unity Editor Callback Handler
 * 기존 Google OAuth API를 활용하면서 Unity Editor를 위한 최소한의 콜백 처리
 */

// Unity Editor용 임시 저장소 (메모리 기반)
const editorCallbacks = new Map()

// 콜백 정리 (10분 후 자동 삭제)
const CALLBACK_TTL = 10 * 60 * 1000 // 10 minutes

/**
 * GET /unity-editor-callback
 * Google OAuth 콜백 또는 Unity Editor 폴링 처리
 */
router.get('/', (req, res) => {
  const { code, state, error, error_description, check } = req.query

  try {
    // Unity Editor가 결과를 확인하러 온 경우 (폴링)
    if (check && state) {
      const callback = editorCallbacks.get(state)
      
      if (!callback) {
        return res.send(`
          <html>
            <head><title>Unity Editor OAuth</title></head>
            <body>
              <h3>⏳ Waiting for OAuth...</h3>
              <p>Google 인증을 완료해주세요.</p>
              <!-- status: pending -->
            </body>
          </html>
        `)
      }

      if (callback.error) {
        return res.send(`
          <html>
            <head><title>Unity Editor OAuth - Error</title></head>
            <body>
              <h3> OAuth Error</h3>
              <p>error: ${callback.error}</p>
              <p>Description: ${callback.error_description || 'Unknown error'}</p>
            </body>
          </html>
        `)
      }

      if (callback.code) {
        // 한 번 사용된 콜백은 삭제
        editorCallbacks.delete(state)
        
        return res.send(`
          <html>
            <head><title>Unity Editor OAuth - Success</title></head>
            <body>
              <h3> OAuth Success</h3>
              <p>authorization_code: ${callback.code}</p>
              <p>Unity에서 토큰을 교환합니다...</p>
            </body>
          </html>
        `)
      }

      // 아직 결과가 없음
      return res.send(`
        <html>
          <head><title>Unity Editor OAuth</title></head>
          <body>
            <h3>⏳ Waiting for OAuth...</h3>
            <p>Google 인증을 완료해주세요.</p>
            <!-- status: pending -->
          </body>
        </html>
      `)
    }

    // Google OAuth에서 리다이렉트된 경우 (콜백 저장)
    if (state) {
      if (error) {
        // 에러를 저장
        editorCallbacks.set(state, {
          error,
          error_description,
          timestamp: new Date()
        })

        logger.warn('Unity Editor OAuth error', {
          state,
          error,
          error_description,
          ip: req.ip
        })

        return res.send(`
          <html>
            <head><title>Unity Editor OAuth - Error</title></head>
            <body style="font-family: Arial; text-align: center; padding: 50px; background: #ffebee; color: #c62828;">
              <h1> OAuth Error</h1>
              <p>에러: ${error}</p>
              <p>설명: ${error_description || 'Unknown error'}</p>
              <div style="margin: 20px; padding: 15px; background: rgba(255,255,255,0.3); border-radius: 10px;">
                <small>Unity Editor로 돌아가서 다시 시도해주세요.</small>
              </div>
            </body>
          </html>
        `)
      }

      if (code) {
        // Authorization code를 저장
        editorCallbacks.set(state, {
          code,
          timestamp: new Date()
        })

        // 자동 정리 타이머 설정
        setTimeout(() => {
          editorCallbacks.delete(state)
        }, CALLBACK_TTL)

        logger.info('Unity Editor OAuth success', {
          state,
          ip: req.ip
        })

        return res.send(`
          <html>
            <head><title>Unity Editor OAuth - Success</title></head>
            <body style="font-family: Arial; text-align: center; padding: 50px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;">
              <h1> Google Login Success!</h1>
              <p>인증이 완료되었습니다. Unity Editor로 돌아가주세요.</p>
              <div style="margin: 20px; padding: 15px; background: rgba(255,255,255,0.1); border-radius: 10px;">
                <small>이 창을 닫고 Unity로 돌아가세요.</small>
              </div>
              <script>
                // 자동으로 창 닫기 시도
                setTimeout(() => {
                  window.close();
                }, 2000);
              </script>
            </body>
          </html>
        `)
      }
    }

    // 파라미터가 부족한 경우
    return res.status(400).send(`
      <html>
        <head><title>Unity Editor OAuth - Invalid Request</title></head>
        <body style="font-family: Arial; text-align: center; padding: 50px;">
          <h1>⚠️ Invalid Request</h1>
          <p>필요한 파라미터가 없습니다.</p>
        </body>
      </html>
    `)

  } catch (error) {
    logger.error('Unity Editor callback error', {
      error: error.message,
      stack: error.stack,
      ip: req.ip
    })

    res.status(500).send(`
      <html>
        <head><title>Unity Editor OAuth - Server Error</title></head>
        <body style="font-family: Arial; text-align: center; padding: 50px;">
          <h1> Server Error</h1>
          <p>서버에서 오류가 발생했습니다.</p>
        </body>
      </html>
    `)
  }
})

/**
 * GET /unity-editor-callback/status
 * Unity Editor 콜백 상태 확인
 */
router.get('/status', (req, res) => {
  res.json({
    status: 'ok',
    activeCallbacks: editorCallbacks.size,
    timestamp: new Date().toISOString()
  })
})

module.exports = router