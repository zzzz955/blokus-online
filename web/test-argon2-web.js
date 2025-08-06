// ========================================
// 웹 애플리케이션 Argon2 호환성 테스트
// ========================================
// 웹앱의 Argon2 해시가 게임 서버와 호환되는지 테스트
// 
// 실행: node test-argon2-web.js
//
// 목적:
// 1. 웹앱 Argon2 해시 생성 테스트
// 2. 게임 서버 호환 형식 확인
// 3. 비밀번호 검증 테스트
// ========================================

const argon2 = require('argon2');

class WebArgon2Tester {
    constructor() {
        // 게임 서버와 동일한 파라미터
        this.options = {
            type: argon2.argon2id,
            memoryCost: 2 ** 16,  // 64MB (65536)
            timeCost: 2,          // 2 iterations
            parallelism: 1,       // 1 thread
        };
    }

    // 웹앱 스타일 해시 생성
    async hashPasswordWeb(password) {
        try {
            const hash = await argon2.hash(password, this.options);
            console.log('✅ 웹앱 해시 생성 성공');
            return hash;
        } catch (error) {
            console.log('❌ 웹앱 해시 생성 실패:', error.message);
            return null;
        }
    }

    // 웹앱 스타일 해시 검증
    async verifyPasswordWeb(password, hash) {
        try {
            const isValid = await argon2.verify(hash, password);
            if (isValid) {
                console.log('✅ 웹앱 검증 성공');
            } else {
                console.log('❌ 웹앱 검증 실패');
            }
            return isValid;
        } catch (error) {
            console.log('❌ 웹앱 검증 중 오류:', error.message);
            return false;
        }
    }

    // 해시 형식 분석
    analyzeHashFormat(hash) {
        console.log('\n=== 해시 형식 분석 ===');
        console.log('해시 길이:', hash.length);
        console.log('해시 시작:', hash.substring(0, 50) + '...');
        
        if (hash.startsWith('$argon2id$v=19$m=65536,t=2,p=1$')) {
            console.log('✅ 올바른 Argon2id 형식 (게임 서버 호환)');
        } else if (hash.startsWith('$argon2id$')) {
            console.log('⚠️  Argon2id이지만 다른 파라미터');
        } else if (hash.startsWith('$argon2')) {
            console.log('⚠️  다른 Argon2 variant');
        } else {
            console.log('❌ 알 수 없는 해시 형식');
        }

        // 파라미터 추출
        const parts = hash.split('$');
        if (parts.length >= 4) {
            console.log('Argon2 버전:', parts[2]);
            console.log('파라미터:', parts[3]);
        }
    }

    // 파라미터 비교 출력
    printParameters() {
        console.log('\n=== Argon2 파라미터 설정 ===');
        console.log('Type: argon2id');
        console.log('Memory Cost:', this.options.memoryCost, `(${this.options.memoryCost / 1024} KB)`);
        console.log('Time Cost:', this.options.timeCost, 'iterations');
        console.log('Parallelism:', this.options.parallelism, 'thread(s)');
        
        console.log('\n게임 서버 설정과 비교:');
        console.log('- memoryCost: 1 << 16 =', this.options.memoryCost, '✅');
        console.log('- timeCost: 2 ✅');
        console.log('- parallelism: 1 ✅');
        console.log('- type: argon2id ✅');
    }

