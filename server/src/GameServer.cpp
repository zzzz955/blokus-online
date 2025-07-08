#include "server/GameServer.h"
#include <chrono>

namespace Blokus {
    namespace Server {

        GameServer::GameServer(const ServerConfig& config)
            : m_config(config)
            , m_ioContext()
            , m_acceptor(m_ioContext, boost::asio::ip::tcp::endpoint(
                boost::asio::ip::tcp::v4(), config.port))
            , m_cleanupTimer(m_ioContext)
            , m_statisticsTimer(m_ioContext)
        {
            spdlog::debug("GameServer ������ ȣ�� - ��Ʈ: {}", config.port);
            initializeServer();
        }

        GameServer::~GameServer() {
            spdlog::debug("GameServer �Ҹ��� ȣ��");
            if (m_isRunning) {
                stop();
            }
        }

        void GameServer::initializeServer() {
            spdlog::info("���� ������Ʈ �ʱ�ȭ ��...");

            try {
                // ���� ���� ������Ʈ �ʱ�ȭ
                m_roomManager = std::make_unique<RoomManager>();
                m_userManager = std::make_unique<UserManager>();

                // �����ͺ��̽� �ʱ�ȭ (���� ����)
                // initializeDatabase();

                // Redis �ʱ�ȭ (���� ����)
                // initializeRedis();

                // ������ Ǯ ����
                setupThreadPool();

                spdlog::info("���� ������Ʈ �ʱ�ȭ �Ϸ�");
            }
            catch (const std::exception& e) {
                spdlog::error("���� ������Ʈ �ʱ�ȭ ����: {}", e.what());
                throw;
            }
        }

        void GameServer::setupThreadPool() {
            spdlog::info("������ Ǯ ���� ��... (ũ��: {})", m_config.threadPoolSize);

            // ������ Ǯ ũ�� ����
            if (m_config.threadPoolSize < 1) {
                m_config.threadPoolSize = std::thread::hardware_concurrency();
                spdlog::warn("�߸��� ������ Ǯ ũ��, CPU �ھ� ���� ����: {}", m_config.threadPoolSize);
            }

            m_threadPool.reserve(m_config.threadPoolSize);
        }

        void GameServer::start() {
            if (m_isRunning.exchange(true)) {
                spdlog::warn("������ �̹� ���� ���Դϴ�.");
                return;
            }

            spdlog::info("���� ���� ��... (��Ʈ: {})", m_config.port);

            try {
                // ������ ����
                m_acceptor.set_option(boost::asio::ip::tcp::acceptor::reuse_address(true));

                // �� ���� ���� ����
                startAccept();

                // ���� Ÿ�̸� ���� (30�ʸ���)
                m_cleanupTimer.expires_after(std::chrono::seconds(30));
                m_cleanupTimer.async_wait([this](const boost::system::error_code& error) {
                    if (!error && m_isRunning) {
                        cleanupInactiveSessions();
                        m_cleanupTimer.expires_after(std::chrono::seconds(30));
                        m_cleanupTimer.async_wait([this](const boost::system::error_code& error) {
                            // ��������� Ÿ�̸� �缳��
                            });
                    }
                    });

                // ��� Ÿ�̸� ����
                startStatisticsTimer();

                spdlog::info("������ ���������� ���۵Ǿ����ϴ�.");
            }
            catch (const std::exception& e) {
                m_isRunning = false;
                spdlog::error("���� ���� ����: {}", e.what());
                throw;
            }
        }

        void GameServer::stop() {
            if (!m_isRunning.exchange(false)) {
                spdlog::warn("������ �̹� �����Ǿ����ϴ�.");
                return;
            }

            spdlog::info("���� ���� ��...");

            try {
                // ������ �ݱ�
                if (m_acceptor.is_open()) {
                    m_acceptor.close();
                }

                // Ÿ�̸ӵ� ���
                m_cleanupTimer.cancel();
                m_statisticsTimer.cancel();

                // I/O ���ؽ�Ʈ ����
                m_ioContext.stop();

                // ������ Ǯ ���� ���
                for (auto& thread : m_threadPool) {
                    if (thread.joinable()) {
                        thread.join();
                    }
                }
                m_threadPool.clear();

                // ���� �۾�
                cleanup();

                spdlog::info("������ ���������� �����Ǿ����ϴ�.");
            }
            catch (const std::exception& e) {
                spdlog::error("���� ���� �� ����: {}", e.what());
            }
        }

        void GameServer::run() {
            if (!m_isRunning) {
                spdlog::error("������ ���۵��� �ʾҽ��ϴ�. start()�� ���� ȣ���ϼ���.");
                return;
            }

            // ������ Ǯ ����
            for (int i = 0; i < m_config.threadPoolSize; ++i) {
                m_threadPool.emplace_back([this]() {
                    spdlog::debug("������ {} ����", std::this_thread::get_id());
                    try {
                        m_ioContext.run();
                    }
                    catch (const std::exception& e) {
                        spdlog::error("�����忡�� ���� �߻�: {}", e.what());
                    }
                    spdlog::debug("������ {} ����", std::this_thread::get_id());
                    });
            }

            // ���� �����忡���� I/O ó��
            try {
                m_ioContext.run();
            }
            catch (const std::exception& e) {
                spdlog::error("���� �����忡�� ���� �߻�: {}", e.what());
            }
        }

