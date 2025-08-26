import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';
import { normalizeBoardState, type BoardState } from '@/lib/board-state-codec';

const prisma = new PrismaClient();

// GET - Retrieve all stages for admin management
export async function GET() {
  try {
    const stages = await prisma.$queryRaw`
      SELECT 
        stage_id,
        stage_number,
        difficulty,
        initial_board_state,
        available_blocks,
        optimal_score,
        time_limit,
        max_undo_count,
        stage_description,
        stage_hints,
        thumbnail_url,
        is_active,
        is_featured,
        created_at,
        updated_at
      FROM stages
      ORDER BY stage_number ASC
    `;

    return NextResponse.json({
      success: true,
      data: {
        // ✅ DB에서 int[] 포맷으로 직접 사용
        stages: (stages as any[]).map(row => ({
          ...row,
          initial_board_state: row.initial_board_state || []
        })),
        total: (stages as any[]).length
      }
    });

  } catch (error) {
    console.error('Failed to retrieve stages:', error);
    return NextResponse.json({
      success: false,
      message: 'Failed to retrieve stages',
      error: 'INTERNAL_SERVER_ERROR'
    }, { status: 500 });
  }
}

// POST - Create new stage
export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const {
      stage_number,
      difficulty,
      initial_board_state,
      available_blocks,
      optimal_score,
      time_limit,
      max_undo_count,
      stage_description,
      stage_hints,
      thumbnail_url,
      is_active,
      is_featured
    } = body;

    // ✅ 보드 상태 정규화: 모든 포맷 → int[] 포맷
    const boardForDB = initial_board_state
      ? normalizeBoardState(initial_board_state)
      : [];

    // ✅ time_limit 기본값: 무제한(null)
    const finalTimeLimit =
      Object.prototype.hasOwnProperty.call(body, 'time_limit')
        ? time_limit
        : null;

    // Validation
    if (!stage_number || stage_number <= 0) {
      return NextResponse.json({
        success: false,
        message: 'Stage number is required and must be positive',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    if (!difficulty || difficulty < 1 || difficulty > 10) {
      return NextResponse.json({
        success: false,
        message: 'Difficulty must be between 1 and 10',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    if (!available_blocks || !Array.isArray(available_blocks) || available_blocks.length === 0) {
      return NextResponse.json({
        success: false,
        message: 'Available blocks array is required and cannot be empty',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    if (optimal_score < 0) {
      return NextResponse.json({
        success: false,
        message: 'Optimal score cannot be negative',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    // Check if stage number already exists
    const existingStage = await prisma.$queryRaw`
      SELECT stage_id FROM stages WHERE stage_number = ${stage_number}
    `;

    if ((existingStage as any[]).length > 0) {
      return NextResponse.json({
        success: false,
        message: `Stage number ${stage_number} already exists`,
        error: 'DUPLICATE_STAGE_NUMBER'
      }, { status: 409 });
    }

    // Generate thumbnail if not provided
    let finalThumbnailUrl = thumbnail_url;
    if (!finalThumbnailUrl || (typeof finalThumbnailUrl === 'string' && finalThumbnailUrl.trim() === '')) {
      finalThumbnailUrl = await generateStageThumbnail(
        stage_number,
        boardForDB,      // ✅ int[] 포맷으로 썸네일 생성
        available_blocks
      );
    }

    // Create new stage
    const newStage = await prisma.$queryRaw`
      INSERT INTO stages (
        stage_number, difficulty, initial_board_state, available_blocks,
        optimal_score, time_limit, max_undo_count, stage_description,
        stage_hints, thumbnail_url, is_active, is_featured
      ) VALUES (
        ${stage_number}, ${difficulty}, ${boardForDB},
        ${available_blocks}, ${optimal_score}, ${finalTimeLimit}, ${max_undo_count},
        ${stage_description}, ${stage_hints}, ${finalThumbnailUrl},
        ${is_active}, ${is_featured}
      ) RETURNING stage_id, stage_number, created_at
    `;

    return NextResponse.json({
      success: true,
      message: 'Stage created successfully',
      data: { stage: (newStage as any[])[0] }
    }, { status: 201 });

  } catch (error) {
    console.error('Failed to create stage:', error);

    // Handle unique constraint violation
    if ((error as any).code === '23505') {
      return NextResponse.json({
        success: false,
        message: 'Stage number already exists',
        error: 'DUPLICATE_STAGE_NUMBER'
      }, { status: 409 });
    }

    return NextResponse.json({
      success: false,
      message: 'Failed to create stage',
      error: 'INTERNAL_SERVER_ERROR'
    }, { status: 500 });
  }
}

// Import thumbnail generation utilities
import { getThumbnailGenerator } from '@/lib/thumbnail-generator';
import fileStorage from '@/lib/file-storage';

// 같은 파일 내 helper
async function generateStageThumbnail(
  stageNumber: number,
  boardState: BoardState,
  availableBlocks: number[]
): Promise<string | null> {
  try {
    // ✅ 기존 썸네일 정리
    await fileStorage.cleanupOldThumbnails(stageNumber); // 또는 deleteOldThumbnailsForStage

    // ✅ 보드 미리보기 생성
    const generator = getThumbnailGenerator();
    const dataUrl = await generator.generateThumbnail(boardState, {
      width: 300,
      height: 300
    });

    const filename = fileStorage.generateThumbnailFilename(stageNumber);
    const thumbnailUrl = await fileStorage.saveThumbnail(dataUrl, filename);
    return thumbnailUrl;
  } catch (error) {
    console.error('Failed to generate thumbnail:', error);
    return null;
  }
}