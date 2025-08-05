// ========================================
// 배치 작업 스케줄러
// ========================================
// Node.js cron을 사용한 주기적 배치 작업 실행
// 
// 기능:
// 1. 게임 통계 배치 처리 스케줄링
// 2. 배치 작업 상태 모니터링
// 3. 실패 시 재시도 로직
// 4. 배치 작업 히스토리 관리
// ========================================

import { runStatsBatch, BatchResult } from './batch-stats';

export interface BatchSchedule {
  id: string;
  name: string;
  cron: string;
  enabled: boolean;
  lastRun?: Date;
  nextRun: Date;
  status: 'idle' | 'running' | 'failed';
  retryCount: number;
  maxRetries: number;
}

export interface BatchHistory {
  id: string;
  scheduleId: string;
  startTime: Date;
  endTime?: Date;
  status: 'running' | 'success' | 'failed';
  result?: BatchResult;
  error?: string;
}

export class BatchScheduler {
  private schedules: Map<string, BatchSchedule> = new Map();
  private history: BatchHistory[] = [];
  private timers: Map<string, NodeJS.Timeout> = new Map();
  private isInitialized = false;

  constructor() {
    this.initializeDefaultSchedules();
  }

  // 기본 스케줄 초기화
  private initializeDefaultSchedules() {
    const statsSchedule: BatchSchedule = {
      id: 'stats-batch',
      name: '게임 통계 배치 처리',
      cron: '0 */6 * * *', // 6시간마다 실행 (0, 6, 12, 18시)
      enabled: true,
      nextRun: this.calculateNextRun('0 */6 * * *'),
      status: 'idle',
      retryCount: 0,
      maxRetries: 3
    };

    this.schedules.set(statsSchedule.id, statsSchedule);
  }

  // 스케줄러 시작
  start() {
    if (this.isInitialized) {
      console.log('[BatchScheduler] 이미 초기화됨');
      return;
    }

    console.log('[BatchScheduler] 배치 스케줄러 시작');
    
    for (const [id, schedule] of this.schedules) {
      if (schedule.enabled) {
        this.scheduleJob(id);
      }
    }

    this.isInitialized = true;
    console.log('[BatchScheduler] 스케줄러 초기화 완료');
  }

  // 스케줄러 중지
  stop() {
    console.log('[BatchScheduler] 배치 스케줄러 중지');
    
    for (const [id, timer] of this.timers) {
      clearTimeout(timer);
      console.log(`[BatchScheduler] ${id} 스케줄 해제`);
    }
    
    this.timers.clear();
    this.isInitialized = false;
  }

  // 개별 작업 스케줄링
  private scheduleJob(scheduleId: string) {
    const schedule = this.schedules.get(scheduleId);
    if (!schedule || !schedule.enabled) {
      return;
    }

    const now = new Date();
    const delay = schedule.nextRun.getTime() - now.getTime();

    if (delay <= 0) {
      // 즉시 실행 후 다음 스케줄 설정
      this.executeJob(scheduleId);
    } else {
      // 지정된 시간에 실행
      const timer = setTimeout(() => {
        this.executeJob(scheduleId);
      }, delay);

      this.timers.set(scheduleId, timer);
      console.log(`[BatchScheduler] ${schedule.name} 다음 실행: ${schedule.nextRun.toLocaleString()}`);
    }
  }

  // 배치 작업 실행
  private async executeJob(scheduleId: string) {
    const schedule = this.schedules.get(scheduleId);
    if (!schedule) {
      return;
    }

    // 실행 히스토리 생성
    const historyId = `${scheduleId}_${Date.now()}`;
    const historyEntry: BatchHistory = {
      id: historyId,
      scheduleId,
      startTime: new Date(),
      status: 'running'
    };
    this.history.push(historyEntry);

    // 스케줄 상태 업데이트
    schedule.status = 'running';
    schedule.lastRun = new Date();

    console.log(`[BatchScheduler] ${schedule.name} 실행 시작`);

    try {
      let result: BatchResult;

      // 작업 타입별 실행
      switch (scheduleId) {
        case 'stats-batch':
          result = await runStatsBatch();
          break;
        default:
          throw new Error(`알 수 없는 배치 작업: ${scheduleId}`);
      }

      // 성공 처리
      historyEntry.endTime = new Date();
      historyEntry.status = result.success ? 'success' : 'failed';
      historyEntry.result = result;

      schedule.status = result.success ? 'idle' : 'failed';
      schedule.retryCount = 0; // 성공 시 재시도 카운트 리셋

      console.log(`[BatchScheduler] ${schedule.name} 실행 완료:`, {
        성공: result.success,
        처리된_항목: result.processed,
        실행_시간: `${result.executionTime}ms`
      });

    } catch (error) {
      // 실패 처리
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      
      historyEntry.endTime = new Date();
      historyEntry.status = 'failed';
      historyEntry.error = errorMessage;

      schedule.status = 'failed';
      schedule.retryCount++;

      console.error(`[BatchScheduler] ${schedule.name} 실행 실패:`, errorMessage);

      // 재시도 로직
      if (schedule.retryCount < schedule.maxRetries) {
        console.log(`[BatchScheduler] ${schedule.name} 재시도 예정 (${schedule.retryCount}/${schedule.maxRetries})`);
        
        // 5분 후 재시도
        setTimeout(() => {
          this.executeJob(scheduleId);
        }, 5 * 60 * 1000);
        return;
      } else {
        console.error(`[BatchScheduler] ${schedule.name} 최대 재시도 횟수 초과`);
      }
    }

    // 다음 실행 스케줄링
    schedule.nextRun = this.calculateNextRun(schedule.cron);
    this.scheduleJob(scheduleId);

    // 히스토리 정리 (최근 100개만 유지)
    if (this.history.length > 100) {
      this.history = this.history.slice(-100);
    }
  }