        void GameServer::startAccept() {
            if (!m_isRunning) {
                return;
            }

            // �� ���� ����
            auto newSession = std::make_shared<Session>(m_ioContext, m_nextSessionId++);

            // ���� ���� ����
            m_acceptor.async_accept(newSession->getSocket(),
                [this, newSession](const boost::system::error_code& error) {
                    handleAccept(newSession, error);
                });
        }

        void GameServer::handleAccept(std::shared_ptr<Session> newSession,
            const boost::system::error_code& error) {
            if (!error) {
                // ���� �� ���� Ȯ��
                if (getConnectionCount() >= static_cast<size_t>(m_config.maxConnections)) {
                    spdlog::warn("�ִ� ���� �� ����, �� ���� �ź�: {}",
                        newSession->getRemoteAddress());
                    newSession->close();
                }
                else {
                    spdlog::info("�� Ŭ���̾�Ʈ ����: {} (���� ID: {})",
                        newSession->getRemoteAddress(), newSession->getId());

                    // ���� �߰� �� ����
                    addSession(newSession);
                    newSession->start();

                    m_totalConnections++;
                }
            }
            else {
                spdlog::warn("���� ���� ����: {}", error.message());
            }

            // ���� ���� ���
            startAccept();
        }

        void GameServer::addSession(std::shared_ptr<Session> session) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            m_sessions[session->getId()] = session;

            spdlog::debug("���� �߰���: {} (�� {}��)", session->getId(), m_sessions.size());
        }

        void GameServer::removeSession(uint32_t sessionId) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            auto it = m_sessions.find(sessionId);
            if (it != m_sessions.end()) {
                spdlog::info("Ŭ���̾�Ʈ ���� ����: {} (���� ID: {})",
                    it->second->getRemoteAddress(), sessionId);
                m_sessions.erase(it);
                spdlog::debug("���� ���ŵ�: {} (�� {}��)", sessionId, m_sessions.size());
            }
        }

        std::shared_ptr<Session> GameServer::findSession(uint32_t sessionId) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            auto it = m_sessions.find(sessionId);
            return (it != m_sessions.end()) ? it->second : nullptr;
        }

        size_t GameServer::getConnectionCount() const {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            return m_sessions.size();
        }

        size_t GameServer::getRoomCount() const {
            return m_roomManager ? m_roomManager->getRoomCount() : 0;
        }

        void GameServer::broadcastToAll(const std::string& message) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);

            for (auto& [sessionId, session] : m_sessions) {
                if (session && session->isConnected()) {
                    session->sendMessage(message);
                }
            }

            m_totalMessagesSent += m_sessions.size();
            spdlog::debug("��ü ��ε�ĳ��Ʈ: {}���� ����", m_sessions.size());
        }

        void GameServer::broadcastToRoom(uint32_t roomId, const std::string& message) {
            if (!m_roomManager) {
                spdlog::warn("RoomManager�� �ʱ�ȭ���� ����");
                return;
            }

            auto room = m_roomManager->findRoom(roomId);
            if (!room) {
                spdlog::warn("�������� �ʴ� �� ID: {}", roomId);
                return;
            }

            int sentCount = 0;
            for (auto sessionId : room->getSessionIds()) {
                auto session = findSession(sessionId);
                if (session && session->isConnected()) {
                    session->sendMessage(message);
                    sentCount++;
                }
            }

            m_totalMessagesSent += sentCount;
            spdlog::debug("�� {} ��ε�ĳ��Ʈ: {}���� ����", roomId, sentCount);
        }

        void GameServer::cleanupInactiveSessions() {
            std::vector<uint32_t> inactiveSessions;

            {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);
                for (const auto& [sessionId, session] : m_sessions) {
                    if (!session || !session->isConnected()) {
                        inactiveSessions.push_back(sessionId);
                    }
                }
            }

            for (uint32_t sessionId : inactiveSessions) {
                removeSession(sessionId);
            }

            if (!inactiveSessions.empty()) {
                spdlog::info("��Ȱ�� ���� {}�� ������", inactiveSessions.size());
            }
        }

        void GameServer::startStatisticsTimer() {
            m_statisticsTimer.expires_after(std::chrono::minutes(1));
            m_statisticsTimer.async_wait([this](const boost::system::error_code& error) {
                if (!error && m_isRunning) {
                    printStatistics();
                    startStatisticsTimer(); // ��������� Ÿ�̸� �缳��
                }
                });
        }

        void GameServer::printStatistics() {
            size_t activeConnections = getConnectionCount();
            size_t activeRooms = getRoomCount();

            spdlog::info("=== ���� ��� ===");
            spdlog::info("Ȱ�� ����: {}/{}", activeConnections, m_config.maxConnections);
            spdlog::info("Ȱ�� ��: {}", activeRooms);
            spdlog::info("�� ���� ��: {}", m_totalConnections.load());
            spdlog::info("���� �޽���: {}", m_totalMessagesReceived.load());
            spdlog::info("�۽� �޽���: {}", m_totalMessagesSent.load());
            spdlog::info("================");
        }

        void GameServer::cleanup() {
            spdlog::info("���� ���� �۾� ���� ��...");

            // ��� ���� ����
            {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);
                for (auto& [sessionId, session] : m_sessions) {
                    if (session) {
                        session->close();
                    }
                }
                m_sessions.clear();
            }

            // ���� ���� ������Ʈ ����
            if (m_roomManager) {
                m_roomManager.reset();
            }

            if (m_userManager) {
                m_userManager.reset();
            }

            spdlog::info("���� ���� �۾� �Ϸ�");
        }

    } // namespace Server
} // namespace Blokus