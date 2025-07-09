#pragma once

#include <memory>
#include <string>
#include <queue>
#include <mutex>
#include <atomic>
#include <chrono>

#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

#include "common/Types.h"
#include "common/ServerTypes.h"

namespace Blokus {
    namespace Server {

        // 전방 선언 (순환 참조 방지)
        class MessageHandler;

        // 클라이언트 연결을 나타내는 세션 클래스
        class Session : public std::enable_shared_from_this<Session> {
        public:
            Session(boost::asio::io_context& ioContext, uint32_t sessionId);
            ~Session();

            // 세션 생명주기 관리
            void start();                                          // 세션 시작
            void close();                                          // 세션 종료

            // 메시지 송수신 (Protobuf 기반)
            void sendMessage(const std::string& serializedMessage); // 직렬화된 메시지 전송
            void sendMessage(const std::vector<uint8_t>& data);   // 바이너리 데이터 전송

            // 세션 정보 조회
            uint32_t getId() const { return m_sessionId; }
            std::string getRemoteAddress() const;
            bool isConnected() const { return m_isConnected; }
            std::chrono::steady_clock::time_point getLastActivity() const { return m_lastActivity; }

            // 사용자 정보 (로그인 후 설정)
            void setUserId(uint32_t userId) { m_userId = userId; }
            uint32_t getUserId() const { return m_userId; }
            void setUsername(const std::string& username) { m_username = username; }
            const std::string& getUsername() const { return m_username; }

            // 방 정보
            void setRoomId(uint32_t roomId) { m_currentRoomId = roomId; }
            uint32_t getRoomId() const { return m_currentRoomId; }
            bool isInRoom() const { return m_currentRoomId != 0; }

            // 소켓 접근 (서버에서 사용)
            boost::asio::ip::tcp::socket& getSocket() { return m_socket; }

            // 메시지 핸들러 설정 (의존성 주입)
            void setMessageHandler(std::shared_ptr<MessageHandler> handler) { m_messageHandler = handler; }

        private:
            // 네트워크 I/O 처리
            void startRead();                                      // 읽기 시작
            void handleRead(const boost::system::error_code& error, // 읽기 완료 처리
                size_t bytesTransferred);
            void handleWrite(const boost::system::error_code& error, // 쓰기 완료 처리
                size_t bytesTransferred);

            // 메시지 처리 (Protobuf)
            void processMessage(const std::vector<uint8_t>& messageData); // 수신 메시지 처리
            void processBuffer();                                  // 수신 버퍼 처리

            // 큐 관리
            void doWrite();                                        // 송신 큐 처리
            void updateActivity();                                 // 마지막 활동 시간 갱신

        private:
            // 기본 정보
            uint32_t m_sessionId;                                  // 세션 고유 ID
            boost::asio::ip::tcp::socket m_socket;                // TCP 소켓
            std::atomic<bool> m_isConnected{ false };             // 연결 상태

            // 사용자 정보
            uint32_t m_userId{ 0 };                               // 사용자 ID (로그인 후)
            std::string m_username;                               // 사용자명
            uint32_t m_currentRoomId{ 0 };                        // 현재 방 ID

            // 네트워크 버퍼
            static constexpr size_t MAX_MESSAGE_SIZE = Server::MAX_MESSAGE_SIZE; // 최대 메시지 크기
            std::array<char, MAX_MESSAGE_SIZE> m_readBuffer;      // 읽기 버퍼
            std::vector<uint8_t> m_messageBuffer;                 // 메시지 조합 버퍼

            // 송신 큐
            std::queue<std::vector<uint8_t>> m_writeQueue;        // 송신 메시지 큐 (바이너리)
            std::mutex m_writeQueueMutex;                         // 송신 큐 보호 뮤텍스
            std::atomic<bool> m_isWriting{ false };               // 송신 중 여부

            // 활동 추적
            std::chrono::steady_clock::time_point m_lastActivity; // 마지막 활동 시간

            // 메시지 핸들러 (의존성 주입)
            std::shared_ptr<MessageHandler> m_messageHandler;     // 메시지 처리기
        };

    } // namespace Server
} // namespace Blokus