import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';
import { getAdminFromRequest, requireAdmin } from '@/lib/server/admin-auth';

const prisma = new PrismaClient();

// Force dynamic rendering for this API route
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

// 후기 업데이트 데이터 검증
function validateTestimonialUpdate(data: any) {
  const errors: string[] = [];
  
  if (data.isPinned !== undefined && typeof data.isPinned !== 'boolean') {
    errors.push('isPinned은 boolean 값이어야 합니다');
  }
  
  if (data.isPublished !== undefined && typeof data.isPublished !== 'boolean') {
    errors.push('isPublished는 boolean 값이어야 합니다');
  }
  
  return {
    isValid: errors.length === 0,
    errors
  };
}

// PUT /api/admin/testimonials/[id] - 후기 상태 업데이트
export async function PUT(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json(
        { success: false, error: '관리자 권한이 필요합니다.' },
        { status: 401 }
      );
    }

    const id = parseInt(params.id);
    if (isNaN(id)) {
      return NextResponse.json(
        { success: false, error: '유효하지 않은 후기 ID입니다.' },
        { status: 400 }
      );
    }

    const body = await request.json();
    
    // 입력 데이터 검증
    const validation = validateTestimonialUpdate(body);
    if (!validation.isValid) {
      return NextResponse.json(
        { 
          success: false, 
          error: validation.errors.join(', ')
        },
        { status: 400 }
      );
    }

    // 업데이트할 필드가 있는지 확인
    if (body.isPinned === undefined && body.isPublished === undefined) {
      return NextResponse.json(
        { success: false, error: '업데이트할 필드가 없습니다.' },
        { status: 400 }
      );
    }

    // 후기 업데이트
    const updateData: any = {};
    if (body.isPinned !== undefined) {
      updateData.is_pinned = body.isPinned;
    }
    if (body.isPublished !== undefined) {
      updateData.is_published = body.isPublished;
    }

    const testimonial = await prisma.testimonial.update({
      where: { id },
      data: updateData,
      select: {
        id: true,
        name: true,
        rating: true,
        comment: true,
        created_at: true,
        is_pinned: true,
        is_published: true
      }
    });

    const updatedTestimonial = {
      id: testimonial.id,
      name: testimonial.name,
      rating: testimonial.rating,
      comment: testimonial.comment,
      createdAt: testimonial.created_at.toISOString(),
      isPinned: testimonial.is_pinned,
      isPublished: testimonial.is_published
    };

    return NextResponse.json({
      success: true,
      data: updatedTestimonial,
      message: '후기가 성공적으로 업데이트되었습니다.'
    });

  } catch (error) {
    console.error('Error updating testimonial:', error);
    
    return NextResponse.json(
      { success: false, error: '후기 업데이트에 실패했습니다.' },
      { status: 500 }
    );
  }
}

// DELETE /api/admin/testimonials/[id] - 후기 삭제
export async function DELETE(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json(
        { success: false, error: '관리자 권한이 필요합니다.' },
        { status: 401 }
      );
    }

    const id = parseInt(params.id);
    if (isNaN(id)) {
      return NextResponse.json(
        { success: false, error: '유효하지 않은 후기 ID입니다.' },
        { status: 400 }
      );
    }

    // 후기 삭제
    await prisma.testimonial.delete({
      where: { id }
    });

    return NextResponse.json({
      success: true,
      message: '후기가 성공적으로 삭제되었습니다.'
    });

  } catch (error) {
    console.error('Error deleting testimonial:', error);
    return NextResponse.json(
      { success: false, error: '후기 삭제에 실패했습니다.' },
      { status: 500 }
    );
  }
}