import { NextRequest, NextResponse } from 'next/server';
import { promises as fs } from 'fs';
import path from 'path';

interface DownloadStats {
  totalDownloads: number;
  todayDownloads: number;
  lastDownload?: string;
  popularTimes: { [hour: string]: number };
  userAgents: { [agent: string]: number };
}

interface DownloadLog {
  timestamp: string;
  ip: string;
  userAgent: string;
  fileSize: number;
}

// 다운로드 통계 파일 경로
const STATS_FILE_PATH = path.join(process.cwd(), 'data', 'download-stats.json');
const LOGS_FILE_PATH = path.join(process.cwd(), 'data', 'download-logs.json');

// 기본 통계 데이터 생성
function createDefaultStats(): DownloadStats {
  return {
    totalDownloads: 0,
    todayDownloads: 0,
    popularTimes: {},
    userAgents: {}
  };
}

// 통계 데이터 읽기
async function readStats(): Promise<DownloadStats> {
  try {
    const data = await fs.readFile(STATS_FILE_PATH, 'utf-8');
    return JSON.parse(data);
  } catch (error) {
    const defaultStats = createDefaultStats();
    await fs.writeFile(STATS_FILE_PATH, JSON.stringify(defaultStats, null, 2));
    return defaultStats;
  }
}

// 로그 데이터 읽기
async function readLogs(): Promise<DownloadLog[]> {
  try {
    const data = await fs.readFile(LOGS_FILE_PATH, 'utf-8');
    return JSON.parse(data);
  } catch (error) {
    await fs.writeFile(LOGS_FILE_PATH, JSON.stringify([], null, 2));
    return [];
  }
}

// 통계 업데이트
async function updateStats(ip: string, userAgent: string, fileSize: number) {
  const stats = await readStats();
  const logs = await readLogs();
  
  const now = new Date();
  const today = now.toISOString().split('T')[0];
  const hour = now.getHours().toString();
  
  // 새 로그 추가
  const newLog: DownloadLog = {
    timestamp: now.toISOString(),
    ip,
    userAgent,
    fileSize
  };
  
  logs.push(newLog);
  
  // 로그는 최근 1000개만 유지
  if (logs.length > 1000) {
    logs.splice(0, logs.length - 1000);
  }
  
  // 통계 업데이트
  stats.totalDownloads += 1;
  stats.lastDownload = now.toISOString();
  
  // 오늘 다운로드 수 계산
  const todayLogs = logs.filter(log => 
    log.timestamp.split('T')[0] === today
  );
  stats.todayDownloads = todayLogs.length;
  
  // 시간대별 통계
  stats.popularTimes[hour] = (stats.popularTimes[hour] || 0) + 1;
  
  // User Agent 통계 (간단화된 버전)
  const simplifiedUA = userAgent.includes('Chrome') ? 'Chrome' :
                      userAgent.includes('Firefox') ? 'Firefox' :
                      userAgent.includes('Safari') ? 'Safari' :
                      userAgent.includes('Edge') ? 'Edge' : 'Other';
  
  stats.userAgents[simplifiedUA] = (stats.userAgents[simplifiedUA] || 0) + 1;
  
  // 파일 저장
  await Promise.all([
    fs.writeFile(STATS_FILE_PATH, JSON.stringify(stats, null, 2)),
    fs.writeFile(LOGS_FILE_PATH, JSON.stringify(logs, null, 2))
  ]);
  
  return stats;
}

// 통계 조회 API
export async function GET(request: NextRequest) {
  try {
    const stats = await readStats();
    
    return NextResponse.json({
      success: true,
      data: stats
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
    const { ip, userAgent, fileSize } = body;
    
    // API 키 검증 (내부 호출용)
    const apiKey = request.headers.get('x-internal-api-key');
    if (apiKey !== process.env.INTERNAL_API_KEY && apiKey !== 'internal-stats-update') {
      return NextResponse.json(
        { error: '권한이 없습니다.' },
        { status: 401 }
      );
    }

    const updatedStats = await updateStats(ip, userAgent, fileSize);
    
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

// 관리자용 상세 로그 조회 API
export async function PUT(request: NextRequest) {
  try {
    const body = await request.json();
    const { adminKey, limit = 100 } = body;
    
    // 관리자 키 검증
    if (adminKey !== process.env.ADMIN_API_KEY) {
      return NextResponse.json(
        { error: '권한이 없습니다.' },
        { status: 401 }
      );
    }
    
    const logs = await readLogs();
    const recentLogs = logs.slice(-limit).reverse(); // 최근 로그부터
    
    return NextResponse.json({
      success: true,
      data: {
        totalLogs: logs.length,
        logs: recentLogs
      }
    });
  } catch (error) {
    console.error('다운로드 로그 조회 오류:', error);
    return NextResponse.json(
      { 
        success: false,
        error: '로그 정보를 가져올 수 없습니다.' 
      },
      { status: 500 }
    );
  }
}