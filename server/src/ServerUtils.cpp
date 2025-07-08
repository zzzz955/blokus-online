#include "server/ServerUtils.h"
#include "common/Utils.h"
#include <nlohmann/json.hpp>
#include <iomanip>
#include <sstream>
#include <algorithm>
#include <random>
#include <regex>
#include <ctime>

#ifdef _WIN32
#include <windows.h>
#include <psapi.h>
#else
#include <sys/resource.h>
#include <unistd.h>
#include <fstream>
#endif

namespace Blokus {
    namespace Server {
        namespace Utils {

            // ========================================
            // ���ڿ� ��ȯ �Լ���
            // ========================================

            std::string messageTypeToString(MessageType type) {
                switch (type) {
                case MessageType::LOGIN_REQUEST: return "LOGIN_REQUEST";
                case MessageType::LOGIN_RESPONSE: return "LOGIN_RESPONSE";
                case MessageType::REGISTER_REQUEST: return "REGISTER_REQUEST";
                case MessageType::REGISTER_RESPONSE: return "REGISTER_RESPONSE";
                case MessageType::LOGOUT_REQUEST: return "LOGOUT_REQUEST";
                case MessageType::CREATE_ROOM_REQUEST: return "CREATE_ROOM_REQUEST";
                case MessageType::CREATE_ROOM_RESPONSE: return "CREATE_ROOM_RESPONSE";
                case MessageType::JOIN_ROOM_REQUEST: return "JOIN_ROOM_REQUEST";
                case MessageType::JOIN_ROOM_RESPONSE: return "JOIN_ROOM_RESPONSE";
                case MessageType::LEAVE_ROOM_REQUEST: return "LEAVE_ROOM_REQUEST";
                case MessageType::LEAVE_ROOM_RESPONSE: return "LEAVE_ROOM_RESPONSE";
                case MessageType::ROOM_LIST_REQUEST: return "ROOM_LIST_REQUEST";
                case MessageType::ROOM_LIST_RESPONSE: return "ROOM_LIST_RESPONSE";
                case MessageType::GAME_START: return "GAME_START";
                case MessageType::GAME_END: return "GAME_END";
                case MessageType::BLOCK_PLACEMENT: return "BLOCK_PLACEMENT";
                case MessageType::TURN_CHANGE: return "TURN_CHANGE";
                case MessageType::GAME_STATE_UPDATE: return "GAME_STATE_UPDATE";
                case MessageType::CHAT_MESSAGE: return "CHAT_MESSAGE";
                case MessageType::CHAT_BROADCAST: return "CHAT_BROADCAST";
                case MessageType::HEARTBEAT: return "HEARTBEAT";
                case MessageType::SYSTEM_NOTIFICATION: return "SYSTEM_NOTIFICATION";
                case MessageType::ERROR_MESSAGE: return "ERROR_MESSAGE";
                default: return "UNKNOWN";
                }
            }

            std::string serverStateToString(ServerState state) {
                switch (state) {
                case ServerState::STOPPED: return "STOPPED";
                case ServerState::STARTING: return "STARTING";
                case ServerState::RUNNING: return "RUNNING";
                case ServerState::STOPPING: return "STOPPING";
                default: return "UNKNOWN";
                }
            }

            std::string sessionStateToString(SessionState state) {
                switch (state) {
                case SessionState::CONNECTING: return "CONNECTING";
                case SessionState::CONNECTED: return "CONNECTED";
                case SessionState::AUTHENTICATED: return "AUTHENTICATED";
                case SessionState::IN_LOBBY: return "IN_LOBBY";
                case SessionState::IN_ROOM: return "IN_ROOM";
                case SessionState::IN_GAME: return "IN_GAME";
                case SessionState::DISCONNECTING: return "DISCONNECTING";
                case SessionState::DISCONNECTED: return "DISCONNECTED";
                default: return "UNKNOWN";
                }
            }

            std::string roomStateToString(RoomState state) {
                switch (state) {
                case RoomState::WAITING: return "WAITING";
                case RoomState::STARTING: return "STARTING";
                case RoomState::PLAYING: return "PLAYING";
                case RoomState::FINISHED: return "FINISHED";
                case RoomState::CLOSED: return "CLOSED";
                default: return "UNKNOWN";
                }
            }

            // ========================================
            // �ð� ���� ��ƿ��Ƽ
            // ========================================

            std::string currentTimeToString() {
                auto now = std::chrono::system_clock::now();
                auto time_t = std::chrono::system_clock::to_time_t(now);
                auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                    now.time_since_epoch()) % 1000;

                std::stringstream ss;
                ss << std::put_time(std::localtime(&time_t), "%Y-%m-%d %H:%M:%S");
                ss << '.' << std::setfill('0') << std::setw(3) << ms.count();
                return ss.str();
            }

