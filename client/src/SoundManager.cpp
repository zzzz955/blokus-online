#include "SoundManager.h"
#include <QUrl>
#include <QDir>
#include <QCoreApplication>
#include <QDebug>
#include <QAudioDeviceInfo>
#include <QLibraryInfo>
#include <QPluginLoader>
#include <QFile>

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
    // Qt Multimedia 환경 진단
    diagnoseMultimedia();
    
    initializeSounds();
    
    // 간단한 사운드 테스트
    testSimpleSound();
    
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
    qDebug() << "Initializing sounds...";
    
    // 먼저 Qt 리소스 시스템 시도
    QStringList resourceFiles;
    resourceFiles << ":/resource/my_turn.wav";
    resourceFiles << ":/resource/time_out.wav";
    resourceFiles << ":/resource/countdown.wav";
    
    bool useQtResources = true;
    for (const QString& resFile : resourceFiles) {
        if (!QFile::exists(resFile)) {
            qDebug() << "Qt resource not found:" << resFile;
            useQtResources = false;
            break;
        }
    }
    
    if (useQtResources) {
        qDebug() << "Using Qt resource system for sounds";
        loadSound(m_myTurnSound.get(), ":/resource/my_turn.wav");
        loadSound(m_timeOutSound.get(), ":/resource/time_out.wav");
        loadSound(m_countdownSound.get(), ":/resource/countdown.wav");
        return;
    }
    
    // Qt 리소스가 없으면 파일 시스템 경로 시도
    qWarning() << "Qt resources not available, trying filesystem paths...";
    
    // 여러 가능한 리소스 경로 시도 (빌드 출력 디렉토리 우선)
    QStringList possiblePaths;
    possiblePaths << QCoreApplication::applicationDirPath() + "/resource/";  // 빌드 출력 디렉토리 (우선순위 1)
    possiblePaths << "./resource/";                                           // 상대 경로 (우선순위 2)
    possiblePaths << QDir::currentPath() + "/resource/";                     // 현재 작업 디렉토리
    possiblePaths << QCoreApplication::applicationDirPath() + "/../client/resource/"; // 소스 디렉토리
    
    QString resourcePath;
    
    // 첫 번째로 존재하는 경로 찾기
    for (const QString& path : possiblePaths) {
        if (QDir(path).exists()) {
            resourcePath = path;
            qDebug() << "Found resource directory:" << resourcePath;
            break;
        } else {
            qDebug() << "Resource path not found:" << path;
        }
    }
    
    if (resourcePath.isEmpty()) {
        qWarning() << "No valid resource directory found!";
        qDebug() << "Application dir:" << QCoreApplication::applicationDirPath();
        qDebug() << "Current dir:" << QDir::currentPath();
        return;
    }
    
    // 각 사운드 파일 로드
    loadSound(m_myTurnSound.get(), resourcePath + "my_turn.wav");
    loadSound(m_timeOutSound.get(), resourcePath + "time_out.wav");
    loadSound(m_countdownSound.get(), resourcePath + "countdown.wav");
    
    qDebug() << "SoundManager initialized with resource path:" << resourcePath;
}

void SoundManager::loadSound(QSoundEffect* sound, const QString& fileName)
{
    if (!sound) return;
    
    // 파일 존재 확인
    if (!QFile::exists(fileName)) {
        qWarning() << "Sound file not found:" << fileName;
        qDebug() << "Current working directory:" << QDir::currentPath();
        qDebug() << "Application directory:" << QCoreApplication::applicationDirPath();
        return;
    }
    
    QUrl soundUrl = QUrl::fromLocalFile(fileName);
    sound->setSource(soundUrl);
    sound->setVolume(m_volume);
    
    qDebug() << "Loading sound file:" << fileName;
    qDebug() << "Sound URL:" << soundUrl.toString();
    
    // QSoundEffect 상태 확인을 위한 연결
    connect(sound, &QSoundEffect::statusChanged, [=]() {
        qDebug() << "Sound status changed for" << fileName << "Status:" << sound->status();
        if (sound->status() == QSoundEffect::Error) {
            qWarning() << "Sound loading error for" << fileName;
        } else if (sound->status() == QSoundEffect::Ready) {
            qDebug() << "Sound ready for playback:" << fileName;
        }
    });
    
    // 로드된 상태 확인
    qDebug() << "Sound loaded - isLoaded:" << sound->isLoaded() << "Status:" << sound->status();
}

void SoundManager::playMyTurnSound()
{
    if (m_muted || !m_myTurnSound) return;
    
    qDebug() << "Playing my turn sound - isLoaded:" << m_myTurnSound->isLoaded() 
             << "Status:" << m_myTurnSound->status()
             << "Volume:" << m_myTurnSound->volume();
    
    if (m_myTurnSound->status() == QSoundEffect::Ready) {
        m_myTurnSound->play();
    } else {
        qWarning() << "Cannot play my turn sound - not ready. Status:" << m_myTurnSound->status();
    }
}

