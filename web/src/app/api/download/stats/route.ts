import { NextRequest, NextResponse } from 'next/server';
import { env } from '@/lib/env';

interface DownloadStats {
  totalDownloads: number;
  todayDownloads: number;
  lastDownload?: string;
  popularTimes: { [hour: string]: number };
  userAgents: { [agent: string]: number };
}

// 메모리 기반 통계 (서버 재시작시 초기화됨)
let memoryStats: DownloadStats = {
  totalDownloads: 0,
  todayDownloads: 0,
  popularTimes: {},
  userAgents: {}
};

// 통계 업데이트 (메모리 기반)
function updateMemoryStats(ip: string, userAgent: string) {
  const now = new Date();
  const today = now.toISOString().split('T')[0];
  const hour = now.getHours().toString();
  
  // 통계 업데이트
  memoryStats.totalDownloads += 1;
  memoryStats.lastDownload = now.toISOString();
  
  // 간단한 오늘 다운로드 수 (정확하지 않지만 참고용)
  memoryStats.todayDownloads += 1;
  
  // 시간대별 통계
  memoryStats.popularTimes[hour] = (memoryStats.popularTimes[hour] || 0) + 1;
  
  // User Agent 통계 (간단화된 버전)
  const simplifiedUA = userAgent.includes('Chrome') ? 'Chrome' :
                      userAgent.includes('Firefox') ? 'Firefox' :
                      userAgent.includes('Safari') ? 'Safari' :
                      userAgent.includes('Edge') ? 'Edge' : 'Other';
  
  memoryStats.userAgents[simplifiedUA] = (memoryStats.userAgents[simplifiedUA] || 0) + 1;
  
  return memoryStats;
}

// 통계 조회 API
export async function GET(request: NextRequest) {
  try {
    return NextResponse.json({
      success: true,
      data: memoryStats,
      note: "Statistics are memory-based and reset on server restart"
    });
  } catch (error) {
    console.error('다운로드 통계 조회 오류:', error);
    return NextResponse.json(
      { 
        success: false,
        error: '통계 정보를 가져올 수 없습니다.' 
      },
      { status: 500 }
    );
  }
}

// 다운로드 통계 업데이트 API (내부 사용)
export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { ip, userAgent } = body;
    
    // API 키 검증 (내부 호출용)
    const apiKey = request.headers.get('x-internal-api-key');
    if (apiKey !== env.INTERNAL_API_KEY && apiKey !== 'internal-stats-update') {
      return NextResponse.json(
        { error: '권한이 없습니다.' },
        { status: 401 }
      );
    }

    const updatedStats = updateMemoryStats(ip, userAgent);
    
    return NextResponse.json({
      success: true,
      message: '통계가 업데이트되었습니다.',
      data: updatedStats
    });
  } catch (error) {
    console.error('다운로드 통계 업데이트 오류:', error);
    return NextResponse.json(
      { 
        success: false,
        error: '통계를 업데이트할 수 없습니다.' 
      },
      { status: 500 }
    );
  }
}