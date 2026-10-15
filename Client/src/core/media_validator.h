#include <QElapsedTimer>
#pragma once
#include <QObject>
#include <QVideoSink>
#include <QPointer>
#include <QImage>
#include <QString>
#include <QDateTime>

class MediaValidator : public QObject {
    Q_OBJECT
public:
    explicit MediaValidator(QObject *parent = nullptr);
    bool isVirtualCamera(const QString &cameraName);
    void setVideoSink(QVideoSink *sink);

    void startVerificationSession();
    void stopVerificationSession();

    void startContinuousProctoring(int elapsedSeconds = 0);
    void stopContinuousProctoring();

signals:
    void instructionReceived(QString instruction);
    void verificationPhaseChanged(QString phase);
    void checkResult(bool passed, QString message);
    void analysisUnavailable(const QString &message);
    void progressUpdated(const QString &message);

    void requestChallenge();
    void verifyIdentityAndLiveness(const QImage &frame, const QString &task);
    void frameCaptured(const QImage &image, int relativeSeconds);

public slots:
    void onChallengeReceived(const QString &task);
    void onVerificationFinished(bool verified, const QString &stage, const QString &reason, double distance);

private slots:
    void grabAndSendFrame();
    void captureVerificationFrame();

private:
    QString translateTask(const QString &task);

    QPointer<QVideoSink> m_videoSink;
    class QTimer *m_analysisTimer = nullptr;
    class QTimer *m_verificationTimer = nullptr;
    QString m_currentTask;
    QDateTime m_sessionStartTime;
    QElapsedTimer m_sessionClock;
    QElapsedTimer m_frameClock;
    int m_startOffset = 0;
};