void SoundManager::playTimeOutSound()
{
    if (m_muted || !m_timeOutSound) return;
    
    qDebug() << "Playing timeout sound - isLoaded:" << m_timeOutSound->isLoaded() 
             << "Status:" << m_timeOutSound->status()
             << "Volume:" << m_timeOutSound->volume();
    
    if (m_timeOutSound->status() == QSoundEffect::Ready) {
        m_timeOutSound->play();
    } else {
        qWarning() << "Cannot play timeout sound - not ready. Status:" << m_timeOutSound->status();
    }
}

void SoundManager::playCountdownSound()
{
    if (m_muted || !m_countdownSound) return;
    
    qDebug() << "Playing countdown sound - isLoaded:" << m_countdownSound->isLoaded() 
             << "Status:" << m_countdownSound->status()
             << "Volume:" << m_countdownSound->volume();
    
    if (m_countdownSound->status() == QSoundEffect::Ready) {
        m_countdownSound->play();
    } else {
        qWarning() << "Cannot play countdown sound - not ready. Status:" << m_countdownSound->status();
    }
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

void SoundManager::diagnoseMultimedia()
{
    qDebug() << "=== Qt Multimedia Diagnosis ===";
    
    // Qt 정보
    qDebug() << "Qt version:" << QT_VERSION_STR;
    qDebug() << "Application directory:" << QCoreApplication::applicationDirPath();
    qDebug() << "Current working directory:" << QDir::currentPath();
    
    // 오디오 장치 정보
    QAudioDeviceInfo defaultOutput = QAudioDeviceInfo::defaultOutputDevice();
    qDebug() << "Default audio output device:" << defaultOutput.deviceName();
    qDebug() << "Default device is null:" << defaultOutput.isNull();
    
    QList<QAudioDeviceInfo> outputs = QAudioDeviceInfo::availableDevices(QAudio::AudioOutput);
    qDebug() << "Available audio output devices:" << outputs.size();
    for (const QAudioDeviceInfo& device : outputs) {
        qDebug() << "  -" << device.deviceName();
    }
    
    // 플러그인 경로 확인
    qDebug() << "Qt plugin paths:";
    QStringList pluginPaths = QCoreApplication::libraryPaths();
    for (const QString& path : pluginPaths) {
        qDebug() << "  -" << path;
        
        // 멀티미디어 플러그인 확인
        QString mediaServicePath = path + "/mediaservice";
        QDir mediaServiceDir(mediaServicePath);
        if (mediaServiceDir.exists()) {
            qDebug() << "    Mediaservice plugins:" << mediaServiceDir.entryList(QStringList() << "*.dll" << "*.so", QDir::Files);
        }
        
        QString audioPath = path + "/audio";
        QDir audioDir(audioPath);
        if (audioDir.exists()) {
            qDebug() << "    Audio plugins:" << audioDir.entryList(QStringList() << "*.dll" << "*.so", QDir::Files);
        }
    }
    
    qDebug() << "=== End Diagnosis ===";
}

void SoundManager::testSimpleSound()
{
    qDebug() << "=== Testing Simple Sound ===";
    
    // 간단한 테스트 사운드 생성
    QSoundEffect* testSound = new QSoundEffect(this);
    
    // 테스트용 간단한 경로들 시도 (빌드 출력 디렉토리 우선)
    QStringList testPaths;
    testPaths << QCoreApplication::applicationDirPath() + "/resource/my_turn.wav";  // 빌드 출력 디렉토리
    testPaths << "./resource/my_turn.wav";                                          // 상대 경로  
    testPaths << QDir::currentPath() + "/resource/my_turn.wav";                    // 현재 작업 디렉토리
    
    for (const QString& testPath : testPaths) {
        qDebug() << "Testing path:" << testPath;
        if (QFile::exists(testPath)) {
            qDebug() << "File exists, attempting to load...";
            
            QUrl testUrl = QUrl::fromLocalFile(testPath);
            testSound->setSource(testUrl);
            testSound->setVolume(1.0);
            
            qDebug() << "Test sound URL:" << testUrl.toString();
            qDebug() << "Test sound status:" << testSound->status();
            qDebug() << "Test sound isLoaded:" << testSound->isLoaded();
            
            // 상태 변경 모니터링
            connect(testSound, &QSoundEffect::statusChanged, [=]() {
                qDebug() << "Test sound status changed to:" << testSound->status();
                if (testSound->status() == QSoundEffect::Ready) {
                    qDebug() << "Test sound is ready, attempting to play...";
                    testSound->play();
                } else if (testSound->status() == QSoundEffect::Error) {
                    qWarning() << "Test sound failed to load";
                }
            });
            
            break;
        } else {
            qDebug() << "File does not exist at:" << testPath;
        }
    }
    
    qDebug() << "=== End Test ===";
}