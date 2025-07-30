#include "ClientConfigManager.h"
#include <QJsonObject>
#include <QJsonDocument>
#include <QFile>
#include <QDir>
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
    
    // 1. 기본값 로드
    loadDefaults();
    qDebug() << "기본 설정 로드 완료";
    
    // 2. default.json 파일 로드
    QString defaultPath = getDefaultConfigPath();
    if (QFile::exists(defaultPath)) {
        if (loadFromFile(defaultPath)) {
            qDebug() << "default.json 로드 완료:" << defaultPath;
        } else {
            qWarning() << "default.json 로드 실패:" << defaultPath;
        }
    } else {
        qWarning() << "default.json 파일 없음:" << defaultPath;
    }
    
    // 3. config.json 파일 로드 (최우선순위)
    QString configPath = getConfigFilePath();
    if (QFile::exists(configPath)) {
        if (loadFromFile(configPath)) {
            qDebug() << "config.json 로드 완료:" << configPath;
        } else {
            qWarning() << "config.json 로드 실패:" << configPath;
        }
    } else {
        qDebug() << "config.json 파일 없음, 기본 설정 사용:" << configPath;
    }
    
    initialized_ = true;
    
    // 로드된 설정 출력
    qDebug() << "=== 로드된 설정 ===";
    qDebug() << "서버:" << server_config_.host << ":" << server_config_.port;
    qDebug() << "창 크기:" << client_config_.window.width << "x" << client_config_.window.height;
    qDebug() << "디버그 모드:" << debug_config_.enable_console_logs;
    qDebug() << "==================";
    
    return true;
}

void ClientConfigManager::reload() {
    initialized_ = false;
    initialize();
}

// ===========================================
// 파일 로드
// ===========================================

bool ClientConfigManager::loadFromFile(const QString& filepath) {
    QFile file(filepath);
    if (!file.open(QIODevice::ReadOnly)) {
        qWarning() << "설정 파일 열기 실패:" << filepath;
        return false;
    }
    
    QByteArray data = file.readAll();
    QJsonDocument doc = QJsonDocument::fromJson(data);
    
    if (doc.isNull() || !doc.isObject()) {
        qWarning() << "잘못된 JSON 형식:" << filepath;
        return false;
    }
    
    QJsonObject root = doc.object();
    
    // 각 섹션별로 로드
    if (root.contains("server")) {
        loadServerConfig(root["server"].toObject());
    }
    
    if (root.contains("client")) {
        loadClientConfig(root["client"].toObject());
    }
    
    if (root.contains("debug")) {
        loadDebugConfig(root["debug"].toObject());
    }
    
    if (root.contains("audio")) {
        loadAudioConfig(root["audio"].toObject());
    }
    
    return true;
}

