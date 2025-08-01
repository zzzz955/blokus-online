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
    // .env 파일 로드 (선택사항)
    loadEnvFile();
    
    // 서버 설정 (환경변수 기반)
    // BLOKUS_SERVER_HOST 환경변수가 있으면 사용, 없으면 기본값 (localhost)
    QString envHost = qgetenv("BLOKUS_SERVER_HOST");
    QString envPort = qgetenv("BLOKUS_SERVER_PORT");
    
    if (!envHost.isEmpty()) {
        server_config_.host = envHost;
        qDebug() << QString::fromUtf8("🌐 환경변수에서 서버 호스트 설정: '%1'").arg(envHost);
    } else {
        server_config_.host = "localhost";  // 기본값: 로컬 서버
        qDebug() << QString::fromUtf8("🏠 기본 서버 호스트 사용: localhost");
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

void ClientConfigManager::loadEnvFile() {
    // .env 파일 경로들 (우선순위 순)
    QStringList envPaths = {
        QDir::currentPath() + "/.env",              // 현재 디렉토리
        QDir::currentPath() + "/../.env",           // 상위 디렉토리 (빌드 시)
        QApplication::applicationDirPath() + "/.env" // 실행 파일 디렉토리
    };
    
    for (const QString& envPath : envPaths) {
        QFile envFile(envPath);
        if (envFile.exists() && envFile.open(QIODevice::ReadOnly | QIODevice::Text)) {
            qDebug() << QString::fromUtf8(".env 파일 로드: %1").arg(envPath);
            
            QTextStream in(&envFile);
            while (!in.atEnd()) {
                QString line = in.readLine().trimmed();
                
                // 주석이나 빈 줄 무시
                if (line.isEmpty() || line.startsWith('#')) {
                    continue;
                }
                
                // KEY=VALUE 형태 파싱
                int equalPos = line.indexOf('=');
                if (equalPos > 0) {
                    QString key = line.left(equalPos).trimmed();
                    QString value = line.mid(equalPos + 1).trimmed();
                    
                    // 따옴표 제거 (있는 경우)
                    if (value.startsWith('"') && value.endsWith('"')) {
                        value = value.mid(1, value.length() - 2);
                    } else if (value.startsWith('\'') && value.endsWith('\'')) {
                        value = value.mid(1, value.length() - 2);
                    }
                    
                    // 환경변수 설정 (기존 시스템 환경변수가 없는 경우에만)
                    QString existingValue = qgetenv(key.toUtf8().constData());
                    if (existingValue.isEmpty()) {
                        qputenv(key.toUtf8().constData(), value.toUtf8());
                        qDebug() << QString::fromUtf8("✅ .env에서 환경변수 설정: %1='%2'").arg(key).arg(value);
                    } else {
                        qDebug() << QString::fromUtf8("⚠️ 시스템 환경변수가 이미 존재: %1='%2' (무시됨: '%3')").arg(key).arg(existingValue).arg(value);
                    }
                }
            }
            envFile.close();
            return; // 첫 번째로 찾은 .env 파일만 사용
        }
    }
    
    qDebug() << QString::fromUtf8(".env 파일을 찾을 수 없습니다. 시스템 환경변수 또는 기본값 사용");
}