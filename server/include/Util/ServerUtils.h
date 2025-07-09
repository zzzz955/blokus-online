#pragma once

#include "common/ServerTypes.h"
#include <string>
#include <chrono>
#include <vector>

namespace Blokus {
    namespace Server {
        namespace Utils {

            // ========================================
            // 문자열 변환 함수들
            // ========================================

            // 메시지 타입을 문자열로 변환
            std::string messageTypeToString(MessageType type);

            // 서버 상태를 문자열로 변환
            std::string connectionStateToString(ConnectionState state);

            // 세션 상태를 문자열로 변환
            std::string sessionStateToString(SessionState state);

            // 방 상태를 문자열로 변환
            std::string roomStateToString(RoomState state);

            // 에러 코드를 문자열로 변환
            std::string errorCodeToString(ServerErrorCode code);

            // ========================================
            // 시간 관련 유틸리티
            // ========================================

            // 현재 시간을 ISO 8601 형식 문자열로 변환
            std::string currentTimeToString();

            // 타임포인트를 문자열로 변환
            std::string timePointToString(const std::chrono::steady_clock::time_point& timePoint);

            // 지속 시간을 사람이 읽기 쉬운 형식으로 변환
            std::string formatDuration(std::chrono::seconds duration);

            // 현재 유닉스 타임스탬프 반환
            uint64_t getCurrentTimestamp();

            // 타임아웃 확인
            bool isTimedOut(const std::chrono::steady_clock::time_point& lastActivity,
                std::chrono::seconds timeout);

            // ========================================
            // 네트워크 관련 유틸리티
            // ========================================

            // 바이트 크기를 사람이 읽기 쉬운 형식으로 변환
            std::string formatBytes(uint64_t bytes);

            // IP 주소 유효성 검증
            bool isValidIPAddress(const std::string& ip);

            // 포트 번호 유효성 검증
            bool isValidPort(int port);

            // 네트워크 주소 파싱
            std::pair<std::string, int> parseAddress(const std::string& address);

            // ========================================
            // 보안 관련 유틸리티
            // ========================================

            // 비밀번호 해싱 (bcrypt 스타일)
            std::string hashPassword(const std::string& password);

            // 비밀번호 검증
            bool verifyPassword(const std::string& password, const std::string& hash);

            // 랜덤 문자열 생성 (세션 토큰용)
            std::string generateRandomString(size_t length);

            // UUID 생성
            std::string generateUUID();

            // 솔트 생성
            std::string generateSalt(size_t length = 16);

            // ========================================
            // 데이터 검증 유틸리티
            // ========================================

            // 사용자명 유효성 검증
            bool isValidUsername(const std::string& username);

            // 이메일 유효성 검증
            bool isValidEmail(const std::string& email);

            // 방 이름 유효성 검증
            bool isValidRoomName(const std::string& roomName);

            // JSON 문자열 유효성 검증
            bool isValidJson(const std::string& jsonString);

            // 메시지 크기 검증
            bool isValidMessageSize(size_t messageSize, size_t maxSize = MAX_MESSAGE_SIZE);

            // ========================================
            // 로깅 관련 유틸리티
            // ========================================

            // 클라이언트 정보를 로깅용 문자열로 변환
            std::string formatClientInfo(uint32_t sessionId, const std::string& remoteAddress);

            // 서버 통계를 로깅용 문자열로 변환
            std::string formatServerStats(const ServerStats& stats);

            // 에러 정보를 로깅용 문자열로 변환
            std::string formatErrorInfo(ServerErrorCode code, const std::string& context);

            // ========================================
            // 성능 측정 유틸리티
            // ========================================

            // 고해상도 타이머 클래스
            class HighResolutionTimer {
            public:
                HighResolutionTimer();
                void start();
                void stop();
                double getElapsedMilliseconds() const;
                double getElapsedMicroseconds() const;
                void reset();

            private:
                std::chrono::high_resolution_clock::time_point m_startTime;
                std::chrono::high_resolution_clock::time_point m_endTime;
                bool m_isRunning;
            };

            // 메모리 사용량 조회 (플랫폼별)
            struct MemoryInfo {
                uint64_t totalMemory;      // 총 메모리 (바이트)
                uint64_t availableMemory;  // 사용 가능한 메모리
                uint64_t usedMemory;       // 사용 중인 메모리
                double usagePercentage;    // 사용률 (%)
            };

            MemoryInfo getMemoryInfo();

            // CPU 사용률 조회 (플랫폼별)
            double getCPUUsage();

            // ========================================
            // 문자열 처리 유틸리티
            // ========================================

            // 문자열 트림 (앞뒤 공백 제거)
            std::string trim(const std::string& str);

            // 문자열 분할
            std::vector<std::string> split(const std::string& str, char delimiter);

            // 문자열 소문자 변환
            std::string toLower(const std::string& str);

            // 문자열 대문자 변환
            std::string toUpper(const std::string& str);

            // 문자열에서 특수 문자 이스케이핑
            std::string escapeString(const std::string& str);

            // URL 인코딩/디코딩
            std::string urlEncode(const std::string& str);
            std::string urlDecode(const std::string& str);

            // ========================================
            // 게임 관련 유틸리티
            // ========================================

            // 플레이어 색상을 문자열로 변환 (서버용)
            std::string playerColorToString(Common::PlayerColor color);

            // 블록 타입을 문자열로 변환 (서버용)
            std::string blockTypeToString(Common::BlockType type);

            // 게임 상태를 문자열로 변환
            std::string gameStateToString(Common::GameState state);

            // 플레이어 순서 섞기
            std::vector<Common::PlayerColor> shufflePlayerOrder(const std::vector<Common::PlayerColor>& players);

            // ========================================
            // 설정 파일 관련 유틸리티
            // ========================================

            // 설정 파일 로드
            bool loadConfigFromFile(const std::string& filename, ServerConfig& config);

            // 설정 파일 저장
            bool saveConfigToFile(const std::string& filename, const ServerConfig& config);

            // 환경 변수에서 설정 로드
            void loadConfigFromEnvironment(ServerConfig& config);

        } // namespace Utils
    } // namespace Server
} // namespace Blokus