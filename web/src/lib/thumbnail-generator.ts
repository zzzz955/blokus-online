/**
 * Stage Thumbnail Generator
 * Creates thumbnails from board state for stage preview
 */

import { toLegacyBoardState, type BoardState } from './board-state-codec';

interface ThumbnailOptions {
  width?: number;
  height?: number;
  backgroundColor?: string;
  obstacleColor?: string;
  preplacedColors?: string[];
  gridColor?: string;
  showGrid?: boolean;
}

const DEFAULT_OPTIONS: Required<ThumbnailOptions> = {
  width: 300,
  height: 300,
  backgroundColor: '#1a1a1a', // Dark background
  obstacleColor: '#666666',   // Gray for obstacles
  preplacedColors: [
    '#3b82f6', // Blue (color 1)
    '#f59e0b', // Yellow (color 2)  
    '#ef4444', // Red (color 3)
    '#22c55e'  // Green (color 4)
  ],
  gridColor: '#333333',
  showGrid: true
};

export class ThumbnailGenerator {
  private canvas: HTMLCanvasElement | null = null;
  private ctx: CanvasRenderingContext2D | null = null;
  private readonly BOARD_SIZE = 20;
  private readonly isServer = typeof window === 'undefined';

  constructor() {
    // Only create canvas on client-side
    if (!this.isServer) {
      this.canvas = document.createElement('canvas');
      const ctx = this.canvas.getContext('2d');
      if (!ctx) {
        throw new Error('Canvas context not supported');
      }
      this.ctx = ctx;
    }
  }

  /**
   * Generate thumbnail from board state
   */
  async generateThumbnail(
    boardState: BoardState,
    options: ThumbnailOptions = {}
  ): Promise<string> {
    // Convert int[] format to legacy format for rendering
    const legacyState = toLegacyBoardState(boardState);
    const opts = { ...DEFAULT_OPTIONS, ...options };
    
    // Use SVG generation on server-side or if canvas is not available
    if (this.isServer || !this.canvas || !this.ctx) {
      return this.generateSVGThumbnail(legacyState, opts);
    }
    
    // Client-side canvas generation
    this.canvas.width = opts.width;
    this.canvas.height = opts.height;

    // Calculate cell size
    const cellSize = Math.min(opts.width, opts.height) / this.BOARD_SIZE;
    const offsetX = (opts.width - cellSize * this.BOARD_SIZE) / 2;
    const offsetY = (opts.height - cellSize * this.BOARD_SIZE) / 2;

    // Clear canvas with background color
    this.ctx.fillStyle = opts.backgroundColor;
    this.ctx.fillRect(0, 0, opts.width, opts.height);

    // Draw grid if enabled
    if (opts.showGrid) {
      this.drawGrid(cellSize, offsetX, offsetY, opts.gridColor);
    }

    // Draw obstacles
    this.drawObstacles(legacyState.obstacles, cellSize, offsetX, offsetY, opts.obstacleColor);

    // Draw preplaced blocks
    this.drawPreplacedBlocks(legacyState.preplaced, cellSize, offsetX, offsetY, opts.preplacedColors);

    // Convert canvas to data URL
    return this.canvas.toDataURL('image/png', 0.8);
  }

  /**
   * Generate SVG-based thumbnail (server-side compatible)
   */
  private generateSVGThumbnail(
    legacyState: { obstacles: Array<{x: number, y: number}>; preplaced: Array<{x: number, y: number, color: number}> },
    options: Required<ThumbnailOptions>
  ): string {
    const { width, height, backgroundColor, obstacleColor, preplacedColors, gridColor, showGrid } = options;
    
    // Calculate cell size
    const cellSize = Math.min(width, height) / this.BOARD_SIZE;
    const offsetX = (width - cellSize * this.BOARD_SIZE) / 2;
    const offsetY = (height - cellSize * this.BOARD_SIZE) / 2;

    let svgContent = `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">`;
    
    // Background
    svgContent += `<rect width="${width}" height="${height}" fill="${backgroundColor}"/>`;
    
    // Grid
    if (showGrid) {
      svgContent += this.generateSVGGrid(cellSize, offsetX, offsetY, gridColor);
    }
    
    // Obstacles
    svgContent += this.generateSVGObstacles(legacyState.obstacles, cellSize, offsetX, offsetY, obstacleColor);
    
    // Preplaced blocks
    svgContent += this.generateSVGPreplacedBlocks(legacyState.preplaced, cellSize, offsetX, offsetY, preplacedColors);
    
    svgContent += '</svg>';
    
    // Convert SVG to data URL
    return `data:image/svg+xml;base64,${btoa(svgContent)}`;
  }

  /**
   * Generate SVG grid
   */
  private generateSVGGrid(cellSize: number, offsetX: number, offsetY: number, gridColor: string): string {
    let grid = '';
    
    // Vertical lines
    for (let x = 0; x <= this.BOARD_SIZE; x++) {
      const posX = offsetX + x * cellSize;
      grid += `<line x1="${posX}" y1="${offsetY}" x2="${posX}" y2="${offsetY + this.BOARD_SIZE * cellSize}" stroke="${gridColor}" stroke-width="1"/>`;
    }
    
    // Horizontal lines
    for (let y = 0; y <= this.BOARD_SIZE; y++) {
      const posY = offsetY + y * cellSize;
      grid += `<line x1="${offsetX}" y1="${posY}" x2="${offsetX + this.BOARD_SIZE * cellSize}" y2="${posY}" stroke="${gridColor}" stroke-width="1"/>`;
    }
    
    return grid;
  }

