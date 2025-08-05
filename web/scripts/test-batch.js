// ========================================
// 배치 처리 테스트 스크립트
// ========================================
// 게임 통계 배치 처리 시스템을 테스트하는 독립 실행 스크립트
// 
// 실행: node scripts/test-batch.js
// ========================================

const { PrismaClient } = require('@prisma/client');

const prisma = new PrismaClient();

// 테스트용 사용자 데이터 생성
async function createTestUsers() {
  console.log('테스트 사용자 데이터 생성 중...');
  
  const testUsers = [
    { username: 'player1', password_hash: '$argon2id$v=19$m=65536,t=2,p=1$test$hash1', email: 'player1@test.com' },
    { username: 'player2', password_hash: '$argon2id$v=19$m=65536,t=2,p=1$test$hash2', email: 'player2@test.com' },
    { username: 'player3', password_hash: '$argon2id$v=19$m=65536,t=2,p=1$test$hash3', email: 'player3@test.com' },
    { username: 'proPlayer', password_hash: '$argon2id$v=19$m=65536,t=2,p=1$test$hash4', email: 'pro@test.com' },
    { username: 'casual_gamer', password_hash: '$argon2id$v=19$m=65536,t=2,p=1$test$hash5', email: 'casual@test.com' }
  ];

  for (const userData of testUsers) {
    try {
      const user = await prisma.user.upsert({
        where: { username: userData.username },
        update: {},
        create: {
          username: userData.username,
          password_hash: userData.password_hash,
          email: userData.email,
          display_name: userData.username,
          is_active: true,
          created_at: new Date(),
          last_login_at: new Date()
        }
      });

      console.log(`✅ 사용자 생성/확인: ${user.username} (ID: ${user.user_id})`);
    } catch (error) {
      console.error(`❌ 사용자 생성 실패 (${userData.username}):`, error.message);
    }
  }
}

// 배치 처리 테스트
async function testBatchProcessing() {
  console.log('\n=== 배치 처리 테스트 시작 ===');
  
  try {
    // 배치 처리 시뮬레이션을 위한 간단한 구현
    const users = await prisma.user.findMany({
      select: { user_id: true, username: true }
    });

    let processed = 0;
    let created = 0;
    let updated = 0;

    for (const user of users) {
      // Mock 게임 통계 데이터 생성
      const mockStats = {
        total_games: Math.floor(Math.random() * 50) + 1,
        wins: Math.floor(Math.random() * 30),
        losses: Math.floor(Math.random() * 20),
        draws: Math.floor(Math.random() * 5),
        best_score: Math.floor(Math.random() * 100) + 20,
        total_score: Math.floor(Math.random() * 1000) + 100,
        longest_win_streak: Math.floor(Math.random() * 10),
        current_win_streak: Math.floor(Math.random() * 5),
        level: Math.floor(Math.random() * 20) + 1,
        experience_points: Math.floor(Math.random() * 5000),
        last_played: new Date(Date.now() - Math.random() * 7 * 24 * 60 * 60 * 1000)
      };

      try {
        const existingStats = await prisma.userStats.findUnique({
          where: { user_id: user.user_id }
        });

        if (existingStats) {
          // 기존 통계 업데이트
          await prisma.userStats.update({
            where: { user_id: user.user_id },
            data: {
              ...mockStats,
              best_score: Math.max(existingStats.best_score, mockStats.best_score),
              longest_win_streak: Math.max(existingStats.longest_win_streak, mockStats.longest_win_streak),
              updated_at: new Date()
            }
          });
          updated++;
          console.log(`📊 통계 업데이트: ${user.username}`);
        } else {
          // 새 통계 생성
          await prisma.userStats.create({
            data: {
              user_id: user.user_id,
              ...mockStats
            }
          });
          created++;
          console.log(`📈 통계 생성: ${user.username}`);
        }

        processed++;
      } catch (error) {
        console.error(`❌ ${user.username} 통계 처리 실패:`, error.message);
      }
    }

    console.log(`\n✅ 배치 처리 완료:`);
    console.log(`   - 처리된 사용자: ${processed}`);
    console.log(`   - 생성된 통계: ${created}`);
    console.log(`   - 업데이트된 통계: ${updated}`);

  } catch (error) {
    console.error('❌ 배치 처리 테스트 실패:', error);
  }
}

// 통계 조회 테스트
async function testStatsQuery() {
  console.log('\n=== 통계 조회 테스트 시작 ===');

  try {
    // 전역 통계
    const [
      totalUsers,
      userStatsAgg,
      topPlayerByWins,
      topPlayerByScore
    ] = await Promise.all([
      prisma.user.count({ where: { is_active: true } }),
      prisma.userStats.aggregate({
        _sum: { total_games: true },
        _avg: { total_games: true }
      }),
      prisma.userStats.findFirst({
        orderBy: { wins: 'desc' },
        include: { user: { select: { username: true } } }
      }),
      prisma.userStats.findFirst({
        orderBy: { best_score: 'desc' },
        include: { user: { select: { username: true } } }
      })
    ]);

    console.log('📈 전역 통계:');
    console.log(`   - 총 사용자: ${totalUsers}`);
    console.log(`   - 총 게임 수: ${userStatsAgg._sum.total_games || 0}`);
    console.log(`   - 평균 게임/사용자: ${(userStatsAgg._avg.total_games || 0).toFixed(1)}`);
    
    if (topPlayerByWins) {
      console.log(`   - 승수 1위: ${topPlayerByWins.user.username} (${topPlayerByWins.wins}승)`);
    }
    
    if (topPlayerByScore) {
      console.log(`   - 점수 1위: ${topPlayerByScore.user.username} (${topPlayerByScore.best_score}점)`);
    }

    // 랭킹 조회
    const ranking = await prisma.userStats.findMany({
      where: { total_games: { gt: 0 } },
      include: { user: { select: { username: true } } },
      orderBy: [{ wins: 'desc' }, { best_score: 'desc' }],
      take: 5
    });

    console.log('\n🏆 상위 5명 랭킹:');
    ranking.forEach((stat, index) => {
      const winRate = stat.total_games > 0 ? (stat.wins / stat.total_games * 100).toFixed(1) : '0.0';
      console.log(`   ${index + 1}. ${stat.user.username} - ${stat.wins}승/${stat.total_games}게임 (승률: ${winRate}%, 최고점수: ${stat.best_score})`);
    });

  } catch (error) {
    console.error('❌ 통계 조회 테스트 실패:', error);
  }
}

// 메인 실행 함수
async function main() {
  console.log('🎮 블로쿠스 통계 배치 처리 테스트');
  console.log('=====================================');
  
  try {
    await createTestUsers();
    await testBatchProcessing();
    await testStatsQuery();
    
    console.log('\n🎉 모든 테스트 완료!');
    console.log('\n💡 다음 단계:');
    console.log('   1. 웹에서 /stats 페이지 확인');
    console.log('   2. API 엔드포인트 테스트: GET /api/stats');
    console.log('   3. 랭킹 API 테스트: GET /api/stats/ranking');
    console.log('   4. 개별 사용자 통계: GET /api/stats/user/[userId]');
    
  } catch (error) {
    console.error('❌ 테스트 실행 중 오류:', error);
  } finally {
    await prisma.$disconnect();
  }
}

// 스크립트 실행
if (require.main === module) {
  main().catch(console.error);
}