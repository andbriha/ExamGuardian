#pragma once
#include <QObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QImage>
#include <QString>
#include <QQueue>
#include "verification_audio_buffer.h"

class MlApiClient : public QObject {
    Q_OBJECT
public:
    explicit MlApiClient(QObject *parent = nullptr);

    void setAuthToken(const QString &token);
    void setAttemptToken(const QString &token) { m_attemptToken = token; }
    void setSessionId(int sessionId) { stopContinuousAudio(); m_sessionId = sessionId; }
    void startContinuousAudio() { stopContinuousAudio(); m_continuousAudioActive = true; }
    void stopContinuousAudio() { ++m_audioEpoch; m_audioQueue.clear(); m_audioPending=false; m_continuousAudioActive=false; }

    void setExamId(int id) { m_examId = id; }
    void sendAudioForAnalysis(const QByteArray &pcm, int relativeSeconds);
    void fetchChallenge();
    void cancelVerification();
    void suspendVerificationAudio(bool suspended);
    void sendVerificationAudio(const QByteArray &pcm);
    void sendVerificationFrame(const QImage &currentFrame, const QString &task);
    void sendFrameForContinuousAnalysis(const QImage &image, int relativeSec = 0);

signals:
    void verificationProgress(int step, int total);
    void verificationAudioStatus(const QString &message);
    void challengeReceived(const QString &task);
    void verificationError(const QString &message);
    void admissionReceived(const QString &token);
    void verificationFinished(bool verified, const QString &stage, const QString &reason, double distance);
    void continuousAnalysisFinished(bool success, const QString &message);
    void violationDetected(const QString &violationType);

private:
    void flushContinuousAudio();
    QQueue<QPair<QByteArray, int>> m_audioQueue;
    quint64 m_audioEpoch = 0;
    bool m_continuousAudioActive = false;
    void flushVerificationAudio();
    void setVerificationTask(const QString &task);
    VerificationAudioBuffer m_verificationAudioBuffer;
    quint64 m_verificationAudioStep = 0;
#ifdef EXAMGUARDIAN_DEVELOPMENT
    void fetchDevelopmentAdmission(quint64 epoch);
#endif
    QNetworkAccessManager *m_manager;
    QString m_authToken;
    QString m_attemptToken;
    int m_sessionId = 0;
    int m_examId = 0;
    QString m_challengeId;
    quint64 m_verificationEpoch = 0;
    bool m_verificationAudioPending = false;
    bool m_verificationAudioSuspended = false;
    int m_verificationFailures = 0;
    bool m_verificationPending = false;
    bool m_analysisPending = false;
    bool m_audioPending = false;
};
