'use client';

import { useState, useEffect, useRef } from 'react';
import thumbnailGenerator from '@/lib/thumbnail-generator';

interface BoardState {
  obstacles: Array<{x: number, y: number}>;
  preplaced: Array<{x: number, y: number, color: number}>;
}

interface ThumbnailPreviewProps {
  boardState: BoardState;
  stageNumber: number;
  onThumbnailGenerated?: (dataUrl: string) => void;
  className?: string;
}

export default function ThumbnailPreview({ 
  boardState, 
  stageNumber, 
  onThumbnailGenerated,
  className = ""
}: ThumbnailPreviewProps) {
  const [thumbnailUrl, setThumbnailUrl] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);

  // Generate thumbnail when board state changes
  useEffect(() => {
    generateThumbnail();
  }, [boardState, stageNumber]);

  const generateThumbnail = async () => {
    setIsGenerating(true);
    setError(null);

    try {
      const dataUrl = await thumbnailGenerator.generateThumbnail(boardState, {
        width: 200,
        height: 200,
        showGrid: true
      });

      setThumbnailUrl(dataUrl);
      onThumbnailGenerated?.(dataUrl);
    } catch (err) {
      console.error('Failed to generate thumbnail:', err);
      setError('썸네일 생성에 실패했습니다.');
    } finally {
      setIsGenerating(false);
    }
  };

  const downloadThumbnail = () => {
    if (!thumbnailUrl) return;

    const link = document.createElement('a');
    link.href = thumbnailUrl;
    link.download = `stage-${stageNumber}-thumbnail.png`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className={`bg-dark-bg border border-dark-border rounded-lg p-4 ${className}`}>
      <div className="flex justify-between items-center mb-3">
        <h3 className="text-lg font-medium text-white">썸네일 미리보기</h3>
        <div className="flex gap-2">
          <button
            onClick={generateThumbnail}
            disabled={isGenerating}
            className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-white text-sm rounded-lg transition-colors"
          >
            {isGenerating ? '생성 중...' : '새로고침'}
          </button>
          <button
            onClick={downloadThumbnail}
            disabled={!thumbnailUrl}
            className="px-3 py-1.5 bg-green-600 hover:bg-green-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-white text-sm rounded-lg transition-colors"
          >
            다운로드
          </button>
        </div>
      </div>

      {/* Thumbnail Display */}
      <div className="relative">
        {isGenerating && (
          <div className="absolute inset-0 bg-dark-bg/80 flex items-center justify-center rounded-lg z-10">
            <div className="flex items-center gap-2 text-gray-400">
              <div className="w-4 h-4 border-2 border-gray-400 border-t-transparent rounded-full animate-spin"></div>
              <span>썸네일 생성 중...</span>
            </div>
          </div>
        )}

        {error && (
          <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-4 text-center">
            <div className="text-red-400 mb-2">⚠️ 오류</div>
            <div className="text-red-300 text-sm">{error}</div>
            <button
              onClick={generateThumbnail}
              className="mt-3 px-3 py-1.5 bg-red-600 hover:bg-red-700 text-white text-sm rounded-lg transition-colors"
            >
              다시 시도
            </button>
          </div>
        )}

        {thumbnailUrl && !error && (
          <div className="space-y-3">
            <div className="flex justify-center">
              <img
                src={thumbnailUrl}
                alt={`Stage ${stageNumber} Thumbnail`}
                className="border border-dark-border rounded-lg shadow-lg max-w-full h-auto"
                style={{ maxWidth: '200px', maxHeight: '200px' }}
              />
            </div>
            
            <div className="text-center">
              <p className="text-gray-400 text-sm">
                스테이지 {stageNumber} 썸네일
              </p>
              <p className="text-gray-500 text-xs mt-1">
                장애물: {boardState.obstacles.length}개, 
                미리 배치: {boardState.preplaced.length}개
              </p>
            </div>
          </div>
        )}

        {!thumbnailUrl && !isGenerating && !error && (
          <div className="bg-gray-800 border-2 border-dashed border-gray-600 rounded-lg p-8 text-center">
            <div className="text-gray-400 mb-2">📷</div>
            <div className="text-gray-400 text-sm">썸네일이 생성되지 않았습니다</div>
            <button
              onClick={generateThumbnail}
              className="mt-3 px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded-lg transition-colors"
            >
              썸네일 생성
            </button>
          </div>
        )}
      </div>

      {/* Info */}
      <div className="mt-4 p-3 bg-dark-card rounded-lg">
        <p className="text-gray-400 text-xs">
          💡 <strong>자동 생성:</strong> 스테이지 저장 시 썸네일 URL이 비어있으면 
          보드 상태를 기반으로 썸네일이 자동 생성됩니다.
        </p>
      </div>

      {/* Hidden canvas for thumbnail generation */}
      <canvas ref={canvasRef} style={{ display: 'none' }} />
    </div>
  );
}