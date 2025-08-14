'use client';

import { useState } from 'react';

interface BlockSelectorProps {
  selectedBlocks: number[];
  onChange: (blocks: number[]) => void;
}

// Block definitions matching Unity's BlokusTypes.cs
const BLOCK_TYPES = [
  // 1칸 블록
  { id: 1, name: 'Single', size: 1, cells: 1, category: '1칸' },
  
  // 2칸 블록  
  { id: 2, name: 'Domino', size: 2, cells: 2, category: '2칸' },
  
  // 3칸 블록
  { id: 3, name: 'TrioLine', size: 3, cells: 3, category: '3칸' },
  { id: 4, name: 'TrioAngle', size: 3, cells: 3, category: '3칸' },
  
  // 4칸 블록 (테트로미노)
  { id: 5, name: 'Tetro_I', size: 4, cells: 4, category: '4칸 (테트로)' },
  { id: 6, name: 'Tetro_O', size: 4, cells: 4, category: '4칸 (테트로)' },
  { id: 7, name: 'Tetro_T', size: 4, cells: 4, category: '4칸 (테트로)' },
  { id: 8, name: 'Tetro_L', size: 4, cells: 4, category: '4칸 (테트로)' },
  { id: 9, name: 'Tetro_S', size: 4, cells: 4, category: '4칸 (테트로)' },
  
  // 5칸 블록 (펜토미노)
  { id: 10, name: 'Pento_F', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 11, name: 'Pento_I', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 12, name: 'Pento_L', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 13, name: 'Pento_N', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 14, name: 'Pento_P', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 15, name: 'Pento_T', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 16, name: 'Pento_U', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 17, name: 'Pento_V', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 18, name: 'Pento_W', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 19, name: 'Pento_X', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 20, name: 'Pento_Y', size: 5, cells: 5, category: '5칸 (펜토)' },
  { id: 21, name: 'Pento_Z', size: 5, cells: 5, category: '5칸 (펜토)' }
];

// Visual representations of blocks (simplified)
const BLOCK_SHAPES: { [key: number]: number[][] } = {
  1: [[1]], // Single
  2: [[1, 1]], // Domino
  3: [[1, 1, 1]], // TrioLine
  4: [[1, 1], [1, 0]], // TrioAngle
  5: [[1, 1, 1, 1]], // Tetro_I
  6: [[1, 1], [1, 1]], // Tetro_O
  7: [[0, 1, 0], [1, 1, 1]], // Tetro_T
  8: [[1, 0, 0], [1, 1, 1]], // Tetro_L
  9: [[1, 1, 0], [0, 1, 1]], // Tetro_S
  10: [[0, 1, 1], [1, 1, 0], [0, 1, 0]], // Pento_F
  11: [[1, 1, 1, 1, 1]], // Pento_I
  12: [[1, 0, 0, 0], [1, 1, 1, 1]], // Pento_L
  13: [[1, 1, 0, 0], [0, 1, 1, 1]], // Pento_N
  14: [[1, 1], [1, 1], [1, 0]], // Pento_P
  15: [[1, 1, 1], [0, 1, 0], [0, 1, 0]], // Pento_T
  16: [[1, 0, 1], [1, 1, 1]], // Pento_U
  17: [[1, 0, 0], [1, 0, 0], [1, 1, 1]], // Pento_V
  18: [[1, 0, 0], [1, 1, 0], [0, 1, 1]], // Pento_W
  19: [[0, 1, 0], [1, 1, 1], [0, 1, 0]], // Pento_X
  20: [[0, 1, 0, 0], [1, 1, 1, 1]], // Pento_Y
  21: [[1, 1, 0], [0, 1, 0], [0, 1, 1]] // Pento_Z
};

