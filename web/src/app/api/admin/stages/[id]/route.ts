import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';
import { expandBoardState, toBoardStateDB } from '@/lib/board-state-codec';

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
      initial_board_state,       // 구/신 포맷 모두 허용
      available_blocks,
      optimal_score,
      time_limit,
      max_undo_count,
      stage_description,
      stage_hints,
      thumbnail_url,             // '' | null 이면 재생성 의사
      is_active,
      is_featured
    } = body;

    // 현재 DB 값 조회
    const existingRows = await prisma.$queryRaw`
      SELECT 
        stage_id,
        stage_number,
        initial_board_state,
        available_blocks,
        thumbnail_url,
        time_limit
      FROM stages
      WHERE stage_id = ${stageId}
    `;
    if (!(existingRows as any[])?.length) {
      return NextResponse.json({
        success: false,
        message: 'Stage not found',
        error: 'NOT_FOUND'
      }, { status: 404 });
    }
    const current = (existingRows as any[])[0];

    // time_limit: 키가 없으면 기존 유지 (null도 허용)
    const finalTimeLimit =
      Object.prototype.hasOwnProperty.call(body, 'time_limit')
        ? time_limit
        : current.time_limit;

    // ── 1) IBS(보드 상태): 저장은 항상 "DATABASE INTEGER[] 포맷"
    const hasIbs = Object.prototype.hasOwnProperty.call(body, 'initial_board_state');
    let dbFormatForSave: number[] | undefined = undefined;  // DB에 저장할 INTEGER[] 포맷
    let expandedForThumb: any | undefined = undefined;      // 썸네일 생성용 확장 포맷

    if (hasIbs) {
      if (initial_board_state === null) {
        dbFormatForSave = []; // DB에 빈 배열 저장
      } else {
        const expanded = expandBoardState(initial_board_state); // 구/신 포맷 상관없이 확장으로
        dbFormatForSave = toBoardStateDB(initial_board_state);
        expandedForThumb = expanded;
      }
    }

    // ── 2) 변경 여부 판단 (썸네일 갱신 조건)
    const jsonEqual = (a: any, b: any) => JSON.stringify(a) === JSON.stringify(b);
    const sortedEqual = (a?: number[], b?: number[]) => {
      if (a == null && b == null) return true;
      if (!Array.isArray(a) || !Array.isArray(b)) return false;
      if (a.length !== b.length) return false;
      const aa = [...a].sort((x, y) => x - y);
      const bb = [...b].sort((x, y) => x - y);
      for (let i = 0; i < aa.length; i++) if (aa[i] !== bb[i]) return false;
      return true;
    };

    const currentIbsDB = Array.isArray(current.initial_board_state) 
      ? current.initial_board_state 
      : (current.initial_board_state || []);

    let ibsChanged = false;
    if (hasIbs) {
      if (dbFormatForSave === undefined) {
        ibsChanged = false; // 값이 제공되지 않음
      } else if (dbFormatForSave.length === 0) {
        ibsChanged = currentIbsDB.length > 0; // 빈 배열로 변경
      } else {
        ibsChanged = !jsonEqual(dbFormatForSave, currentIbsDB);
      }
    }

    let blocksChanged = false;
    if (available_blocks !== undefined) {
      blocksChanged = !sortedEqual(available_blocks, current.available_blocks as number[]);
    }

    const numberChanged = (stage_number !== undefined && stage_number !== current.stage_number);

    const hasThumbField = Object.prototype.hasOwnProperty.call(body, 'thumbnail_url');
    const forceRegen = hasThumbField && (thumbnail_url === '' || thumbnail_url === null);

    // ── 3) 썸네일 결정: CREATE와 동일 흐름 (커스텀 라이브러리 사용)
    let finalThumbnailUrl: string;

    // 관리자가 명시 값을 넣으면 그대로 사용
    if (hasThumbField && typeof thumbnail_url === 'string' && thumbnail_url.trim() !== '') {
      finalThumbnailUrl = thumbnail_url.trim();
    } else if (forceRegen || ibsChanged || blocksChanged || numberChanged) {
      // 내부 유틸 import (동적 import: 라우트 핫리로드/번들 충돌 방지)
      const { getThumbnailGenerator } = await import('@/lib/thumbnail-generator');
      const fileStorage = (await import('@/lib/file-storage')).default;

      // stage 번호 기준 기존 썸네일 정리
      const targetStageNum = stage_number ?? current.stage_number;

      // 번호가 바뀐다면, 이전 번호/새 번호 둘 다 청소(중복 정리 안전)
      await fileStorage.cleanupOldThumbnails(current.stage_number);
      if (numberChanged) await fileStorage.cleanupOldThumbnails(targetStageNum);

      // 썸네일 생성: 확장 포맷 필요
      const generator = getThumbnailGenerator();

      // 확장 포맷 준비 (요청에 안 왔으면 DB 저장값을 확장)
      const boardForThumb = expandedForThumb
        ?? expandBoardState(currentIbsDB);

      const blocksForThumb = available_blocks ?? (current.available_blocks as number[]);

      const dataUrl = await generator.generateThumbnail(boardForThumb, {
        width: 300,
        height: 300
      });

      const filename = fileStorage.generateThumbnailFilename(targetStageNum);
      finalThumbnailUrl = await fileStorage.saveThumbnail(dataUrl, filename);
    } else {
      // 변경 없음 → 기존 유지
      finalThumbnailUrl = current.thumbnail_url as string;
    }

    // ── 4) UPDATE (썸네일은 "항상" 최종값 직접 세팅)
    const updated = await prisma.$queryRaw`
      UPDATE stages SET
        stage_number = COALESCE(${stage_number}, stage_number),
        difficulty = COALESCE(${difficulty}, difficulty),
        initial_board_state = COALESCE(${dbFormatForSave}, initial_board_state),
        available_blocks = COALESCE(${available_blocks}, available_blocks),
        optimal_score = COALESCE(${optimal_score}, optimal_score),
        time_limit = ${finalTimeLimit},
        max_undo_count = COALESCE(${max_undo_count}, max_undo_count),
        stage_description = COALESCE(${stage_description}, stage_description),
        stage_hints = COALESCE(${stage_hints}, stage_hints),
        thumbnail_url = ${finalThumbnailUrl},    -- ★ COALESCE 아님: 항상 최종값으로 설정
        is_active = COALESCE(${is_active}, is_active),
        is_featured = COALESCE(${is_featured}, is_featured),
        updated_at = NOW()
      WHERE stage_id = ${stageId}
      RETURNING stage_id, stage_number, updated_at
    `;

    return NextResponse.json({
      success: true,
      message: 'Stage updated successfully',
      data: { stage: (updated as any[])[0] }
    });

  } catch (error) {
    console.error('Failed to update stage:', error);
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