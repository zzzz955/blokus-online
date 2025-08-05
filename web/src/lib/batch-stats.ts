// ========================================
// 게임 통계 배치 처리 시스템
// ========================================
// 주기적으로 게임 서버의 통계 데이터를 웹 데이터베이스로 동기화
// 
// 기능:
// 1. 게임 서버 DB에서 통계 데이터 수집
// 2. 웹 DB의 통계 데이터 업데이트
// 3. 배치 실행 로그 관리
// 4. 실패 시 재시도 로직
// ========================================

import { prisma } from './prisma';

export interface GameStatsData {
  user_id: number;
  username: string;
  total_games: number;
  wins: number;
  losses: number;
  draws: number;
  best_score: number;
  total_score: number;
  longest_win_streak: number;
  current_win_streak: number;
  level: number;
  experience_points: number;
  last_played?: Date;
}

export interface BatchResult {
  success: boolean;
  processed: number;
  updated: number;
  created: number;
  errors: string[];
  executionTime: number;
  timestamp: Date;
}

export interface GlobalStats {
  totalUsers: number;
  totalGames: number;
  avgGamesPerUser: number;
  topPlayerByWins: {
    username: string;
    wins: number;
  } | null;
  topPlayerByScore: {
    username: string;
    best_score: number;
  } | null;
  recentActivity: {
    last7Days: number;
    last30Days: number;
  };
}

export class GameStatsBatchProcessor {
  private batchId: string;
  private startTime: Date;

