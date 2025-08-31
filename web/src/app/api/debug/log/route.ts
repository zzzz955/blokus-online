import { NextRequest, NextResponse } from 'next/server';

interface LogEntry {
  timestamp: string;
  level: string;
  message: string;
  stackTrace?: string;
  category: string;
}

interface LogBatch {
  deviceId: string;
  platform: string;
  appVersion: string;
  logs: LogEntry[];
}

const logHistory: LogEntry[] = [];
const MAX_LOGS = 1000; // 최대 1000개의 로그만 보관

export async function POST(request: NextRequest) {
  try {
    const body: LogBatch = await request.json();
    
    // 로그 검증
    if (!body.logs || !Array.isArray(body.logs)) {
      return NextResponse.json({ error: 'Invalid log format' }, { status: 400 });
    }
    
    // 로그 추가
    body.logs.forEach(log => {
      logHistory.push({
        ...log,
        // 추가 메타데이터
        deviceId: body.deviceId,
        platform: body.platform,
        appVersion: body.appVersion
      } as LogEntry & { deviceId: string; platform: string; appVersion: string });
    });
    
    // 로그 히스토리 크기 제한
    while (logHistory.length > MAX_LOGS) {
      logHistory.shift();
    }
    
    // 콘솔에도 출력
    body.logs.forEach(log => {
      const prefix = `[${body.platform}/${body.deviceId.substring(0, 8)}]`;
      
      switch (log.level) {
        case 'ERROR':
          console.error(`${prefix} ${log.message}`, log.stackTrace || '');
          break;
        case 'WARN':
          console.warn(`${prefix} ${log.message}`);
          break;
        case 'INFO':
          console.info(`${prefix} ${log.message}`);
          break;
        default:
          console.log(`${prefix} ${log.message}`);
      }
    });
    
    return NextResponse.json({ 
      success: true, 
      received: body.logs.length,
      totalLogs: logHistory.length 
    });
    
  } catch (error) {
    console.error('Error processing debug logs:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

// 로그 조회용 GET 엔드포인트
export async function GET(request: NextRequest) {
  try {
    const searchParams = request.nextUrl.searchParams;
    const category = searchParams.get('category');
    const level = searchParams.get('level');
    const limit = parseInt(searchParams.get('limit') || '50');
    
    let filteredLogs = logHistory;
    
    // 필터링
    if (category) {
      filteredLogs = filteredLogs.filter(log => log.category === category);
    }
    
    if (level) {
      filteredLogs = filteredLogs.filter(log => log.level === level);
    }
    
    // 최신 로그부터 반환
    const result = filteredLogs
      .slice(-limit)
      .reverse();
    
    return NextResponse.json({
      logs: result,
      total: filteredLogs.length
    });
    
  } catch (error) {
    console.error('Error retrieving debug logs:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}