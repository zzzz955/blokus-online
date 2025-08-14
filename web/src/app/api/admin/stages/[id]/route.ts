import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

// GET - Retrieve specific stage
export async function GET(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const stageId = parseInt(params.id);

    if (isNaN(stageId)) {
      return NextResponse.json({
        success: false,
        message: 'Invalid stage ID',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    const stage = await prisma.$queryRaw`
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
      WHERE stage_id = ${stageId}
    `;

    const stageData = (stage as any[])[0];

    if (!stageData) {
      return NextResponse.json({
        success: false,
        message: 'Stage not found',
        error: 'STAGE_NOT_FOUND'
      }, { status: 404 });
    }

    return NextResponse.json({
      success: true,
      message: 'Stage retrieved successfully',
      data: {
        stage: stageData
      }
    });

  } catch (error) {
    console.error('Failed to retrieve stage:', error);
    return NextResponse.json({
      success: false,
      message: 'Failed to retrieve stage',
      error: 'INTERNAL_SERVER_ERROR'
    }, { status: 500 });
  }
}

// PUT - Update specific stage
export async function PUT(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const stageId = parseInt(params.id);

    if (isNaN(stageId)) {
      return NextResponse.json({
        success: false,
        message: 'Invalid stage ID',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

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

    // Validation
    if (stage_number && stage_number <= 0) {
      return NextResponse.json({
        success: false,
        message: 'Stage number must be positive',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    if (difficulty && (difficulty < 1 || difficulty > 10)) {
      return NextResponse.json({
        success: false,
        message: 'Difficulty must be between 1 and 10',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    if (available_blocks && (!Array.isArray(available_blocks) || available_blocks.length === 0)) {
      return NextResponse.json({
        success: false,
        message: 'Available blocks array cannot be empty',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    if (optimal_score !== undefined && optimal_score < 0) {
      return NextResponse.json({
        success: false,
        message: 'Optimal score cannot be negative',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    // Check if stage exists
    const existingStage = await prisma.$queryRaw`
      SELECT stage_id, stage_number FROM stages WHERE stage_id = ${stageId}
    `;

    if ((existingStage as any[]).length === 0) {
      return NextResponse.json({
        success: false,
        message: 'Stage not found',
        error: 'STAGE_NOT_FOUND'
      }, { status: 404 });
    }

    // Check if stage number is being changed to an existing number
    if (stage_number && stage_number !== (existingStage as any[])[0].stage_number) {
      const duplicateCheck = await prisma.$queryRaw`
        SELECT stage_id FROM stages WHERE stage_number = ${stage_number} AND stage_id != ${stageId}
      `;

      if ((duplicateCheck as any[]).length > 0) {
        return NextResponse.json({
          success: false,
          message: `Stage number ${stage_number} already exists`,
          error: 'DUPLICATE_STAGE_NUMBER'
        }, { status: 409 });
      }
    }

    const current = (existingStage as any[])[0];
    const finalTimeLimit =
      Object.prototype.hasOwnProperty.call(body, 'time_limit')
        ? time_limit  // undefined가 아니라면 값 그대로(설령 null이어도)
        : current.time_limit;

    // Generate thumbnail if not provided but board state or blocks changed
    let finalThumbnailUrl = thumbnail_url;
    if ((!thumbnail_url || thumbnail_url.trim() === '') && (initial_board_state || available_blocks)) {
      // Get current stage data for thumbnail generation
      const currentStage = (existingStage as any[])[0];
      const boardStateForThumbnail = initial_board_state || JSON.parse(JSON.stringify(currentStage.initial_board_state));
      const blocksForThumbnail = available_blocks || currentStage.available_blocks;
      const stageNumberForThumbnail = stage_number || currentStage.stage_number;

      // Import thumbnail generation utilities
      const { getThumbnailGenerator } = await import('@/lib/thumbnail-generator');
      const fileStorage = (await import('@/lib/file-storage')).default;

      // ✅ 기존 썸네일 정리
      await fileStorage.cleanupOldThumbnails(stageNumberForThumbnail);

      const generator = getThumbnailGenerator();
      const dataUrl = await generator.generateThumbnail(boardStateForThumbnail, {
        width: 300,
        height: 300
      });
      const filename = fileStorage.generateThumbnailFilename(stageNumberForThumbnail);
      finalThumbnailUrl = await fileStorage.saveThumbnail(dataUrl, filename);
    }

    // Update stage
    const updatedStage = await prisma.$queryRaw`
      UPDATE stages SET
        stage_number = COALESCE(${stage_number}, stage_number),
        difficulty = COALESCE(${difficulty}, difficulty),
        initial_board_state = COALESCE(${JSON.stringify(initial_board_state)}::jsonb, initial_board_state),
        available_blocks = COALESCE(${available_blocks}, available_blocks),
        optimal_score = COALESCE(${optimal_score}, optimal_score),
        time_limit = ${finalTimeLimit},
        max_undo_count = COALESCE(${max_undo_count}, max_undo_count),
        stage_description = COALESCE(${stage_description}, stage_description),
        stage_hints = COALESCE(${stage_hints}, stage_hints),
        thumbnail_url = COALESCE(${finalThumbnailUrl}, thumbnail_url),
        is_active = COALESCE(${is_active}, is_active),
        is_featured = COALESCE(${is_featured}, is_featured),
        updated_at = NOW()
      WHERE stage_id = ${stageId}
      RETURNING stage_id, stage_number, updated_at
    `;

    const updatedData = (updatedStage as any[])[0];

    return NextResponse.json({
      success: true,
      message: 'Stage updated successfully',
      data: {
        stage: updatedData
      }
    });

  } catch (error) {
    console.error('Failed to update stage:', error);

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
      message: 'Failed to update stage',
      error: 'INTERNAL_SERVER_ERROR'
    }, { status: 500 });
  }
}

// DELETE - Delete specific stage
export async function DELETE(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const stageId = parseInt(params.id);

    if (isNaN(stageId)) {
      return NextResponse.json({
        success: false,
        message: 'Invalid stage ID',
        error: 'VALIDATION_ERROR'
      }, { status: 400 });
    }

    // Check if stage exists
    const existingStage = await prisma.$queryRaw`
      SELECT stage_id, stage_number FROM stages WHERE stage_id = ${stageId}
    `;

    if ((existingStage as any[]).length === 0) {
      return NextResponse.json({
        success: false,
        message: 'Stage not found',
        error: 'STAGE_NOT_FOUND'
      }, { status: 404 });
    }

    const stageNumber = (existingStage as any[])[0].stage_number;

    // Check if there are user progress records for this stage
    const progressCount = await prisma.$queryRaw`
      SELECT COUNT(*) as count FROM user_stage_progress WHERE stage_id = ${stageId}
    `;

    const progressRecords = (progressCount as any[])[0].count;

    // If there are progress records, consider soft delete or ask for confirmation
    if (parseInt(progressRecords) > 0) {
      return NextResponse.json({
        success: false,
        message: `Cannot delete stage ${stageNumber}. It has ${progressRecords} user progress records.`,
        error: 'STAGE_HAS_PROGRESS',
        data: {
          progressRecords: parseInt(progressRecords)
        }
      }, { status: 409 });
    }

    // Delete the stage
    await prisma.$queryRaw`
      DELETE FROM stages WHERE stage_id = ${stageId}
    `;

    return NextResponse.json({
      success: true,
      message: `Stage ${stageNumber} deleted successfully`,
      data: {
        deletedStageId: stageId,
        deletedStageNumber: stageNumber
      }
    });

  } catch (error) {
    console.error('Failed to delete stage:', error);
    return NextResponse.json({
      success: false,
      message: 'Failed to delete stage',
      error: 'INTERNAL_SERVER_ERROR'
    }, { status: 500 });
  }
}