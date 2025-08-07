#include "BGMManager.h"
#include <QDir>
#include <QFile>

BGMManager::BGMManager()
    : m_currentMusic(nullptr)
    , m_initialized(false)
    , m_currentState(GameState::NONE)
    , m_volume(0.7f)  // 게임 적정 볼륨 70%
    , m_muted(false)
{
    qDebug() << "🎮 BGMManager initializing with SDL_mixer...";
    
    // 상태별 BGM 파일 경로 설정
    m_musicPaths[GameState::LOBBY] = "resource/lobby/bgm_lobby.mp3";
    m_musicPaths[GameState::GAME_ROOM] = "resource/gameroom/bgm_gameroom.mp3";
    // GameState::IN_GAME는 향후 추가 예정
    
    // SDL_mixer 초기화
    m_initialized = initializeSDL();
    
    if (m_initialized) {
        qDebug() << "✅ BGMManager initialized successfully";
    } else {
        qWarning() << "❌ BGMManager initialization failed - BGM disabled";
    }
}

BGMManager::~BGMManager()
{
    qDebug() << "🎮 BGMManager shutting down...";
    cleanupSDL();
}

BGMManager& BGMManager::getInstance()
{
    static BGMManager instance;
    return instance;
}

bool BGMManager::initializeSDL()
{
    // SDL 오디오 초기화
    if (SDL_Init(SDL_INIT_AUDIO) < 0) {
        qWarning() << "SDL_Init failed:" << SDL_GetError();
        return false;
    }
    
    // SDL_mixer 초기화 (44.1kHz, 16-bit, 스테레오, 4096 바이트 버퍼)
    if (Mix_OpenAudio(44100, MIX_DEFAULT_FORMAT, 2, 4096) < 0) {
        qWarning() << "Mix_OpenAudio failed:" << Mix_GetError();
        SDL_Quit();
        return false;
    }
    
    // 음악 채널 할당 (BGM용)
    Mix_AllocateChannels(16);
    
    qDebug() << "🔊 SDL_mixer audio system initialized";
    qDebug() << "   - Sample rate: 44100 Hz";
    qDebug() << "   - Format: 16-bit stereo";
    qDebug() << "   - Buffer size: 4096 bytes";
    
    // Check supported audio formats
    int formats = Mix_Init(0);
    qDebug() << "🎵 Supported audio formats:";
    qDebug() << "   - OGG:" << (formats & MIX_INIT_OGG ? "YES" : "NO");
    qDebug() << "   - MP3:" << (formats & MIX_INIT_MP3 ? "YES" : "NO");
    qDebug() << "   - FLAC:" << (formats & MIX_INIT_FLAC ? "YES" : "NO");
    
    return true;
}

void BGMManager::cleanupSDL()
{
    // 현재 재생 중인 음악 정지 및 해제
    stopCurrentBGM();
    
    // SDL_mixer 종료
    Mix_CloseAudio();
    SDL_Quit();
    
    qDebug() << "🔊 SDL_mixer audio system cleaned up";
}

void BGMManager::transitionToState(GameState newState)
{
    if (!m_initialized) {
        qDebug() << "BGM system not initialized - state transition ignored";
        return;
    }
    
    if (m_currentState == newState) {
        qDebug() << "BGM state unchanged:" << stateToString(newState);
        return;
    }
    
    qDebug() << "🎵 BGM state transition:" << stateToString(m_currentState) 
             << "→" << stateToString(newState);
    
    // 1. 현재 BGM 정지
    stopCurrentBGM();
    
    // 2. 상태 업데이트
    m_currentState = newState;
    
    // 3. 새 상태의 BGM 로드 및 재생
    if (newState != GameState::NONE && !m_muted) {
        loadAndPlayBGM(newState);
    }
}

