#pragma once

#include "server/ServerTypes.h"
#include <string>
#include <chrono>

namespace Blokus {
    namespace Server {
        namespace Utils {

            // ========================================
            // ���ڿ� ��ȯ �Լ���
            // ========================================

            // �޽��� Ÿ���� ���ڿ��� ��ȯ
            std::string messageTypeToString(MessageType type);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string serverStateToString(ServerState state);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string sessionStateToString(SessionState state);

            // �� ���¸� ���ڿ��� ��ȯ
            std::string roomStateToString(RoomState state);

            // ========================================
            // �ð� ���� ��ƿ��Ƽ
            // ========================================

            // ���� �ð��� ISO 8601 ���� ���ڿ��� ��ȯ
            std::string currentTimeToString();

            // Ÿ�ӽ������� ���ڿ��� ��ȯ
            std::string timePointToString(const std::chrono::steady_clock::time_point& timePoint);

            // ���� �ð��� ����� �б� ���� �������� ��ȯ
            std::string formatDuration(std::chrono::seconds duration);

            // ���� ���н� Ÿ�ӽ����� ��ȯ
            uint64_t getCurrentTimestamp();

            // ========================================
            // ��Ʈ��ũ ���� ��ƿ��Ƽ
            // ========================================

            // ����Ʈ ũ�⸦ ����� �б� ���� �������� ��ȯ
            std::string formatBytes(uint64_t bytes);

            // IP �ּ� ��ȿ�� ����
            bool isValidIPAddress(const std::string& ip);

            // ��Ʈ ��ȣ ��ȿ�� ����
            bool isValidPort(int port);

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

            // ========================================
            // �α� ���� ��ƿ��Ƽ
            // ========================================

            // Ŭ���̾�Ʈ ������ �α׿� ���ڿ��� ��ȯ
            std::string formatClientInfo(uint32_t sessionId, const std::string& remoteAddress);

            // ���� �̺�Ʈ�� �α׿� ���ڿ��� ��ȯ
            std::string formatGameEvent(const GameEvent& event);

            // ���� ��踦 �α׿� ���ڿ��� ��ȯ
            std::string formatServerStats(const ServerStatistics& stats);

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

            // ========================================
            // ���� ���� ��ƿ��Ƽ
            // ========================================

            // �÷��̾� ������ ���ڿ��� ��ȯ (������)
            std::string playerColorToString(Common::PlayerColor color);

            // ��� Ÿ���� ���ڿ��� ��ȯ (������)
            std::string blockTypeToString(Common::BlockType type);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string gameStateToString(Common::GameState state);

        } // namespace Utils
    } // namespace Server
} // namespace Blokus