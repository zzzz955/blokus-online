import { NextRequest, NextResponse } from 'next/server';
import { env } from '@/lib/env';

// 간단한 로그 저장소 (메모리 기반)
const debugLogs: any[] = [];
const MAX_LOGS = 1000;

export async function GET() {
  try {
    // 기본적인 서버 상태 체크
    const healthCheck = {
      status: 'ok',
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      environment: env.NODE_ENV,
      version: env.NPM_PACKAGE_VERSION,
      debugLogs: debugLogs.slice(-10), // 최근 10개 로그만 반환
      totalLogs: debugLogs.length,
    };

    return NextResponse.json(healthCheck, { status: 200 });
  } catch (error) {
    return NextResponse.json(
      { 
        status: 'error', 
        message: 'Health check failed',
        timestamp: new Date().toISOString(),
      },
      { status: 500 }
    );
  }
}

// Unity 디버그 로그 수신용 POST 엔드포인트
export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    
    // 로그 검증
    if (!body.logs || !Array.isArray(body.logs)) {
      return NextResponse.json({ error: 'Invalid log format' }, { status: 400 });
    }
    
    // 로그 추가
    body.logs.forEach((log: any) => {
      const logEntry = {
        ...log,
        deviceId: body.deviceId,
        platform: body.platform,
        appVersion: body.appVersion,
        receivedAt: new Date().toISOString()
      };
      
      debugLogs.push(logEntry);
      
      // 콘솔에도 출력
      const prefix = `[${body.platform}/${body.deviceId?.substring(0, 8)}]`;
      
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
    
    // 로그 히스토리 크기 제한
    while (debugLogs.length > MAX_LOGS) {
      debugLogs.shift();
    }
    
    return NextResponse.json({ 
      success: true, 
      received: body.logs.length,
      totalLogs: debugLogs.length 
    });
    
  } catch (error) {
    console.error('Error processing debug logs:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}