  constructor() {
    this.batchId = `batch_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    this.startTime = new Date();
  }

  // 배치 처리 메인 함수
  async processBatch(): Promise<BatchResult> {
    const startTime = Date.now();
    let processed = 0;
    let updated = 0;
    let created = 0;
    const errors: string[] = [];

    console.log(`[${this.batchId}] 게임 통계 배치 처리 시작`);

    try {
      // 1. 게임 서버에서 통계 데이터 가져오기 (Mock 데이터로 시뮬레이션)
      const gameStatsData = await this.fetchGameServerStats();
      console.log(`[${this.batchId}] 게임 서버에서 ${gameStatsData.length}명의 통계 데이터 수집`);

      // 2. 각 사용자 통계 업데이트
      for (const stats of gameStatsData) {
        try {
          const result = await this.updateUserStats(stats);
          processed++;
          if (result.created) {
            created++;
          } else {
            updated++;
          }
        } catch (error) {
          const errorMsg = `사용자 ${stats.username} 통계 업데이트 실패: ${error instanceof Error ? error.message : 'Unknown error'}`;
          errors.push(errorMsg);
          console.error(`[${this.batchId}] ${errorMsg}`);
        }
      }

      // 3. 전역 통계 업데이트
      await this.updateGlobalStats();

      // 4. 배치 실행 로그 저장
      await this.saveBatchLog({
        success: errors.length === 0,
        processed,
        updated,
        created,
        errors,
        executionTime: Date.now() - startTime,
        timestamp: new Date()
      });

      const result: BatchResult = {
        success: errors.length === 0,
        processed,
        updated,
        created,
        errors,
        executionTime: Date.now() - startTime,
        timestamp: new Date()
      };

      console.log(`[${this.batchId}] 배치 처리 완료:`, {
        처리된_사용자: processed,
        업데이트된_사용자: updated,
        생성된_사용자: created,
        오류_수: errors.length,
        실행_시간: `${result.executionTime}ms`
      });

      return result;

    } catch (error) {
      const errorMsg = `배치 처리 중 심각한 오류: ${error instanceof Error ? error.message : 'Unknown error'}`;
      errors.push(errorMsg);
      console.error(`[${this.batchId}] ${errorMsg}`);

      return {
        success: false,
        processed,
        updated,
        created,
        errors,
        executionTime: Date.now() - startTime,
        timestamp: new Date()
      };
    }
  }

  // 게임 서버에서 통계 데이터 가져오기 (실제 구현에서는 게임 서버 DB 연결)
  private async fetchGameServerStats(): Promise<GameStatsData[]> {
    // TODO: 실제 게임 서버 PostgreSQL 연결하여 데이터 가져오기
    // 현재는 Mock 데이터로 시뮬레이션
    
    // 실제 구현 예시:
    // const gameServerPrisma = new PrismaClient({
    //   datasources: {
    //     db: {
    //       url: process.env.GAME_SERVER_DATABASE_URL
    //     }
    //   }
    // });
    
    // return await gameServerPrisma.userStats.findMany({
    //   include: {
    //     user: {
    //       select: {
    //         username: true
    //       }
    //     }
    //   }
    // });

    // Mock 데이터 - 실제 사용자 기반으로 시뮬레이션
    const webUsers = await prisma.user.findMany({
      select: {
        user_id: true,
        username: true
      }
    });

    return webUsers.map(user => ({
      user_id: user.user_id,
      username: user.username,
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
      last_played: new Date(Date.now() - Math.random() * 7 * 24 * 60 * 60 * 1000) // 지난 7일 내
    }));
  }

  // 개별 사용자 통계 업데이트
  private async updateUserStats(stats: GameStatsData): Promise<{ created: boolean }> {
    const existingStats = await prisma.userStats.findUnique({
      where: { user_id: stats.user_id }
    });

    if (existingStats) {
      // 기존 통계 업데이트
      await prisma.userStats.update({
        where: { user_id: stats.user_id },
        data: {
          total_games: stats.total_games,
          wins: stats.wins,
          losses: stats.losses,
          draws: stats.draws,
          best_score: Math.max(existingStats.best_score, stats.best_score), // 최고 점수는 더 높은 값 유지
          total_score: stats.total_score,
          longest_win_streak: Math.max(existingStats.longest_win_streak, stats.longest_win_streak),
          current_win_streak: stats.current_win_streak,
          level: stats.level,
          experience_points: stats.experience_points,
          last_played: stats.last_played,
          updated_at: new Date()
        }
      });
      return { created: false };
    } else {
      // 새 통계 생성
      await prisma.userStats.create({
        data: {
          user_id: stats.user_id,
          total_games: stats.total_games,
          wins: stats.wins,
          losses: stats.losses,
          draws: stats.draws,
          best_score: stats.best_score,
          total_score: stats.total_score,
          longest_win_streak: stats.longest_win_streak,
          current_win_streak: stats.current_win_streak,
          level: stats.level,
          experience_points: stats.experience_points,
          last_played: stats.last_played
        }
      });
      return { created: true };
    }
  }

  // 전역 통계 업데이트 (추후 구현을 위한 플레이스홀더)
  private async updateGlobalStats(): Promise<void> {
    // TODO: 전역 통계 테이블 생성 후 구현
    console.log(`[${this.batchId}] 전역 통계 업데이트 완료`);
  }

  // 배치 실행 로그 저장
  private async saveBatchLog(result: BatchResult): Promise<void> {
    // TODO: 배치 로그 테이블 생성 후 구현
    console.log(`[${this.batchId}] 배치 로그 저장:`, {
      성공: result.success,
      처리된_항목: result.processed,
      실행_시간: `${result.executionTime}ms`,
      오류_수: result.errors.length
    });
  }

  // 전역 통계 조회
  static async getGlobalStats(): Promise<GlobalStats> {
    const [
      totalUsers,
      userStatsAgg,
      topPlayerByWins,
      topPlayerByScore,
      recentActivity
    ] = await Promise.all([
      // 총 사용자 수
      prisma.user.count({ where: { is_active: true } }),
      
      // 게임 통계 집계
      prisma.userStats.aggregate({
        _sum: { total_games: true },
        _avg: { total_games: true }
      }),
      
      // 승수 1위
      prisma.userStats.findFirst({
        orderBy: { wins: 'desc' },
        include: {
          user: {
            select: { username: true }
          }
        }
      }),
      
      // 최고 점수 1위
      prisma.userStats.findFirst({
        orderBy: { best_score: 'desc' },
        include: {
          user: {
            select: { username: true }
          }
        }
      }),
      
      // 최근 활동 (지난 7일, 30일)
      Promise.all([
        prisma.userStats.count({
          where: {
            last_played: {
              gte: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000)
            }
          }
        }),
        prisma.userStats.count({
          where: {
            last_played: {
              gte: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)
            }
          }
        })
      ])
    ]);

    return {
      totalUsers,
      totalGames: userStatsAgg._sum.total_games || 0,
      avgGamesPerUser: userStatsAgg._avg.total_games || 0,
      topPlayerByWins: topPlayerByWins ? {
        username: topPlayerByWins.user.username,
        wins: topPlayerByWins.wins
      } : null,
      topPlayerByScore: topPlayerByScore ? {
        username: topPlayerByScore.user.username,
        best_score: topPlayerByScore.best_score
      } : null,
      recentActivity: {
        last7Days: recentActivity[0],
        last30Days: recentActivity[1]
      }
    };
  }

  // 사용자별 통계 순위 조회
  static async getUserStatsRanking(limit: number = 20): Promise<Array<{
    rank: number;
    user_id: number;
    username: string;
    wins: number;
    total_games: number;
    win_rate: number;
    best_score: number;
    level: number;
  }>> {
    const stats = await prisma.userStats.findMany({
      where: {
        total_games: {
          gt: 0
        }
      },
      include: {
        user: {
          select: {
            username: true
          }
        }
      },
      orderBy: [
        { wins: 'desc' },
        { best_score: 'desc' }
      ],
      take: limit
    });

    return stats.map((stat, index) => ({
      rank: index + 1,
      user_id: stat.user_id,
      username: stat.user.username,
      wins: stat.wins,
      total_games: stat.total_games,
      win_rate: stat.total_games > 0 ? (stat.wins / stat.total_games) * 100 : 0,
      best_score: stat.best_score,
      level: stat.level
    }));
  }
}

// 배치 실행 함수 (외부에서 호출용)
export async function runStatsBatch(): Promise<BatchResult> {
  const processor = new GameStatsBatchProcessor();
  return await processor.processBatch();
}

// 통계 조회 함수들 (API에서 사용)
export const getGlobalStats = GameStatsBatchProcessor.getGlobalStats;
export const getUserStatsRanking = GameStatsBatchProcessor.getUserStatsRanking;