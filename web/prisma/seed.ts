import { PrismaClient } from '@prisma/client';
import * as argon2 from 'argon2';

const prisma = new PrismaClient();

async function main() {
  console.log('🌱 웹사이트 데이터베이스 시딩 시작...');

  // 관리자 계정 생성
  const adminUsername = process.env.ADMIN_USERNAME || 'admin';
  const adminPassword = process.env.ADMIN_PASSWORD || 'admin123';
  const hashedPassword = await argon2.hash(adminPassword, {
    type: argon2.argon2id,
    memoryCost: 65536, // 64MB
    timeCost: 2,
    parallelism: 1,
  });

  const admin = await prisma.adminUser.upsert({
    where: { username: adminUsername },
    update: {},
    create: {
      username: adminUsername,
      password_hash: hashedPassword,
      role: 'SUPER_ADMIN',
    },
  });

  console.log('✅ 관리자 계정 생성:', admin.username);
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