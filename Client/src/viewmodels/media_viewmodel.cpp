#include <QUrl>
#include "media_viewmodel.h"
#include <QStandardPaths>
#include <QDir>
#include <QUuid>

MediaViewModel::MediaViewModel(QObject *parent) : QObject(parent) {
    connect(&m_audio,&AudioCapture::captured,this,[this](const QByteArray &data,int seconds){
        if (m_verifying) m_mlApi.sendVerificationAudio(data);
        else m_mlApi.sendAudioForAnalysis(data,seconds);
    });
    connect(&m_audio, &AudioCapture::unavailable, this, [this](const QString &message) {
        if (m_verifying) emit verificationAudioStatus(message);
        else emit analysisStatusChanged(message);
    });
    connect(&m_mlApi, &MlApiClient::verificationAudioStatus, this, &MediaViewModel::verificationAudioStatus);
    connect(&m_mlApi, &MlApiClient::continuousAnalysisFinished, this, [this](bool, const QString &message) { emit analysisStatusChanged(message); });
    connect(&m_media, &MediaValidator::analysisUnavailable, this, &MediaViewModel::analysisStatusChanged);
    // 1. Validator -> QML UI
    connect(&m_media, &MediaValidator::instructionReceived, this, &MediaViewModel::instructionReceived);
    connect(&m_media, &MediaValidator::verificationPhaseChanged, this, &MediaViewModel::verificationPhaseChanged);
    connect(&m_media,&MediaValidator::checkResult,this,[this](bool passed,const QString &message){
        if (passed) stopVerificationSession();
        emit checkResult(passed,message);
    });
    connect(&m_media, &MediaValidator::progressUpdated, this, &MediaViewModel::progressUpdated);

    // 2. Validator -> ML API
    connect(&m_media, &MediaValidator::requestChallenge, &m_mlApi, &MlApiClient::fetchChallenge);
    connect(&m_media, &MediaValidator::verifyIdentityAndLiveness, &m_mlApi, &MlApiClient::sendVerificationFrame);
    connect(&m_media,&MediaValidator::frameCaptured,this,[this](const QImage &data,int seconds){
        m_mlApi.sendFrameForContinuousAnalysis(data,seconds);
    });

    // 3. ML API -> Validator
    connect(&m_mlApi, &MlApiClient::challengeReceived, &m_media, &MediaValidator::onChallengeReceived);
    connect(&m_mlApi, &MlApiClient::challengeReceived, this, [this](const QString &) {
        if (m_verifying && !m_audio.isActive()) m_audio.start(0, 1);
    });
    connect(&m_mlApi, &MlApiClient::verificationFinished, &m_media, &MediaValidator::onVerificationFinished);

    connect(&m_mlApi, &MlApiClient::verificationError, this, [this](const QString &message) {
        stopVerificationSession();
        emit verificationError(message);
    });
    connect(&m_mlApi,&MlApiClient::admissionReceived,this,[this](const QString &token){
        emit admissionReceived(token);
    });
    connect(&m_coreApi, &CoreApiClient::pendingUploadsChanged, this, &MediaViewModel::pendingUploadsChanged);
    connect(&m_coreApi, &CoreApiClient::videoUploadFailed, this, &MediaViewModel::videoUploadFailed);
    connect(&m_coreApi, &CoreApiClient::evidenceFinalized, this, &MediaViewModel::evidenceFinalized);
    connect(&m_coreApi, &CoreApiClient::recordingReady, this, &MediaViewModel::recordingReady);
    // 4. Core API -> Video uploads
    connect(&m_coreApi, &CoreApiClient::videoUploadedSuccessfully, this, &MediaViewModel::videoUploadFinished);
}

void MediaViewModel::startVerificationSession() {
    // Токен вже має бути встановлений в m_mlApi перед цим викликом (при логіні)
    stopVerificationSession();
    m_verifying = true;
    emit verificationAudioStatus("Мікрофон: очікуємо завдання сервера");
    m_media.startVerificationSession();
}

void MediaViewModel::uploadExamVideoChunk(const QString &filePath, int chunkIndex, const QString &stream, double startOffsetSeconds, double durationSeconds) {
    m_coreApi.uploadVideoChunk(QString::number(m_sessionId), chunkIndex, QUrl(filePath).isLocalFile() ? QUrl(filePath).toLocalFile() : filePath, stream, startOffsetSeconds, durationSeconds);
}
QString MediaViewModel::beginVideoChunk(const QString &stream, int chunkIndex, double offsetSeconds) {
    return m_coreApi.beginRecording(m_sessionId,stream,chunkIndex,offsetSeconds);
}

bool MediaViewModel::isVirtualCamera(const QString &cameraName) {
    return m_media.isVirtualCamera(cameraName);
}

void MediaViewModel::stopVerificationSession() {
    if (m_verifying) m_audio.stop();
    m_verifying = false;
    m_media.stopVerificationSession();
    m_mlApi.cancelVerification();
}

void MediaViewModel::setVideoSink(QVideoSink *sink) {
    m_media.setVideoSink(sink);
}

void MediaViewModel::startContinuousProctoring(int elapsedSeconds) {
    stopVerificationSession();
    m_media.startContinuousProctoring(elapsedSeconds);
    m_audio.start(elapsedSeconds);
}

void MediaViewModel::stopContinuousProctoring() {
    m_media.stopContinuousProctoring();
    m_audio.stop();
}

void MediaViewModel::setAuthToken(const QString &token) {
    m_mlApi.setAuthToken(token);
    m_coreApi.setAuthToken(token);
}

void MediaViewModel::setSessionId(int sessionId) {
    m_sessionId = sessionId;
    m_mlApi.setSessionId(sessionId);
}
