#include "NetworkManager.h"
#include "GameServer.h"
#include "Session.h"
#include <spdlog/spdlog.h>
#include <algorithm>

namespace Blokus::Server {

    NetworkManager::NetworkManager(GameServer* server)
        : server_(server)
        , acceptor_(server->getIOContext())
        , running_(false)
        , host_("")
        , port_(0)
    {
        spdlog::debug("NetworkManager created");
    }

    NetworkManager::~NetworkManager()
    {
        stop();
        spdlog::debug("NetworkManager destroyed");
    }

    bool NetworkManager::initialize(const std::string& host, int port)
    {
        try {
            host_ = host;
            port_ = port;

            // Create TCP endpoint
            auto address = boost::asio::ip::address::from_string(host);
            boost::asio::ip::tcp::endpoint endpoint(address, port);

            // Open and configure acceptor
            acceptor_.open(endpoint.protocol());
            acceptor_.set_option(boost::asio::ip::tcp::acceptor::reuse_address(true));
            acceptor_.bind(endpoint);
            acceptor_.listen();

            spdlog::info("NetworkManager initialized on {}:{}", host, port);
            return true;
        }
        catch (const std::exception& e) {
            spdlog::error("Failed to initialize NetworkManager: {}", e.what());
            return false;
        }
    }

    void NetworkManager::start()
    {
        if (running_.load()) {
            spdlog::warn("NetworkManager already running");
            return;
        }

        running_ = true;
        spdlog::info("NetworkManager starting on {}:{}", host_, port_);
        
        startAccepting();
    }

    void NetworkManager::stop()
    {
        if (!running_.load()) {
            return;
        }

        running_ = false;
        spdlog::info("NetworkManager stopping...");

        try {
            acceptor_.close();
        }
        catch (const std::exception& e) {
            spdlog::error("Error closing acceptor: {}", e.what());
        }

        spdlog::info("NetworkManager stopped");
    }

    void NetworkManager::startAccepting()
    {
        if (!running_.load()) {
            return;
        }

        createNewSession();
    }

    void NetworkManager::createNewSession()
    {
        if (!running_.load()) {
            return;
        }

        try {
            auto newSession = std::make_shared<Session>(
                boost::asio::ip::tcp::socket(acceptor_.get_executor())
            );

            // Setup session callbacks
            newSession->setDisconnectCallback([this](const std::string& sessionId) {
                onDisconnection(sessionId);
            });

            newSession->setMessageCallback([this](const std::string& sessionId, const std::string& message) {
                onMessage(sessionId, message);
            });

            acceptor_.async_accept(
                newSession->getSocket(),
                [this, newSession](const boost::system::error_code& error) {
                    handleAccept(newSession, error);
                }
            );
        }
        catch (const std::exception& e) {
            spdlog::error("Error creating new session: {}", e.what());
            // Retry after short delay
            if (running_.load()) {
                auto timer = std::make_shared<boost::asio::steady_timer>(acceptor_.get_executor());
                timer->expires_after(std::chrono::milliseconds(100));
                timer->async_wait([this, timer](const boost::system::error_code&) {
                    startAccepting();
                });
            }
        }
    }

    void NetworkManager::handleAccept(std::shared_ptr<Session> newSession,
                                    const boost::system::error_code& error)
    {
        if (!error) {
            spdlog::info("New client connected from {}", newSession->getRemoteAddress());
            
            // Start the session
            newSession->start();
            
            // Notify server of new connection
            onConnection(newSession);
        }
        else if (error != boost::asio::error::operation_aborted) {
            spdlog::error("Accept error: {}", error.message());
        }

        // Continue accepting new connections
        if (running_.load()) {
            startAccepting();
        }
    }

    void NetworkManager::broadcastMessage(const std::string& message)
    {
        if (messageCallback_) {
            // Get all session IDs from server
            auto sessionIds = getSessionIds();
            for (const auto& sessionId : sessionIds) {
                sendToSession(sessionId, message);
            }
        }
    }

    void NetworkManager::sendToSession(const std::string& sessionId, const std::string& message)
    {
        // Delegate to GameServer to find and send to specific session
        if (server_) {
            server_->sendToSession(sessionId, message);
        }
    }

    size_t NetworkManager::getActiveConnections() const
    {
        // Delegate to GameServer for session count
        return server_ ? server_->getActiveSessionCount() : 0;
    }

    std::vector<std::string> NetworkManager::getSessionIds() const
    {
        // Delegate to GameServer for session IDs
        return server_ ? server_->getSessionIds() : std::vector<std::string>();
    }

    void NetworkManager::onConnection(std::shared_ptr<Session> session)
    {
        if (connectionCallback_) {
            connectionCallback_(session);
        }
    }

    void NetworkManager::onDisconnection(const std::string& sessionId)
    {
        spdlog::info("Client disconnected: {}", sessionId);
        
        if (disconnectionCallback_) {
            disconnectionCallback_(sessionId);
        }
    }

    void NetworkManager::onMessage(const std::string& sessionId, const std::string& message)
    {
        if (messageCallback_) {
            messageCallback_(sessionId, message);
        }
    }

} // namespace Blokus::Server