#pragma once
#include <QObject>
#include <QString>
#include <QVideoSink>
#include "media_validator.h"
#include "ml_api_client.h"
#include "core_api_client.h"

class MediaViewModel : public QObject {
    Q_OBJECT
public:
    explicit MediaViewModel(QObject *parent = nullptr);

    Q_INVOKABLE bool isVirtualCamera(const QString &cameraName);
    Q_INVOKABLE void startVerificationSession();
    Q_INVOKABLE void simulateUserActionCompleted();
    Q_INVOKABLE void setVideoSink(QVideoSink *sink);

    Q_INVOKABLE void startContinuousProctoring();
    Q_INVOKABLE void stopContinuousProctoring();

    Q_INVOKABLE void uploadExamVideoChunk(const QString &filePath, int chunkIndex);
    Q_INVOKABLE QString generateTempVideoPath();

    Q_INVOKABLE void setAuthToken(const QString &token);
    Q_INVOKABLE void setSessionId(int sessionId);

signals:
    void instructionReceived(QString instruction);
    void checkResult(bool passed, QString message);
    void progressUpdated(const QString &message);
    void videoUploadFinished();

private:
    MediaValidator m_media;
    MlApiClient    m_mlApi;
    CoreApiClient  m_coreApi;
    int            m_sessionId = 1;
};
