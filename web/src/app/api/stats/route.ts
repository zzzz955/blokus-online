// ========================================
// 게임 통계 API 엔드포인트
// ========================================
// GET /api/stats - 전역 게임 통계 조회
// POST /api/stats/batch - 배치 처리 실행 (관리자 전용)
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getGlobalStats, runStatsBatch } from '@/lib/batch-stats';
import { ApiResponse } from '@/types';

// GET /api/stats - 전역 통계 조회
export async function GET() {
  try {
    const stats = await getGlobalStats();
    
    const response: ApiResponse<typeof stats> = {
      success: true,
      data: stats
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('전역 통계 조회 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '통계 데이터를 불러오는데 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}

// POST /api/stats/batch - 배치 처리 실행 (관리자 전용)
export async function POST(request: NextRequest) {
  try {
    // TODO: 관리자 권한 검증
    // const session = await getServerSession(authOptions);
    // if (!session || !isAdmin(session)) {
    //   return NextResponse.json({ success: false, error: '권한이 없습니다.' }, { status: 403 });
    // }

    const result = await runStatsBatch();
    
    const response: ApiResponse<typeof result> = {
      success: result.success,
      data: result,
      error: result.success ? undefined : '배치 처리 중 일부 오류가 발생했습니다.'
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('통계 배치 처리 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '배치 처리를 실행하는데 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}