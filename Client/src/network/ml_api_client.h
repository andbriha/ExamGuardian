#pragma once
#include <QObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QImage>
#include <QString>

class MlApiClient : public QObject {
    Q_OBJECT
public:
    explicit MlApiClient(QObject *parent = nullptr);

    void setAuthToken(const QString &token);
    void setAttemptToken(const QString &token) { m_attemptToken = token; }
    void setSessionId(int sessionId) { m_sessionId = sessionId; }

    void setExamId(int id) { m_examId = id; }
    void sendAudioForAnalysis(const QByteArray &pcm, int relativeSeconds);
    void fetchChallenge();
    void cancelVerification();
    void sendVerificationAudio(const QByteArray &pcm);
    void sendVerificationFrame(const QImage &currentFrame, const QString &task);
    void sendFrameForContinuousAnalysis(const QImage &image, int relativeSec = 0);

signals:
    void verificationAudioStatus(const QString &message);
    void challengeReceived(const QString &task);
    void verificationError(const QString &message);
    void admissionReceived(const QString &token);
    void verificationFinished(bool verified, const QString &stage, const QString &reason, double distance);
    void continuousAnalysisFinished(bool success, const QString &message);
    void violationDetected(const QString &violationType);

private:
    QNetworkAccessManager *m_manager;
    QString m_authToken;
    QString m_attemptToken;
    int m_sessionId = 0;
    int m_examId = 0;
    QString m_challengeId;
    quint64 m_verificationEpoch = 0;
    bool m_verificationAudioPending = false;
    int m_verificationFailures = 0;
    bool m_verificationPending = false;
    bool m_analysisPending = false;
    bool m_audioPending = false;
};
