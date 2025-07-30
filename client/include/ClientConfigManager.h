#pragma once

#include <QString>
#include <QDebug>

/**
 * @brief 클라이언트 설정 관리 클래스
 * 
 * JSON 파일 기반으로 클라이언트 설정을 관리합니다.
 * 우선순위: config.json > default.json > 하드코딩된 기본값
 */
class ClientConfigManager {
public:
    // ===========================================
    // 설정 구조체들
    // ===========================================
    
    struct ServerConfig {
        QString host = "localhost";
        int port = 9999;
        int timeout_ms = 5000;
        int reconnect_attempts = 3;
        int reconnect_interval_ms = 2000;
    };
    
    struct WindowConfig {
        int width = 1280;
        int height = 800;
        int min_width = 800;
        int min_height = 500;
    };
    
    struct GameBoardConfig {
        int cell_size = 25;
        int grid_line_width = 1;
        int animation_duration_ms = 300;
    };
    
    struct UIConfig {
        QString theme = "default";
        QString language = "ko";
        int font_size = 12;
        int auto_save_interval_ms = 30000;
    };
    
    struct ClientConfig {
        WindowConfig window;
        GameBoardConfig game_board;
        UIConfig ui;
    };
    
    struct DebugConfig {
        bool enable_console_logs = true;
        QString log_level = "INFO";
        bool log_network_messages = false;
        bool show_fps = false;
        bool enable_debug_overlay = false;
    };
    
    struct AudioConfig {
        double master_volume = 0.8;
        double sfx_volume = 0.7;
        double music_volume = 0.5;
        bool mute_on_focus_loss = true;
    };

public:
    // ===========================================  
    // 싱글톤 패턴
    // ===========================================
    
    static ClientConfigManager& instance() {
        static ClientConfigManager instance;
        return instance;
    }
    
    // 복사 생성자와 대입 연산자 삭제
    ClientConfigManager(const ClientConfigManager&) = delete;
    ClientConfigManager& operator=(const ClientConfigManager&) = delete;

    // ===========================================
    // 초기화 및 로드
    // ===========================================
    
    /**
     * @brief 설정 파일을 로드하고 초기화합니다
     * @return 성공 여부
     */
    bool initialize();
    
    /**
     * @brief 설정을 다시 로드합니다
     */
    void reload();

    // ===========================================
    // 설정 접근자들
    // ===========================================
    
    const ServerConfig& getServerConfig() const { return server_config_; }
    const ClientConfig& getClientConfig() const { return client_config_; }
    const DebugConfig& getDebugConfig() const { return debug_config_; }
    const AudioConfig& getAudioConfig() const { return audio_config_; }
    
    // 편의 함수들
    QString getServerHost() const { return server_config_.host; }
    int getServerPort() const { return server_config_.port; }
    bool isDebugMode() const { return debug_config_.enable_console_logs; }
    QString getLogLevel() const { return debug_config_.log_level; }

private:
    ClientConfigManager() = default;
    ~ClientConfigManager() = default;

    // ===========================================
    // 내부 함수들
    // ===========================================
    
    void loadDefaults();

private:
    // 설정 데이터
    ServerConfig server_config_;
    ClientConfig client_config_;
    DebugConfig debug_config_;
    AudioConfig audio_config_;
    
    // 초기화 상태
    bool initialized_ = false;
};