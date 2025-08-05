// ========================================
// 개별 사용자 게임 통계 API 엔드포인트
// ========================================
// GET /api/stats/user/[userId] - 특정 사용자 게임 통계 조회
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse } from '@/types';

interface UserStatsDetail {
  user_id: number;
  username: string;
  display_name?: string;
  total_games: number;
  wins: number;
  losses: number;
  draws: number;
  win_rate: number;
  best_score: number;
  total_score: number;
  average_score: number;
  longest_win_streak: number;
  current_win_streak: number;
  level: number;
  experience_points: number;
  last_played?: Date;
  updated_at: Date;
  rank?: {
    by_wins: number;
    by_score: number;
  };
}

// GET /api/stats/user/[userId] - 사용자별 통계 조회
export async function GET(
  request: NextRequest,
  { params }: { params: { userId: string } }
) {
  try {
    const userId = parseInt(params.userId, 10);
    
    if (isNaN(userId)) {
      const response: ApiResponse<null> = {
        success: false,
        error: '잘못된 사용자 ID입니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 사용자 통계 조회
    const userStats = await prisma.userStats.findUnique({
      where: { user_id: userId },
      include: {
        user: {
          select: {
            username: true,
            display_name: true
          }
        }
      }
    });

    if (!userStats) {
      const response: ApiResponse<null> = {
        success: false,
        error: '해당 사용자의 통계 데이터를 찾을 수 없습니다.'
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 랭킹 계산 (승수 기준, 점수 기준)
    const [winsRank, scoreRank] = await Promise.all([
      prisma.userStats.count({
        where: {
          wins: {
            gt: userStats.wins
          }
        }
      }),
      prisma.userStats.count({
        where: {
          best_score: {
            gt: userStats.best_score
          }
        }
      })
    ]);

    const result: UserStatsDetail = {
      user_id: userStats.user_id,
      username: userStats.user.username,
      display_name: userStats.user.display_name || undefined,
      total_games: userStats.total_games,
      wins: userStats.wins,
      losses: userStats.losses,
      draws: userStats.draws,
      win_rate: userStats.total_games > 0 ? (userStats.wins / userStats.total_games) * 100 : 0,
      best_score: userStats.best_score,
      total_score: userStats.total_score,
      average_score: userStats.total_games > 0 ? userStats.total_score / userStats.total_games : 0,
      longest_win_streak: userStats.longest_win_streak,
      current_win_streak: userStats.current_win_streak,
      level: userStats.level,
      experience_points: userStats.experience_points,
      last_played: userStats.last_played || undefined,
      updated_at: userStats.updated_at,
      rank: {
        by_wins: winsRank + 1,
        by_score: scoreRank + 1
      }
    };

    const response: ApiResponse<UserStatsDetail> = {
      success: true,
      data: result
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('사용자 통계 조회 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '사용자 통계 데이터를 불러오는데 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}