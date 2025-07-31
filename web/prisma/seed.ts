import { PrismaClient } from '@prisma/client';
import bcrypt from 'bcryptjs';

const prisma = new PrismaClient();

async function main() {
  console.log('🌱 웹사이트 데이터베이스 시딩 시작...');

  // 관리자 계정 생성
  const adminPassword = process.env.ADMIN_PASSWORD || 'admin123';
  const hashedPassword = await bcrypt.hash(adminPassword, 12);

  const admin = await prisma.adminUser.upsert({
    where: { username: 'admin' },
    update: {},
    create: {
      username: 'admin',
      passwordHash: hashedPassword,
      role: 'SUPER_ADMIN',
    },
  });

  console.log('✅ 관리자 계정 생성:', admin.username);

  // 샘플 공지사항 생성
  const announcement1 = await prisma.announcement.upsert({
    where: { id: 1 },
    update: {},
    create: {
      title: '블로쿠스 온라인 공식 웹사이트 오픈!',
      content: `# 블로쿠스 온라인 공식 웹사이트가 드디어 오픈했습니다! 🎉

안녕하세요, 블로쿠스 온라인 개발팀입니다.

오랜 기다림 끝에 공식 웹사이트가 정식으로 오픈되었습니다. 이제 더욱 편리하게 게임 정보를 확인하고, 최신 업데이트 소식을 받아보실 수 있습니다.

## 웹사이트 주요 기능

- **게임 다운로드**: 최신 클라이언트를 안전하게 다운로드
- **게임 가이드**: 초보자부터 고수까지 모든 전략 가이드
- **공지사항**: 중요한 게임 소식과 이벤트 정보
- **패치 노트**: 게임 업데이트 및 밸런스 변경사항
- **고객지원**: 빠르고 정확한 문의 및 지원 서비스

앞으로도 더 나은 서비스를 제공하기 위해 최선을 다하겠습니다.

감사합니다!`,
      author: 'admin',
      isPinned: true,
      isPublished: true,
    },
  });

  const announcement2 = await prisma.announcement.upsert({
    where: { id: 2 },
    update: {},
    create: {
      title: '게임 이용 규칙 및 매너 안내',
      content: `# 블로쿠스 온라인 이용 규칙

모든 플레이어가 즐겁게 게임을 즐길 수 있도록 다음 규칙을 준수해 주시기 바랍니다.

## 기본 매너

1. **상대방 존중**: 모든 플레이어를 존중하며 게임하세요
2. **페어플레이**: 부정행위나 버그 악용을 금지합니다
3. **적절한 언어 사용**: 욕설이나 비방은 제재 대상입니다

## 금지 행위

- 핵/치트 프로그램 사용
- 계정 공유 및 대리 플레이
- 의도적인 게임 방해 행위
- 광고성 채팅 및 스팸

위반 시 경고 → 정지 → 영구 정지 순서로 제재됩니다.

건전한 게임 문화 조성에 함께해 주세요!`,
      author: 'admin',
      isPinned: false,
      isPublished: true,
    },
  });

  console.log('✅ 샘플 공지사항 생성:', announcement1.title, announcement2.title);

  // 샘플 패치노트 생성
  const patchNote1 = await prisma.patchNote.upsert({
    where: { version: '1.0.0' },
    update: {},
    create: {
      version: '1.0.0',
      title: '블로쿠스 온라인 정식 출시',
      content: `# 블로쿠스 온라인 v1.0.0 정식 출시

## 🎉 새로운 기능

### 멀티플레이어 시스템
- **온라인 대전**: 전 세계 플레이어와 실시간 대전
- **친구 시스템**: 친구 추가 및 초대 기능
- **대기실**: 게임 시작 전 채팅 및 준비 시스템

### 게임 기능
- **클래식 블로쿠스**: 전통적인 4인용 블로쿠스 게임
- **랭킹 시스템**: 개인 실력에 따른 순위 시스템
- **통계**: 승률, 평균 점수 등 상세한 게임 통계

### UI/UX 개선
- **직관적인 인터페이스**: 쉽고 편리한 조작법
- **반응형 디자인**: 다양한 화면 크기 지원
- **테마 시스템**: 다크/라이트 테마 지원

## 🔧 시스템 요구사항

- **OS**: Windows 10 이상
- **메모리**: 4GB RAM 이상
- **저장공간**: 1GB 이상
- **네트워크**: 안정적인 인터넷 연결

## 📥 다운로드

공식 웹사이트에서 최신 클라이언트를 다운로드하세요!

즐거운 게임 되세요! 🎮`,
      releaseDate: new Date('2024-01-15'),
      downloadUrl: '/download',
    },
  });

  console.log('✅ 샘플 패치노트 생성:', patchNote1.title);

  // 샘플 고객지원 문의 생성 (테스트용)
  const supportTicket = await prisma.supportTicket.upsert({
    where: { id: 1 },
    update: {},
    create: {
      email: 'test@example.com',
      subject: '게임 실행 오류 문의',
      message: '게임을 실행하려고 하는데 "Missing DLL" 오류가 발생합니다. 어떻게 해결할 수 있나요?',
      status: 'PENDING',
    },
  });

  console.log('✅ 샘플 고객지원 문의 생성:', supportTicket.subject);

  console.log('🎉 데이터베이스 시딩 완료!');
}

main()
  .then(async () => {
    await prisma.$disconnect();
  })
  .catch(async (e) => {
    console.error('❌ 시딩 중 오류 발생:', e);
    await prisma.$disconnect();
    process.exit(1);
  });