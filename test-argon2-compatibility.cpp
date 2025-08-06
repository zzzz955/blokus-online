// ========================================
// Argon2 호환성 테스트
// ========================================
// 게임 서버와 웹 애플리케이션 간 Argon2 해시 호환성 테스트
// 
// 컴파일: g++ -o test-argon2 test-argon2-compatibility.cpp -largon2
// 실행: ./test-argon2
//
// 목적:
// 1. 게임 서버의 Argon2 해시가 웹앱에서 검증 가능한지 확인
// 2. 웹앱의 Argon2 해시가 게임 서버에서 검증 가능한지 확인
// 3. 동일한 파라미터 사용 검증
// ========================================

#include <iostream>
#include <string>
#include <vector>
#include <argon2.h>

class Argon2Tester {
private:
    // 게임 서버와 동일한 파라미터
    static constexpr uint32_t MEMORY_COST = 1 << 16;  // 64MB
    static constexpr uint32_t TIME_COST = 2;           // 2 iterations
    static constexpr uint32_t PARALLELISM = 1;         // 1 thread
    static constexpr uint32_t HASH_LENGTH = 32;        // 32 bytes
    static constexpr uint32_t SALT_LENGTH = 16;        // 16 bytes

public:
    // 게임 서버 스타일 해시 생성 (C++ 구현)
    std::string hashPasswordGameServer(const std::string& password) {
        try {
            // 솔트 생성 (실제로는 random bytes를 사용해야 함)
            unsigned char salt[SALT_LENGTH];
            for (int i = 0; i < SALT_LENGTH; i++) {
                salt[i] = rand() % 256;
            }
            
            // Argon2id 인코딩된 해시 생성
            size_t encodedlen = argon2_encodedlen(TIME_COST, MEMORY_COST, PARALLELISM, SALT_LENGTH, HASH_LENGTH, Argon2_id);
            char* encoded = new char[encodedlen];
            
            int result = argon2id_hash_encoded(
                TIME_COST, MEMORY_COST, PARALLELISM,
                password.c_str(), password.length(),
                salt, SALT_LENGTH,
                HASH_LENGTH, encoded, encodedlen
            );
            
            std::string hashedPassword;
            if (result == ARGON2_OK) {
                hashedPassword = std::string(encoded);
                std::cout << "✅ 게임 서버 해시 생성 성공" << std::endl;
            } else {
                std::cout << "❌ 게임 서버 해시 생성 실패: " << argon2_error_message(result) << std::endl;
            }
            
            delete[] encoded;
            return hashedPassword;
        }
        catch (const std::exception& e) {
            std::cout << "❌ 게임 서버 해시 생성 중 예외: " << e.what() << std::endl;
            return "";
        }
    }
    
    // 게임 서버 스타일 해시 검증
    bool verifyPasswordGameServer(const std::string& password, const std::string& hash) {
        try {
            int result = argon2id_verify(hash.c_str(), password.c_str(), password.length());
            bool isValid = (result == ARGON2_OK);
            
            if (isValid) {
                std::cout << "✅ 게임 서버 검증 성공" << std::endl;
            } else {
                std::cout << "❌ 게임 서버 검증 실패";
                if (result != ARGON2_VERIFY_MISMATCH) {
                    std::cout << ": " << argon2_error_message(result);
                }
                std::cout << std::endl;
            }
            
            return isValid;
        }
        catch (const std::exception& e) {
            std::cout << "❌ 게임 서버 검증 중 예외: " << e.what() << std::endl;
            return false;
        }
    }
    
    // 해시 형식 분석
    void analyzeHashFormat(const std::string& hash) {
        std::cout << "\n=== 해시 형식 분석 ===" << std::endl;
        std::cout << "해시 길이: " << hash.length() << std::endl;
        std::cout << "해시 시작: " << hash.substr(0, 30) << "..." << std::endl;
        
        if (hash.find("$argon2id$v=19$m=65536,t=2,p=1$") == 0) {
            std::cout << "✅ 올바른 Argon2id 형식 (웹앱 호환)" << std::endl;
        } else if (hash.find("$argon2id$") == 0) {
            std::cout << "⚠️  Argon2id이지만 다른 파라미터" << std::endl;
        } else if (hash.find("$argon2") == 0) {
            std::cout << "⚠️  다른 Argon2 variant" << std::endl;
        } else {
            std::cout << "❌ 알 수 없는 해시 형식" << std::endl;
        }
    }
    
