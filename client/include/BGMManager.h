#ifndef BGMMANAGER_H
#define BGMMANAGER_H

#include <QObject>
#include <QString>
#include <QDebug>
#include <QCoreApplication>
#include <map>
#include <string>

// SDL2 헤더 포함
#ifdef _WIN32
#include <SDL.h>
#include <SDL_mixer_ext.h>
#else
#include <SDL2/SDL.h>
#include <SDL2/SDL_mixer_ext.h>
#endif

/**
 * @brief BGMManager - SDL_mixer 기반 게임 BGM 관리자
 * 
 * 🎮 게임에 최적화된 이벤트 기반 BGM 시스템
 * - 상태 기반 BGM 관리 (LOBBY, GAME_ROOM, IN_GAME)
 * - UI 블로킹 없는 즉시 재생
 * - Window 생명주기와 완전 분리
 * - 시그널 성공 시점에 상태 전환
 * 
 * 🔥 핵심 메서드: transitionToState() - 상태 전환 시 자동 BGM 교체
 */
class BGMManager : public QObject
{
    Q_OBJECT

public:
    // 🎮 게임 상태 기반 BGM 관리
    enum class GameState {
        NONE = 0,       // BGM 없음
        LOBBY = 1,      // 로비 BGM  
        GAME_ROOM = 2,  // 게임룸 BGM
        IN_GAME = 3     // 게임 중 BGM (향후 확장)
    };

    static BGMManager& getInstance();
    
    // 🔥 핵심: 이벤트 기반 상태 전환 (Window 생명주기와 분리)
    void transitionToState(GameState newState);
    
    // 💡 편의 메서드 (시그널 성공 지점에서 호출)
    void onLobbyEntered()    { transitionToState(GameState::LOBBY); }
    void onGameRoomEntered() { transitionToState(GameState::GAME_ROOM); }
    void onGameStarted()     { transitionToState(GameState::IN_GAME); }
    void onBGMDisabled()     { transitionToState(GameState::NONE); }
    
    // 상태 조회
    GameState getCurrentState() const { return m_currentState; }
    
    // 볼륨 제어 (0.0 ~ 1.0)
    void setVolume(float volume);
    float getVolume() const { return m_volume; }
    
    // 음소거
    void setMuted(bool muted);
    bool isMuted() const { return m_muted; }
    
    // 초기화 상태 확인
    bool isInitialized() const { return m_initialized; }

private:
    BGMManager();
    ~BGMManager();
    
    // 복사/할당 방지 (싱글톤)
    BGMManager(const BGMManager&) = delete;
    BGMManager& operator=(const BGMManager&) = delete;
    
    // SDL_mixer 초기화/정리
    bool initializeSDL();
    void cleanupSDL();
    
    // BGM 파일 로딩 및 재생
    void loadAndPlayBGM(GameState state);
    void stopCurrentBGM();
    
    // 상태별 파일 경로 해결
    std::string getStateMusicPath(GameState state) const;
    std::string getApplicationPath() const;
    
    // 디버깅 유틸리티
    const char* stateToString(GameState state) const;

private:
    // SDL_mixer 관련
    Mix_Music* m_currentMusic;
    bool m_initialized;
    
    // 게임 상태 및 설정
    GameState m_currentState;
    float m_volume;        // 0.0 ~ 1.0
    bool m_muted;
    
    // 상태별 BGM 파일 경로 맵핑
    std::map<GameState, std::string> m_musicPaths;
};

#endif // BGMMANAGER_H