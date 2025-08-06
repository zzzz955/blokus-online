// ========================================
// 게임 통계 랭킹 API 엔드포인트
// ========================================
// GET /api/stats/ranking - 사용자 게임 통계 랭킹 조회
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getUserStatsRanking } from '@/lib/batch-stats';
import { ApiResponse } from '@/types';

// GET /api/stats/ranking - 통계 랭킹 조회
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const limitParam = searchParams.get('limit');
    const limit = limitParam ? parseInt(limitParam, 10) : 20;
    
    // limit 범위 검증
    const validatedLimit = Math.min(Math.max(limit, 1), 100); // 1-100 사이
    
    const ranking = await getUserStatsRanking(validatedLimit);
    
    const response: ApiResponse<typeof ranking> = {
      success: true,
      data: ranking
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('통계 랭킹 조회 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '랭킹 데이터를 불러오는데 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}