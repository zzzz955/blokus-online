const passport = require('passport')
const GoogleStrategy = require('passport-google-oauth20').Strategy
const dbService = require('./database')
const logger = require('./logger')

/**
 * Google OAuth 2.0 Strategy Configuration
 * OIDC 서버가 Google OAuth를 통해 사용자를 인증하고 사용자 정보를 가져온다
 */

// Passport 직렬화/역직렬화 - 세션 관리
passport.serializeUser((user, done) => {
  logger.debug('Serializing user', { userId: user.user_id, username: user.username })
  done(null, user.user_id)
})

passport.deserializeUser(async (userId, done) => {
  try {
    logger.debug('Deserializing user', { userId })
    const user = await dbService.getUserById(userId)
    if (!user) {
      logger.warn('User not found during deserialization', { userId })
      return done(null, false)
    }
    done(null, user)
  } catch (error) {
    logger.error('Error deserializing user', { 
      error: error.message,
      userId 
    })
    done(error, null)
  }
})

// Google OAuth 2.0 Strategy 설정
passport.use(new GoogleStrategy({
  clientID: process.env.GOOGLE_CLIENT_ID,
  clientSecret: process.env.GOOGLE_CLIENT_SECRET,
  callbackURL: "/auth/google/callback",
  scope: ['profile', 'email']
}, async (accessToken, refreshToken, profile, done) => {
  try {
    logger.info('Google OAuth callback received', {
      oauthId: profile.id,
      email: profile.emails?.[0]?.value,
      name: profile.displayName,
      provider: 'google'
    })

    const email = profile.emails?.[0]?.value
    if (!email) {
      logger.warn('No email provided by Google OAuth', { oauthId: profile.id, provider: 'google' })
      return done(new Error('Google 계정에서 이메일 정보를 찾을 수 없습니다.'), null)
    }

    // 기존 사용자 확인 (이메일 기준)
    let user = await dbService.getUserByEmail(email)
    
    if (user) {
      // 기존 사용자 - OAuth ID 업데이트 (없는 경우만)
      if (!user.oauth_id) {
        await dbService.updateUserOAuthId(user.user_id, profile.id, 'google')
        user.oauth_id = profile.id
        user.oauth_provider = 'google'
        logger.info('Updated existing user with OAuth ID', { 
          userId: user.user_id, 
          email: email,
          oauthId: profile.id,
          provider: 'google'
        })
      }
    } else {
      // 새 사용자 생성
      const userData = {
        username: await generateUniqueUsername(profile.displayName || email.split('@')[0]),
        email: email,
        display_name: profile.displayName || email.split('@')[0],
        oauth_id: profile.id,
        oauth_provider: 'google',
        // OAuth 사용자는 password_hash 없음 (null)
        password_hash: null
      }

      user = await dbService.createOAuthUser(userData)
      logger.info('Created new OAuth user', {
        userId: user.user_id,
        username: user.username,
        email: email,
        oauthId: profile.id,
        provider: 'google'
      })
    }

    return done(null, user)

  } catch (error) {
    logger.error('Error in Google OAuth strategy', {
      error: error.message,
      stack: error.stack,
      oauthId: profile.id,
      provider: 'google'
    })
    return done(error, null)
  }
}))

/**
 * 고유한 사용자명 생성 (중복 시 숫자 추가)
 */
async function generateUniqueUsername(baseUsername) {
  // 특수문자 제거 및 소문자 변환
  let username = baseUsername.toLowerCase()
    .replace(/[^a-z0-9]/g, '')
    .substring(0, 20)
  
  if (username.length < 3) {
    username = 'user' + Math.floor(Math.random() * 10000)
  }

  let counter = 0
  let finalUsername = username

  while (await dbService.getUserByUsername(finalUsername)) {
    counter++
    finalUsername = `${username}${counter}`
    
    if (counter > 1000) {
      // 무한루프 방지
      finalUsername = `user${Date.now()}`
      break
    }
  }

  return finalUsername
}

module.exports = passport