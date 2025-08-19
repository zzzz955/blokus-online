'use client';

import { useState, useRef, useEffect } from 'react';
import { 
  BoardState, 
  toLegacyBoardState, 
  fromLegacyBoardState, 
  addObstacle, 
  addPreplacedPiece, 
  removePiece, 
  getPieceAt 
} from '@/lib/board-state-codec';

interface BoardEditorProps {
  boardState: BoardState;
  onChange: (boardState: BoardState) => void;
}

type CellState = {
  type: 'empty' | 'obstacle' | 'preplaced';
  color?: number; // For preplaced blocks
};

const BOARD_SIZE = 20;
const PLAYER_COLORS = [
  { id: 1, name: '플레이어 1', color: '#3B82F6' }, // Blue
  { id: 2, name: '플레이어 2', color: '#EAB308' }, // Yellow
  { id: 3, name: '플레이어 3', color: '#EF4444' }, // Red
  { id: 4, name: '플레이어 4', color: '#10B981' }, // Green
];

export default function BoardEditor({ boardState, onChange }: BoardEditorProps) {
  const [editMode, setEditMode] = useState<'obstacle' | 'preplaced' | 'erase'>('obstacle');
  const [selectedPlayerColor, setSelectedPlayerColor] = useState(1);
  const [isDrawing, setIsDrawing] = useState(false);
  const [showGrid, setShowGrid] = useState(true);
  const boardRef = useRef<HTMLDivElement>(null);

  // Convert boardState to 2D array for easier manipulation
  const getBoardMatrix = (): CellState[][] => {
    const matrix: CellState[][] = Array(BOARD_SIZE).fill(null).map(() => 
      Array(BOARD_SIZE).fill(null).map(() => ({ type: 'empty' as const }))
    );

    // Process int[] format directly
    for (let y = 0; y < BOARD_SIZE; y++) {
      for (let x = 0; x < BOARD_SIZE; x++) {
        const piece = getPieceAt(boardState, x, y);
        if (piece === 5) {
          matrix[y][x] = { type: 'obstacle' };
        } else if (piece >= 1 && piece <= 4) {
          matrix[y][x] = { type: 'preplaced', color: piece };
        }
      }
    }

    return matrix;
  };

  // Convert 2D array back to boardState format
  const matrixToBoardState = (matrix: CellState[][]): BoardState => {
    let result: BoardState = [];

    matrix.forEach((row, y) => {
      row.forEach((cell, x) => {
        if (cell.type === 'obstacle') {
          result = addObstacle(result, x, y);
        } else if (cell.type === 'preplaced' && cell.color) {
          result = addPreplacedPiece(result, x, y, cell.color);
        }
      });
    });

    return result;
  };

  const handleCellClick = (x: number, y: number) => {
    const matrix = getBoardMatrix();
    const currentCell = matrix[y][x];

    if (editMode === 'erase') {
      matrix[y][x] = { type: 'empty' };
    } else if (editMode === 'obstacle') {
      matrix[y][x] = currentCell.type === 'obstacle' 
        ? { type: 'empty' } 
        : { type: 'obstacle' };
    } else if (editMode === 'preplaced') {
      matrix[y][x] = currentCell.type === 'preplaced' && currentCell.color === selectedPlayerColor
        ? { type: 'empty' }
        : { type: 'preplaced', color: selectedPlayerColor };
    }

    onChange(matrixToBoardState(matrix));
  };

  const handleMouseDown = (x: number, y: number, event: React.MouseEvent) => {
    event.preventDefault();
    setIsDrawing(true);
    handleCellClick(x, y);
  };

  const handleMouseEnter = (x: number, y: number) => {
    if (isDrawing) {
      handleCellClick(x, y);
    }
  };

  const handleMouseUp = () => {
    setIsDrawing(false);
  };

  useEffect(() => {
    const handleGlobalMouseUp = () => setIsDrawing(false);
    document.addEventListener('mouseup', handleGlobalMouseUp);
    return () => document.removeEventListener('mouseup', handleGlobalMouseUp);
  }, []);

  const clearBoard = () => {
    if (confirm('정말로 보드를 초기화하시겠습니까?')) {
      onChange([]);
    }
  };

  const generateRandomObstacles = (count: number) => {
    const matrix = getBoardMatrix();
    const emptyCells: Array<{x: number, y: number}> = [];

    // Find all empty cells
    matrix.forEach((row, y) => {
      row.forEach((cell, x) => {
        if (cell.type === 'empty') {
          emptyCells.push({ x, y });
        }
      });
    });

    // Randomly place obstacles
    const shuffled = emptyCells.sort(() => Math.random() - 0.5);
    const newObstacles = shuffled.slice(0, Math.min(count, emptyCells.length));

    newObstacles.forEach(({x, y}) => {
      matrix[y][x] = { type: 'obstacle' };
    });

    onChange(matrixToBoardState(matrix));
  };

  const getCellStyle = (cell: CellState) => {
    if (cell.type === 'obstacle') {
      return 'bg-gray-800 border-gray-600';
    } else if (cell.type === 'preplaced') {
      const playerColor = PLAYER_COLORS.find(p => p.id === cell.color);
      return `border-gray-600 opacity-80`;
    } else {
      return 'bg-dark-card border-dark-border hover:bg-dark-bg';
    }
  };

  const getCellBackgroundColor = (cell: CellState) => {
    if (cell.type === 'preplaced') {
      const playerColor = PLAYER_COLORS.find(p => p.id === cell.color);
      return playerColor?.color || '#6B7280';
    }
    return undefined;
  };

  const matrix = getBoardMatrix();
  const legacyState = toLegacyBoardState(boardState);
  const totalObstacles = legacyState.obstacles.length;
  const totalPreplaced = legacyState.preplaced.length;
  const emptySpaces = BOARD_SIZE * BOARD_SIZE - totalObstacles - totalPreplaced;

  return (
    <div className="space-y-6">
      {/* Control Panel */}
      <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
        <div className="flex flex-wrap gap-4 items-center justify-between">
          {/* Edit Mode Selection */}
          <div className="flex gap-2">
            <button
              onClick={() => setEditMode('obstacle')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                editMode === 'obstacle' 
                  ? 'bg-gray-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              장애물
            </button>
            <button
              onClick={() => setEditMode('preplaced')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                editMode === 'preplaced' 
                  ? 'bg-blue-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              미리 배치
            </button>
            <button
              onClick={() => setEditMode('erase')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                editMode === 'erase' 
                  ? 'bg-red-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              지우개
            </button>
          </div>

          {/* Player Color Selection (for preplaced mode) */}
          {editMode === 'preplaced' && (
            <div className="flex gap-2 items-center">
              <span className="text-gray-400 text-sm">플레이어:</span>
              {PLAYER_COLORS.map((player) => (
                <button
                  key={player.id}
                  onClick={() => setSelectedPlayerColor(player.id)}
                  className={`w-8 h-8 rounded-lg border-2 transition-all ${
                    selectedPlayerColor === player.id 
                      ? 'border-white scale-110' 
                      : 'border-gray-600 hover:border-gray-400'
                  }`}
                  style={{ backgroundColor: player.color }}
                  title={player.name}
                />
              ))}
            </div>
          )}

          {/* Utility Buttons */}
          <div className="flex gap-2">
            <button
              onClick={() => setShowGrid(!showGrid)}
              className="px-3 py-2 bg-dark-card text-gray-400 hover:text-white rounded-lg transition-colors text-sm"
            >
              {showGrid ? '격자 숨기기' : '격자 보기'}
            </button>
            <button
              onClick={() => generateRandomObstacles(20)}
              className="px-3 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors text-sm"
            >
              랜덤 장애물
            </button>
            <button
              onClick={clearBoard}
              className="px-3 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors text-sm"
            >
              초기화
            </button>
          </div>
        </div>

        {/* Statistics */}
        <div className="mt-4 flex gap-6 text-sm">
          <div>
            <span className="text-gray-400">장애물:</span>
            <span className="text-white ml-2">{totalObstacles}개</span>
          </div>
          <div>
            <span className="text-gray-400">미리 배치:</span>
            <span className="text-white ml-2">{totalPreplaced}개</span>
          </div>
          <div>
            <span className="text-gray-400">빈 공간:</span>
            <span className={`ml-2 ${emptySpaces < 100 ? 'text-red-400' : 'text-green-400'}`}>
              {emptySpaces}개
            </span>
          </div>
        </div>
      </div>

      {/* Board */}
      <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-lg font-medium text-white">보드 편집 (20×20)</h3>
          <div className="text-sm text-gray-400">
            {editMode === 'obstacle' && '클릭: 장애물 배치/제거'}
            {editMode === 'preplaced' && '클릭: 미리 배치된 블록 설정'}
            {editMode === 'erase' && '클릭: 셀 지우기'}
          </div>
        </div>

        <div 
          ref={boardRef}
          className="relative mx-auto bg-dark-card border border-dark-border rounded-lg p-2 select-none"
          style={{ width: 'fit-content' }}
          onMouseUp={handleMouseUp}
        >
          {/* Coordinate Labels */}
          <div className="flex mb-1">
            <div className="w-6"></div> {/* Corner space */}
            {Array.from({ length: BOARD_SIZE }, (_, i) => (
              <div key={i} className="w-6 h-4 text-xs text-gray-500 text-center">
                {i}
              </div>
            ))}
          </div>

          {/* Board Grid */}
          {matrix.map((row, y) => (
            <div key={y} className="flex">
              {/* Row label */}
              <div className="w-6 h-6 text-xs text-gray-500 flex items-center justify-center">
                {y}
              </div>
              
              {/* Row cells */}
              {row.map((cell, x) => (
                <div
                  key={`${x}-${y}`}
                  className={`w-6 h-6 border cursor-pointer transition-all ${getCellStyle(cell)} ${
                    showGrid ? 'border-opacity-50' : 'border-opacity-20'
                  }`}
                  style={{ backgroundColor: getCellBackgroundColor(cell) }}
                  onMouseDown={(e) => handleMouseDown(x, y, e)}
                  onMouseEnter={() => handleMouseEnter(x, y)}
                  title={`(${x}, ${y}) - ${
                    cell.type === 'obstacle' ? '장애물' : 
                    cell.type === 'preplaced' ? `플레이어 ${cell.color}` : '빈 칸'
                  }`}
                >
                  {/* Visual indicators */}
                  {cell.type === 'obstacle' && (
                    <div className="w-full h-full bg-gray-600 opacity-80 rounded-sm"></div>
                  )}
                  {cell.type === 'preplaced' && (
                    <div className="w-full h-full rounded-sm flex items-center justify-center">
                      <div className="w-3 h-3 bg-white bg-opacity-30 rounded-full"></div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          ))}
        </div>

        {/* Legend */}
        <div className="mt-4 flex flex-wrap gap-4 justify-center text-sm">
          <div className="flex items-center gap-2">
            <div className="w-4 h-4 bg-dark-card border border-dark-border rounded"></div>
            <span className="text-gray-400">빈 칸</span>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-4 h-4 bg-gray-800 border border-gray-600 rounded"></div>
            <span className="text-gray-400">장애물</span>
          </div>
          {PLAYER_COLORS.map((player) => (
            <div key={player.id} className="flex items-center gap-2">
              <div 
                className="w-4 h-4 border border-gray-600 rounded flex items-center justify-center"
                style={{ backgroundColor: player.color }}
              >
                <div className="w-2 h-2 bg-white bg-opacity-30 rounded-full"></div>
              </div>
              <span className="text-gray-400">{player.name}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Instructions */}
      <div className="bg-blue-500/10 border border-blue-500/30 rounded-lg p-4">
        <h4 className="text-blue-400 font-medium mb-2">사용 방법</h4>
        <ul className="text-blue-300 text-sm space-y-1">
          <li>• <strong>장애물 모드:</strong> 클릭하여 장애물을 배치하거나 제거합니다</li>
          <li>• <strong>미리 배치 모드:</strong> 선택한 플레이어 색상의 블록을 미리 배치합니다</li>
          <li>• <strong>지우개 모드:</strong> 클릭한 셀의 내용을 지웁니다</li>
          <li>• <strong>드래그 그리기:</strong> 마우스를 누른 채로 드래그하여 연속으로 편집할 수 있습니다</li>
          <li>• <strong>최소 빈 공간:</strong> 플레이 가능성을 위해 최소 100개의 빈 칸을 유지하는 것을 권장합니다</li>
        </ul>
      </div>
    </div>
  );
}