            std::string timePointToString(const std::chrono::steady_clock::time_point& timePoint) {
                auto duration = timePoint.time_since_epoch();
                auto seconds = std::chrono::duration_cast<std::chrono::seconds>(duration);
                auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(duration) % 1000;

                std::stringstream ss;
                ss << seconds.count() << "." << std::setfill('0') << std::setw(3) << ms.count();
                return ss.str();
            }

            std::string formatDuration(std::chrono::seconds duration) {
                auto hours = std::chrono::duration_cast<std::chrono::hours>(duration);
                auto minutes = std::chrono::duration_cast<std::chrono::minutes>(duration % std::chrono::hours(1));
                auto seconds = duration % std::chrono::minutes(1);

                std::stringstream ss;
                if (hours.count() > 0) {
                    ss << hours.count() << "�ð� ";
                }
                if (minutes.count() > 0) {
                    ss << minutes.count() << "�� ";
                }
                ss << seconds.count() << "��";

                return ss.str();
            }

            uint64_t getCurrentTimestamp() {
                return std::chrono::duration_cast<std::chrono::seconds>(
                    std::chrono::system_clock::now().time_since_epoch()).count();
            }

            // ========================================
            // ��Ʈ��ũ ���� ��ƿ��Ƽ
            // ========================================

            std::string formatBytes(uint64_t bytes) {
                const char* units[] = { "B", "KB", "MB", "GB", "TB" };
                int unitIndex = 0;
                double size = static_cast<double>(bytes);

                while (size >= 1024.0 && unitIndex < 4) {
                    size /= 1024.0;
                    unitIndex++;
                }

                std::stringstream ss;
                ss << std::fixed << std::setprecision(2) << size << " " << units[unitIndex];
                return ss.str();
            }

            bool isValidIPAddress(const std::string& ip) {
                std::regex ipRegex(
                    R"(^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$)"
                );
                return std::regex_match(ip, ipRegex);
            }

            bool isValidPort(int port) {
                return port > 0 && port <= 65535;
            }

            // ========================================
            // ���� ���� ��ƿ��Ƽ
            // ========================================

            std::string hashPassword(const std::string& password) {
                // TODO: ���� bcrypt ���� �Ǵ� ���̺귯�� ���
                // �ӽ÷� �ܼ��� �ؽ� ��� (���� ������� bcrypt ��� �ʿ�)
                std::hash<std::string> hasher;
                size_t hashValue = hasher(password + "blokus_salt");

                std::stringstream ss;
                ss << std::hex << hashValue;
                return ss.str();
            }

            bool verifyPassword(const std::string& password, const std::string& hash) {
                return hashPassword(password) == hash;
            }

            std::string generateRandomString(size_t length) {
                const std::string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
                std::random_device rd;
                std::mt19937 gen(rd());
                std::uniform_int_distribution<> dis(0, chars.size() - 1);

                std::string result;
                result.reserve(length);
                for (size_t i = 0; i < length; ++i) {
                    result += chars[dis(gen)];
                }
                return result;
            }

            std::string generateUUID() {
                // ������ UUID v4 ��Ÿ�� ����
                std::random_device rd;
                std::mt19937 gen(rd());
                std::uniform_int_distribution<> dis(0, 15);

                std::stringstream ss;
                ss << std::hex;
                for (int i = 0; i < 32; ++i) {
                    if (i == 8 || i == 12 || i == 16 || i == 20) {
                        ss << "-";
                    }
                    ss << dis(gen);
                }
                return ss.str();
            }

            // ========================================
            // ������ ���� ��ƿ��Ƽ
            // ========================================

            bool isValidUsername(const std::string& username) {
                return Common::Utils::isValidUsername(username);
            }

            bool isValidEmail(const std::string& email) {
                std::regex emailRegex(R"(^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$)");
                return std::regex_match(email, emailRegex);
            }

            bool isValidRoomName(const std::string& roomName) {
                return Common::Utils::isValidRoomName(roomName);
            }

            bool isValidJson(const std::string& jsonString) {
                try {
                    nlohmann::json::parse(jsonString);
                    return true;
                }
                catch (const nlohmann::json::exception&) {
                    return false;
                }
            }

            // ========================================
            // �α� ���� ��ƿ��Ƽ
            // ========================================

            std::string formatClientInfo(uint32_t sessionId, const std::string& remoteAddress) {
                std::stringstream ss;
                ss << "����[" << sessionId << "] " << remoteAddress;
                return ss.str();
            }

            std::string formatGameEvent(const GameEvent& event) {
                std::stringstream ss;
                ss << "��[" << event.roomId << "] "
                    << "�÷��̾�[" << event.playerId << "] "
                    << "�̺�Ʈ[" << event.eventType << "]";
                return ss.str();
            }

            std::string formatServerStats(const ServerStatistics& stats) {
                std::stringstream ss;
                ss << "����: " << stats.currentConnections << "/" << stats.totalConnections
                    << ", ��: " << stats.currentRooms << "/" << stats.totalRoomsCreated
                    << ", ����: " << stats.totalGamesPlayed
                    << ", �޽���: " << stats.messagesReceived << "/" << stats.messagesSent
                    << ", �����ð�: " << formatDuration(stats.getUptime());
                return ss.str();
            }