  // 크론 표현식으로 다음 실행 시간 계산
  private calculateNextRun(cronExpression: string): Date {
    // 간단한 크론 파서 구현 (실제로는 node-cron 라이브러리 사용 권장)
    const now = new Date();
    
    // "0 */6 * * *" = 매 6시간마다 0분에 실행
    if (cronExpression === '0 */6 * * *') {
      const nextHour = Math.ceil(now.getHours() / 6) * 6;
      const nextRun = new Date(now);
      
      if (nextHour >= 24) {
        nextRun.setDate(nextRun.getDate() + 1);
        nextRun.setHours(0, 0, 0, 0);
      } else {
        nextRun.setHours(nextHour, 0, 0, 0);
      }
      
      return nextRun;
    }

    // 기본: 1시간 후
    return new Date(now.getTime() + 60 * 60 * 1000);
  }

  // 수동 실행
  async runJobManually(scheduleId: string): Promise<BatchResult | null> {
    const schedule = this.schedules.get(scheduleId);
    if (!schedule) {
      throw new Error(`스케줄을 찾을 수 없습니다: ${scheduleId}`);
    }

    if (schedule.status === 'running') {
      throw new Error('이미 실행 중인 작업입니다');
    }

    console.log(`[BatchScheduler] ${schedule.name} 수동 실행`);

    // 별도의 실행이므로 스케줄 상태는 변경하지 않음
    try {
      switch (scheduleId) {
        case 'stats-batch':
          return await runStatsBatch();
        default:
          throw new Error(`알 수 없는 배치 작업: ${scheduleId}`);
      }
    } catch (error) {
      console.error(`[BatchScheduler] ${schedule.name} 수동 실행 실패:`, error);
      throw error;
    }
  }

  // 스케줄 목록 조회
  getSchedules(): BatchSchedule[] {
    return Array.from(this.schedules.values());
  }

  // 실행 히스토리 조회
  getHistory(limit: number = 20): BatchHistory[] {
    return this.history
      .sort((a, b) => b.startTime.getTime() - a.startTime.getTime())
      .slice(0, limit);
  }

  // 스케줄 활성화/비활성화
  toggleSchedule(scheduleId: string, enabled: boolean) {
    const schedule = this.schedules.get(scheduleId);
    if (!schedule) {
      throw new Error(`스케줄을 찾을 수 없습니다: ${scheduleId}`);
    }

    schedule.enabled = enabled;

    if (enabled) {
      schedule.nextRun = this.calculateNextRun(schedule.cron);
      this.scheduleJob(scheduleId);
    } else {
      const timer = this.timers.get(scheduleId);
      if (timer) {
        clearTimeout(timer);
        this.timers.delete(scheduleId);
      }
    }

    console.log(`[BatchScheduler] ${schedule.name} ${enabled ? '활성화' : '비활성화'}`);
  }
}

// 전역 스케줄러 인스턴스
let globalScheduler: BatchScheduler | null = null;

// 스케줄러 인스턴스 가져오기 (싱글톤)
export function getScheduler(): BatchScheduler {
  if (!globalScheduler) {
    globalScheduler = new BatchScheduler();
  }
  return globalScheduler;
}

// 스케줄러 시작 (서버 시작 시 호출)
export function startScheduler() {
  const scheduler = getScheduler();
  scheduler.start();
}

// 스케줄러 중지 (서버 종료 시 호출)
export function stopScheduler() {
  if (globalScheduler) {
    globalScheduler.stop();
  }
}