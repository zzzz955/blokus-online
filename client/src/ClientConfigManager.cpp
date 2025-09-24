#include "ClientConfigManager.h"
#include <QApplication>
#include <QDebug>
#include <QFile>
#include <QTextStream>
#include <QDir>

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
    // 서버 설정: 빌드 모드에 따른 하드코딩된 값 사용
    // Debug 모드: localhost
    // Release 모드: blokus-online.mooo.com
    
    #ifdef _DEBUG
        // Debug 모드: localhost 사용
        server_config_.host = "localhost";
        qDebug() << QString::fromUtf8("디버그 모드: localhost 서버 사용");
    #else
        // Release 모드: 프로덕션 서버 사용
        server_config_.host = "blokus-online.mooo.com";
        qDebug() << QString::fromUtf8("릴리즈 모드: 프로덕션 서버 사용 (blokus-online.mooo.com)");
    #endif
    
    // 포트는 항상 9999 사용
    server_config_.port = 9999;
    qDebug() << QString::fromUtf8("서버 설정 - 호스트: '%1', 포트: %2").arg(server_config_.host).arg(server_config_.port);
    
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

