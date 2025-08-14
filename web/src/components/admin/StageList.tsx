'use client';

import { useState } from 'react';

interface Stage {
  stage_id: number;
  stage_number: number;
  difficulty: number;
  initial_board_state: {
    obstacles: Array<{x: number, y: number}>;
    preplaced: Array<{x: number, y: number, color: number}>;
  };
  available_blocks: number[];
  optimal_score: number;
  time_limit: number | null;
  max_undo_count: number;
  stage_description: string | null;
  stage_hints: string | null;
  thumbnail_url: string | null;
  is_active: boolean;
  is_featured: boolean;
  created_at: string;
  updated_at: string;
}

interface StageListProps {
  stages: Stage[];
  onEditStage: (stage: Stage) => void;
  onDeleteStage: (stageId: number) => void;
}

export default function StageList({ stages, onEditStage, onDeleteStage }: StageListProps) {
  const [sortField, setSortField] = useState<keyof Stage>('stage_number');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [filterDifficulty, setFilterDifficulty] = useState<number | null>(null);
  const [filterActive, setFilterActive] = useState<boolean | null>(null);
  const [searchTerm, setSearchTerm] = useState('');

  const handleSort = (field: keyof Stage) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const getDifficultyColor = (difficulty: number) => {
    if (difficulty <= 2) return 'text-green-400';
    if (difficulty <= 4) return 'text-yellow-400';
    if (difficulty <= 6) return 'text-orange-400';
    return 'text-red-400';
  };

  const getDifficultyLabel = (difficulty: number) => {
    if (difficulty <= 2) return '쉬움';
    if (difficulty <= 4) return '보통';
    if (difficulty <= 6) return '어려움';
    return '매우 어려움';
  };

  const formatTimeLimit = (seconds: number | null) => {
    if (!seconds) return '제한 없음';
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return `${minutes}분 ${remainingSeconds}초`;
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('ko-KR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    });
  };

  // Filter and sort stages
  const filteredAndSortedStages = stages
    .filter(stage => {
      const matchesSearch = searchTerm === '' || 
        stage.stage_number.toString().includes(searchTerm) ||
        stage.stage_description?.toLowerCase().includes(searchTerm.toLowerCase());
      
      const matchesDifficulty = filterDifficulty === null || stage.difficulty === filterDifficulty;
      const matchesActive = filterActive === null || stage.is_active === filterActive;
      
      return matchesSearch && matchesDifficulty && matchesActive;
    })
    .sort((a, b) => {
      const aValue = a[sortField];
      const bValue = b[sortField];
      
      let comparison = 0;
      if (aValue < bValue) comparison = -1;
      if (aValue > bValue) comparison = 1;
      
      return sortDirection === 'asc' ? comparison : -comparison;
    });

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-400 mb-2">검색</label>
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="스테이지 번호 또는 설명..."
              className="w-full px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white placeholder-gray-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-400 mb-2">난이도</label>
            <select
              value={filterDifficulty || ''}
              onChange={(e) => setFilterDifficulty(e.target.value ? parseInt(e.target.value) : null)}
              className="w-full px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            >
              <option value="">전체</option>
              <option value="1">1 - 매우 쉬움</option>
              <option value="2">2 - 쉬움</option>
              <option value="3">3 - 보통</option>
              <option value="4">4 - 어려움</option>
              <option value="5">5 - 매우 어려움</option>
              <option value="6">6 - 최고 난이도</option>
            </select>
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-400 mb-2">상태</label>
            <select
              value={filterActive === null ? '' : filterActive.toString()}
              onChange={(e) => setFilterActive(e.target.value === '' ? null : e.target.value === 'true')}
              className="w-full px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            >
              <option value="">전체</option>
              <option value="true">활성</option>
              <option value="false">비활성</option>
            </select>
          </div>
          
          <div className="flex items-end">
            <button
              onClick={() => {
                setSearchTerm('');
                setFilterDifficulty(null);
                setFilterActive(null);
              }}
              className="w-full px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-lg transition-colors"
            >
              필터 초기화
            </button>
          </div>
        </div>
      </div>

      {/* Results Info */}
      <div className="text-sm text-gray-400">
        총 {filteredAndSortedStages.length}개의 스테이지 (전체 {stages.length}개 중)
      </div>

      {/* Table */}
      <div className="bg-dark-card border border-dark-border rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-dark-bg border-b border-dark-border">
              <tr>
                <th 
                  className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:text-white transition-colors"
                  onClick={() => handleSort('stage_number')}
                >
                  번호 {sortField === 'stage_number' && (sortDirection === 'asc' ? '↑' : '↓')}
                </th>
                <th 
                  className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:text-white transition-colors"
                  onClick={() => handleSort('difficulty')}
                >
                  난이도 {sortField === 'difficulty' && (sortDirection === 'asc' ? '↑' : '↓')}
                </th>
                <th className="px-4 py-3 text-left text-sm font-medium text-gray-300">설명</th>
                <th className="px-4 py-3 text-left text-sm font-medium text-gray-300">최적 점수</th>
                <th className="px-4 py-3 text-left text-sm font-medium text-gray-300">제한 시간</th>
                <th className="px-4 py-3 text-left text-sm font-medium text-gray-300">블록 수</th>
                <th className="px-4 py-3 text-left text-sm font-medium text-gray-300">상태</th>
                <th 
                  className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:text-white transition-colors"
                  onClick={() => handleSort('created_at')}
                >
                  생성일 {sortField === 'created_at' && (sortDirection === 'asc' ? '↑' : '↓')}
                </th>
                <th className="px-4 py-3 text-right text-sm font-medium text-gray-300">작업</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-dark-border">
              {filteredAndSortedStages.map((stage) => (
                <tr key={stage.stage_id} className="hover:bg-dark-bg/50 transition-colors">
                  <td className="px-4 py-3 text-white font-mono">
                    #{stage.stage_number}
                    {stage.is_featured && (
                      <span className="ml-2 text-xs bg-yellow-500/20 text-yellow-400 px-2 py-1 rounded">
                        추천
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`font-medium ${getDifficultyColor(stage.difficulty)}`}>
                      {stage.difficulty} - {getDifficultyLabel(stage.difficulty)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-300 max-w-xs truncate">
                    {stage.stage_description || '설명 없음'}
                  </td>
                  <td className="px-4 py-3 text-blue-400 font-mono">
                    {stage.optimal_score}점
                  </td>
                  <td className="px-4 py-3 text-gray-300">
                    {formatTimeLimit(stage.time_limit)}
                  </td>
                  <td className="px-4 py-3 text-gray-300">
                    {stage.available_blocks.length}개
                  </td>
                  <td className="px-4 py-3">
                    <span className={`text-xs px-2 py-1 rounded ${
                      stage.is_active 
                        ? 'bg-green-500/20 text-green-400' 
                        : 'bg-red-500/20 text-red-400'
                    }`}>
                      {stage.is_active ? '활성' : '비활성'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-400 text-sm">
                    {formatDate(stage.created_at)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex gap-2 justify-end">
                      <button
                        onClick={() => onEditStage(stage)}
                        className="text-blue-400 hover:text-blue-300 transition-colors"
                        title="편집"
                      >
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                        </svg>
                      </button>
                      <button
                        onClick={() => onDeleteStage(stage.stage_id)}
                        className="text-red-400 hover:text-red-300 transition-colors"
                        title="삭제"
                      >
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                        </svg>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          
          {filteredAndSortedStages.length === 0 && (
            <div className="text-center py-8 text-gray-400">
              조건에 맞는 스테이지가 없습니다.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}