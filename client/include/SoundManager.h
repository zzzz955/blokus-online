#ifndef SOUNDMANAGER_H
#define SOUNDMANAGER_H

#include <QObject>
#include <QSoundEffect>
#include <QTimer>
#include <memory>

class SoundManager : public QObject
{
    Q_OBJECT

public:
    static SoundManager& getInstance();
    
    // 효과음 재생 메서드
    void playMyTurnSound();           // 나의 턴 시작
    void playTimeOutSound();          // 시간 초과
    void playCountdownSound();        // 카운트다운 (5초부터)
    
    // 카운트다운 시작/중지
    void startCountdown(int remainingSeconds);
    void stopCountdown();
    
    // 볼륨 제어
    void setVolume(float volume);     // 0.0 ~ 1.0
    float getVolume() const;
    
    // 음소거
    void setMuted(bool muted);
    bool isMuted() const;

private slots:
    void onCountdownTimer();

private:
    SoundManager();
    ~SoundManager();
    
    // 복사/할당 방지
    SoundManager(const SoundManager&) = delete;
    SoundManager& operator=(const SoundManager&) = delete;
    
    void initializeSounds();
    void loadSound(QSoundEffect* sound, const QString& fileName);
    
private:
    std::unique_ptr<QSoundEffect> m_myTurnSound;
    std::unique_ptr<QSoundEffect> m_timeOutSound;
    std::unique_ptr<QSoundEffect> m_countdownSound;
    
    QTimer* m_countdownTimer;
    int m_countdownRemaining;
    bool m_countdownActive;
    
    float m_volume;
    bool m_muted;
};

#endif // SOUNDMANAGER_H