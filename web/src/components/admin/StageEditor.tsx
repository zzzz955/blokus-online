'use client';

import { useState, useEffect, useRef } from 'react';
import BoardEditor from './BoardEditor';
import BlockSelector from './BlockSelector';
import { calculateOptimalScoreExact } from '@/lib/blokus/calc';
import ThumbnailPreview from './ThumbnailPreview';
import { BoardState, toLegacyBoardState, fromLegacyBoardState } from '@/lib/board-state-codec';

function withTimeout<T>(promise: Promise<T>, ms: number, message = '계산 시간이 초과되었습니다.'): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(message)), ms);
    promise.then(
      (v) => { clearTimeout(timer); resolve(v); },
      (e) => { clearTimeout(timer); reject(e); }
    );
  });
}

interface Stage {
  stage_id?: number;
  stage_number: number;
  difficulty: number;
  initial_board_state: BoardState; // Changed to int[] format
  available_blocks: number[];
  optimal_score: number;
  time_limit: number | null;
  max_undo_count: number;
  stage_description: string | null;
  stage_hints: string | null;
  thumbnail_url?: string | null;
  is_active: boolean;
  is_featured: boolean;
}

interface StageEditorProps {
  stage: Stage;
  onSave: (stage: Partial<Stage>) => void;
  onCancel: () => void;
}