void ClientConfigManager::loadDefaults() {
    // 서버 기본값
    server_config_.host = "localhost";
    server_config_.port = 9999;
    server_config_.timeout_ms = 5000;
    server_config_.reconnect_attempts = 3;
    server_config_.reconnect_interval_ms = 2000;
    
    // 클라이언트 기본값
    client_config_.window.width = 1400;
    client_config_.window.height = 900;
    client_config_.window.min_width = 1000;
    client_config_.window.min_height = 700;
    
    client_config_.game_board.cell_size = 25;
    client_config_.game_board.grid_line_width = 1;
    client_config_.game_board.animation_duration_ms = 300;
    
    client_config_.ui.theme = "default";
    client_config_.ui.language = "ko";
    client_config_.ui.font_size = 12;
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

void ClientConfigManager::loadServerConfig(const QJsonObject& obj) {
    if (obj.contains("host") && obj["host"].isString()) {
        server_config_.host = obj["host"].toString();
    }
    if (obj.contains("port") && obj["port"].isDouble()) {
        server_config_.port = obj["port"].toInt();
    }
    if (obj.contains("timeout_ms") && obj["timeout_ms"].isDouble()) {
        server_config_.timeout_ms = obj["timeout_ms"].toInt();
    }
    if (obj.contains("reconnect_attempts") && obj["reconnect_attempts"].isDouble()) {
        server_config_.reconnect_attempts = obj["reconnect_attempts"].toInt();
    }
    if (obj.contains("reconnect_interval_ms") && obj["reconnect_interval_ms"].isDouble()) {
        server_config_.reconnect_interval_ms = obj["reconnect_interval_ms"].toInt();
    }
}

void ClientConfigManager::loadClientConfig(const QJsonObject& obj) {
    // 윈도우 설정
    if (obj.contains("window") && obj["window"].isObject()) {
        QJsonObject window = obj["window"].toObject();
        if (window.contains("width") && window["width"].isDouble()) {
            client_config_.window.width = window["width"].toInt();
        }
        if (window.contains("height") && window["height"].isDouble()) {
            client_config_.window.height = window["height"].toInt();
        }
        if (window.contains("min_width") && window["min_width"].isDouble()) {
            client_config_.window.min_width = window["min_width"].toInt();
        }
        if (window.contains("min_height") && window["min_height"].isDouble()) {
            client_config_.window.min_height = window["min_height"].toInt();
        }
    }
    
    // 게임 보드 설정
    if (obj.contains("game_board") && obj["game_board"].isObject()) {
        QJsonObject board = obj["game_board"].toObject();
        if (board.contains("cell_size") && board["cell_size"].isDouble()) {
            client_config_.game_board.cell_size = board["cell_size"].toInt();
        }
        if (board.contains("grid_line_width") && board["grid_line_width"].isDouble()) {
            client_config_.game_board.grid_line_width = board["grid_line_width"].toInt();
        }
        if (board.contains("animation_duration_ms") && board["animation_duration_ms"].isDouble()) {
            client_config_.game_board.animation_duration_ms = board["animation_duration_ms"].toInt();
        }
    }
    
    // UI 설정
    if (obj.contains("ui") && obj["ui"].isObject()) {
        QJsonObject ui = obj["ui"].toObject();
        if (ui.contains("theme") && ui["theme"].isString()) {
            client_config_.ui.theme = ui["theme"].toString();
        }
        if (ui.contains("language") && ui["language"].isString()) {
            client_config_.ui.language = ui["language"].toString();
        }
        if (ui.contains("font_size") && ui["font_size"].isDouble()) {
            client_config_.ui.font_size = ui["font_size"].toInt();
        }
        if (ui.contains("auto_save_interval_ms") && ui["auto_save_interval_ms"].isDouble()) {
            client_config_.ui.auto_save_interval_ms = ui["auto_save_interval_ms"].toInt();
        }
    }
}

void ClientConfigManager::loadDebugConfig(const QJsonObject& obj) {
    if (obj.contains("enable_console_logs") && obj["enable_console_logs"].isBool()) {
        debug_config_.enable_console_logs = obj["enable_console_logs"].toBool();
    }
    if (obj.contains("log_level") && obj["log_level"].isString()) {
        debug_config_.log_level = obj["log_level"].toString();
    }
    if (obj.contains("log_network_messages") && obj["log_network_messages"].isBool()) {
        debug_config_.log_network_messages = obj["log_network_messages"].toBool();
    }
    if (obj.contains("show_fps") && obj["show_fps"].isBool()) {
        debug_config_.show_fps = obj["show_fps"].toBool();
    }
    if (obj.contains("enable_debug_overlay") && obj["enable_debug_overlay"].isBool()) {
        debug_config_.enable_debug_overlay = obj["enable_debug_overlay"].toBool();
    }
}

void ClientConfigManager::loadAudioConfig(const QJsonObject& obj) {
    if (obj.contains("master_volume") && obj["master_volume"].isDouble()) {
        audio_config_.master_volume = obj["master_volume"].toDouble();
    }
    if (obj.contains("sfx_volume") && obj["sfx_volume"].isDouble()) {
        audio_config_.sfx_volume = obj["sfx_volume"].toDouble();
    }
    if (obj.contains("music_volume") && obj["music_volume"].isDouble()) {
        audio_config_.music_volume = obj["music_volume"].toDouble();
    }
    if (obj.contains("mute_on_focus_loss") && obj["mute_on_focus_loss"].isBool()) {
        audio_config_.mute_on_focus_loss = obj["mute_on_focus_loss"].toBool();
    }
}

// ===========================================
// 경로 관리
// ===========================================

QString ClientConfigManager::getConfigDirectory() const {
    // 실행 파일과 같은 위치의 config 폴더
    return QApplication::applicationDirPath() + "/config";
}

QString ClientConfigManager::getConfigFilePath() const {
    return getConfigDirectory() + "/config.json";
}

QString ClientConfigManager::getDefaultConfigPath() const {
    return getConfigDirectory() + "/default.json";
}

// ===========================================
// 런타임 설정 업데이트
// ===========================================

void ClientConfigManager::updateServerHost(const QString& host) {
    server_config_.host = host;
    qDebug() << "서버 주소 업데이트:" << host;
}

void ClientConfigManager::updateServerPort(int port) {
    server_config_.port = port;
    qDebug() << "서버 포트 업데이트:" << port;
}

void ClientConfigManager::updateDebugMode(bool enabled) {
    debug_config_.enable_console_logs = enabled;
    qDebug() << "디버그 모드 업데이트:" << enabled;
}

bool ClientConfigManager::saveConfig() {
    // TODO: 현재 설정을 JSON 파일로 저장하는 기능
    // 지금은 로드만 구현하고, 필요시 나중에 추가
    qDebug() << "설정 저장 기능은 아직 구현되지 않았습니다";
    return false;
}