  /**
   * Generate SVG obstacles
   */
  private generateSVGObstacles(
    obstacles: Array<{x: number, y: number}>,
    cellSize: number,
    offsetX: number,
    offsetY: number,
    obstacleColor: string
  ): string {
    let obstaclesSVG = '';
    
    obstacles.forEach(({ x, y }) => {
      if (x >= 0 && x < this.BOARD_SIZE && y >= 0 && y < this.BOARD_SIZE) {
        const posX = offsetX + x * cellSize + 1;
        const posY = offsetY + y * cellSize + 1;
        const size = cellSize - 2;
        
        obstaclesSVG += `<rect x="${posX}" y="${posY}" width="${size}" height="${size}" fill="${obstacleColor}" stroke="#999999" stroke-width="1"/>`;
      }
    });
    
    return obstaclesSVG;
  }

  /**
   * Generate SVG preplaced blocks
   */
  private generateSVGPreplacedBlocks(
    preplaced: Array<{x: number, y: number, color: number}>,
    cellSize: number,
    offsetX: number,
    offsetY: number,
    colors: string[]
  ): string {
    let blocksSVG = '';
    
    preplaced.forEach(({ x, y, color }) => {
      if (x >= 0 && x < this.BOARD_SIZE && y >= 0 && y < this.BOARD_SIZE) {
        const colorIndex = Math.max(0, Math.min(color - 1, colors.length - 1));
        const blockColor = colors[colorIndex];
        const lightColor = this.lightenColorSVG(blockColor, 0.3);
        
        const posX = offsetX + x * cellSize + 2;
        const posY = offsetY + y * cellSize + 2;
        const size = cellSize - 4;
        
        // Main block
        blocksSVG += `<rect x="${posX}" y="${posY}" width="${size}" height="${size}" fill="${blockColor}" stroke="${lightColor}" stroke-width="2"/>`;
        
        // Highlight effect
        blocksSVG += `<rect x="${posX}" y="${posY}" width="${size}" height="3" fill="${lightColor}"/>`;
        blocksSVG += `<rect x="${posX}" y="${posY}" width="3" height="${size}" fill="${lightColor}"/>`;
      }
    });
    
    return blocksSVG;
  }

  /**
   * Lighten color for SVG (simplified version)
   */
  private lightenColorSVG(color: string, factor: number): string {
    return this.lightenColor(color, factor);
  }

  /**
   * Generate thumbnail and save to file system
   */
  async generateAndSaveThumbnail(
    boardState: BoardState,
    stageNumber: number,
    options: ThumbnailOptions = {}
  ): Promise<string> {
    const dataUrl = await this.generateThumbnail(boardState, options);
    
    // For server-side, we'll just return a placeholder path
    if (this.isServer) {
      const filename = `stage-${stageNumber}-thumbnail.png`;
      return `/api/stages/thumbnails/${filename}`;
    }
    
    // Client-side: Convert data URL to blob
    const response = await fetch(dataUrl);
    const blob = await response.blob();
    
    // Create filename
    const filename = `stage-${stageNumber}-thumbnail.png`;
    const filepath = `/api/stages/thumbnails/${filename}`;
    
    // In a real implementation, you would save to file system here
    // For now, we'll return the data URL or file path
    return filepath;
  }

  /**
   * Draw grid lines
   */
  private drawGrid(cellSize: number, offsetX: number, offsetY: number, gridColor: string): void {
    if (!this.ctx) return;
    
    this.ctx.strokeStyle = gridColor;
    this.ctx.lineWidth = 1;

    // Vertical lines
    for (let x = 0; x <= this.BOARD_SIZE; x++) {
      const posX = offsetX + x * cellSize;
      this.ctx.beginPath();
      this.ctx.moveTo(posX, offsetY);
      this.ctx.lineTo(posX, offsetY + this.BOARD_SIZE * cellSize);
      this.ctx.stroke();
    }

    // Horizontal lines
    for (let y = 0; y <= this.BOARD_SIZE; y++) {
      const posY = offsetY + y * cellSize;
      this.ctx.beginPath();
      this.ctx.moveTo(offsetX, posY);
      this.ctx.lineTo(offsetX + this.BOARD_SIZE * cellSize, posY);
      this.ctx.stroke();
    }
  }

