#pragma once
#include <QObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QString>

class CoreApiClient : public QObject {
    Q_OBJECT
public:
    explicit CoreApiClient(QObject *parent = nullptr);
    void setAuthToken(const QString &token);
    void uploadVideoChunk(const QString &sessionId, int chunkIndex, const QString &filePath);
    void reportViolation(int sessionId, const QString &violationType, double confidence, int relativeTimestampSeconds, const QString &metadata = "{}");

signals:
    void videoUploadedSuccessfully(const QString &filePath);
    void videoUploadFailed(const QString &error);

private:
    QNetworkAccessManager *m_manager;
    QString m_authToken;
    const QString m_baseUrl = "http://localhost";
};