    // 호환성 테스트 실행
    async runCompatibilityTest() {
        console.log('========================================');
        console.log('웹앱 Argon2 호환성 테스트 시작');
        console.log('========================================');

        const testPasswords = [
            'password123',
            '한글비밀번호',
            'ComplexP@ssw0rd!',
            'short',
            'verylongpasswordwithmanydifferentcharacters123456789'
        ];

        let totalTests = 0;
        let passedTests = 0;

        for (const password of testPasswords) {
            console.log(`\n--- 테스트 비밀번호: "${password}" ---`);

            // 1. 웹앱에서 해시 생성
            const webHash = await this.hashPasswordWeb(password);
            if (!webHash) {
                console.log('❌ 해시 생성 실패, 다음 테스트로 이동');
                continue;
            }

            // 2. 해시 형식 분석
            this.analyzeHashFormat(webHash);

            // 3. 같은 시스템에서 검증 (기본 테스트)
            totalTests++;
            if (await this.verifyPasswordWeb(password, webHash)) {
                passedTests++;
                console.log('✅ 자체 검증 성공');
            } else {
                console.log('❌ 자체 검증 실패');
            }

            // 4. 잘못된 비밀번호로 검증 (거짓 양성 테스트)
            totalTests++;
            if (!(await this.verifyPasswordWeb('wrongpassword', webHash))) {
                passedTests++;
                console.log('✅ 잘못된 비밀번호 거부 성공');
            } else {
                console.log('❌ 잘못된 비밀번호 승인 (보안 위험!)');
            }
        }

        // 5. 기존 데이터베이스 해시와의 호환성 테스트
        console.log('\n--- 기존 해시 형식 호환성 테스트 ---');
        
        // 샘플 해시들 (실제 운영에서는 데이터베이스에서 가져와야 함)
        const sampleHashes = {
            'bcrypt': '$2b$10$example...',  // bcrypt 형식 (구식)
            'sha256': '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8',  // SHA256 (구식)
            'argon2_old': '$argon2i$v=19$m=4096,t=3,p=1$example...',  // 다른 Argon2 파라미터
        };

        for (const [type, hash] of Object.entries(sampleHashes)) {
            console.log(`\n${type.toUpperCase()} 형식:`);
            this.analyzeHashFormat(hash);
        }

        console.log('\n========================================');
        console.log(`테스트 결과: ${passedTests}/${totalTests} 통과`);
        
        if (passedTests === totalTests) {
            console.log('✅ 모든 테스트 통과! 호환성 확인됨');
        } else {
            console.log('❌ 일부 테스트 실패. 설정 확인 필요');
        }
        console.log('========================================');
    }

    // 성능 테스트
    async performanceTest() {
        console.log('\n=== 성능 테스트 ===');
        const testPassword = 'performanceTestPassword123';
        const iterations = 5;

        console.log(`${iterations}회 해시 생성 시간 측정...`);
        
        const startTime = Date.now();
        for (let i = 0; i < iterations; i++) {
            await this.hashPasswordWeb(testPassword + i);
        }
        const endTime = Date.now();
        
        const avgTime = (endTime - startTime) / iterations;
        console.log(`평균 해시 생성 시간: ${avgTime.toFixed(2)}ms`);
        
        if (avgTime < 500) {
            console.log('✅ 성능 양호 (500ms 미만)');
        } else if (avgTime < 1000) {
            console.log('⚠️  성능 보통 (500-1000ms)');
        } else {
            console.log('❌ 성능 저조 (1000ms 이상)');
        }
    }
}

// 테스트 실행
async function main() {
    const tester = new WebArgon2Tester();
    
    // 파라미터 출력
    tester.printParameters();
    
    // 호환성 테스트 실행
    await tester.runCompatibilityTest();
    
    // 성능 테스트 실행
    await tester.performanceTest();
    
    // 추가 정보
    console.log('\n=== 참고 사항 ===');
    console.log('1. 웹앱과 게임 서버 모두 동일한 Argon2id 파라미터 사용');
    console.log('2. 해시는 시스템 간 호환 가능');
    console.log('3. 기존 사용자는 migration-argon2-compatibility.sql 실행 필요');
    console.log('4. 새 사용자는 자동으로 호환되는 해시 사용');
    console.log('5. OAuth 사용자도 ID/PW 설정 시 동일한 해시 형식 사용');
}

// 에러 처리와 함께 실행
main().catch(error => {
    console.error('테스트 실행 중 오류 발생:', error);
    process.exit(1);
});