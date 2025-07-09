#pragma once

#include "common/ServerTypes.h"
#include <string>
#include <chrono>
#include <vector>

namespace Blokus {
    namespace Server {
        namespace Utils {

            // ========================================
            // ���ڿ� ��ȯ �Լ���
            // ========================================

            // �޽��� Ÿ���� ���ڿ��� ��ȯ
            std::string messageTypeToString(MessageType type);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string connectionStateToString(ConnectionState state);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string sessionStateToString(SessionState state);

            // �� ���¸� ���ڿ��� ��ȯ
            std::string roomStateToString(RoomState state);

            // ���� �ڵ带 ���ڿ��� ��ȯ
            std::string errorCodeToString(ServerErrorCode code);

            // ========================================
            // �ð� ���� ��ƿ��Ƽ
            // ========================================

            // ���� �ð��� ISO 8601 ���� ���ڿ��� ��ȯ
            std::string currentTimeToString();

            // Ÿ������Ʈ�� ���ڿ��� ��ȯ
            std::string timePointToString(const std::chrono::steady_clock::time_point& timePoint);

            // ���� �ð��� ����� �б� ���� �������� ��ȯ
            std::string formatDuration(std::chrono::seconds duration);

            // ���� ���н� Ÿ�ӽ����� ��ȯ
            uint64_t getCurrentTimestamp();

            // Ÿ�Ӿƿ� Ȯ��
            bool isTimedOut(const std::chrono::steady_clock::time_point& lastActivity,
                std::chrono::seconds timeout);

            // ========================================
            // ��Ʈ��ũ ���� ��ƿ��Ƽ
            // ========================================

            // ����Ʈ ũ�⸦ ����� �б� ���� �������� ��ȯ
            std::string formatBytes(uint64_t bytes);

            // IP �ּ� ��ȿ�� ����
            bool isValidIPAddress(const std::string& ip);

            // ��Ʈ ��ȣ ��ȿ�� ����
            bool isValidPort(int port);

            // ��Ʈ��ũ �ּ� �Ľ�
            std::pair<std::string, int> parseAddress(const std::string& address);

            // ========================================
            // ���� ���� ��ƿ��Ƽ
            // ========================================

            // ��й�ȣ �ؽ� (bcrypt ��Ÿ��)
            std::string hashPassword(const std::string& password);

            // ��й�ȣ ����
            bool verifyPassword(const std::string& password, const std::string& hash);

            // ���� ���ڿ� ���� (���� ��ū��)
            std::string generateRandomString(size_t length);

            // UUID ����
            std::string generateUUID();

            // ��Ʈ ����
            std::string generateSalt(size_t length = 16);

            // ========================================
            // ������ ���� ��ƿ��Ƽ
            // ========================================

            // ����ڸ� ��ȿ�� ����
            bool isValidUsername(const std::string& username);

            // �̸��� ��ȿ�� ����
            bool isValidEmail(const std::string& email);

            // �� �̸� ��ȿ�� ����
            bool isValidRoomName(const std::string& roomName);

            // JSON ���ڿ� ��ȿ�� ����
            bool isValidJson(const std::string& jsonString);

            // �޽��� ũ�� ����
            bool isValidMessageSize(size_t messageSize, size_t maxSize = MAX_MESSAGE_SIZE);

            // ========================================
            // �α� ���� ��ƿ��Ƽ
            // ========================================

            // Ŭ���̾�Ʈ ������ �α�� ���ڿ��� ��ȯ
            std::string formatClientInfo(uint32_t sessionId, const std::string& remoteAddress);

            // ���� ��踦 �α�� ���ڿ��� ��ȯ
            std::string formatServerStats(const ServerStats& stats);

            // ���� ������ �α�� ���ڿ��� ��ȯ
            std::string formatErrorInfo(ServerErrorCode code, const std::string& context);

            // ========================================
            // ���� ���� ��ƿ��Ƽ
            // ========================================

            // ���ػ� Ÿ�̸� Ŭ����
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

            // �޸� ��뷮 ��ȸ (�÷�����)
            struct MemoryInfo {
                uint64_t totalMemory;      // �� �޸� (����Ʈ)
                uint64_t availableMemory;  // ��� ������ �޸�
                uint64_t usedMemory;       // ��� ���� �޸�
                double usagePercentage;    // ���� (%)
            };

            MemoryInfo getMemoryInfo();

            // CPU ���� ��ȸ (�÷�����)
            double getCPUUsage();

            // ========================================
            // ���ڿ� ó�� ��ƿ��Ƽ
            // ========================================

            // ���ڿ� Ʈ�� (�յ� ���� ����)
            std::string trim(const std::string& str);

            // ���ڿ� ����
            std::vector<std::string> split(const std::string& str, char delimiter);

            // ���ڿ� �ҹ��� ��ȯ
            std::string toLower(const std::string& str);

            // ���ڿ� �빮�� ��ȯ
            std::string toUpper(const std::string& str);

            // ���ڿ����� Ư�� ���� �̽�������
            std::string escapeString(const std::string& str);

            // URL ���ڵ�/���ڵ�
            std::string urlEncode(const std::string& str);
            std::string urlDecode(const std::string& str);

            // ========================================
            // ���� ���� ��ƿ��Ƽ
            // ========================================

            // �÷��̾� ������ ���ڿ��� ��ȯ (������)
            std::string playerColorToString(Common::PlayerColor color);

            // ��� Ÿ���� ���ڿ��� ��ȯ (������)
            std::string blockTypeToString(Common::BlockType type);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string gameStateToString(Common::GameState state);

            // �÷��̾� ���� ����
            std::vector<Common::PlayerColor> shufflePlayerOrder(const std::vector<Common::PlayerColor>& players);

            // ========================================
            // ���� ���� ���� ��ƿ��Ƽ
            // ========================================

            // ���� ���� �ε�
            bool loadConfigFromFile(const std::string& filename, ServerConfig& config);

            // ���� ���� ����
            bool saveConfigToFile(const std::string& filename, const ServerConfig& config);

            // ȯ�� �������� ���� �ε�
            void loadConfigFromEnvironment(ServerConfig& config);

        } // namespace Utils
    } // namespace Server
} // namespace Blokus