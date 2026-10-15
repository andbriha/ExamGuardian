#include "audio_capture.h"
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
    Q_INVOKABLE void stopVerificationSession();
    Q_INVOKABLE void setVideoSink(QVideoSink *sink);

    Q_INVOKABLE void startContinuousProctoring(int elapsedSeconds);
    Q_INVOKABLE void stopContinuousProctoring();

    Q_INVOKABLE void prepareRecording() { m_coreApi.fetchNextChunkIndex(m_sessionId); }
    Q_INVOKABLE void uploadExamVideoChunk(const QString &filePath, int chunkIndex, const QString &stream, double startOffsetSeconds, double durationSeconds);
    Q_INVOKABLE QString beginVideoChunk(const QString &stream, int chunkIndex, double offsetSeconds);
    Q_INVOKABLE void finalizeEvidence(int cameraLast, int screenLast) { m_coreApi.finalizeEvidence(m_sessionId, cameraLast, screenLast); }

    Q_INVOKABLE void setAuthToken(const QString &token);
    Q_INVOKABLE void setSessionId(int sessionId);
    void setAttemptTokens(int sessionId, const QString &attempt, const QString &evidence) { m_mlApi.setAttemptToken(attempt); m_coreApi.setAttemptTokens(sessionId,attempt,evidence); }
    Q_INVOKABLE void setExamId(int examId) { m_examId=examId; m_mlApi.setExamId(examId); }

    void queueObservation(int sessionId, const QString &type, int seconds, const QString &metadata) { m_coreApi.reportViolation(sessionId,type,1.0,seconds,metadata); }
signals:
    void instructionReceived(QString instruction);
    void verificationPhaseChanged(QString phase);
    void checkResult(bool passed, QString message);
    void progressUpdated(const QString &message);
    void videoUploadFinished();
    void pendingUploadsChanged(int count);
    void videoUploadFailed(const QString &message);
    void recordingReady(int nextChunkIndex);
    void evidenceFinalized(const QString &status);
    void analysisStatusChanged(const QString &message);
    void verificationError(const QString &message);
    void admissionReceived(const QString &token);
    void verificationAudioStatus(const QString &message);

private:
    bool m_verifying = false;
    AudioCapture m_audio;
    MediaValidator m_media;
    MlApiClient    m_mlApi;
    CoreApiClient  m_coreApi;
    int            m_sessionId = 0;
    int m_examId=0;
};
