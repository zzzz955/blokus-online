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
const BOARD_COLORS = [
  { id: 1, name: '파란색', color: '#3B82F6' }, // Blue
  { id: 2, name: '노란색', color: '#EAB308' }, // Yellow
  { id: 3, name: '빨간색', color: '#EF4444' }, // Red
  { id: 4, name: '초록색', color: '#10B981' }, // Green
  { id: 5, name: '장애물', color: '#1F2937' }, // Black/Obstacle
];

// Block shapes ported from common/src/Block.cpp
type Position = [number, number];
type BlockShape = Position[];

enum BlockType {
  Single = 'Single',
  Domino = 'Domino',
  TrioLine = 'TrioLine',
  TrioAngle = 'TrioAngle',
  Tetro_I = 'Tetro_I',
  Tetro_O = 'Tetro_O', 
  Tetro_T = 'Tetro_T',
  Tetro_L = 'Tetro_L',
  Tetro_S = 'Tetro_S',
  Pento_F = 'Pento_F',
  Pento_I = 'Pento_I',
  Pento_L = 'Pento_L',
  Pento_N = 'Pento_N',
  Pento_P = 'Pento_P',
  Pento_T = 'Pento_T',
  Pento_U = 'Pento_U',
  Pento_V = 'Pento_V',
  Pento_W = 'Pento_W',
  Pento_X = 'Pento_X',
  Pento_Y = 'Pento_Y',
  Pento_Z = 'Pento_Z'
}

const BLOCK_SHAPES: Record<BlockType, BlockShape> = {
  [BlockType.Single]: [[0, 0]],
  [BlockType.Domino]: [[0, 0], [0, 1]],
  [BlockType.TrioLine]: [[0, 0], [0, 1], [0, 2]],
  [BlockType.TrioAngle]: [[0, 0], [0, 1], [1, 1]],
  [BlockType.Tetro_I]: [[0, 0], [0, 1], [0, 2], [0, 3]],
  [BlockType.Tetro_O]: [[0, 0], [0, 1], [1, 0], [1, 1]],
  [BlockType.Tetro_T]: [[0, 0], [0, 1], [0, 2], [1, 1]],
  [BlockType.Tetro_L]: [[0, 0], [0, 1], [0, 2], [1, 0]],
  [BlockType.Tetro_S]: [[0, 0], [0, 1], [1, 1], [1, 2]],
  [BlockType.Pento_F]: [[0, 1], [0, 2], [1, 0], [1, 1], [2, 1]],
  [BlockType.Pento_I]: [[0, 0], [0, 1], [0, 2], [0, 3], [0, 4]],
  [BlockType.Pento_L]: [[0, 0], [0, 1], [0, 2], [0, 3], [1, 0]],
  [BlockType.Pento_N]: [[0, 0], [0, 1], [0, 2], [1, 2], [1, 3]],
  [BlockType.Pento_P]: [[0, 0], [0, 1], [1, 0], [1, 1], [2, 0]],
  [BlockType.Pento_T]: [[0, 0], [0, 1], [0, 2], [1, 1], [2, 1]],
  [BlockType.Pento_U]: [[0, 0], [0, 2], [1, 0], [1, 1], [1, 2]],
  [BlockType.Pento_V]: [[0, 0], [1, 0], [2, 0], [2, 1], [2, 2]],
  [BlockType.Pento_W]: [[0, 0], [1, 0], [1, 1], [2, 1], [2, 2]],
  [BlockType.Pento_X]: [[0, 1], [1, 0], [1, 1], [1, 2], [2, 1]],
  [BlockType.Pento_Y]: [[0, 0], [0, 1], [0, 2], [0, 3], [1, 1]],
  [BlockType.Pento_Z]: [[0, 0], [0, 1], [1, 1], [2, 1], [2, 2]]
};

