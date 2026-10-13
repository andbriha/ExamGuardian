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
    void setSessionId(int sessionId) { m_sessionId = sessionId; }

    void fetchChallenge();
    void sendVerificationFrame(const QImage &currentFrame, const QString &task);
    void sendFrameForContinuousAnalysis(const QImage &image, int relativeSec = 0);

signals:
    void challengeReceived(const QString &task);
    void verificationFinished(bool verified, const QString &stage, const QString &reason, double distance);
    void continuousAnalysisFinished(bool success, const QString &message);
    void violationDetected(const QString &violationType);

private:
    QNetworkAccessManager *m_manager;
    QString m_authToken;
    int m_sessionId = 0;
    const QString m_baseUrl = "http://127.0.0.1:8000";
};
