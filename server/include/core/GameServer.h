#pragma once

#include <memory>
#include <atomic>
#include <thread>
#include <vector>
#include <unordered_map>
#include <mutex>
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

// ���� ���ǵ� Ÿ�Ե� ���
#include "common/ServerTypes.h"
#include "manager/ConfigManager.h"

// ���� ����
namespace Blokus::Server {
	class Session;
	class NetworkManager;
	class DatabaseManager;

	// ���� ���� ���� Ŭ����
	class GameServer {
	public:
		explicit GameServer();
		~GameServer();

		// �⺻ ���� ����
		bool initialize();
		void start();
		void stop();
		void run();

		bool isRunning() const { return running_.load(); }

		// ConfigManager�� ���� ���� ����
		static int getServerPort() { return ConfigManager::serverPort; }
		static int getMaxClients() { return ConfigManager::maxClients; }
		static int getThreadPoolSize() { return ConfigManager::threadPoolSize; }

		// Ŭ���̾�Ʈ ����
		void addSession(std::shared_ptr<Session> session);
		void removeSession(const std::string& sessionId);

		// ���� ��ȸ - �Ͻ��� ���� (shared_ptr)
		std::shared_ptr<Session> getSession(const std::string& sessionId);

		// ���� ��ȸ - ������ (weak_ptr) - �����ֱ⿡ ���� ����
		std::weak_ptr<Session> getSessionWeak(const std::string& sessionId);

		// ������ ���� �۾� - ���ٷ� �۾� ����
		bool withSession(const std::string& sessionId,
			std::function<void(std::shared_ptr<Session>)> action);

		// ������
		boost::asio::io_context& getIOContext() { return ioContext_; }

		// ��� ������
		int getCurrentConnections() const {
			std::lock_guard<std::mutex> lock(statsMutex_);
			return stats_.currentConnections;
		}

		ServerStats getStats() const {
			std::lock_guard<std::mutex> lock(statsMutex_);
			return stats_;
		}

	private:
		// ���� �ʱ�ȭ �Լ���
		bool initializeConfig();
		bool initializeDatabase();
		bool initializeNetwork();

		// ��Ʈ��ũ ó��
		void startAccepting();
		void handleNewConnection(std::shared_ptr<Session> session,
			const boost::system::error_code& error);

		// ���� �̺�Ʈ �ڵ鷯 (�ݹ����� ȣ���)
		void onSessionDisconnect(const std::string& sessionId);
		void onSessionMessage(const std::string& sessionId, const std::string& message);

		// MessageHandler �ݹ� ó�� �Լ���
		void handleAuthentication(const std::string& sessionId, const std::string& username, bool success);
		void handleRoomAction(const std::string& sessionId, const std::string& action, const std::string& data);
		void handleChatBroadcast(const std::string& sessionId, const std::string& message);

		// ���� �۾�
		void startHeartbeatTimer();
		void handleHeartbeat();
		void cleanupSessions();

	private:
		// �⺻ ����
		std::atomic<bool> running_{ false };

		// Boost.Asio �ٽ�
		boost::asio::io_context ioContext_;
		boost::asio::ip::tcp::acceptor acceptor_;
		std::vector<std::thread> threadPool_;

		// Ÿ�̸�
		std::unique_ptr<boost::asio::steady_timer> heartbeatTimer_;

		// �Ŵ����� (���� ���� ����)
		//std::unique_ptr<NetworkManager> networkManager_;

		// ���� ����
		std::unordered_map<std::string, std::shared_ptr<Session>> sessions_;
		std::mutex sessionsMutex_;

		// ���� ��� (ServerTypes.h�� ServerStats ���)
		ServerStats stats_;
		mutable std::mutex statsMutex_;
	};

} // namespace Blokus::Server