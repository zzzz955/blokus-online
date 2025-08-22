#!/usr/bin/env node

/**
 * OIDC 테스트 사용자 생성 스크립트
 * Usage: node scripts/create-test-user.js
 */

require('dotenv').config()
const argon2 = require('argon2')
const { Client } = require('pg')

async function createTestUser() {
  const client = new Client({
    host: process.env.DB_HOST || 'localhost',
    port: process.env.DB_PORT || 5432,
    database: process.env.DB_NAME || 'blokus_game',
    user: process.env.DB_USER || 'blokus',
    password: process.env.DB_PASSWORD || 'blokus123'
  })

  try {
    await client.connect()
    console.log('Database connected successfully')

    // 테스트 사용자 정보
    const testUsers = [
      {
        username: 'testuser',
        password: 'testpass123',
        email: 'test@example.com',
        display_name: 'Test User'
      },
      {
        username: 'zzzz955',
        password: 'fostj137sw!@',
        email: 'zzzz955@example.com', 
        display_name: 'zzzz955'
      }
    ]

    for (const userData of testUsers) {
      // 비밀번호 해싱
      const hashedPassword = await argon2.hash(userData.password)

      // 사용자 존재 확인
      const existingUser = await client.query(
        'SELECT user_id FROM users WHERE username = $1',
        [userData.username]
      )

      if (existingUser.rows.length > 0) {
        console.log(`User ${userData.username} already exists, updating password...`)
        
        // 비밀번호 업데이트
        await client.query(
          `UPDATE users SET 
           password_hash = $1,
           email = $2,
           display_name = $3,
           updated_at = CURRENT_TIMESTAMP
           WHERE username = $4`,
          [hashedPassword, userData.email, userData.display_name, userData.username]
        )
        
        console.log(`✅ User ${userData.username} password updated`)
      } else {
        // 새 사용자 생성
        const result = await client.query(
          `INSERT INTO users (
            username, 
            password_hash, 
            email, 
            display_name,
            level,
            experience_points,
            single_player_level,
            max_stage_completed,
            created_at,
            updated_at
          ) VALUES ($1, $2, $3, $4, 1, 0, 1, 0, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
          RETURNING user_id`,
          [userData.username, hashedPassword, userData.email, userData.display_name]
        )

        const userId = result.rows[0].user_id

        // 기본 사용자 통계 생성
        await client.query(
          `INSERT INTO user_stats (
            user_id, 
            total_games, 
            wins, 
            losses, 
            total_score, 
            best_score
          ) VALUES ($1, 0, 0, 0, 0, 0)`,
          [userId]
        )

        console.log(`✅ User ${userData.username} created with ID: ${userId}`)
      }

      console.log(`   Username: ${userData.username}`)
      console.log(`   Password: ${userData.password}`)
      console.log(`   Email: ${userData.email}`)
      console.log('')
    }

    console.log('🎉 Test users ready for OIDC authentication!')
    console.log('\n📝 Test Login Instructions:')
    console.log('1. Unity에서 로그인 시도')
    console.log('2. 브라우저가 열리면 위의 계정으로 로그인')
    console.log('3. Unity 앱으로 자동 복귀')

  } catch (error) {
    console.error('❌ Error creating test user:', error.message)
    console.error('Stack:', error.stack)
  } finally {
    await client.end()
  }
}

// 스크립트 실행
if (require.main === module) {
  createTestUser()
    .then(() => {
      console.log('\n✅ Script completed successfully')
      process.exit(0)
    })
    .catch(error => {
      console.error('❌ Script failed:', error.message)
      process.exit(1)
    })
}

module.exports = { createTestUser }