#include "SoundManager.h"
#include <QUrl>
#include <QDir>
#include <QCoreApplication>
#include <QDebug>

SoundManager& SoundManager::getInstance()
{
    static SoundManager instance;
    return instance;
}

SoundManager::SoundManager()
    : m_myTurnSound(std::make_unique<QSoundEffect>())
    , m_timeOutSound(std::make_unique<QSoundEffect>())
    , m_countdownSound(std::make_unique<QSoundEffect>())
    , m_countdownTimer(new QTimer(this))
    , m_countdownRemaining(0)
    , m_countdownActive(false)
    , m_volume(1.0f)
    , m_muted(false)
{
    initializeSounds();
    
    // 카운트다운 타이머 설정
    m_countdownTimer->setSingleShot(true);
    connect(m_countdownTimer, &QTimer::timeout, this, &SoundManager::onCountdownTimer);
}

SoundManager::~SoundManager()
{
    stopCountdown();
}

void SoundManager::initializeSounds()
{
    // 리소스 디렉토리 경로 설정
    QString resourcePath = QCoreApplication::applicationDirPath() + "/resource/";
    
    // 각 사운드 파일 로드
    loadSound(m_myTurnSound.get(), resourcePath + "my_turn.wav");
    loadSound(m_timeOutSound.get(), resourcePath + "time_out.wav");
    loadSound(m_countdownSound.get(), resourcePath + "countdown.wav");
    
    qDebug() << "SoundManager initialized with resource path:" << resourcePath;
}

void SoundManager::loadSound(QSoundEffect* sound, const QString& fileName)
{
    if (!sound) return;
    
    QUrl soundUrl = QUrl::fromLocalFile(fileName);
    sound->setSource(soundUrl);
    sound->setVolume(m_volume);
    
    // 파일 존재 확인
    if (QFile::exists(fileName)) {
        qDebug() << "Sound loaded successfully:" << fileName;
    } else {
        qWarning() << "Sound file not found:" << fileName;
    }
}

void SoundManager::playMyTurnSound()
{
    if (m_muted || !m_myTurnSound) return;
    
    qDebug() << "Playing my turn sound";
    m_myTurnSound->play();
}

void SoundManager::playTimeOutSound()
{
    if (m_muted || !m_timeOutSound) return;
    
    qDebug() << "Playing timeout sound";
    m_timeOutSound->play();
}

void SoundManager::playCountdownSound()
{
    if (m_muted || !m_countdownSound) return;
    
    qDebug() << "Playing countdown sound";
    m_countdownSound->play();
}

void SoundManager::startCountdown(int remainingSeconds)
{
    // 5초 이하일 때만 카운트다운 시작
    if (remainingSeconds > 5) {
        return;
    }
    
    stopCountdown(); // 기존 카운트다운 중지
    
    m_countdownRemaining = remainingSeconds;
    m_countdownActive = true;
    
    qDebug() << "Starting countdown from" << remainingSeconds << "seconds";
    
    // 즉시 첫 번째 카운트다운 사운드 재생
    if (m_countdownRemaining > 0) {
        playCountdownSound();
        m_countdownRemaining--;
        
        // 다음 카운트다운을 위한 타이머 시작 (1초 후)
        if (m_countdownRemaining > 0) {
            m_countdownTimer->start(1000);
        } else {
            m_countdownActive = false;
        }
    }
}

void SoundManager::stopCountdown()
{
    if (m_countdownTimer->isActive()) {
        m_countdownTimer->stop();
    }
    m_countdownActive = false;
    m_countdownRemaining = 0;
    
    qDebug() << "Countdown stopped";
}

void SoundManager::onCountdownTimer()
{
    if (!m_countdownActive || m_countdownRemaining <= 0) {
        m_countdownActive = false;
        return;
    }
    
    // 카운트다운 사운드 재생
    playCountdownSound();
    m_countdownRemaining--;
    
    // 다음 카운트다운이 있으면 타이머 재시작
    if (m_countdownRemaining > 0) {
        m_countdownTimer->start(1000);
    } else {
        m_countdownActive = false;
        qDebug() << "Countdown finished";
    }
}

void SoundManager::setVolume(float volume)
{
    m_volume = qBound(0.0f, volume, 1.0f);
    
    if (m_myTurnSound) m_myTurnSound->setVolume(m_volume);
    if (m_timeOutSound) m_timeOutSound->setVolume(m_volume);
    if (m_countdownSound) m_countdownSound->setVolume(m_volume);
    
    qDebug() << "Volume set to:" << m_volume;
}

float SoundManager::getVolume() const
{
    return m_volume;
}

void SoundManager::setMuted(bool muted)
{
    m_muted = muted;
    qDebug() << "Sound" << (muted ? "muted" : "unmuted");
    
    if (muted) {
        stopCountdown(); // 음소거 시 카운트다운도 중지
    }
}

bool SoundManager::isMuted() const
{
    return m_muted;
}