export default function BlockSelector({ selectedBlocks, onChange }: BlockSelectorProps) {
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [showPresets, setShowPresets] = useState(true);

  const categories = ['all', '1칸', '2칸', '3칸', '4칸 (테트로)', '5칸 (펜토)'];

  const filteredBlocks = selectedCategory === 'all' 
    ? BLOCK_TYPES 
    : BLOCK_TYPES.filter(block => block.category === selectedCategory);

  const handleBlockToggle = (blockId: number) => {
    const isSelected = selectedBlocks.includes(blockId);
    if (isSelected) {
      onChange(selectedBlocks.filter(id => id !== blockId));
    } else {
      onChange([...selectedBlocks, blockId].sort((a, b) => a - b));
    }
  };

  const handleSelectAll = () => {
    const allIds = BLOCK_TYPES.map(block => block.id);
    onChange(allIds);
  };

  const handleSelectNone = () => {
    onChange([]);
  };

  const handleSelectCategory = (category: string) => {
    const categoryBlocks = category === 'all' 
      ? BLOCK_TYPES 
      : BLOCK_TYPES.filter(block => block.category === category);
    const categoryIds = categoryBlocks.map(block => block.id);
    const newSelection = [...new Set([...selectedBlocks, ...categoryIds])];
    onChange(newSelection.sort((a, b) => a - b));
  };

  const handleDeselectCategory = (category: string) => {
    const categoryBlocks = category === 'all' 
      ? BLOCK_TYPES 
      : BLOCK_TYPES.filter(block => block.category === category);
    const categoryIds = categoryBlocks.map(block => block.id);
    onChange(selectedBlocks.filter(id => !categoryIds.includes(id)));
  };

  // Preset configurations
  const presets = [
    { name: '튜토리얼 (1-3칸)', blocks: [1, 2, 3, 4] },
    { name: '초급 (1-4칸)', blocks: [1, 2, 3, 4, 5, 6, 7, 8, 9] },
    { name: '중급 (2-5칸)', blocks: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21] },
    { name: '고급 (4-5칸)', blocks: [5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21] },
    { name: '전문가 (5칸만)', blocks: [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21] },
    { name: '전체 블록', blocks: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21] }
  ];

  const renderBlockShape = (blockId: number) => {
    const shape = BLOCK_SHAPES[blockId];
    if (!shape) return <div className="w-8 h-8 bg-gray-600 rounded"></div>;

    const maxRows = shape.length;
    const maxCols = Math.max(...shape.map(row => row.length));
    const cellSize = Math.min(32 / maxCols, 32 / maxRows);

    return (
      <div 
        className="flex flex-col justify-center items-center"
        style={{ width: '32px', height: '32px' }}
      >
        {shape.map((row, y) => (
          <div key={y} className="flex">
            {row.map((cell, x) => (
              <div
                key={x}
                className={`${cell ? 'bg-blue-500' : ''}`}
                style={{
                  width: `${cellSize}px`,
                  height: `${cellSize}px`,
                  margin: '0.5px'
                }}
              />
            ))}
          </div>
        ))}
      </div>
    );
  };

  const totalSelectedScore = selectedBlocks.reduce((sum, blockId) => {
    const block = BLOCK_TYPES.find(b => b.id === blockId);
    return sum + (block ? block.cells : 0);
  }, 0);

  return (
    <div className="space-y-6">
      {/* Summary */}
      <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
        <div className="flex justify-between items-center">
          <div>
            <h3 className="text-lg font-medium text-white">블록 선택</h3>
            <p className="text-gray-400 text-sm mt-1">
              선택된 블록: {selectedBlocks.length}개 / 전체: {BLOCK_TYPES.length}개 
              | 최대 점수: {totalSelectedScore}점
            </p>
          </div>
          <div className="flex gap-2">
            <button
              onClick={handleSelectAll}
              className="px-3 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors text-sm"
            >
              전체 선택
            </button>
            <button
              onClick={handleSelectNone}
              className="px-3 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors text-sm"
            >
              전체 해제
            </button>
          </div>
        </div>
      </div>

      {/* Presets */}
      {showPresets && (
        <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
          <div className="flex justify-between items-center mb-4">
            <h4 className="font-medium text-white">프리셋</h4>
            <button
              onClick={() => setShowPresets(!showPresets)}
              className="text-gray-400 hover:text-white transition-colors text-sm"
            >
              숨기기
            </button>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-2">
            {presets.map((preset, index) => (
              <button
                key={index}
                onClick={() => onChange(preset.blocks)}
                className="px-3 py-2 bg-dark-card hover:bg-gray-700 text-white rounded-lg transition-colors text-sm text-center"
                title={`${preset.blocks.length}개 블록`}
              >
                {preset.name}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Category Filter */}
      <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
        <div className="flex flex-wrap gap-2 mb-4">
          {categories.map((category) => (
            <button
              key={category}
              onClick={() => setSelectedCategory(category)}
              className={`px-3 py-2 rounded-lg transition-colors text-sm ${
                selectedCategory === category
                  ? 'bg-blue-600 text-white'
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              {category === 'all' ? '전체' : category}
            </button>
          ))}
        </div>

        <div className="flex gap-2">
          <button
            onClick={() => handleSelectCategory(selectedCategory)}
            className="px-3 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors text-sm"
          >
            현재 카테고리 선택
          </button>
          <button
            onClick={() => handleDeselectCategory(selectedCategory)}
            className="px-3 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors text-sm"
          >
            현재 카테고리 해제
          </button>
        </div>
      </div>

      {/* Block Grid */}
      <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 xl:grid-cols-7 gap-3">
          {filteredBlocks.map((block) => {
            const isSelected = selectedBlocks.includes(block.id);
            return (
              <div
                key={block.id}
                onClick={() => handleBlockToggle(block.id)}
                className={`relative p-3 rounded-lg border-2 cursor-pointer transition-all ${
                  isSelected
                    ? 'border-blue-500 bg-blue-500/20'
                    : 'border-dark-border bg-dark-card hover:border-gray-500 hover:bg-dark-bg'
                }`}
              >
                {/* Selection indicator */}
                {isSelected && (
                  <div className="absolute top-1 right-1 w-4 h-4 bg-blue-500 rounded-full flex items-center justify-center">
                    <svg className="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                  </div>
                )}

                {/* Block visual */}
                <div className="flex justify-center mb-2">
                  {renderBlockShape(block.id)}
                </div>

                {/* Block info */}
                <div className="text-center">
                  <div className="text-white text-sm font-medium">#{block.id}</div>
                  <div className="text-gray-400 text-xs">{block.name}</div>
                  <div className="text-blue-400 text-xs">{block.cells}점</div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Selected Blocks Summary */}
      {selectedBlocks.length > 0 && (
        <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
          <h4 className="font-medium text-white mb-3">선택된 블록 요약</h4>
          <div className="space-y-2 text-sm">
            <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
              {categories.slice(1).map((category) => {
                const categoryBlocks = BLOCK_TYPES.filter(block => 
                  block.category === category && selectedBlocks.includes(block.id)
                );
                return (
                  <div key={category}>
                    <span className="text-gray-400">{category}:</span>
                    <span className="text-white ml-2">{categoryBlocks.length}개</span>
                  </div>
                );
              })}
            </div>
            <div className="pt-2 border-t border-dark-border">
              <span className="text-gray-400">총 최대 점수:</span>
              <span className="text-blue-400 ml-2 font-medium">{totalSelectedScore}점</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}