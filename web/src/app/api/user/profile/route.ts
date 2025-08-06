// ========================================
// 사용자 프로필 API 엔드포인트
// ========================================
// GET /api/user/profile - 현재 로그인한 사용자의 프로필 정보 조회
// PUT /api/user/profile - 사용자 프로필 정보 수정
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth/next';
import { prisma } from '@/lib/prisma';
import { ApiResponse } from '@/types';
import { authOptions } from '@/lib/auth';

interface UserProfile {
  user_id: number;
  username: string;
  email?: string;
  oauth_provider?: string;
  display_name?: string;
  avatar_url?: string;
  created_at: Date;
  last_login_at?: Date;
  // 게임 통계
  stats: {
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
  };
  // 활동 통계
  activity_stats: {
    posts_count: number;
    testimonials_count: number;
    support_tickets_count: number;
  };
}

interface UpdateProfileRequest {
  display_name?: string;
}

// GET /api/user/profile - 현재 사용자 프로필 조회
export async function GET(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions);
    
    if (!session?.user?.email) {
      const response: ApiResponse<null> = {
        success: false,
        error: '로그인이 필요합니다.'
      };
      return NextResponse.json(response, { status: 401 });
    }

    // 사용자 정보 조회
    const user = await prisma.user.findUnique({
      where: { email: session.user.email },
      include: {
        user_stats: true,
        posts: {
          where: { is_deleted: false },
          select: { id: true }
        },
        testimonials: {
          where: { is_published: true },
          select: { id: true }
        },
        support_tickets: {
          select: { id: true }
        }
      }
    });

    if (!user) {
      const response: ApiResponse<null> = {
        success: false,
        error: '사용자 정보를 찾을 수 없습니다.'
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 사용자 통계가 없는 경우 기본값으로 생성
    let userStats = user.user_stats;
    if (!userStats) {
      userStats = await prisma.userStats.create({
        data: {
          user_id: user.user_id,
        }
      });
    }

    const result: UserProfile = {
      user_id: user.user_id,
      username: user.username,
      email: user.email || undefined,
      oauth_provider: user.oauth_provider || undefined,
      display_name: user.display_name || undefined,
      avatar_url: user.avatar_url || undefined,
      created_at: user.created_at,
      last_login_at: user.last_login_at || undefined,
      stats: {
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
        last_played: userStats.last_played || undefined
      },
      activity_stats: {
        posts_count: user.posts.length,
        testimonials_count: user.testimonials.length,
        support_tickets_count: user.support_tickets.length
      }
    };

    const response: ApiResponse<UserProfile> = {
      success: true,
      data: result
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('사용자 프로필 조회 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '사용자 프로필을 불러오는데 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}

// PUT /api/user/profile - 사용자 프로필 수정
export async function PUT(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions);
    
    if (!session?.user?.email) {
      const response: ApiResponse<null> = {
        success: false,
        error: '로그인이 필요합니다.'
      };
      return NextResponse.json(response, { status: 401 });
    }

    const body: UpdateProfileRequest = await request.json();
    
    // 입력 유효성 검사
    if (body.display_name !== undefined) {
      if (typeof body.display_name !== 'string') {
        const response: ApiResponse<null> = {
          success: false,
          error: '표시명은 문자열이어야 합니다.'
        };
        return NextResponse.json(response, { status: 400 });
      }
      
      if (body.display_name.length > 30) {
        const response: ApiResponse<null> = {
          success: false,
          error: '표시명은 30자를 초과할 수 없습니다.'
        };
        return NextResponse.json(response, { status: 400 });
      }
      
      // 빈 문자열인 경우 null로 처리
      if (body.display_name.trim() === '') {
        body.display_name = undefined;
      }
    }

    // 사용자 정보 업데이트
    const updatedUser = await prisma.user.update({
      where: { email: session.user.email },
      data: {
        display_name: body.display_name === undefined ? undefined : body.display_name || null,
        updated_at: new Date()
      },
      select: {
        user_id: true,
        username: true,
        display_name: true,
        updated_at: true
      }
    });

    const response: ApiResponse<{ 
      user_id: number; 
      username: string; 
      display_name: string | null; 
      updated_at: Date 
    }> = {
      success: true,
      data: updatedUser,
      message: '프로필이 성공적으로 수정되었습니다.'
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('사용자 프로필 수정 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '프로필 수정에 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}