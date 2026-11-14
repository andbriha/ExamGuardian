#pragma once
#include <QObject>
#include <QMediaPlayer>
#include <QAudioOutput>
#include <QTimer>
#include <QStringList>
#include <QUrl>

// Fixed recordings establish pronunciation independently of the OS/TTS language.
// active stays true through the final quiet interval, keeping capture suspended.
class PronunciationPlayer : public QObject {
    Q_OBJECT
public:
    explicit PronunciationPlayer(QObject *parent = nullptr) : QObject(parent) {
        m_player.setAudioOutput(&m_output);
        m_timer.setSingleShot(true);
        m_watchdog.setSingleShot(true);
        connect(&m_watchdog, &QTimer::timeout, this, [this] { emit unavailable(); stop(); });
        connect(&m_timer, &QTimer::timeout, this, &PronunciationPlayer::next);
        connect(&m_player, &QMediaPlayer::mediaStatusChanged, this, [this](auto status) {
            if (m_active && status == QMediaPlayer::EndOfMedia) m_timer.start(500);
        });
        connect(&m_player, &QMediaPlayer::errorOccurred, this, [this] {
            if (!m_active) return;
            emit unavailable();
            m_queue.clear();
            m_timer.start(500);
        });
    }
    void play(const QString &phrase) {
        if (m_active) return;
        const auto parts = phrase.split(' ', Qt::SkipEmptyParts);
        if (parts.isEmpty()) return;
        for (const auto &part : parts) if (!QStringList{"MA", "MI", "MU"}.contains(part)) return;
        m_queue = parts;
        m_active = true;
        m_watchdog.start(15000);
        emit activeChanged(true); // Suspend capture BEFORE starting the speaker.
        next();
    }
    void stop() {
        m_timer.stop(); m_watchdog.stop(); m_queue.clear(); m_player.stop();
        if (m_active) { m_active = false; emit activeChanged(false); }
    }
signals:
    void activeChanged(bool active);
    void unavailable();
private:
    void next() {
        if (!m_active) return;
        if (m_queue.isEmpty()) { stop(); return; }
        m_player.setSource(QUrl("qrc:/pronunciation/" + m_queue.takeFirst() + ".wav"));
        m_player.play();
    }
    bool m_active = false;
    QStringList m_queue;
    QMediaPlayer m_player;
    QAudioOutput m_output;
    QTimer m_timer;
    QTimer m_watchdog;
};
