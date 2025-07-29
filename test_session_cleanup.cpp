/**
 * 세션 정리 시스템 시뮬레이션 테스트
 * 실제 네트워크 연결 없이 세션 타임아웃 로직 검증
 */
#include <iostream>
#include <chrono>
#include <thread>
#include <vector>
#include <memory>
#include <string>

// 간단한 세션 시뮬레이터
class SessionSimulator {
private:
    std::string userId_;
    std::chrono::steady_clock::time_point lastActivity_;
    bool isInGame_;
    bool isActive_;

public:
    SessionSimulator(const std::string& userId, bool inGame = false) 
        : userId_(userId), isInGame_(inGame), isActive_(true) {
        updateActivity();
    }
    
    void updateActivity() {
        lastActivity_ = std::chrono::steady_clock::now();
    }
    
    bool isTimedOut(std::chrono::seconds timeout) const {
        auto now = std::chrono::steady_clock::now();
        auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - lastActivity_);
        return elapsed > timeout;
    }
    
    bool isInGame() const { return isInGame_; }
    bool isActive() const { return isActive_; }
    void setActive(bool active) { isActive_ = active; }
    const std::string& getUserId() const { return userId_; }
    
    void simulateDisconnect() {
        isActive_ = false;
        std::cout << "🔌 [DISCONNECT] 세션 " << userId_ << " 연결 끊어짐 시뮬레이션\n";
    }
};

// 세션 정리 시뮬레이터
class CleanupSimulator {
private:
    std::vector<std::unique_ptr<SessionSimulator>> sessions_;
    
public:
    void addSession(const std::string& userId, bool inGame = false) {
        sessions_.push_back(std::make_unique<SessionSimulator>(userId, inGame));
        std::cout << "➕ [ADD] 세션 추가: " << userId 
                  << (inGame ? " (게임 중)" : " (로비)") << "\n";
    }
    
    void simulateCleanup() {
        std::cout << "\n🧹 [CLEANUP] 세션 정리 시뮬레이션 시작\n";
        
        auto it = sessions_.begin();
        while (it != sessions_.end()) {
            auto& session = *it;
            
            if (!session->isActive()) {
                std::cout << "❌ [REMOVE] 비활성 세션 제거: " << session->getUserId() << "\n";
                it = sessions_.erase(it);
                continue;
            }
            
            // 타임아웃 체크 - 게임 중인 세션은 2분, 일반은 5분
            std::chrono::seconds timeoutDuration = std::chrono::seconds(300); // 5분
            if (session->isInGame()) {
                timeoutDuration = std::chrono::seconds(120); // 2분
            }
            
            if (session->isTimedOut(timeoutDuration)) {
                if (session->isInGame()) {
                    std::cout << "🎮 [TIMEOUT] 게임 중 세션 타임아웃 (좀비방 방지): " 
                             << session->getUserId() << " (" << timeoutDuration.count() / 60 << "분)\n";
                } else {
                    std::cout << "⏰ [TIMEOUT] 세션 타임아웃: " 
                             << session->getUserId() << " (" << timeoutDuration.count() / 60 << "분)\n";
                }
                it = sessions_.erase(it);
            } else {
                ++it;
            }
        }
        
        std::cout << "📊 [STATUS] 현재 활성 세션 수: " << sessions_.size() << "\n\n";
    }
    
    void simulatePlayerDisconnect(const std::string& userId) {
        for (auto& session : sessions_) {
            if (session->getUserId() == userId) {
                session->simulateDisconnect();
                break;
            }
        }
    }
    
    void printStatus() {
        std::cout << "📋 [STATUS] 현재 세션 상태:\n";
        for (const auto& session : sessions_) {
            std::cout << "  - " << session->getUserId() 
                     << " (" << (session->isInGame() ? "게임중" : "로비") 
                     << ", " << (session->isActive() ? "활성" : "비활성") << ")\n";
        }
        std::cout << "\n";
    }
};

int main() {
    std::cout << "🚀 세션 정리 시스템 시뮬레이션 테스트 시작\n";
    std::cout << "========================================\n\n";
    
    CleanupSimulator simulator;
    
    // 테스트 시나리오 1: 로비 세션들 추가
    std::cout << "📋 [SCENARIO 1] 로비 세션 추가\n";
    simulator.addSession("user1_lobby", false);
    simulator.addSession("user2_lobby", false);
    simulator.printStatus();
    
    // 테스트 시나리오 2: 게임 중 세션들 추가
    std::cout << "📋 [SCENARIO 2] 게임 중 세션 추가\n";
    simulator.addSession("user3_ingame", true);
    simulator.addSession("user4_ingame", true);
    simulator.printStatus();
    
    // 테스트 시나리오 3: 즉시 정리 (아직 타임아웃 안됨)
    std::cout << "📋 [SCENARIO 3] 즉시 정리 (타임아웃 전)\n";
    simulator.simulateCleanup();
    
    // 테스트 시나리오 4: 한 플레이어 연결 끊기
    std::cout << "📋 [SCENARIO 4] 플레이어 연결 끊기 시뮬레이션\n";
    simulator.simulatePlayerDisconnect("user2_lobby");
    simulator.simulateCleanup();
    
    // 테스트 시나리오 5: 2분 대기 후 정리 (게임 중 세션 타임아웃)
    std::cout << "📋 [SCENARIO 5] 2분 대기 후 게임 중 세션 타임아웃 테스트\n";
    std::cout << "⏳ 2분 대기 중...\n";
    std::this_thread::sleep_for(std::chrono::seconds(121)); // 2분 1초 대기
    
    simulator.simulateCleanup();
    
    // 테스트 시나리오 6: 추가 3분 대기 후 정리 (로비 세션도 타임아웃)  
    std::cout << "📋 [SCENARIO 6] 추가 3분 대기 후 로비 세션 타임아웃 테스트\n";
    std::cout << "⏳ 3분 대기 중...\n";
    std::this_thread::sleep_for(std::chrono::seconds(181)); // 3분 1초 대기
    
    simulator.simulateCleanup();
    
    std::cout << "✅ 세션 정리 시스템 시뮬레이션 테스트 완료\n";
    std::cout << "========================================\n";
    
    return 0;
}