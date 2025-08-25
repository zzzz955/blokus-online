import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';
import { getAdminFromRequest, requireAdmin } from '@/lib/server/admin-auth';

const prisma = new PrismaClient();

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

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
    const status = searchParams.get('status'); // 'published', 'draft', 'all'
    
    const skip = (page - 1) * limit;

    // 검색 조건 구성
    const where: any = {};
    
    if (search) {
      where.OR = [
        { title: { contains: search, mode: 'insensitive' } },
        { content: { contains: search, mode: 'insensitive' } },
        { author: { contains: search, mode: 'insensitive' } }
      ];
    }

    if (status === 'published') {
      where.is_published = true;
    } else if (status === 'draft') {
      where.is_published = false;
    }

    // 총 개수 조회
    const totalCount = await prisma.announcement.count({ where });

    // 공지사항 목록 조회
    const announcements = await prisma.announcement.findMany({
      where,
      orderBy: [
        { is_pinned: 'desc' },
        { created_at: 'desc' }
      ],
      skip,
      take: limit
    });

    return NextResponse.json({
      success: true,
      data: {
        announcements,
        pagination: {
          page,
          limit,
          totalCount,
          totalPages: Math.ceil(totalCount / limit)
        }
      }
    });

  } catch (error) {
    console.error('공지사항 목록 조회 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}

export async function POST(request: NextRequest) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json({
        success: false,
        error: '관리자 권한이 필요합니다.'
      }, { status: 401 });
    }

    const body = await request.json();
    const { title, content, is_pinned = false, is_published = true } = body;

    // 입력 검증
    if (!title || !content) {
      return NextResponse.json({
        success: false,
        error: '제목과 내용을 입력해주세요.'
      }, { status: 400 });
    }

    if (title.length > 200) {
      return NextResponse.json({
        success: false,
        error: '제목은 200자 이하로 입력해주세요.'
      }, { status: 400 });
    }

    // 공지사항 생성
    const announcement = await prisma.announcement.create({
      data: {
        title,
        content,
        author: admin!.username,
        is_pinned,
        is_published
      }
    });

    return NextResponse.json({
      success: true,
      data: announcement,
      message: '공지사항이 생성되었습니다.'
    });

  } catch (error) {
    console.error('공지사항 생성 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}

/**
 * 공지사항 수정
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
        error: '공지사항 ID가 필요합니다.'
      }, { status: 400 });
    }

    const body = await request.json();
    const { title, content, is_pinned, is_published } = body;

    // 입력 검증
    if (title && title.length > 200) {
      return NextResponse.json({
        success: false,
        error: '제목은 200자 이하로 입력해주세요.'
      }, { status: 400 });
    }

    // 기존 공지사항 확인
    const existingAnnouncement = await prisma.announcement.findUnique({
      where: { id }
    });

    if (!existingAnnouncement) {
      return NextResponse.json({
        success: false,
        error: '존재하지 않는 공지사항입니다.'
      }, { status: 404 });
    }

    // 업데이트할 데이터 구성
    const updateData: any = {};
    if (title !== undefined) updateData.title = title;
    if (content !== undefined) updateData.content = content;
    if (is_pinned !== undefined) updateData.is_pinned = is_pinned;
    if (is_published !== undefined) updateData.is_published = is_published;

    // 공지사항 수정
    const updatedAnnouncement = await prisma.announcement.update({
      where: { id },
      data: updateData
    });

    return NextResponse.json({
      success: true,
      data: updatedAnnouncement,
      message: '공지사항이 수정되었습니다.'
    });

  } catch (error) {
    console.error('공지사항 수정 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}

/**
 * 공지사항 삭제
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
        error: '공지사항 ID가 필요합니다.'
      }, { status: 400 });
    }

    // 기존 공지사항 확인
    const existingAnnouncement = await prisma.announcement.findUnique({
      where: { id }
    });

    if (!existingAnnouncement) {
      return NextResponse.json({
        success: false,
        error: '존재하지 않는 공지사항입니다.'
      }, { status: 404 });
    }

    // 공지사항 삭제
    await prisma.announcement.delete({
      where: { id }
    });

    return NextResponse.json({
      success: true,
      message: '공지사항이 삭제되었습니다.'
    });

  } catch (error) {
    console.error('공지사항 삭제 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}