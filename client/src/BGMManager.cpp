#include "BGMManager.h"
#include <QDir>
#include <QFile>

BGMManager::BGMManager()
    : m_currentMusic(nullptr)
    , m_initialized(false)
    , m_currentState(GameState::NONE)
    , m_bgmVolume(0.7f)  // BGM 적정 볼륨 70%
    , m_sfxVolume(0.8f)  // SFX 적정 볼륨 80%
    , m_bgmMuted(false)
    , m_sfxMuted(false)
{
    qDebug() << "🎮 BGMManager initializing with SDL_mixer (BGM + SFX)...";
    
    // SFX 배열 초기화
    for (int i = 0; i < 3; ++i) {
        m_soundEffects[i] = nullptr;
    }
    
    // 상태별 BGM 파일 경로 설정
    m_musicPaths[GameState::LOBBY] = "resource/lobby/bgm_lobby.mp3";
    m_musicPaths[GameState::GAME_ROOM] = "resource/gameroom/bgm_gameroom.mp3";
    // GameState::IN_GAME는 향후 추가 예정
    
    // 효과음 파일 경로 설정
    m_soundEffectPaths[SoundEffect::MY_TURN] = "resource/my_turn.wav";
    m_soundEffectPaths[SoundEffect::TIME_OUT] = "resource/time_out.wav";
    m_soundEffectPaths[SoundEffect::COUNTDOWN] = "resource/countdown.wav";
    
    // SDL_mixer 초기화
    m_initialized = initializeSDL();
    
    if (m_initialized) {
        qDebug() << " BGMManager initialized successfully";
    } else {
        qWarning() << " BGMManager initialization failed - BGM disabled";
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
    int wanted_formats = MIX_INIT_OGG | MIX_INIT_MP3 | MIX_INIT_FLAC;
    int formats = Mix_Init(wanted_formats);
    qDebug() << "🎵 Supported audio formats:";
    qDebug() << "   - WAV: YES (always supported)";
    qDebug() << "   - OGG:" << (formats & MIX_INIT_OGG ? "YES" : "NO");
    qDebug() << "   - MP3:" << (formats & MIX_INIT_MP3 ? "YES" : "NO");
    qDebug() << "   - FLAC:" << (formats & MIX_INIT_FLAC ? "YES" : "NO");
    
    // 효과음 파일 로딩
    loadSoundEffects();
    
    return true;
}

void BGMManager::cleanupSDL()
{
    // 현재 재생 중인 음악 정지 및 해제
    stopCurrentBGM();
    
    // 효과음 정리
    for (int i = 0; i < 3; ++i) {
        if (m_soundEffects[i]) {
            Mix_FreeChunk(m_soundEffects[i]);
            m_soundEffects[i] = nullptr;
        }
    }
    
    // SDL_mixer 종료
    Mix_CloseAudio();
    SDL_Quit();
    
    qDebug() << "🔊 SDL_mixer audio system cleaned up (BGM + SFX)";
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
    if (newState != GameState::NONE && !m_bgmMuted) {
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
        qWarning() << " BGM file load failed:" << musicPath.c_str();
        qWarning() << "   SDL_mixer error:" << Mix_GetError();
        qWarning() << "   File exists:" << QFile::exists(QString::fromStdString(musicPath));
        qWarning() << "   Continuing without background music...";
        return;
    }
    
    // 볼륨 설정 (0~128 범위로 변환)
    int sdlVolume = static_cast<int>(m_bgmVolume * MIX_MAX_VOLUME);
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

// ========== BGM 볼륨 제어 ==========
void BGMManager::setBGMVolume(float volume)
{
    m_bgmVolume = qBound(0.0f, volume, 1.0f);
    applyBGMVolume();
    qDebug() << "🎵 BGM volume set to:" << (m_bgmVolume * 100.0f) << "%";
}

// 하위 호환성용 기존 API
void BGMManager::setVolume(float volume)
{
    setBGMVolume(volume);
}

void BGMManager::setBGMMuted(bool muted)
{
    if (m_bgmMuted == muted) return;
    
    m_bgmMuted = muted;
    if (!m_initialized) return;
    
    if (muted) {
        if (Mix_PlayingMusic()) {
            Mix_PauseMusic();
            qDebug() << "🔇 BGM muted";
        }
    } else {
        if (Mix_PausedMusic()) {
            Mix_ResumeMusic();
            qDebug() << "🔊 BGM unmuted (resumed)";
        } else if (m_currentState != GameState::NONE) {
            loadAndPlayBGM(m_currentState);
            qDebug() << "🔊 BGM unmuted (restarted)";
        }
    }
}

// 하위 호환성용 기존 API
void BGMManager::setMuted(bool muted)
{
    setBGMMuted(muted);
}

// ========== SFX 볼륨 제어 ==========
void BGMManager::setSFXVolume(float volume)
{
    m_sfxVolume = qBound(0.0f, volume, 1.0f);
    applySFXVolume();
    qDebug() << "🎵 SFX volume set to:" << (m_sfxVolume * 100.0f) << "%";
}

void BGMManager::setSFXMuted(bool muted)
{
    m_sfxMuted = muted;
    qDebug() << "🔇 SFX" << (muted ? "muted" : "unmuted");
}

// ========== 효과음 재생 ==========
void BGMManager::playSoundEffect(SoundEffect effect)
{
    if (!m_initialized || m_sfxMuted) return;
    
    int effectIndex = static_cast<int>(effect);
    if (effectIndex < 0 || effectIndex >= 3 || !m_soundEffects[effectIndex]) {
        qWarning() << " Invalid sound effect:" << soundEffectToString(effect);
        return;
    }
    
    // 현재 볼륨 적용해서 재생 (-1은 첫 번째 빈 채널을 찾아서 재생)
    int channel = Mix_PlayChannel(-1, m_soundEffects[effectIndex], 0);
    if (channel == -1) {
        qWarning() << " Failed to play sound effect:" << Mix_GetError();
    } else {
        qDebug() << "🎵 Playing sound effect:" << soundEffectToString(effect);
    }
}

// ========== 효과음 로딩 ==========
void BGMManager::loadSoundEffects()
{
    qDebug() << "🎵 Loading sound effects...";
    
    loadSoundEffect(&m_soundEffects[0], "my_turn.wav", SoundEffect::MY_TURN);
    loadSoundEffect(&m_soundEffects[1], "time_out.wav", SoundEffect::TIME_OUT);
    loadSoundEffect(&m_soundEffects[2], "countdown.wav", SoundEffect::COUNTDOWN);
    
    // 초기 볼륨 적용
    applySFXVolume();
}

void BGMManager::loadSoundEffect(Mix_Chunk** chunk, const char* filename, SoundEffect effect)
{
    std::string fullPath = getApplicationPath() + "/" + getSoundEffectPath(effect);
    
    *chunk = Mix_LoadWAV(fullPath.c_str());
    if (*chunk) {
        qDebug() << " Loaded sound effect:" << soundEffectToString(effect) << "from" << QString::fromStdString(fullPath);
    } else {
        qWarning() << " Failed to load sound effect:" << soundEffectToString(effect) << "-" << Mix_GetError();
    }
}

// ========== 파일 경로 해결 ==========
std::string BGMManager::getSoundEffectPath(SoundEffect effect) const
{
    auto it = m_soundEffectPaths.find(effect);
    return (it != m_soundEffectPaths.end()) ? it->second : "";
}

// ========== 볼륨 적용 ==========
void BGMManager::applyBGMVolume()
{
    if (m_initialized && Mix_PlayingMusic()) {
        int sdlVolume = static_cast<int>(m_bgmVolume * MIX_MAX_VOLUME);
        Mix_VolumeMusic(sdlVolume);
    }
}

void BGMManager::applySFXVolume()
{
    if (!m_initialized) return;
    
    int sdlVolume = static_cast<int>(m_sfxVolume * MIX_MAX_VOLUME);
    for (int i = 0; i < 3; ++i) {
        if (m_soundEffects[i]) {
            Mix_VolumeChunk(m_soundEffects[i], sdlVolume);
        }
    }
}

// ========== 디버깅 유틸리티 ==========
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

const char* BGMManager::soundEffectToString(SoundEffect effect) const
{
    switch (effect) {
        case SoundEffect::MY_TURN:   return "MY_TURN";
        case SoundEffect::TIME_OUT:  return "TIME_OUT";  
        case SoundEffect::COUNTDOWN: return "COUNTDOWN";
        default:                     return "UNKNOWN";
    }
}