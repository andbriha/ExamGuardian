#pragma once
#include <QObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QString>
#include <QJsonArray>
#include <QJsonObject>
#include <QTimer>

class CoreApiClient : public QObject {
    Q_OBJECT
public:
    explicit CoreApiClient(QObject *parent = nullptr);
    void setAuthToken(const QString &token);
    void setAttemptTokens(int sessionId, const QString &attempt, const QString &evidence);
    void uploadVideoChunk(const QString &sessionId, int chunkIndex, const QString &filePath, const QString &stream, double startOffsetSeconds, double durationSeconds);
    QString beginRecording(int sessionId, const QString &stream, int index, double offset);
    void finalizeEvidence(int sessionId, int cameraLast, int screenLast);
    void fetchNextChunkIndex(int sessionId);
    int pendingUploads() const { return m_queue.size(); }
    void reportViolation(int sessionId, const QString &violationType, double confidence, int relativeTimestampSeconds, const QString &metadata = "{}");

signals:
    void videoUploadedSuccessfully(const QString &filePath);
    void videoUploadFailed(const QString &error);
    void pendingUploadsChanged(int count);
    void recordingReady(int nextChunkIndex);
    void evidenceFinalized(const QString &status);

private:
    QNetworkAccessManager *m_manager;
    QString m_authToken;
    QJsonArray m_queue;
    QString m_queuePath;
    QString m_attemptToken;
    QJsonObject m_evidenceTokens;
    QTimer m_retryTimer;
    bool m_uploadInFlight = false;
    int m_retrySeconds = 1;
    bool persistQueue();
    void pumpUploads();
    void markUnrecoverable(const QString &path, const QString &message);
};