            // ========================================
            // ���� ���� ��ƿ��Ƽ
            // ========================================

            HighResolutionTimer::HighResolutionTimer() : m_isRunning(false) {}

            void HighResolutionTimer::start() {
                m_startTime = std::chrono::high_resolution_clock::now();
                m_isRunning = true;
            }

            void HighResolutionTimer::stop() {
                m_endTime = std::chrono::high_resolution_clock::now();
                m_isRunning = false;
            }

            double HighResolutionTimer::getElapsedMilliseconds() const {
                auto endTime = m_isRunning ? std::chrono::high_resolution_clock::now() : m_endTime;
                auto duration = std::chrono::duration_cast<std::chrono::microseconds>(endTime - m_startTime);
                return duration.count() / 1000.0;
            }

            double HighResolutionTimer::getElapsedMicroseconds() const {
                auto endTime = m_isRunning ? std::chrono::high_resolution_clock::now() : m_endTime;
                auto duration = std::chrono::duration_cast<std::chrono::microseconds>(endTime - m_startTime);
                return static_cast<double>(duration.count());
            }

            void HighResolutionTimer::reset() {
                m_startTime = std::chrono::high_resolution_clock::now();
                m_isRunning = false;
            }

            MemoryInfo getMemoryInfo() {
                MemoryInfo info = {};

#ifdef _WIN32
                MEMORYSTATUSEX memInfo;
                memInfo.dwLength = sizeof(MEMORYSTATUSEX);
                GlobalMemoryStatusEx(&memInfo);

                info.totalMemory = memInfo.ullTotalPhys;
                info.availableMemory = memInfo.ullAvailPhys;
                info.usedMemory = info.totalMemory - info.availableMemory;
                info.usagePercentage = (static_cast<double>(info.usedMemory) / info.totalMemory) * 100.0;
#else
                // Linux/Unix ����
                std::ifstream meminfo("/proc/meminfo");
                std::string line;

                while (std::getline(meminfo, line)) {
                    if (line.find("MemTotal:") == 0) {
                        std::istringstream iss(line);
                        std::string label, value, unit;
                        iss >> label >> value >> unit;
                        info.totalMemory = std::stoull(value) * 1024; // KB to bytes
                    }
                    else if (line.find("MemAvailable:") == 0) {
                        std::istringstream iss(line);
                        std::string label, value, unit;
                        iss >> label >> value >> unit;
                        info.availableMemory = std::stoull(value) * 1024; // KB to bytes
                    }
                }
                info.usedMemory = info.totalMemory - info.availableMemory;
                info.usagePercentage = (static_cast<double>(info.usedMemory) / info.totalMemory) * 100.0;
#endif

                return info;
            }

            double getCPUUsage() {
                // �÷����� CPU ���� ��ȸ
                // ������ ������ ���� 0.0 ��ȯ (�����δ� �÷����� API ���)
                return 0.0;
            }

            // ========================================
            // ���ڿ� ó�� ��ƿ��Ƽ
            // ========================================

            std::string trim(const std::string& str) {
                return Common::Utils::trim(str);
            }

            std::vector<std::string> split(const std::string& str, char delimiter) {
                std::vector<std::string> tokens;
                std::string token;
                std::istringstream tokenStream(str);

                while (std::getline(tokenStream, token, delimiter)) {
                    tokens.push_back(token);
                }

                return tokens;
            }

            std::string toLower(const std::string& str) {
                std::string result = str;
                std::transform(result.begin(), result.end(), result.begin(), ::tolower);
                return result;
            }

            std::string toUpper(const std::string& str) {
                std::string result = str;
                std::transform(result.begin(), result.end(), result.begin(), ::toupper);
                return result;
            }

            std::string escapeString(const std::string& str) {
                std::string result;
                result.reserve(str.length() * 2); // �־��� ��츦 ���

                for (char c : str) {
                    switch (c) {
                    case '"': result += "\\\""; break;
                    case '\\': result += "\\\\"; break;
                    case '\n': result += "\\n"; break;
                    case '\r': result += "\\r"; break;
                    case '\t': result += "\\t"; break;
                    default: result += c; break;
                    }
                }

                return result;
            }

            // ========================================
            // ���� ���� ��ƿ��Ƽ
            // ========================================

            std::string playerColorToString(Common::PlayerColor color) {
                return Common::Utils::playerColorToString(color);
            }

            std::string blockTypeToString(Common::BlockType type) {
                return Common::Utils::getBlockName(type);
            }

            std::string gameStateToString(Common::GameState state) {
                switch (state) {
                case Common::GameState::Waiting: return "WAITING";
                case Common::GameState::Playing: return "PLAYING";
                case Common::GameState::Finished: return "FINISHED";
                case Common::GameState::Paused: return "PAUSED";
                default: return "UNKNOWN";
                }
            }

        } // namespace Utils
    } // namespace Server
} // namespace Blokus