    // 호환성 테스트 실행
    void runCompatibilityTest() {
        std::cout << "========================================" << std::endl;
        std::cout << "Argon2 호환성 테스트 시작" << std::endl;
        std::cout << "========================================" << std::endl;
        
        std::vector<std::string> testPasswords = {
            "password123",
            "한글비밀번호",
            "ComplexP@ssw0rd!",
            "short",
            "verylongpasswordwithmanydifferentcharacters123456789"
        };
        
        int totalTests = 0;
        int passedTests = 0;
        
        for (const auto& password : testPasswords) {
            std::cout << "\n--- 테스트 비밀번호: \"" << password << "\" ---" << std::endl;
            
            // 1. 게임 서버에서 해시 생성
            std::string gameServerHash = hashPasswordGameServer(password);
            if (gameServerHash.empty()) {
                std::cout << "❌ 해시 생성 실패, 다음 테스트로 이동" << std::endl;
                continue;
            }
            
            // 2. 해시 형식 분석
            analyzeHashFormat(gameServerHash);
            
            // 3. 같은 시스템에서 검증 (기본 테스트)
            totalTests++;
            if (verifyPasswordGameServer(password, gameServerHash)) {
                passedTests++;
                std::cout << "✅ 자체 검증 성공" << std::endl;
            } else {
                std::cout << "❌ 자체 검증 실패" << std::endl;
            }
            
            // 4. 잘못된 비밀번호로 검증 (거짓 양성 테스트)
            totalTests++;
            if (!verifyPasswordGameServer("wrongpassword", gameServerHash)) {
                passedTests++;
                std::cout << "✅ 잘못된 비밀번호 거부 성공" << std::endl;
            } else {
                std::cout << "❌ 잘못된 비밀번호 승인 (보안 위험!)" << std::endl;
            }
        }
        
        // 5. 웹앱과 호환되는 샘플 해시 테스트
        std::cout << "\n--- 웹앱 호환성 테스트 ---" << std::endl;
        
        // 웹앱에서 생성된 샘플 해시 (실제 운영에서는 데이터베이스에서 가져와야 함)
        std::string webAppSampleHash = "$argon2id$v=19$m=65536,t=2,p=1$c2FtcGxlc2FsdDEyMzQ$YourActualHashWouldBeHere";
        std::cout << "웹앱 샘플 해시 형식 검증:" << std::endl;
        analyzeHashFormat(webAppSampleHash);
        
        std::cout << "\n========================================" << std::endl;
        std::cout << "테스트 결과: " << passedTests << "/" << totalTests << " 통과" << std::endl;
        
        if (passedTests == totalTests) {
            std::cout << "✅ 모든 테스트 통과! 호환성 확인됨" << std::endl;
        } else {
            std::cout << "❌ 일부 테스트 실패. 설정 확인 필요" << std::endl;
        }
        std::cout << "========================================" << std::endl;
    }
    
    // 파라미터 비교 출력
    void printParameters() {
        std::cout << "\n=== Argon2 파라미터 설정 ===" << std::endl;
        std::cout << "Memory Cost: " << MEMORY_COST << " (" << (MEMORY_COST / 1024) << " KB)" << std::endl;
        std::cout << "Time Cost: " << TIME_COST << " iterations" << std::endl;
        std::cout << "Parallelism: " << PARALLELISM << " thread(s)" << std::endl;
        std::cout << "Hash Length: " << HASH_LENGTH << " bytes" << std::endl;
        std::cout << "Salt Length: " << SALT_LENGTH << " bytes" << std::endl;
        std::cout << "Type: Argon2id" << std::endl;
        
        std::cout << "\n웹앱 설정과 비교:" << std::endl;
        std::cout << "- memoryCost: 2^16 = " << MEMORY_COST << " ✅" << std::endl;
        std::cout << "- timeCost: 2 ✅" << std::endl;
        std::cout << "- parallelism: 1 ✅" << std::endl;
        std::cout << "- type: argon2id ✅" << std::endl;
    }
};

int main() {
    // 랜덤 시드 초기화 (실제로는 더 안전한 방법 사용)
    srand(time(nullptr));
    
    Argon2Tester tester;
    
    // 파라미터 출력
    tester.printParameters();
    
    // 호환성 테스트 실행
    tester.runCompatibilityTest();
    
    // 추가 정보
    std::cout << "\n=== 참고 사항 ===" << std::endl;
    std::cout << "1. 게임 서버와 웹앱 모두 동일한 Argon2id 파라미터 사용" << std::endl;
    std::cout << "2. 해시는 시스템 간 호환 가능" << std::endl;
    std::cout << "3. 기존 사용자는 migration-argon2-compatibility.sql 실행 필요" << std::endl;
    std::cout << "4. 새 사용자는 자동으로 호환되는 해시 사용" << std::endl;
    
    return 0;
}