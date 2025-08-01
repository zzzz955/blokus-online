#include "ClientConfigManager.h"
#include <QApplication>
#include <QDebug>

// ===========================================
// 초기화 및 로드
// ===========================================

bool ClientConfigManager::initialize() {
    if (initialized_) {
        qDebug() << "ClientConfigManager already initialized";
        return true;
    }
    
    qDebug() << "ClientConfigManager 초기화 시작";
    
    loadDefaults();
    qDebug() << "설정 로드 완료";
    
    initialized_ = true;    
    return true;
}

void ClientConfigManager::reload() {
    initialized_ = false;
    initialize();
}

void ClientConfigManager::loadDefaults() {
    // 서버 설정 (환경변수 기반)
    // BLOKUS_SERVER_HOST 환경변수가 있으면 사용, 없으면 기본값 (localhost)
    QString envHost = qgetenv("BLOKUS_SERVER_HOST");
    QString envPort = qgetenv("BLOKUS_SERVER_PORT");
    
    if (!envHost.isEmpty()) {
        server_config_.host = envHost;
        qDebug() << QString::fromUtf8("환경변수에서 서버 호스트 설정: %1").arg(envHost);
    } else {
        server_config_.host = "localhost";  // 기본값: 로컬 서버
        qDebug() << QString::fromUtf8("기본 서버 호스트 사용: localhost");
    }
    
    if (!envPort.isEmpty()) {
        bool ok;
        int port = envPort.toInt(&ok);
        if (ok && port > 0 && port <= 65535) {
            server_config_.port = port;
            qDebug() << QString::fromUtf8("환경변수에서 서버 포트 설정: %1").arg(port);
        } else {
            server_config_.port = 9999;  // 잘못된 포트 값일 경우 기본값
            qDebug() << QString::fromUtf8("잘못된 포트 값, 기본 포트 사용: 9999");
        }
    } else {
        server_config_.port = 9999;  // 기본값: 9999 포트
        qDebug() << QString::fromUtf8("기본 서버 포트 사용: 9999");
    }
    
    server_config_.timeout_ms = 5000;
    server_config_.reconnect_attempts = 3;
    server_config_.reconnect_interval_ms = 2000;
    
    // 클라이언트 기본값
    client_config_.window.width = 1280;
    client_config_.window.height = 800;
    client_config_.window.min_width = 800;
    client_config_.window.min_height = 500;
    
    client_config_.game_board.cell_size = 25;
    client_config_.game_board.grid_line_width = 1;
    client_config_.game_board.animation_duration_ms = 300;
    
    client_config_.ui.theme = "default";
    client_config_.ui.language = "ko";
    client_config_.ui.font_size = 9;
    client_config_.ui.auto_save_interval_ms = 30000;
    
    // 디버그 기본값
    debug_config_.enable_console_logs = true;
    debug_config_.log_level = "INFO";
    debug_config_.log_network_messages = false;
    debug_config_.show_fps = false;
    debug_config_.enable_debug_overlay = false;
    
    // 오디오 기본값
    audio_config_.master_volume = 0.8;
    audio_config_.sfx_volume = 0.7;
    audio_config_.music_volume = 0.5;
    audio_config_.mute_on_focus_loss = true;
}