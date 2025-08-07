// ========================================
// 관리자 배치 작업 관리 페이지
// ========================================
// 배치 스케줄 관리 및 실행 히스토리 확인
// ========================================

'use client';

import { useState, useEffect } from 'react';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { api } from '@/utils/api';
import { formatDateTime } from '@/utils/format';

interface BatchSchedule {
  id: string;
  name: string;
  cron: string;
  enabled: boolean;
  lastRun?: string;
  nextRun: string;
  status: 'idle' | 'running' | 'failed';
  retryCount: number;
  maxRetries: number;
}

interface BatchHistory {
  id: string;
  scheduleId: string;
  startTime: string;
  endTime?: string;
  status: 'running' | 'success' | 'failed';
  result?: {
    success: boolean;
    processed: number;
    updated: number;
    created: number;
    errors: string[];
    executionTime: number;
  };
  error?: string;
}

export default function AdminBatchPage() {
  const [schedules, setSchedules] = useState<BatchSchedule[]>([]);
  const [history, setHistory] = useState<BatchHistory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [running, setRunning] = useState<string | null>(null);

  // 데이터 로드
  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await api.get<{
        schedules: BatchSchedule[];
        history: BatchHistory[];
      }>('/api/admin/batch');

      setSchedules(response.schedules);
      setHistory(response.history);
    } catch (err) {
      console.error('배치 데이터 로드 실패:', err);
      setError('배치 데이터를 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  // 수동 실행
  const runManually = async (scheduleId: string) => {
    try {
      setRunning(scheduleId);
      
      const result = await api.post<{
        success: boolean;
        processed: number;
        updated: number;
        created: number;
        errors: string[];
        executionTime: number;
      }>(`/api/admin/batch/run`, { scheduleId });
      
      alert(`배치 실행 완료!\n처리된 항목: ${result.processed}\n실행 시간: ${result.executionTime}ms`);
      
      // 데이터 새로고침
      await loadData();
    } catch (err) {
      console.error('배치 수동 실행 실패:', err);
      alert('배치 실행에 실패했습니다.');
    } finally {
      setRunning(null);
    }
  };

  // 스케줄 토글
  const toggleSchedule = async (scheduleId: string, enabled: boolean) => {
    try {
      await api.put('/api/admin/batch/toggle', { scheduleId, enabled });
      
      // 로컬 상태 업데이트
      setSchedules(schedules.map(schedule => 
        schedule.id === scheduleId 
          ? { ...schedule, enabled }
          : schedule
      ));
    } catch (err) {
      console.error('스케줄 토글 실패:', err);
      alert('스케줄 변경에 실패했습니다.');
    }
  };

  // 상태별 배지 스타일
  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'idle':
        return 'bg-gray-500/20 text-gray-300';
      case 'running':
        return 'bg-blue-500/20 text-blue-300';
      case 'failed':
        return 'bg-red-500/20 text-red-300';
      case 'success':
        return 'bg-green-500/20 text-green-300';
      default:
        return 'bg-gray-500/20 text-gray-300';
    }
  };

  // 상태 한글 변환
  const getStatusText = (status: string) => {
    switch (status) {
      case 'idle': return '대기중';
      case 'running': return '실행중';
      case 'failed': return '실패';
      case 'success': return '성공';
      default: return status;
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  if (loading) {
    return (
      <div className="p-8 max-w-7xl mx-auto">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500 mx-auto"></div>
          <p className="mt-4 text-gray-300">배치 데이터를 불러오는 중...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-8 max-w-7xl mx-auto">
        <Card className="p-8 text-center">
          <div className="text-red-400 mb-4">
            <svg className="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold text-white mb-2">데이터 로드 실패</h2>
          <p className="text-gray-300 mb-4">{error}</p>
          <Button onClick={loadData}>다시 시도</Button>
        </Card>
      </div>
    );
  }

  return (
    <div className="p-8 max-w-7xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-white">배치 작업 관리</h1>
        <p className="mt-2 text-gray-300">게임 통계 배치 처리 및 스케줄 관리</p>
      </div>

      {/* 배치 스케줄 */}
      <Card className="p-6 mb-8">
        <div className="flex justify-between items-center mb-6">
          <h2 className="text-xl font-semibold text-white">배치 스케줄</h2>
          <Button onClick={loadData} className="flex items-center space-x-2">
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            <span>새로고침</span>
          </Button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-dark-border">
                <th className="text-left py-3 px-4 font-semibold text-white">작업명</th>
                <th className="text-center py-3 px-4 font-semibold text-white">스케줄</th>
                <th className="text-center py-3 px-4 font-semibold text-white">상태</th>
                <th className="text-center py-3 px-4 font-semibold text-white">마지막 실행</th>
                <th className="text-center py-3 px-4 font-semibold text-white">다음 실행</th>
                <th className="text-center py-3 px-4 font-semibold text-white">활성화</th>
                <th className="text-center py-3 px-4 font-semibold text-white">작업</th>
              </tr>
            </thead>
            <tbody>
              {schedules.map((schedule) => (
                <tr key={schedule.id} className="border-b border-dark-border">
                  <td className="py-3 px-4 font-medium text-white">{schedule.name}</td>
                  <td className="py-3 px-4 text-center text-sm text-gray-300">{schedule.cron}</td>
                  <td className="py-3 px-4 text-center">
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${getStatusBadge(schedule.status)}`}>
                      {getStatusText(schedule.status)}
                    </span>
                    {schedule.retryCount > 0 && (
                      <div className="text-xs text-red-400 mt-1">
                        재시도: {schedule.retryCount}/{schedule.maxRetries}
                      </div>
                    )}
                  </td>
                  <td className="py-3 px-4 text-center text-sm text-gray-300">
                    {schedule.lastRun ? formatDateTime(new Date(schedule.lastRun)) : '-'}
                  </td>
                  <td className="py-3 px-4 text-center text-sm text-gray-300">
                    {formatDateTime(new Date(schedule.nextRun))}
                  </td>
                  <td className="py-3 px-4 text-center">
                    <label className="inline-flex items-center">
                      <input
                        type="checkbox"
                        checked={schedule.enabled}
                        onChange={(e) => toggleSchedule(schedule.id, e.target.checked)}
                        className="form-checkbox h-4 w-4 text-primary-500 bg-dark-card border-dark-border"
                      />
                    </label>
                  </td>
                  <td className="py-3 px-4 text-center">
                    <Button
                      onClick={() => runManually(schedule.id)}
                      disabled={running === schedule.id || schedule.status === 'running'}
                      className="text-sm"
                    >
                      {running === schedule.id ? '실행중...' : '수동 실행'}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {/* 실행 히스토리 */}
      <Card className="p-6">
        <h2 className="text-xl font-semibold text-white mb-6">실행 히스토리 (최근 20개)</h2>
        
        {history.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-gray-200">
                  <th className="text-left py-3 px-4 font-semibold text-white">시작 시간</th>
                  <th className="text-center py-3 px-4 font-semibold text-white">상태</th>
                  <th className="text-center py-3 px-4 font-semibold text-white">처리된 항목</th>
                  <th className="text-center py-3 px-4 font-semibold text-white">실행 시간</th>
                  <th className="text-center py-3 px-4 font-semibold text-white">결과</th>
                </tr>
              </thead>
              <tbody>
                {history.map((entry) => (
                  <tr key={entry.id} className="border-b border-dark-border">
                    <td className="py-3 px-4 text-sm text-gray-300">
                      {formatDateTime(new Date(entry.startTime))}
                    </td>
                    <td className="py-3 px-4 text-center">
                      <span className={`px-2 py-1 rounded-full text-xs font-medium ${getStatusBadge(entry.status)}`}>
                        {getStatusText(entry.status)}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-center text-sm text-gray-300">
                      {entry.result ? entry.result.processed : '-'}
                    </td>
                    <td className="py-3 px-4 text-center text-sm text-gray-300">
                      {entry.result ? `${entry.result.executionTime}ms` : '-'}
                    </td>
                    <td className="py-3 px-4 text-center text-sm">
                      {entry.result && (
                        <div className="text-left">
                          <div className="text-green-400">생성: {entry.result.created}</div>
                          <div className="text-blue-400">업데이트: {entry.result.updated}</div>
                          {entry.result.errors.length > 0 && (
                            <div className="text-red-400">오류: {entry.result.errors.length}</div>
                          )}
                        </div>
                      )}
                      {entry.error && (
                        <div className="text-red-400 text-xs">
                          {entry.error.length > 50 ? entry.error.substring(0, 50) + '...' : entry.error}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="text-center py-8 text-gray-400">
            실행 히스토리가 없습니다.
          </div>
        )}
      </Card>
    </div>
  );
}