  /**
   * Draw obstacles on the board
   */
  private drawObstacles(
    obstacles: Array<{x: number, y: number}>,
    cellSize: number,
    offsetX: number,
    offsetY: number,
    obstacleColor: string
  ): void {
    if (!this.ctx) return;
    
    this.ctx.fillStyle = obstacleColor;

    obstacles.forEach(({ x, y }) => {
      if (x >= 0 && x < this.BOARD_SIZE && y >= 0 && y < this.BOARD_SIZE) {
        const posX = offsetX + x * cellSize;
        const posY = offsetY + y * cellSize;
        
        // Draw filled rectangle with slight border
        this.ctx!.fillRect(posX + 1, posY + 1, cellSize - 2, cellSize - 2);
        
        // Add border for better visibility
        this.ctx!.strokeStyle = '#999999';
        this.ctx!.lineWidth = 1;
        this.ctx!.strokeRect(posX + 1, posY + 1, cellSize - 2, cellSize - 2);
      }
    });
  }

  /**
   * Draw preplaced blocks
   */
  private drawPreplacedBlocks(
    preplaced: Array<{x: number, y: number, color: number}>,
    cellSize: number,
    offsetX: number,
    offsetY: number,
    colors: string[]
  ): void {
    if (!this.ctx) return;
    
    preplaced.forEach(({ x, y, color }) => {
      if (x >= 0 && x < this.BOARD_SIZE && y >= 0 && y < this.BOARD_SIZE) {
        const colorIndex = Math.max(0, Math.min(color - 1, colors.length - 1));
        const blockColor = colors[colorIndex];
        
        const posX = offsetX + x * cellSize;
        const posY = offsetY + y * cellSize;
        
        // Draw filled rectangle
        this.ctx!.fillStyle = blockColor;
        this.ctx!.fillRect(posX + 2, posY + 2, cellSize - 4, cellSize - 4);
        
        // Add border
        this.ctx!.strokeStyle = this.lightenColor(blockColor, 0.3);
        this.ctx!.lineWidth = 2;
        this.ctx!.strokeRect(posX + 2, posY + 2, cellSize - 4, cellSize - 4);
        
        // Add highlight for 3D effect
        this.ctx!.fillStyle = this.lightenColor(blockColor, 0.5);
        this.ctx!.fillRect(posX + 2, posY + 2, cellSize - 4, 3);
        this.ctx!.fillRect(posX + 2, posY + 2, 3, cellSize - 4);
      }
    });
  }

  /**
   * Lighten a color by a factor
   */
  private lightenColor(color: string, factor: number): string {
    // Simple color lightening - in production you might want a more robust solution
    const hex = color.replace('#', '');
    const r = parseInt(hex.substr(0, 2), 16);
    const g = parseInt(hex.substr(2, 2), 16);
    const b = parseInt(hex.substr(4, 2), 16);
    
    const newR = Math.min(255, Math.floor(r + (255 - r) * factor));
    const newG = Math.min(255, Math.floor(g + (255 - g) * factor));
    const newB = Math.min(255, Math.floor(b + (255 - b) * factor));
    
    return `#${newR.toString(16).padStart(2, '0')}${newG.toString(16).padStart(2, '0')}${newB.toString(16).padStart(2, '0')}`;
  }

  /**
   * Cleanup resources
   */
  dispose(): void {
    // Canvas cleanup if needed
  }
}

/**
 * Server-side thumbnail generation using node-canvas (if available)
 * This is a fallback for server-side rendering
 */
export async function generateServerThumbnail(
  boardState: BoardState,
  stageNumber: number,
  options: ThumbnailOptions = {}
): Promise<string> {
  // In a Node.js environment, you would use node-canvas here
  // For now, return a placeholder path
  const filename = `stage-${stageNumber}-thumbnail.png`;
  return `/api/stages/thumbnails/${filename}`;
}

/**
 * Generate a simple text-based thumbnail as fallback
 */
export function generateTextThumbnail(
  boardState: BoardState,
  stageNumber: number
): string {
  const legacyState = toLegacyBoardState(boardState);
  const obstacleCount = legacyState.obstacles.length;
  const preplacedCount = legacyState.preplaced.length;
  
  // Create a simple SVG thumbnail
  const svg = `
    <svg width="300" height="300" xmlns="http://www.w3.org/2000/svg">
      <rect width="300" height="300" fill="#1a1a1a"/>
      <text x="150" y="120" text-anchor="middle" fill="#ffffff" font-family="Arial" font-size="24" font-weight="bold">
        Stage ${stageNumber}
      </text>
      <text x="150" y="160" text-anchor="middle" fill="#cccccc" font-family="Arial" font-size="16">
        ${obstacleCount} obstacles
      </text>
      <text x="150" y="180" text-anchor="middle" fill="#cccccc" font-family="Arial" font-size="16">
        ${preplacedCount} preplaced
      </text>
      <rect x="50" y="200" width="200" height="80" fill="none" stroke="#666666" stroke-width="2" rx="10"/>
    </svg>
  `;
  
  // Convert SVG to data URL
  const dataUrl = `data:image/svg+xml;base64,${btoa(svg)}`;
  return dataUrl;
}

// Export default instance (conditionally)
let defaultInstance: ThumbnailGenerator | null = null;

export function getThumbnailGenerator(): ThumbnailGenerator {
  if (!defaultInstance) {
    defaultInstance = new ThumbnailGenerator();
  }
  return defaultInstance;
}

// Export default for backward compatibility
export default getThumbnailGenerator();