// ========================================
// 게임 통계 페이지
// ========================================
// 전역 통계와 사용자 랭킹을 표시하는 페이지
// ========================================

'use client';

import { useState, useEffect } from 'react';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { api } from '@/utils/api';
import { formatDateTime } from '@/utils/format';

interface GlobalStats {
  totalUsers: number;
  totalGames: number;
  avgGamesPerUser: number;
  topPlayerByWins: {
    username: string;
    wins: number;
  } | null;
  topPlayerByScore: {
    username: string;
    best_score: number;
  } | null;
  recentActivity: {
    last7Days: number;
    last30Days: number;
  };
}

interface UserRanking {
  rank: number;
  user_id: number;
  username: string;
  wins: number;
  total_games: number;
  win_rate: number;
  best_score: number;
  level: number;
}

export default function StatsPage() {
  const [globalStats, setGlobalStats] = useState<GlobalStats | null>(null);
  const [ranking, setRanking] = useState<UserRanking[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  // 데이터 로드 함수
  const loadStats = async (showRefreshIndicator = false) => {
    try {
      if (showRefreshIndicator) {
        setRefreshing(true);
      } else {
        setLoading(true);
      }
      setError(null);

      const [statsResponse, rankingResponse] = await Promise.all([
        api.get<GlobalStats>('/api/stats'),
        api.get<UserRanking[]>('/api/stats/ranking?limit=20')
      ]);

      setGlobalStats(statsResponse);
      setRanking(rankingResponse);
    } catch (err) {
      console.error('통계 데이터 로드 실패:', err);
      setError('통계 데이터를 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  // 컴포넌트 마운트 시 데이터 로드
  useEffect(() => {
    loadStats();
  }, []);

  // 새로고침 핸들러
  const handleRefresh = () => {
    loadStats(true);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 py-8">
        <div className="max-w-7xl mx-auto px-4">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
            <p className="mt-4 text-gray-600">통계 데이터를 불러오는 중...</p>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gray-50 py-8">
        <div className="max-w-7xl mx-auto px-4">
          <Card className="p-8 text-center">
            <div className="text-red-600 mb-4">
              <svg className="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <h2 className="text-xl font-semibold text-gray-900 mb-2">통계 로드 실패</h2>
            <p className="text-gray-600 mb-4">{error}</p>
            <Button onClick={handleRefresh}>다시 시도</Button>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-7xl mx-auto px-4">
        {/* 헤더 */}
        <div className="mb-8 flex justify-between items-center">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">게임 통계</h1>
            <p className="mt-2 text-gray-600">블로쿠스 온라인 게임 통계 및 랭킹</p>
          </div>
          <Button 
            onClick={handleRefresh}
            disabled={refreshing}
            className="flex items-center space-x-2"
          >
            <svg 
              className={`w-4 h-4 ${refreshing ? 'animate-spin' : ''}`} 
              fill="none" 
              stroke="currentColor" 
              viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            <span>{refreshing ? '새로고침 중...' : '새로고침'}</span>
          </Button>
        </div>

        {/* 전역 통계 */}
        {globalStats && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
            <Card className="p-6">
              <div className="text-2xl font-bold text-blue-600">{globalStats.totalUsers.toLocaleString()}</div>
              <div className="text-sm text-gray-600">총 사용자</div>
            </Card>
            
            <Card className="p-6">
              <div className="text-2xl font-bold text-green-600">{globalStats.totalGames.toLocaleString()}</div>
              <div className="text-sm text-gray-600">총 게임 수</div>
            </Card>
            
            <Card className="p-6">
              <div className="text-2xl font-bold text-purple-600">{globalStats.avgGamesPerUser.toFixed(1)}</div>
              <div className="text-sm text-gray-600">사용자당 평균 게임</div>
            </Card>
            
            <Card className="p-6">
              <div className="text-2xl font-bold text-orange-600">{globalStats.recentActivity.last7Days}</div>
              <div className="text-sm text-gray-600">지난 7일 활성 사용자</div>
            </Card>
          </div>
        )}

        {/* 톱 플레이어 */}
        {globalStats && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
            <Card className="p-6">
              <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center">
                <svg className="w-5 h-5 text-yellow-500 mr-2" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
                </svg>
                승수 1위
              </h3>
              {globalStats.topPlayerByWins ? (
                <div>
                  <div className="text-xl font-bold text-gray-900">{globalStats.topPlayerByWins.username}</div>
                  <div className="text-2xl font-bold text-yellow-600">{globalStats.topPlayerByWins.wins}승</div>
                </div>
              ) : (
                <div className="text-gray-500">데이터 없음</div>
              )}
            </Card>

            <Card className="p-6">
              <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center">
                <svg className="w-5 h-5 text-red-500 mr-2" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M12.395 2.553a1 1 0 00-1.45-.385c-.345.23-.614.558-.822.88-.214.33-.403.713-.57 1.116-.334.804-.614 1.768-.84 2.734a31.365 31.365 0 00-.613 3.58 2.64 2.64 0 01-.945-1.067c-.328-.68-.398-1.534-.398-2.654A1 1 0 005.05 6.05 6.981 6.981 0 003 11a7 7 0 1011.95-4.95c-.592-.591-.98-.985-1.348-1.467-.363-.476-.724-1.063-1.207-2.03zM12.12 15.12A3 3 0 017 13s.879.5 2.5.5c0-1 .5-4 1.25-4.5.5 1 .786 1.293 1.371 1.879A2.99 2.99 0 0113 13a2.99 2.99 0 01-.879 2.121z" clipRule="evenodd" />
                </svg>
                최고 점수 1위
              </h3>
              {globalStats.topPlayerByScore ? (
                <div>
                  <div className="text-xl font-bold text-gray-900">{globalStats.topPlayerByScore.username}</div>
                  <div className="text-2xl font-bold text-red-600">{globalStats.topPlayerByScore.best_score}점</div>
                </div>
              ) : (
                <div className="text-gray-500">데이터 없음</div>
              )}
            </Card>
          </div>
        )}

        {/* 사용자 랭킹 */}
        <Card className="p-6">
          <h3 className="text-xl font-semibold text-gray-900 mb-6">사용자 랭킹 (상위 20명)</h3>
          
          {ranking.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-gray-200">
                    <th className="text-left py-3 px-4 font-semibold text-gray-900">순위</th>
                    <th className="text-left py-3 px-4 font-semibold text-gray-900">사용자명</th>
                    <th className="text-center py-3 px-4 font-semibold text-gray-900">레벨</th>
                    <th className="text-center py-3 px-4 font-semibold text-gray-900">승수</th>
                    <th className="text-center py-3 px-4 font-semibold text-gray-900">총 게임</th>
                    <th className="text-center py-3 px-4 font-semibold text-gray-900">승률</th>
                    <th className="text-center py-3 px-4 font-semibold text-gray-900">최고 점수</th>
                  </tr>
                </thead>
                <tbody>
                  {ranking.map((player) => (
                    <tr key={player.user_id} className="border-b border-gray-100 hover:bg-gray-50">
                      <td className="py-3 px-4">
                        <div className="flex items-center">
                          {player.rank <= 3 && (
                            <svg className={`w-5 h-5 mr-2 ${
                              player.rank === 1 ? 'text-yellow-500' : 
                              player.rank === 2 ? 'text-gray-400' : 
                              'text-orange-600'
                            }`} fill="currentColor" viewBox="0 0 20 20">
                              <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
                            </svg>
                          )}
                          <span className="font-semibold">{player.rank}</span>
                        </div>
                      </td>
                      <td className="py-3 px-4 font-medium text-gray-900">{player.username}</td>
                      <td className="py-3 px-4 text-center">
                        <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded-full text-sm font-medium">
                          Lv.{player.level}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-center font-semibold text-green-600">{player.wins}</td>
                      <td className="py-3 px-4 text-center text-gray-600">{player.total_games}</td>
                      <td className="py-3 px-4 text-center">
                        <span className={`font-semibold ${
                          player.win_rate >= 70 ? 'text-green-600' :
                          player.win_rate >= 50 ? 'text-yellow-600' : 'text-red-600'
                        }`}>
                          {player.win_rate.toFixed(1)}%
                        </span>
                      </td>
                      <td className="py-3 px-4 text-center font-semibold text-purple-600">
                        {player.best_score.toLocaleString()}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="text-center py-8 text-gray-500">
              아직 랭킹 데이터가 없습니다.
            </div>
          )}
        </Card>

        {/* 마지막 업데이트 시간 */}
        <div className="mt-8 text-center text-sm text-gray-500">
          마지막 업데이트: {formatDateTime(new Date())}
        </div>
      </div>
    </div>
  );
}