const BLOCK_NAMES: Record<BlockType, string> = {
  [BlockType.Single]: '단일',
  [BlockType.Domino]: '도미노',
  [BlockType.TrioLine]: '트리오 라인',
  [BlockType.TrioAngle]: '트리오 앵글',
  [BlockType.Tetro_I]: 'I 블록',
  [BlockType.Tetro_O]: 'O 블록',
  [BlockType.Tetro_T]: 'T 블록',
  [BlockType.Tetro_L]: 'L 블록',
  [BlockType.Tetro_S]: 'S 블록',
  [BlockType.Pento_F]: 'F 블록',
  [BlockType.Pento_I]: 'I5 블록',
  [BlockType.Pento_L]: 'L5 블록',
  [BlockType.Pento_N]: 'N 블록',
  [BlockType.Pento_P]: 'P 블록',
  [BlockType.Pento_T]: 'T5 블록',
  [BlockType.Pento_U]: 'U 블록',
  [BlockType.Pento_V]: 'V 블록',
  [BlockType.Pento_W]: 'W 블록',
  [BlockType.Pento_X]: 'X 블록',
  [BlockType.Pento_Y]: 'Y 블록',
  [BlockType.Pento_Z]: 'Z 블록'
};

// Block transformation functions
const applyRotation = (shape: BlockShape, rotation: number): BlockShape => {
  return shape.map(([r, c]) => {
    switch (rotation) {
      case 0: return [r, c] as Position;
      case 1: return [c, -r] as Position; // 90° clockwise
      case 2: return [-r, -c] as Position; // 180°
      case 3: return [-c, r] as Position; // 270° clockwise
      default: return [r, c] as Position;
    }
  });
};

const applyFlip = (shape: BlockShape, flipHorizontal: boolean, flipVertical: boolean): BlockShape => {
  return shape.map(([r, c]) => {
    let newR = r, newC = c;
    if (flipVertical) newR = -r;
    if (flipHorizontal) newC = -c;
    return [newR, newC] as Position;
  });
};

const normalizeShape = (shape: BlockShape): BlockShape => {
  if (shape.length === 0) return shape;
  
  const minR = Math.min(...shape.map(([r]) => r));
  const minC = Math.min(...shape.map(([, c]) => c));
  
  return shape.map(([r, c]) => [r - minR, c - minC] as Position);
};