export default function StageEditor({ stage, onSave, onCancel }: StageEditorProps) {
  const [formData, setFormData] = useState<Stage>({ ...stage, time_limit: stage.time_limit ?? null });
  const [activeTab, setActiveTab] = useState<'basic' | 'board' | 'blocks' | 'advanced'>('basic');
  const [isCalculating, setIsCalculating] = useState(false);
  const [calculatedScore, setCalculatedScore] = useState<number | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const calcSeqRef = useRef(0);

  useEffect(() => {
    validateForm();
  }, [formData]);

  const validateForm = () => {
    const errors: string[] = [];

    if (formData.stage_number <= 0) {
      errors.push('스테이지 번호는 1 이상이어야 합니다.');
    }

    if (formData.difficulty < 1 || formData.difficulty > 10) {
      errors.push('난이도는 1-10 사이여야 합니다.');
    }

    if (formData.available_blocks.length === 0) {
      errors.push('최소 1개 이상의 블록을 선택해야 합니다.');
    }

    if (formData.time_limit !== null && formData.time_limit <= 0) {
      errors.push('제한 시간은 0보다 커야 합니다.');
    }

    if (formData.optimal_score < 0) {
      errors.push('최적 점수는 0 이상이어야 합니다.');
    }

    // Board validation - count entries in int[] format
    const totalOccupied = formData.initial_board_state.length;

    if (totalOccupied >= 300) { // 20x20 = 400, leaving at least 100 empty spaces
      errors.push('보드에 빈 공간이 너무 적습니다. (최소 100개 이상의 빈 칸 필요)');
    }

    setValidationErrors(errors);
  };

  const handleInputChange = (field: keyof Stage, value: any) => {
    setFormData(prev => ({
      ...prev,
      [field]: value
    }));
  };

  const handleBoardStateChange = (boardState: Stage['initial_board_state']) => {
    setFormData(prev => ({
      ...prev,
      initial_board_state: boardState
    }));
  };

  const handleBlockSelectionChange = (blocks: number[]) => {
    setFormData(prev => ({
      ...prev,
      available_blocks: blocks
    }));
  };

  const calculateOptimalScore = async () => {
    if (isCalculating) return;

    setIsCalculating(true);
    const mySeq = ++calcSeqRef.current;  // ✅ ref 사용

    try {
      const score = await withTimeout(
        calculateOptimalScoreExact(
          formData.initial_board_state,
          formData.available_blocks,
          15000,
          1
        ),
        30000
      );

      // ✅ 최신 실행만 반영
      if (mySeq !== calcSeqRef.current) return;

      setCalculatedScore(score);
      setFormData(prev => ({ ...prev, optimal_score: score }));
    } catch (err: any) {
      if (mySeq !== calcSeqRef.current) return;
      console.error(err);
      alert(err?.message || '최적 점수 계산 중 오류가 발생했습니다.');
    } finally {
      if (mySeq === calcSeqRef.current) setIsCalculating(false);
    }
  };

  const handleSave = () => {
    if (validationErrors.length > 0) {
      alert('폼 유효성 검사 실패:\n' + validationErrors.join('\n'));
      return;
    }

    onSave(formData);
  };

  const getDifficultyLabel = (difficulty: number) => {
    const labels = {
      1: '매우 쉬움', 2: '쉬움', 3: '보통', 4: '어려움',
      5: '매우 어려움', 6: '극한', 7: '악몽', 8: '지옥',
      9: '전설', 10: '신화'
    };
    return labels[difficulty as keyof typeof labels] || '알 수 없음';
  };

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
      <div className="bg-dark-card border border-dark-border rounded-lg overflow-hidden">
        {/* Header */}
        <div className="bg-dark-bg border-b border-dark-border px-6 py-4">
          <div className="flex justify-between items-center">
            <div>
              <h2 className="text-xl font-bold text-white">
                {stage.stage_id ? `스테이지 #${formData.stage_number} 편집` : '새 스테이지 생성'}
              </h2>
              <p className="text-gray-400 text-sm mt-1">
                {stage.stage_id ? '기존 스테이지를 수정합니다' : '새로운 스테이지를 생성합니다'}
              </p>
            </div>
            <div className="flex gap-3">
              <button
                onClick={onCancel}
                className="px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-lg transition-colors"
              >
                취소
              </button>
              <button
                onClick={handleSave}
                disabled={validationErrors.length > 0}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-white rounded-lg transition-colors"
              >
                저장
              </button>
            </div>
          </div>

          {/* Validation Errors */}
          {validationErrors.length > 0 && (
            <div className="mt-4 bg-red-500/10 border border-red-500/30 rounded-lg p-3">
              <div className="flex items-center mb-2">
                <svg className="w-4 h-4 text-red-400 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span className="text-red-400 font-medium">유효성 검사 오류</span>
              </div>
              <ul className="text-red-300 text-sm space-y-1">
                {validationErrors.map((error, index) => (
                  <li key={index}>• {error}</li>
                ))}
              </ul>
            </div>
          )}
        </div>

        {/* Tab Navigation */}
        <div className="border-b border-dark-border">
          <div className="px-6">
            <nav className="flex gap-8">
              {[
                { key: 'basic', label: '기본 정보' },
                { key: 'board', label: '보드 편집' },
                { key: 'blocks', label: '블록 설정' },
                { key: 'advanced', label: '고급 설정' }
              ].map((tab) => (
                <button
                  key={tab.key}
                  onClick={() => setActiveTab(tab.key as any)}
                  className={`py-4 px-2 border-b-2 font-medium text-sm transition-colors ${activeTab === tab.key
                    ? 'border-blue-500 text-blue-400'
                    : 'border-transparent text-gray-400 hover:text-gray-300 hover:border-gray-600'
                    }`}
                >
                  {tab.label}
                </button>
              ))}
            </nav>
          </div>
        </div>

        {/* Tab Content */}
        <div className="p-6">
          {/* Basic Info Tab */}
          {activeTab === 'basic' && (
            <div className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">
                    스테이지 번호 *
                  </label>
                  <input
                    type="number"
                    min="1"
                    value={formData.stage_number}
                    onChange={(e) => handleInputChange('stage_number', parseInt(e.target.value) || 1)}
                    className="w-full px-3 py-2 bg-dark-bg border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">
                    난이도 * ({getDifficultyLabel(formData.difficulty)})
                  </label>
                  <input
                    type="range"
                    min="1"
                    max="10"
                    value={formData.difficulty}
                    onChange={(e) => handleInputChange('difficulty', parseInt(e.target.value))}
                    className="w-full h-2 bg-gray-700 rounded-lg appearance-none cursor-pointer slider"
                  />
                  <div className="flex justify-between text-xs text-gray-400 mt-1">
                    <span>1 (쉬움)</span>
                    <span className="text-yellow-400">{formData.difficulty}</span>
                    <span>10 (신화)</span>
                  </div>
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-300 mb-2">
                  스테이지 설명
                </label>
                <textarea
                  value={formData.stage_description || ''}
                  onChange={(e) => handleInputChange('stage_description', e.target.value || null)}
                  rows={3}
                  className="w-full px-3 py-2 bg-dark-bg border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none"
                  placeholder="플레이어에게 보여질 스테이지 설명을 입력하세요..."
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-300 mb-2">
                  힌트 (각 줄이 하나의 힌트)
                </label>
                <textarea
                  value={formData.stage_hints || ''}
                  onChange={(e) => handleInputChange('stage_hints', e.target.value || null)}
                  rows={4}
                  className="w-full px-3 py-2 bg-dark-bg border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none"
                  placeholder="코너에서 시작하세요&#10;큰 블록부터 배치하세요&#10;장애물을 피해 배치하세요"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">
                    제한 시간 (초)
                  </label>
                  <div className="flex gap-2">
                    <input
                      type="number"
                      min="0"
                      value={formData.time_limit || ''}
                      onChange={(e) => handleInputChange('time_limit', e.target.value ? parseInt(e.target.value) : null)}
                      className="flex-1 px-3 py-2 bg-dark-bg border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                      placeholder="제한 없음"
                    />
                    <button
                      onClick={() => handleInputChange('time_limit', null)}
                      className="px-3 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-lg transition-colors"
                    >
                      무제한
                    </button>
                  </div>
                  <p className="text-xs text-gray-400 mt-1">
                    {formData.time_limit ?
                      `${Math.floor(formData.time_limit / 60)}분 ${formData.time_limit % 60}초` :
                      '시간 제한 없음'}
                  </p>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">
                    최대 되돌리기 횟수
                  </label>
                  <input
                    type="number"
                    min="0"
                    max="10"
                    value={formData.max_undo_count}
                    onChange={(e) => handleInputChange('max_undo_count', parseInt(e.target.value) || 0)}
                    className="w-full px-3 py-2 bg-dark-bg border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                </div>
              </div>

              <div className="flex gap-4">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.is_active}
                    onChange={(e) => handleInputChange('is_active', e.target.checked)}
                    className="rounded border-gray-600 text-blue-600 focus:ring-blue-500 focus:ring-2"
                  />
                  <span className="text-gray-300">스테이지 활성화</span>
                </label>

                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.is_featured}
                    onChange={(e) => handleInputChange('is_featured', e.target.checked)}
                    className="rounded border-gray-600 text-blue-600 focus:ring-blue-500 focus:ring-2"
                  />
                  <span className="text-gray-300">추천 스테이지</span>
                </label>
              </div>
            </div>
          )}

          {/* Board Editor Tab */}
          {activeTab === 'board' && (
            <div>
              <div className="mb-4">
                <h3 className="text-lg font-medium text-white mb-2">보드 상태 편집</h3>
                <p className="text-gray-400 text-sm">
                  클릭하여 장애물을 배치하거나 제거하고, 우클릭으로 미리 배치된 블록을 설정하세요.
                </p>
              </div>
              <BoardEditor
                boardState={formData.initial_board_state}
                onChange={handleBoardStateChange}
              />
            </div>
          )}

          {/* Block Selector Tab */}
          {activeTab === 'blocks' && (
            <div>
              <div className="mb-4">
                <h3 className="text-lg font-medium text-white mb-2">사용 가능한 블록 설정</h3>
                <p className="text-gray-400 text-sm">
                  이 스테이지에서 플레이어가 사용할 수 있는 블록을 선택하세요.
                </p>
              </div>
              <BlockSelector
                selectedBlocks={formData.available_blocks}
                onChange={handleBlockSelectionChange}
              />
            </div>
          )}

          {/* Advanced Settings Tab */}
          {activeTab === 'advanced' && (
            <div className="space-y-6">
              <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
                <h3 className="text-lg font-medium text-white mb-4">최적 점수 계산</h3>
                <p className="text-gray-400 text-sm mb-4">
                  실제 블로쿠스 규칙을 적용하여 최대 점수를 계산합니다.
                  (모서리 시작, 대각선 인접, 면 인접 금지 규칙 적용)
                </p>
                <div className="bg-blue-500/10 border border-blue-500/30 rounded-lg p-3 mb-4">
                  <p className="text-blue-300 text-xs">
                    <strong>블로쿠스 규칙:</strong><br />
                    • 첫 블록: 보드 모서리 (0,0), (0,19), (19,0), (19,19) 중 하나에 배치<br />
                    • 이후 블록: 같은 색 블록과 대각선으로 인접 (모서리 연결)<br />
                    • 금지사항: 같은 색 블록과 면(상하좌우)으로 인접 불가
                  </p>
                </div>

                <div className="flex items-center gap-4">
                  <div className="flex-1">
                    <label className="block text-sm font-medium text-gray-300 mb-2">
                      최적 점수
                    </label>
                    <input
                      type="number"
                      min="0"
                      value={formData.optimal_score}
                      onChange={(e) => handleInputChange('optimal_score', parseInt(e.target.value) || 0)}
                      className="w-full px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    />
                  </div>
                  <div className="pt-6">
                    <button
                      onClick={calculateOptimalScore}
                      disabled={isCalculating}
                      className="px-4 py-2 bg-green-600 hover:bg-green-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-white rounded-lg transition-colors flex items-center gap-2"
                    >
                      {isCalculating && (
                        <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                      )}
                      {isCalculating ? '계산 중...' : '자동 계산'}
                    </button>
                  </div>
                </div>

                {calculatedScore !== null && (
                  <div className="mt-3 p-3 bg-green-500/10 border border-green-500/30 rounded-lg">
                    <p className="text-green-400 text-sm">
                      ✓ 계산 완료: 최적 점수는 {calculatedScore}점입니다.
                    </p>
                  </div>
                )}
              </div>

              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* Thumbnail Settings */}
                <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
                  <h3 className="text-lg font-medium text-white mb-4">썸네일 설정</h3>
                  <div>
                    <label className="block text-sm font-medium text-gray-300 mb-2">
                      썸네일 URL (선택사항)
                    </label>
                    <input
                      type="url"
                      value={formData.thumbnail_url || ''}
                      onChange={(e) => handleInputChange('thumbnail_url', e.target.value || null)}
                      className="w-full px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                      placeholder="https://example.com/thumbnail.jpg"
                    />
                    <p className="text-xs text-gray-400 mt-1">
                      비워두면 자동으로 보드 상태 기반 썸네일이 생성됩니다.
                    </p>
                  </div>
                </div>

                {/* Thumbnail Preview */}
                <ThumbnailPreview
                  boardState={formData.initial_board_state}
                  stageNumber={formData.stage_number}
                  onThumbnailGenerated={(dataUrl) => {
                    // Optional: Auto-fill thumbnail URL if empty
                    if (!formData.thumbnail_url) {
                      console.log('Generated thumbnail preview:', dataUrl.substring(0, 50) + '...');
                    }
                  }}
                />
              </div>

              <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
                <h3 className="text-lg font-medium text-white mb-4">스테이지 통계</h3>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                  <div>
                    <span className="text-gray-400">장애물 수:</span>
                    <span className="text-white ml-2">{toLegacyBoardState(formData.initial_board_state).obstacles.length}</span>
                  </div>
                  <div>
                    <span className="text-gray-400">미리 배치된 블록:</span>
                    <span className="text-white ml-2">{toLegacyBoardState(formData.initial_board_state).preplaced.length}</span>
                  </div>
                  <div>
                    <span className="text-gray-400">사용 가능한 블록:</span>
                    <span className="text-white ml-2">{formData.available_blocks.length}개</span>
                  </div>
                  <div>
                    <span className="text-gray-400">빈 공간:</span>
                    <span className="text-white ml-2">
                      {400 - formData.initial_board_state.length}칸
                    </span>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}