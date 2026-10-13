#pragma once
#include <QObject>
#include <QVideoSink>
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
    void simulateUserActionCompleted();

    void startContinuousProctoring();
    void stopContinuousProctoring();

signals:
    void instructionReceived(QString instruction);
    void checkResult(bool passed, QString message);
    void progressUpdated(const QString &message);

    void requestChallenge();
    void verifyIdentityAndLiveness(const QImage &frame, const QString &task);
    void frameCaptured(const QImage &image, int relativeSeconds);

public slots:
    void onChallengeReceived(const QString &task);
    void onVerificationFinished(bool verified, const QString &stage, const QString &reason, double distance);

private slots:
    void grabAndSendFrame();

private:
    QString translateTask(const QString &task);

    QVideoSink *m_videoSink = nullptr;
    class QTimer *m_analysisTimer = nullptr;
    QString m_currentTask;
    QDateTime m_sessionStartTime;
};
