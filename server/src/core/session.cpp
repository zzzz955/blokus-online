#include "core/Session.h"
#include "handler/MessageHandler.h"
#include <spdlog/spdlog.h>
#include <openssl/rand.h>
#include <openssl/sha.h>
#include <chrono>
#include <iomanip>
#include <sstream>

namespace Blokus::Server {

    // ========================================
    // ������ �� �Ҹ���
    // ========================================

    Session::Session(boost::asio::ip::tcp::socket socket)
        : socket_(std::move(socket))
        , sessionId_(generateSessionId())
        , userId_("")
        , username_("")
        , state_(ConnectionState::Connected)
        , active_(true)
        , lastActivity_(std::chrono::steady_clock::now())
        , messageHandler_(nullptr)
        , writing_(false)
    {
        spdlog::debug("Session ����: {}", sessionId_);
    }

    Session::~Session() {
        spdlog::debug("Session �Ҹ�: {}", sessionId_);
        if (active_.load()) {
            stop();
        }
    }

    // ========================================
    // ���� ����
    // ========================================

    void Session::start() {
        if (!active_.load()) {
            spdlog::warn("�̹� ��Ȱ��ȭ�� ���� ���� �õ�: {}", sessionId_);
            return;
        }

        try {
            // ���� �ּ� �α�
            std::string remoteAddr = getRemoteAddress();
            spdlog::info("���� ����: {} (Ŭ���̾�Ʈ: {})", sessionId_, remoteAddr);

            // ���� ������Ʈ
            state_ = ConnectionState::Connected;
            updateLastActivity();

            // �񵿱� �б� ����
            startRead();

        }
        catch (const std::exception& e) {
            spdlog::error("���� ���� �� ���� ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::setMessageHandler(std::unique_ptr<MessageHandler> handler) {
        messageHandler_ = std::move(handler);
    }

    void Session::stop() {
        bool expected = true;
        if (!active_.compare_exchange_strong(expected, false)) {
            return; // �̹� ������
        }

        spdlog::info("���� ����: {}", sessionId_);

        try {
            // ���� ����
            state_ = ConnectionState::Disconnecting;

            // ���� ����
            if (socket_.is_open()) {
                boost::system::error_code ec;
                socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ec);
                socket_.close(ec);
                // ������ ���� (�̹� �������� �� ����)
            }

            // ���� ���� �ݹ� ȣ��
            notifyDisconnect();

            // ���� ���� ����
            state_ = ConnectionState::Disconnected;

        }
        catch (const std::exception& e) {
            spdlog::error("���� ���� �� ���� ({}): {}", sessionId_, e.what());
        }
    }

    // ========================================
    // �޽��� �ۼ���
    // ========================================

    void Session::sendMessage(const std::string& message) {
        if (!active_.load() || !socket_.is_open()) {
            spdlog::debug("��Ȱ�� ���ǿ� �޽��� ���� �õ�: {}", sessionId_);
            return;
        }

        try {
            std::lock_guard<std::mutex> lock(sendMutex_);

            // �޽����� ť�� �߰�
            outgoingMessages_.push(message + "\n"); // �ٹٲ����� ����

            // ���� ���� ���� �ƴϸ� ���� ����
            if (!writing_) {
                writing_ = true;
                doWrite();
            }

        }
        catch (const std::exception& e) {
            spdlog::error("�޽��� ���� �غ� �� ���� ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::sendBinary(const std::vector<uint8_t>& data) {
        // ���̳ʸ� �����͸� Base64�� hex�� ���ڵ��Ͽ� �ؽ�Ʈ�� ����
        // �Ǵ� ������ ���̳ʸ� �������� ����
        // ����� ������ ũ�� ������ ����
        std::string message = "BINARY_DATA:" + std::to_string(data.size());
        sendMessage(message);
    }

    // ========================================
    // ���� ���� ����
    // ========================================

    void Session::setAuthenticated(const std::string& userId, const std::string& username) {
        userId_ = userId;
        username_ = username;
        state_ = ConnectionState::Authenticated;
        updateLastActivity();

        spdlog::info("���� ���� �Ϸ�: {} (�����: {})", sessionId_, username);
    }

    bool Session::isTimedOut(std::chrono::seconds timeout) const {
        auto now = std::chrono::steady_clock::now();
        auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - lastActivity_);
        return elapsed > timeout;
    }

    std::string Session::getRemoteAddress() const {
        try {
            if (socket_.is_open()) {
                auto endpoint = socket_.remote_endpoint();
                return endpoint.address().to_string() + ":" + std::to_string(endpoint.port());
            }
        }
        catch (const std::exception&) {
            // ������ �̹� �����ų� ���� �߻�
        }
        return "unknown";
    }

    // ========================================
    // �񵿱� �б�/����
    // ========================================

    void Session::startRead() {
        if (!active_.load()) {
            return;
        }

        auto self = shared_from_this();
        socket_.async_read_some(
            boost::asio::buffer(readBuffer_),
            [this, self](const boost::system::error_code& error, size_t bytesTransferred) {
                handleRead(error, bytesTransferred);
            });
    }

    void Session::handleRead(const boost::system::error_code& error, size_t bytesTransferred) {
        if (!active_.load()) {
            return;
        }

        if (error) {
            if (error != boost::asio::error::eof &&
                error != boost::asio::error::connection_reset) {
                spdlog::error("�б� ���� ({}): {}", sessionId_, error.message());
            }
            handleError(error);
            return;
        }

        try {
            // ���ŵ� �����͸� ���ۿ� �߰�
            messageBuffer_.append(readBuffer_.data(), bytesTransferred);
            updateLastActivity();

            // ������ �޽����� ó��
            processReceivedData();

            // ���� �б� ����
            startRead();

        }
        catch (const std::exception& e) {
            spdlog::error("�޽��� ó�� �� ���� ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::doWrite() {
        // ���ؽ��� �̹� sendMessage()���� ��������
        if (!active_.load() || outgoingMessages_.empty()) {
            writing_ = false;
            return;
        }

        auto self = shared_from_this();
        boost::asio::async_write(
            socket_,
            boost::asio::buffer(outgoingMessages_.front()),
            [this, self](const boost::system::error_code& error, size_t bytesTransferred) {
                handleWrite(error, bytesTransferred);
            });
    }

    void Session::handleWrite(const boost::system::error_code& error, size_t bytesTransferred) {
        if (!active_.load()) {
            return;
        }

        if (error) {
            spdlog::error("���� ���� ({}): {}", sessionId_, error.message());
            handleError(error);
            return;
        }

        try {
            std::lock_guard<std::mutex> lock(sendMutex_);

            // ���� �Ϸ�� �޽��� ���� - O(1) ����!
            if (!outgoingMessages_.empty()) {
                outgoingMessages_.pop();
            }

            // ��� ���� �޽����� ������ ��� ����
            if (!outgoingMessages_.empty()) {
                doWrite();
            }
            else {
                writing_ = false;  // �� �̻� �� �޽��� ����
            }

        }
        catch (const std::exception& e) {
            spdlog::error("���� �Ϸ� ó�� �� ���� ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    // ========================================
    // �޽��� ó��
    // ========================================

    void Session::processReceivedData() {
        // �ٹٲ����� ���е� �޽����� ó��
        size_t pos = 0;
        while ((pos = messageBuffer_.find('\n')) != std::string::npos) {
            std::string message = messageBuffer_.substr(0, pos);
            messageBuffer_.erase(0, pos + 1);

            // �� �޽��� ����
            if (!message.empty()) {
                // \r ���� (Windows Ŭ���̾�Ʈ ����)
                if (!message.empty() && message.back() == '\r') {
                    message.pop_back();
                }

                processMessage(message);
            }
        }

        // ���� ũ�� ���� (DoS ���� ����)
        const size_t MAX_BUFFER_SIZE = 64 * 1024; // 64KB
        if (messageBuffer_.size() > MAX_BUFFER_SIZE) {
            spdlog::warn("�޽��� ���� ũ�� �ʰ� ({}): {} bytes", sessionId_, messageBuffer_.size());
            handleError(boost::system::error_code());
        }
    }

    void Session::processMessage(const std::string& message) {
        updateLastActivity();

        spdlog::debug("�޽��� ���� ({}): {}", sessionId_,
            message.length() > 100 ? message.substr(0, 100) + "..." : message);

        // �޽��� �ڵ鷯�� ���� ó��
        if (messageHandler_) {
            try {
                messageHandler_->handleMessage(message);
            }
            catch (const std::exception& e) {
                spdlog::error("�޽��� �ڵ鷯 ���� ({}): {}", sessionId_, e.what());
                // ���� ���� ����
                sendMessage("ERROR:Message processing failed");
            }
        }
        else {
            // �޽��� �ݹ� ȣ��
            notifyMessage(message);
        }
    }

    // ========================================
    // ���� ó�� �� ����
    // ========================================

    void Session::handleError(const boost::system::error_code& error) {
        if (error && error != boost::asio::error::eof &&
            error != boost::asio::error::connection_reset) {
            spdlog::error("���� ���� ({}): {}", sessionId_, error.message());
        }

        stop();
    }

    void Session::cleanup() {
        // �޽��� �ڵ鷯 ����
        messageHandler_.reset();

        // ���� ����
        messageBuffer_.clear();

        // ť ���� - while ������ ��� ��� ����
        std::lock_guard<std::mutex> lock(sendMutex_);
        while (!outgoingMessages_.empty()) {
            outgoingMessages_.pop();
        }
        writing_ = false;
    }

    // ========================================
    // �ݹ� ȣ��
    // ========================================

    void Session::notifyDisconnect() {
        if (disconnectCallback_) {
            try {
                disconnectCallback_(sessionId_);
            }
            catch (const std::exception& e) {
                spdlog::error("���� ���� �ݹ� ���� ({}): {}", sessionId_, e.what());
            }
        }
    }

    void Session::notifyMessage(const std::string& message) {
        if (messageCallback_) {
            try {
                messageCallback_(sessionId_, message);
            }
            catch (const std::exception& e) {
                spdlog::error("�޽��� �ݹ� ���� ({}): {}", sessionId_, e.what());
            }
        }
    }

    // ========================================
    // ��ƿ��Ƽ �Լ�
    // ========================================

    std::string Session::generateSessionId() {
        // OpenSSL�� ����� ������ ���� ���� ID ����
        unsigned char randomBytes[16]; // 128��Ʈ
        if (RAND_bytes(randomBytes, sizeof(randomBytes)) != 1) {
            // ���� ���� ���� �� �ð� ��� fallback
            auto now = std::chrono::high_resolution_clock::now();
            auto timestamp = now.time_since_epoch().count();
            return "session_" + std::to_string(timestamp);
        }

        // ����Ʈ�� 16���� ���ڿ��� ��ȯ
        std::stringstream ss;
        ss << std::hex << std::setfill('0');
        for (int i = 0; i < sizeof(randomBytes); ++i) {
            ss << std::setw(2) << static_cast<unsigned>(randomBytes[i]);
        }

        return "sess_" + ss.str();
    }

} // namespace Blokus::Server