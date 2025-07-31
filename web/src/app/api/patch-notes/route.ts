import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse, PaginatedResponse, PatchNote } from '@/types';

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '10');
    
    const skip = (page - 1) * limit;

    const [patchNotes, total] = await Promise.all([
      prisma.patchNote.findMany({
        orderBy: { releaseDate: 'desc' },
        skip,
        take: limit,
      }),
      prisma.patchNote.count(),
    ]);

    const response: PaginatedResponse<PatchNote> = {
      success: true,
      data: patchNotes.map(patchNote => ({
        ...patchNote,
        releaseDate: patchNote.releaseDate.toISOString(),
        createdAt: patchNote.createdAt.toISOString(),
      })),
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
      },
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Get patch notes error:', error);
    const response: ApiResponse = {
      success: false,
      error: '패치 노트를 불러오는데 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}