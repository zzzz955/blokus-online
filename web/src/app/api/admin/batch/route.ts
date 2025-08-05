// ========================================
// 관리자 배치 작업 관리 API
// ========================================
// GET /api/admin/batch - 배치 스케줄 및 히스토리 조회
// POST /api/admin/batch/run - 수동 배치 실행
// PUT /api/admin/batch/toggle - 스케줄 활성화/비활성화
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getScheduler } from '@/lib/batch-scheduler';
import { ApiResponse } from '@/types';

// GET /api/admin/batch - 배치 정보 조회
export async function GET() {
  try {
    const scheduler = getScheduler();
    
    const schedules = scheduler.getSchedules();
    const history = scheduler.getHistory(50);
    
    const response: ApiResponse<{
      schedules: typeof schedules;
      history: typeof history;
    }> = {
      success: true,
      data: {
        schedules,
        history
      }
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('배치 정보 조회 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '배치 정보를 불러오는데 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}

// POST /api/admin/batch/run - 수동 배치 실행
export async function POST(request: NextRequest) {
  try {
    // TODO: 관리자 권한 검증
    // const session = await getServerSession(authOptions);
    // if (!session || !isAdmin(session)) {
    //   return NextResponse.json({ success: false, error: '권한이 없습니다.' }, { status: 403 });
    // }

    const body = await request.json();
    const { scheduleId } = body;

    if (!scheduleId) {
      const response: ApiResponse<null> = {
        success: false,
        error: '스케줄 ID가 필요합니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    const scheduler = getScheduler();
    const result = await scheduler.runJobManually(scheduleId);
    
    const response: ApiResponse<typeof result> = {
      success: true,
      data: result
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('수동 배치 실행 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: error instanceof Error ? error.message : '배치 실행에 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}

// PUT /api/admin/batch/toggle - 스케줄 활성화/비활성화
export async function PUT(request: NextRequest) {
  try {
    // TODO: 관리자 권한 검증

    const body = await request.json();
    const { scheduleId, enabled } = body;

    if (!scheduleId || typeof enabled !== 'boolean') {
      const response: ApiResponse<null> = {
        success: false,
        error: '스케줄 ID와 활성화 상태가 필요합니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    const scheduler = getScheduler();
    scheduler.toggleSchedule(scheduleId, enabled);
    
    const response: ApiResponse<{ success: boolean }> = {
      success: true,
      data: { success: true }
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('스케줄 토글 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: error instanceof Error ? error.message : '스케줄 변경에 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}