export default function BoardEditor({ boardState, onChange }: BoardEditorProps) {
  const [selectedColor, setSelectedColor] = useState(5); // Default to Black/Obstacle
  const [selectedTool, setSelectedTool] = useState<'cell' | 'tetromino' | 'fill' | 'eraser' | 'rangeFill' | 'image'>('cell');
  const [avoidBlueColor, setAvoidBlueColor] = useState(false);
  const [selectedBlockType, setSelectedBlockType] = useState<BlockType>(BlockType.Tetro_I);
  const [blockRotation, setBlockRotation] = useState(0);
  const [blockFlipH, setBlockFlipH] = useState(false);
  const [blockFlipV, setBlockFlipV] = useState(false);
  const [eraserSize, setEraserSize] = useState(1);
  const [isDrawing, setIsDrawing] = useState(false);
  const [showGrid, setShowGrid] = useState(true);
  const [hoverPos, setHoverPos] = useState<{x: number, y: number} | null>(null);
  const boardRef = useRef<HTMLDivElement>(null);

  // Get current block shape with transformations applied
  const getCurrentBlockShape = (): BlockShape => {
    let shape = BLOCK_SHAPES[selectedBlockType];
    shape = applyFlip(shape, blockFlipH, blockFlipV);
    shape = applyRotation(shape, blockRotation);
    shape = normalizeShape(shape);
    return shape;
  };

  // Convert boardState to 2D array for easier manipulation
  const getBoardMatrix = (): CellState[][] => {
    const matrix: CellState[][] = Array(BOARD_SIZE).fill(null).map(() => 
      Array(BOARD_SIZE).fill(null).map(() => ({ type: 'empty' as const }))
    );

    // Process int[] format directly
    for (let y = 0; y < BOARD_SIZE; y++) {
      for (let x = 0; x < BOARD_SIZE; x++) {
        const piece = getPieceAt(boardState, x, y);
        if (piece >= 1 && piece <= 5) {
          matrix[y][x] = { type: piece === 5 ? 'obstacle' : 'preplaced', color: piece };
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
        if (cell.color) {
          if (cell.color === 5) {
            result = addObstacle(result, x, y);
          } else if (cell.color >= 1 && cell.color <= 4) {
            result = addPreplacedPiece(result, x, y, cell.color);
          }
        }
      });
    });

    return result;
  };

  // Place tetromino block at position
  const placeTetromino = (baseX: number, baseY: number) => {
    const matrix = getBoardMatrix();
    const blockShape = getCurrentBlockShape();
    
    // Check if placement is valid
    const canPlace = blockShape.every(([dy, dx]) => {
      const x = baseX + dx;
      const y = baseY + dy;
      return x >= 0 && x < BOARD_SIZE && y >= 0 && y < BOARD_SIZE;
    });
    
    if (!canPlace) return;
    
    // Place the block
    blockShape.forEach(([dy, dx]) => {
      const x = baseX + dx;
      const y = baseY + dy;
      matrix[y][x] = { 
        type: selectedColor === 5 ? 'obstacle' : 'preplaced', 
        color: selectedColor 
      };
    });
    
    onChange(matrixToBoardState(matrix));
  };

  // Erase area around position
  const eraseArea = (centerX: number, centerY: number) => {
    const matrix = getBoardMatrix();
    const halfSize = Math.floor(eraserSize / 2);
    
    for (let dy = -halfSize; dy <= halfSize; dy++) {
      for (let dx = -halfSize; dx <= halfSize; dx++) {
        const x = centerX + dx;
        const y = centerY + dy;
        if (x >= 0 && x < BOARD_SIZE && y >= 0 && y < BOARD_SIZE) {
          matrix[y][x] = { type: 'empty' };
        }
      }
    }
    
    onChange(matrixToBoardState(matrix));
  };

  // Fill area around position with selected color
  const fillRange = (centerX: number, centerY: number) => {
    const matrix = getBoardMatrix();
    const halfSize = Math.floor(eraserSize / 2);
    
    for (let dy = -halfSize; dy <= halfSize; dy++) {
      for (let dx = -halfSize; dx <= halfSize; dx++) {
        const x = centerX + dx;
        const y = centerY + dy;
        if (x >= 0 && x < BOARD_SIZE && y >= 0 && y < BOARD_SIZE) {
          matrix[y][x] = { 
            type: selectedColor === 5 ? 'obstacle' : 'preplaced', 
            color: selectedColor 
          };
        }
      }
    }
    
    onChange(matrixToBoardState(matrix));
  };

  // Fill entire board with selected color
  const fillAll = () => {
    if (confirm(`전체 보드를 ${BOARD_COLORS.find(c => c.id === selectedColor)?.name}로 채우시겠습니까?`)) {
      const matrix = getBoardMatrix();
      for (let y = 0; y < BOARD_SIZE; y++) {
        for (let x = 0; x < BOARD_SIZE; x++) {
          matrix[y][x] = { 
            type: selectedColor === 5 ? 'obstacle' : 'preplaced', 
            color: selectedColor 
          };
        }
      }
      onChange(matrixToBoardState(matrix));
    }
  };

  // Convert RGB to closest board color
  const rgbToClosestColor = (r: number, g: number, b: number): number => {
    // Use different color mapping based on avoidBlueColor setting
    const colors = avoidBlueColor ? [
      { id: 2, rgb: [234, 179, 8] },    // Yellow (replaces blue)
      { id: 3, rgb: [239, 68, 68] },    // Red  
      { id: 4, rgb: [16, 185, 129] },   // Green
      { id: 5, rgb: [31, 41, 55] },     // Black/Obstacle
    ] : [
      { id: 1, rgb: [59, 130, 246] },   // Blue
      { id: 2, rgb: [234, 179, 8] },    // Yellow  
      { id: 3, rgb: [239, 68, 68] },    // Red
      { id: 4, rgb: [16, 185, 129] },   // Green
      { id: 5, rgb: [31, 41, 55] },     // Black/Obstacle
    ];
    
    // If pixel is mostly white/transparent, return 0 (empty)
    if (r > 240 && g > 240 && b > 240) {
      return 0;
    }
    
    let minDistance = Infinity;
    let closestColor = avoidBlueColor ? 2 : 5; // Default to yellow if avoiding blue, otherwise black
    
    colors.forEach(color => {
      const [cr, cg, cb] = color.rgb;
      const distance = Math.sqrt(
        Math.pow(r - cr, 2) + Math.pow(g - cg, 2) + Math.pow(b - cb, 2)
      );
      if (distance < minDistance) {
        minDistance = distance;
        closestColor = color.id;
      }
    });
    
    return closestColor;
  };

  // Process uploaded image
  const processImage = (file: File) => {
    if (!file.type.startsWith('image/')) {
      alert('이미지 파일만 업로드 가능합니다.');
      return;
    }
    
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');
    const img = new Image();
    
    img.onload = () => {
      canvas.width = BOARD_SIZE;
      canvas.height = BOARD_SIZE;
      
      // Draw resized image
      ctx?.drawImage(img, 0, 0, BOARD_SIZE, BOARD_SIZE);
      
      // Get pixel data
      const imageData = ctx?.getImageData(0, 0, BOARD_SIZE, BOARD_SIZE);
      if (!imageData) return;
      
      const matrix = getBoardMatrix();
      
      // Convert each pixel to closest color
      for (let y = 0; y < BOARD_SIZE; y++) {
        for (let x = 0; x < BOARD_SIZE; x++) {
          const pixelIndex = (y * BOARD_SIZE + x) * 4;
          const r = imageData.data[pixelIndex];
          const g = imageData.data[pixelIndex + 1];
          const b = imageData.data[pixelIndex + 2];
          const a = imageData.data[pixelIndex + 3];
          
          // Handle transparency
          if (a < 128) {
            matrix[y][x] = { type: 'empty' };
          } else {
            const colorId = rgbToClosestColor(r, g, b);
            if (colorId === 0) {
              matrix[y][x] = { type: 'empty' };
            } else {
              matrix[y][x] = { 
                type: colorId === 5 ? 'obstacle' : 'preplaced', 
                color: colorId 
              };
            }
          }
        }
      }
      
      onChange(matrixToBoardState(matrix));
    };
    
    img.src = URL.createObjectURL(file);
  };

  const handleCellClick = (x: number, y: number) => {
    const matrix = getBoardMatrix();
    const currentCell = matrix[y][x];

    switch (selectedTool) {
      case 'cell':
        // Toggle placement of selected color
        matrix[y][x] = currentCell.color === selectedColor
          ? { type: 'empty' }
          : { 
              type: selectedColor === 5 ? 'obstacle' : 'preplaced', 
              color: selectedColor 
            };
        onChange(matrixToBoardState(matrix));
        break;
      
      case 'tetromino':
        placeTetromino(x, y);
        break;
      
      case 'eraser':
        eraseArea(x, y);
        break;
      
      case 'rangeFill':
        fillRange(x, y);
        break;
      
      case 'fill':
        fillAll();
        break;
        
      case 'image':
        // Image tool doesn't need click handling
        break;
    }
  };

  const handleMouseDown = (x: number, y: number, event: React.MouseEvent) => {
    event.preventDefault();
    setIsDrawing(true);
    handleCellClick(x, y);
  };

  const handleMouseEnter = (x: number, y: number) => {
    setHoverPos({ x, y });
    if (isDrawing && selectedTool !== 'fill') {
      handleCellClick(x, y);
    }
  };

  const handleMouseLeave = () => {
    setHoverPos(null);
  };

  const handleMouseUp = () => {
    setIsDrawing(false);
  };

  useEffect(() => {
    const handleGlobalMouseUp = () => setIsDrawing(false);
    document.addEventListener('mouseup', handleGlobalMouseUp);
    return () => document.removeEventListener('mouseup', handleGlobalMouseUp);
  }, []);

  // Keyboard shortcuts for tetromino tool
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Only activate when tetromino tool is selected and not typing in an input
      if (selectedTool !== 'tetromino' || 
          e.target instanceof HTMLInputElement || 
          e.target instanceof HTMLSelectElement ||
          e.target instanceof HTMLTextAreaElement) {
        return;
      }

      switch (e.key.toLowerCase()) {
        case 'r':
          e.preventDefault();
          setBlockRotation((prev) => (prev + 1) % 4);
          break;
        case 'f':
          e.preventDefault();
          setBlockFlipH((prev) => !prev);
          break;
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [selectedTool]);

  const clearBoard = () => {
    if (confirm('정말로 보드를 초기화하시겠습니까?')) {
      onChange([]);
    }
  };

  const generateRandomPlacements = (count: number) => {
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

    // Randomly place selected color
    const shuffled = emptyCells.sort(() => Math.random() - 0.5);
    const newPlacements = shuffled.slice(0, Math.min(count, emptyCells.length));

    newPlacements.forEach(({x, y}) => {
      matrix[y][x] = { 
        type: selectedColor === 5 ? 'obstacle' : 'preplaced', 
        color: selectedColor 
      };
    });

    onChange(matrixToBoardState(matrix));
  };

  // Check if position is part of tetromino preview
  const isInTetominoPreview = (x: number, y: number): boolean => {
    if (selectedTool !== 'tetromino' || !hoverPos) return false;
    
    const blockShape = getCurrentBlockShape();
    return blockShape.some(([dy, dx]) => {
      return hoverPos.x + dx === x && hoverPos.y + dy === y;
    });
  };

  // Check if position is in eraser/range fill preview
  const isInRangePreview = (x: number, y: number): boolean => {
    if ((selectedTool !== 'eraser' && selectedTool !== 'rangeFill') || !hoverPos) return false;
    
    const halfSize = Math.floor(eraserSize / 2);
    return Math.abs(x - hoverPos.x) <= halfSize && Math.abs(y - hoverPos.y) <= halfSize;
  };

  const getCellStyle = (cell: CellState, x: number, y: number) => {
    let baseStyle = '';
    
    if (cell.color) {
      baseStyle = 'border-gray-600 opacity-80';
    } else {
      baseStyle = 'bg-dark-card border-dark-border hover:bg-dark-bg';
    }
    
    // Add preview styles with better border visibility
    if (isInTetominoPreview(x, y)) {
      baseStyle += ' outline outline-2 outline-blue-400 outline-offset-[-1px] relative z-10';
    } else if (isInRangePreview(x, y)) {
      const outlineColor = selectedTool === 'eraser' ? 'outline-red-400' : 'outline-green-400';
      baseStyle += ` outline outline-2 ${outlineColor} outline-offset-[-1px] relative z-10`;
    }
    
    return baseStyle;
  };

  const getCellBackgroundColor = (cell: CellState, x: number, y: number) => {
    // Show tetromino preview color
    if (isInTetominoPreview(x, y) && !cell.color) {
      const color = BOARD_COLORS.find(c => c.id === selectedColor);
      return color?.color + '60'; // Add transparency
    }
    
    // Show range fill preview color
    if (selectedTool === 'rangeFill' && isInRangePreview(x, y) && !cell.color) {
      const color = BOARD_COLORS.find(c => c.id === selectedColor);
      return color?.color + '40'; // Add transparency
    }
    
    if (cell.color) {
      const color = BOARD_COLORS.find(c => c.id === cell.color);
      return color?.color || '#6B7280';
    }
    return undefined;
  };

  const matrix = getBoardMatrix();
  const legacyState = toLegacyBoardState(boardState);
  const totalObstacles = legacyState.obstacles.length;
  const totalPreplaced = legacyState.preplaced.length;
  const emptySpaces = BOARD_SIZE * BOARD_SIZE - totalObstacles - totalPreplaced;
  
  // Count by color
  const colorCounts = BOARD_COLORS.reduce((acc, color) => {
    acc[color.id] = matrix.flat().filter(cell => cell.color === color.id).length;
    return acc;
  }, {} as Record<number, number>);

  return (
    <div className="space-y-6" onMouseLeave={handleMouseLeave}>
      {/* Control Panel */}
      <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
        <div className="flex flex-wrap gap-4 items-center justify-between">
          {/* Color Selection */}
          <div className="flex gap-2 items-center">
            <span className="text-gray-400 text-sm">색상:</span>
            {BOARD_COLORS.map((color) => (
              <button
                key={color.id}
                onClick={() => setSelectedColor(color.id)}
                className={`w-8 h-8 rounded-lg border-2 transition-all ${
                  selectedColor === color.id 
                    ? 'border-white scale-110 ring-2 ring-white ring-opacity-50' 
                    : 'border-gray-600 hover:border-gray-400'
                }`}
                style={{ backgroundColor: color.color }}
                title={color.name}
              />
            ))}
          </div>

          {/* Tool Selection */}
          <div className="flex gap-2">
            <button
              onClick={() => setSelectedTool('cell')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                selectedTool === 'cell' 
                  ? 'bg-blue-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              단일 셀
            </button>
            <button
              onClick={() => setSelectedTool('tetromino')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                selectedTool === 'tetromino' 
                  ? 'bg-purple-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              블록 배치
            </button>
            <button
              onClick={() => setSelectedTool('rangeFill')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                selectedTool === 'rangeFill' 
                  ? 'bg-green-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              범위 채우기
            </button>
            <button
              onClick={() => setSelectedTool('fill')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                selectedTool === 'fill' 
                  ? 'bg-yellow-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              전체 채우기
            </button>
            <button
              onClick={() => setSelectedTool('eraser')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                selectedTool === 'eraser' 
                  ? 'bg-red-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              지우개
            </button>
            <button
              onClick={() => setSelectedTool('image')}
              className={`px-3 py-2 rounded-lg transition-colors ${
                selectedTool === 'image' 
                  ? 'bg-indigo-600 text-white' 
                  : 'bg-dark-card text-gray-400 hover:text-white'
              }`}
            >
              이미지
            </button>
          </div>

          {/* Utility Buttons */}
          <div className="flex gap-2">
            <button
              onClick={() => setShowGrid(!showGrid)}
              className="px-3 py-2 bg-dark-card text-gray-400 hover:text-white rounded-lg transition-colors text-sm"
            >
              {showGrid ? '격자 숨기기' : '격자 보기'}
            </button>
            <button
              onClick={() => generateRandomPlacements(20)}
              className="px-3 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors text-sm"
            >
              랜덤 배치
            </button>
            <button
              onClick={clearBoard}
              className="px-3 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors text-sm"
            >
              초기화
            </button>
          </div>
        </div>

        {/* Tool-specific Controls */}
        {selectedTool === 'tetromino' && (
          <div className="mt-4 p-3 bg-dark-card rounded-lg">
            <h4 className="text-white font-medium mb-3">블록 도구 설정</h4>
            <div className="flex flex-wrap gap-4 items-center">
              {/* Block Type Selection */}
              <div className="w-full">
                <span className="text-gray-400 text-sm mb-2 block">블록 선택:</span>
                <div className="grid grid-cols-6 gap-2">
                  {Object.entries(BlockType).map(([key, value]) => {
                    const shape = BLOCK_SHAPES[value];
                    const isSelected = selectedBlockType === value;
                    const selectedColorInfo = BOARD_COLORS.find(c => c.id === selectedColor);
                    
                    return (
                      <button
                        key={key}
                        onClick={() => setSelectedBlockType(value)}
                        className={`relative p-2 rounded border-2 transition-all hover:border-blue-400 ${
                          isSelected 
                            ? 'border-blue-500 bg-blue-500/20' 
                            : 'border-gray-600 bg-dark-bg'
                        }`}
                        title={BLOCK_NAMES[value]}
                      >
                        <div className="grid grid-cols-5 gap-px w-10 h-10 mx-auto">
                          {Array.from({ length: 25 }, (_, i) => {
                            const x = i % 5;
                            const y = Math.floor(i / 5);
                            // Use transformed shape and center in 5x5 grid
                            const transformedShape = normalizeShape(
                              applyFlip(
                                applyRotation(shape, blockRotation), 
                                blockFlipH, 
                                blockFlipV
                              )
                            );
                            const isBlockCell = transformedShape.some(([dy, dx]) => dx === x - 1 && dy === y - 1);
                            
                            return (
                              <div
                                key={i}
                                className={`w-1.5 h-1.5 rounded-sm ${
                                  isBlockCell ? 'opacity-100' : 'opacity-15'
                                }`}
                                style={{ 
                                  backgroundColor: isBlockCell 
                                    ? selectedColorInfo?.color || '#6B7280'
                                    : '#374151'
                                }}
                              />
                            );
                          })}
                        </div>
                        <span className="text-xs text-gray-300 mt-1 block truncate">
                          {BLOCK_NAMES[value]}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
              
              {/* Transform Controls */}
              <div className="flex flex-wrap gap-4 items-center mt-3">
                {/* Rotation */}
                <div className="flex items-center gap-2">
                  <span className="text-gray-400 text-sm">회전 (R키):</span>
                  <button 
                    onClick={() => setBlockRotation((blockRotation + 3) % 4)}
                    className="px-2 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm"
                    title="반시계 방향"
                  >
                    ↺
                  </button>
                  <span className="text-white text-sm min-w-[30px] text-center">{blockRotation * 90}°</span>
                  <button 
                    onClick={() => setBlockRotation((blockRotation + 1) % 4)}
                    className="px-2 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm"
                    title="시계 방향"
                  >
                    ↻
                  </button>
                </div>
                
                {/* Flip Controls */}
                <div className="flex items-center gap-2">
                  <span className="text-gray-400 text-sm">뒤집기 (F키):</span>
                  <button 
                    onClick={() => setBlockFlipH(!blockFlipH)}
                    className={`px-2 py-1 rounded text-sm transition-colors ${
                      blockFlipH ? 'bg-green-600 text-white' : 'bg-dark-bg text-gray-400 hover:text-white'
                    }`}
                  >
                    ↔️ 수평
                  </button>
                  <button 
                    onClick={() => setBlockFlipV(!blockFlipV)}
                    className={`px-2 py-1 rounded text-sm transition-colors ${
                      blockFlipV ? 'bg-green-600 text-white' : 'bg-dark-bg text-gray-400 hover:text-white'
                    }`}
                  >
                    ↕️ 수직
                  </button>
                </div>
                
                {/* Reset */}
                <button 
                  onClick={() => {
                    setBlockRotation(0);
                    setBlockFlipH(false);
                    setBlockFlipV(false);
                  }}
                  className="px-2 py-1 bg-gray-600 hover:bg-gray-700 text-white rounded text-sm"
                >
                  초기화
                </button>
              </div>
            </div>
          </div>
        )}
        
        {selectedTool === 'image' && (
          <div className="mt-4 p-3 bg-dark-card rounded-lg">
            <h4 className="text-white font-medium mb-3">이미지 도트화</h4>
            <div className="space-y-3">
              <div className="flex items-center gap-4">
                <label className="flex items-center justify-center px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg cursor-pointer transition-colors">
                  <input 
                    type="file" 
                    accept="image/*" 
                    className="hidden" 
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) processImage(file);
                      e.target.value = ''; // Reset input
                    }}
                  />
                  📷 이미지 업로드
                </label>
                <span className="text-gray-400 text-sm">
                  PNG, JPG 파일을 20x20 보드에 맞게 자동 변환
                </span>
              </div>
              
              {/* Blue color avoidance option */}
              <div className="flex items-center gap-3 p-3 bg-yellow-500/10 border border-yellow-500/30 rounded-lg">
                <input
                  type="checkbox"
                  id="avoidBlue"
                  checked={avoidBlueColor}
                  onChange={(e) => setAvoidBlueColor(e.target.checked)}
                  className="w-4 h-4 text-yellow-600 bg-dark-bg border-gray-600 rounded focus:ring-yellow-500"
                />
                <label htmlFor="avoidBlue" className="text-yellow-300 text-sm">
                  🚫 파란색 피하기 (모바일 플레이어 블록과 충돌 방지)
                </label>
              </div>
              
              <div className="text-sm text-gray-300">
                <p>• 이미지가 자동으로 {avoidBlueColor ? '4색' : '5색'}으로 변환됩니다: {avoidBlueColor ? '노란, 빨간, 초록, 검정' : '파란, 노란, 빨간, 초록, 검정'}</p>
                <p>• 흰색/투명 영역은 빈 공간으로 처리됩니다</p>
                <p>• 기존 보드 내용이 대체됩니다</p>
                {avoidBlueColor && (
                  <p className="text-yellow-400">• 파란상 영역은 노란색으로 대체됩니다</p>
                )}
              </div>
            </div>
          </div>
        )}
        
        {(selectedTool === 'eraser' || selectedTool === 'rangeFill') && (
          <div className="mt-4 p-3 bg-dark-card rounded-lg">
            <h4 className="text-white font-medium mb-3">
              {selectedTool === 'eraser' ? '지우개' : '범위 채우기'} 도구 설정
            </h4>
            <div className="flex items-center gap-4">
              <span className="text-gray-400 text-sm">크기:</span>
              {[1, 2, 3, 4, 5].map(size => {
                const isSelected = eraserSize === size;
                const colorClass = selectedTool === 'eraser' 
                  ? (isSelected ? 'border-red-400 bg-red-600' : 'hover:border-red-400')
                  : (isSelected ? 'border-green-400 bg-green-600' : 'hover:border-green-400');
                
                return (
                  <button
                    key={size}
                    onClick={() => setEraserSize(size)}
                    className={`w-8 h-8 rounded border-2 transition-all flex items-center justify-center text-sm ${
                      isSelected 
                        ? `${colorClass} text-white` 
                        : `border-gray-600 text-gray-400 hover:text-white ${colorClass}`
                    }`}
                  >
                    {size}
                  </button>
                );
              })}
              <span className="text-gray-400 text-sm">({eraserSize}x{eraserSize} 영역)</span>
            </div>
          </div>
        )}

        {/* Statistics */}
        <div className="mt-4 flex gap-6 text-sm">
          {BOARD_COLORS.map(color => (
            <div key={color.id}>
              <span className="text-gray-400">{color.name}:</span>
              <span className="text-white ml-2">{colorCounts[color.id] || 0}개</span>
            </div>
          ))}
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
            {selectedTool === 'cell' && `클릭: ${BOARD_COLORS.find(c => c.id === selectedColor)?.name} 배치/제거`}
            {selectedTool === 'tetromino' && `클릭: ${BLOCK_NAMES[selectedBlockType]} 블록 배치`}
            {selectedTool === 'rangeFill' && `클릭: ${eraserSize}x${eraserSize} 범위 ${BOARD_COLORS.find(c => c.id === selectedColor)?.name} 채우기`}
            {selectedTool === 'fill' && '클릭: 전체 보드 채우기'}
            {selectedTool === 'eraser' && `클릭: ${eraserSize}x${eraserSize} 영역 지우기`}
            {selectedTool === 'image' && '이미지 파일을 업로드하여 20x20 보드로 변환'}
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
              {row.map((cell, x) => {
                const colorInfo = cell.color ? BOARD_COLORS.find(c => c.id === cell.color) : null;
                return (
                  <div
                    key={`${x}-${y}`}
                    className={`w-6 h-6 border cursor-pointer transition-all ${getCellStyle(cell, x, y)} ${
                      showGrid ? 'border-opacity-50' : 'border-opacity-20'
                    }`}
                    style={{ backgroundColor: getCellBackgroundColor(cell, x, y) }}
                    onMouseDown={(e) => handleMouseDown(x, y, e)}
                    onMouseEnter={() => handleMouseEnter(x, y)}
                    title={`(${x}, ${y}) - ${
                      cell.color ? colorInfo?.name || '알 수 없음' : '빈 칸'
                    }`}
                  >
                    {/* Visual indicators */}
                    {cell.color === 5 && (
                      <div className="w-full h-full bg-gray-600 opacity-80 rounded-sm"></div>
                    )}
                    {cell.color && cell.color !== 5 && (
                      <div className="w-full h-full rounded-sm flex items-center justify-center">
                        <div className="w-3 h-3 bg-white bg-opacity-30 rounded-full"></div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          ))}
        </div>

        {/* Legend */}
        <div className="mt-4 flex flex-wrap gap-4 justify-center text-sm">
          <div className="flex items-center gap-2">
            <div className="w-4 h-4 bg-dark-card border border-dark-border rounded"></div>
            <span className="text-gray-400">빈 칸</span>
          </div>
          {BOARD_COLORS.map((color) => (
            <div key={color.id} className="flex items-center gap-2">
              <div 
                className="w-4 h-4 border border-gray-600 rounded flex items-center justify-center"
                style={{ backgroundColor: color.color }}
              >
                {color.id === 5 ? (
                  <div className="w-full h-full bg-gray-600 opacity-80 rounded-sm"></div>
                ) : (
                  <div className="w-2 h-2 bg-white bg-opacity-30 rounded-full"></div>
                )}
              </div>
              <span className="text-gray-400">{color.name}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Instructions */}
      <div className="bg-blue-500/10 border border-blue-500/30 rounded-lg p-4">
        <h4 className="text-blue-400 font-medium mb-2">사용 방법</h4>
        <ul className="text-blue-300 text-sm space-y-1">
          <li>• <strong>색상 선택:</strong> 원하는 색상을 선택하세요 (기본: 검정/장애물)</li>
          <li>• <strong>단일 셀:</strong> 클릭하여 선택한 색상으로 배치/제거</li>
          <li>• <strong>블록 배치:</strong> 21개 테트로미노 블록 시각적 선택, R키(회전)/F키(뒤집기) 지원</li>
          <li>• <strong>범위 채우기:</strong> 1x1~5x5 크기로 선택한 색상 채우기</li>
          <li>• <strong>전체 채우기:</strong> 보드 전체를 선택한 색상으로 채웁</li>
          <li>• <strong>지우개:</strong> 1x1~5x5 크기로 영역 지우기</li>
          <li>• <strong>이미지:</strong> PNG/JPG 업로드로 5색 도트화 변환</li>
          <li>• <strong>드래그:</strong> 마우스를 누른 채 드래그하여 연속 편집</li>
        </ul>
      </div>
    </div>
  );
}