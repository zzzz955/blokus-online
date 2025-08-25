#!/usr/bin/env node

/**
 * 관리자 계정 초기화 스크립트
 * 환경변수로 설정된 관리자 계정을 데이터베이스에 생성
 */

const { PrismaClient } = require('@prisma/client');
const argon2 = require('argon2');

const prisma = new PrismaClient();

async function createAdminUser() {
  try {
    const adminUsername = process.env.ADMIN_USERNAME;
    const adminPassword = process.env.ADMIN_PASSWORD;
    
    if (!adminUsername || !adminPassword) {
      console.error('❌ ADMIN_USERNAME 또는 ADMIN_PASSWORD 환경변수가 설정되지 않았습니다.');
      process.exit(1);
    }

    // 기존 관리자 계정 확인
    const existingAdmin = await prisma.adminUser.findUnique({
      where: { username: adminUsername }
    });

    if (existingAdmin) {
      console.log(`✅ 관리자 계정 '${adminUsername}'이 이미 존재합니다.`);
      return;
    }

    // 비밀번호 해싱
    const hashedPassword = await argon2.hash(adminPassword, {
      type: argon2.argon2id,
      memoryCost: 65536, // 64MB
      timeCost: 2,
      parallelism: 1,
    });

    // 관리자 계정 생성
    const admin = await prisma.adminUser.create({
      data: {
        username: adminUsername,
        password_hash: hashedPassword,
        role: 'SUPER_ADMIN',
      }
    });

    console.log(`✅ 관리자 계정이 성공적으로 생성되었습니다:`);
    console.log(`   사용자명: ${admin.username}`);
    console.log(`   역할: ${admin.role}`);
    console.log(`   생성일시: ${admin.created_at}`);

  } catch (error) {
    console.error('❌ 관리자 계정 생성 중 오류 발생:', error.message);
    process.exit(1);
  } finally {
    await prisma.$disconnect();
  }
}

// 스크립트 실행
if (require.main === module) {
  createAdminUser();
}

module.exports = { createAdminUser };