void BGMManager::loadAndPlayBGM(GameState state)
{
    if (state == GameState::NONE) {
        return;
    }
    
    // 상태별 음악 파일 경로 가져오기
    std::string musicPath = getStateMusicPath(state);
    if (musicPath.empty()) {
        qDebug() << "No BGM file configured for state:" << stateToString(state);
        return;
    }
    
    // 음악 파일 로드
    qDebug() << "🎵 Attempting to load BGM:" << musicPath.c_str();
    
    m_currentMusic = Mix_LoadMUS(musicPath.c_str());
    if (!m_currentMusic) {
        qWarning() << "❌ BGM file load failed:" << musicPath.c_str();
        qWarning() << "   SDL_mixer error:" << Mix_GetError();
        qWarning() << "   File exists:" << QFile::exists(QString::fromStdString(musicPath));
        qWarning() << "   Continuing without background music...";
        return;
    }
    
    // 볼륨 설정 (0~128 범위로 변환)
    int sdlVolume = static_cast<int>(m_volume * MIX_MAX_VOLUME);
    Mix_VolumeMusic(sdlVolume);
    
    // 무한 반복 재생 (-1 = 무한반복)
    if (Mix_PlayMusic(m_currentMusic, -1) < 0) {
        qWarning() << "Mix_PlayMusic failed:" << Mix_GetError();
        Mix_FreeMusic(m_currentMusic);
        m_currentMusic = nullptr;
        return;
    }
    
    qDebug() << "🎵 BGM playing:" << stateToString(state) 
             << "(" << QString::fromStdString(musicPath) << ")";
}

void BGMManager::stopCurrentBGM()
{
    if (m_currentMusic) {
        Mix_HaltMusic();
        Mix_FreeMusic(m_currentMusic);
        m_currentMusic = nullptr;
        qDebug() << "🔇 BGM stopped";
    }
}

std::string BGMManager::getStateMusicPath(GameState state) const
{
    auto it = m_musicPaths.find(state);
    if (it == m_musicPaths.end()) {
        return "";
    }
    
    std::string appPath = getApplicationPath();
    return appPath + "/" + it->second;
}

std::string BGMManager::getApplicationPath() const
{
    QString qAppPath = QCoreApplication::applicationDirPath();
    return qAppPath.toStdString();
}

void BGMManager::setVolume(float volume)
{
    // 볼륨 범위 제한 (0.0 ~ 1.0)
    m_volume = qBound(0.0f, volume, 1.0f);
    
    if (m_initialized && Mix_PlayingMusic()) {
        int sdlVolume = static_cast<int>(m_volume * MIX_MAX_VOLUME);
        Mix_VolumeMusic(sdlVolume);
    }
    
    qDebug() << "🔊 BGM volume set to:" << (m_volume * 100.0f) << "%";
}

void BGMManager::setMuted(bool muted)
{
    if (m_muted == muted) {
        return;
    }
    
    m_muted = muted;
    
    if (!m_initialized) {
        return;
    }
    
    if (muted) {
        // 음소거: 현재 재생 중인 BGM 정지
        if (Mix_PlayingMusic()) {
            Mix_PauseMusic();
            qDebug() << "🔇 BGM muted";
        }
    } else {
        // 음소거 해제: 일시정지된 BGM이 있으면 재개, 없으면 현재 상태 BGM 재생
        if (Mix_PausedMusic()) {
            Mix_ResumeMusic();
            qDebug() << "🔊 BGM unmuted (resumed)";
        } else if (m_currentState != GameState::NONE) {
            // 현재 상태에 맞는 BGM 새로 재생
            loadAndPlayBGM(m_currentState);
            qDebug() << "🔊 BGM unmuted (restarted)";
        }
    }
}

const char* BGMManager::stateToString(GameState state) const
{
    switch (state) {
        case GameState::NONE:      return "NONE";
        case GameState::LOBBY:     return "LOBBY";
        case GameState::GAME_ROOM: return "GAME_ROOM";
        case GameState::IN_GAME:   return "IN_GAME";
        default:                   return "UNKNOWN";
    }
}