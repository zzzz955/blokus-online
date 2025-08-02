import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';
import { getAdminFromRequest, requireAdmin } from '@/lib/admin-auth';

const prisma = new PrismaClient();

export interface CreatePatchNoteRequest {
  version: string;
  title: string;
  content: string;
  release_date: string;
  download_url?: string;
}

export interface UpdatePatchNoteRequest {
  version?: string;
  title?: string;
  content?: string;
  release_date?: string;
  download_url?: string;
}

/**
 * 패치노트 목록 조회 (관리자용)
 */
export async function GET(request: NextRequest) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json({
        success: false,
        error: '관리자 권한이 필요합니다.'
      }, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '10');
    const search = searchParams.get('search') || '';
    
    const skip = (page - 1) * limit;

    // 검색 조건 구성
    const where: any = {};
    
    if (search) {
      where.OR = [
        { version: { contains: search, mode: 'insensitive' } },
        { title: { contains: search, mode: 'insensitive' } },
        { content: { contains: search, mode: 'insensitive' } }
      ];
    }

    // 총 개수 조회
    const totalCount = await prisma.patchNote.count({ where });

    // 패치노트 목록 조회
    const patchNotes = await prisma.patchNote.findMany({
      where,
      orderBy: [
        { release_date: 'desc' },
        { created_at: 'desc' }
      ],
      skip,
      take: limit
    });

    return NextResponse.json({
      success: true,
      data: {
        patchNotes,
        pagination: {
          page,
          limit,
          totalCount,
          totalPages: Math.ceil(totalCount / limit)
        }
      }
    });

  } catch (error) {
    console.error('패치노트 목록 조회 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}

/**
 * 패치노트 생성
 */
export async function POST(request: NextRequest) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json({
        success: false,
        error: '관리자 권한이 필요합니다.'
      }, { status: 401 });
    }

    const body: CreatePatchNoteRequest = await request.json();
    const { version, title, content, release_date, download_url } = body;

    // 입력 검증
    if (!version || !title || !content || !release_date) {
      return NextResponse.json({
        success: false,
        error: '버전, 제목, 내용, 릴리즈 날짜를 모두 입력해주세요.'
      }, { status: 400 });
    }

    // 버전 형식 검증 (예: v1.0.0, 1.0.0)
    const versionPattern = /^v?\d+\.\d+\.\d+$/;
    if (!versionPattern.test(version)) {
      return NextResponse.json({
        success: false,
        error: '버전 형식이 올바르지 않습니다. (예: v1.0.0 또는 1.0.0)'
      }, { status: 400 });
    }

    if (title.length > 200) {
      return NextResponse.json({
        success: false,
        error: '제목은 200자 이하로 입력해주세요.'
      }, { status: 400 });
    }

    // 날짜 형식 검증
    const parsedDate = new Date(release_date);
    if (isNaN(parsedDate.getTime())) {
      return NextResponse.json({
        success: false,
        error: '유효하지 않은 날짜 형식입니다.'
      }, { status: 400 });
    }

    // 중복 버전 체크
    const existingPatchNote = await prisma.patchNote.findUnique({
      where: { version }
    });

    if (existingPatchNote) {
      return NextResponse.json({
        success: false,
        error: '이미 존재하는 버전입니다.'
      }, { status: 400 });
    }

    // 패치노트 생성
    const patchNote = await prisma.patchNote.create({
      data: {
        version,
        title,
        content,
        release_date: parsedDate,
        download_url
      }
    });

    return NextResponse.json({
      success: true,
      data: patchNote,
      message: '패치노트가 생성되었습니다.'
    });

  } catch (error) {
    console.error('패치노트 생성 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}

/**
 * 패치노트 수정
 */
export async function PUT(request: NextRequest) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json({
        success: false,
        error: '관리자 권한이 필요합니다.'
      }, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const id = parseInt(searchParams.get('id') || '0');

    if (!id) {
      return NextResponse.json({
        success: false,
        error: '패치노트 ID가 필요합니다.'
      }, { status: 400 });
    }

    const body: UpdatePatchNoteRequest = await request.json();
    const { version, title, content, release_date, download_url } = body;

    // 입력 검증
    if (version) {
      const versionPattern = /^v?\d+\.\d+\.\d+$/;
      if (!versionPattern.test(version)) {
        return NextResponse.json({
          success: false,
          error: '버전 형식이 올바르지 않습니다. (예: v1.0.0 또는 1.0.0)'
        }, { status: 400 });
      }
    }

    if (title && title.length > 200) {
      return NextResponse.json({
        success: false,
        error: '제목은 200자 이하로 입력해주세요.'
      }, { status: 400 });
    }

    if (release_date) {
      const parsedDate = new Date(release_date);
      if (isNaN(parsedDate.getTime())) {
        return NextResponse.json({
          success: false,
          error: '유효하지 않은 날짜 형식입니다.'
        }, { status: 400 });
      }
    }

    // 기존 패치노트 확인
    const existingPatchNote = await prisma.patchNote.findUnique({
      where: { id }
    });

    if (!existingPatchNote) {
      return NextResponse.json({
        success: false,
        error: '존재하지 않는 패치노트입니다.'
      }, { status: 404 });
    }

    // 버전 중복 체크 (다른 패치노트와의 중복)
    if (version && version !== existingPatchNote.version) {
      const duplicateVersion = await prisma.patchNote.findUnique({
        where: { version }
      });

      if (duplicateVersion) {
        return NextResponse.json({
          success: false,
          error: '이미 존재하는 버전입니다.'
        }, { status: 400 });
      }
    }

    // 업데이트할 데이터 구성
    const updateData: any = {};
    if (version !== undefined) updateData.version = version;
    if (title !== undefined) updateData.title = title;
    if (content !== undefined) updateData.content = content;
    if (release_date !== undefined) updateData.release_date = new Date(release_date);
    if (download_url !== undefined) updateData.download_url = download_url;

    // 패치노트 수정
    const updatedPatchNote = await prisma.patchNote.update({
      where: { id },
      data: updateData
    });

    return NextResponse.json({
      success: true,
      data: updatedPatchNote,
      message: '패치노트가 수정되었습니다.'
    });

  } catch (error) {
    console.error('패치노트 수정 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}

/**
 * 패치노트 삭제
 */
export async function DELETE(request: NextRequest) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json({
        success: false,
        error: '관리자 권한이 필요합니다.'
      }, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const id = parseInt(searchParams.get('id') || '0');

    if (!id) {
      return NextResponse.json({
        success: false,
        error: '패치노트 ID가 필요합니다.'
      }, { status: 400 });
    }

    // 기존 패치노트 확인
    const existingPatchNote = await prisma.patchNote.findUnique({
      where: { id }
    });

    if (!existingPatchNote) {
      return NextResponse.json({
        success: false,
        error: '존재하지 않는 패치노트입니다.'
      }, { status: 404 });
    }

    // 패치노트 삭제
    await prisma.patchNote.delete({
      where: { id }
    });

    return NextResponse.json({
      success: true,
      message: '패치노트가 삭제되었습니다.'
    });

  } catch (error) {
    console.error('패치노트 삭제 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}