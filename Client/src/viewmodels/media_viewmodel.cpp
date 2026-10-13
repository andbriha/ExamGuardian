#include "media_viewmodel.h"
#include <QStandardPaths>
#include <QDir>
#include <QUuid>

MediaViewModel::MediaViewModel(QObject *parent) : QObject(parent) {
    // 1. Validator -> QML UI
    connect(&m_media, &MediaValidator::instructionReceived, this, &MediaViewModel::instructionReceived);
    connect(&m_media, &MediaValidator::checkResult, this, &MediaViewModel::checkResult);
    connect(&m_media, &MediaValidator::progressUpdated, this, &MediaViewModel::progressUpdated);

    // 2. Validator -> ML API
    connect(&m_media, &MediaValidator::requestChallenge, &m_mlApi, &MlApiClient::fetchChallenge);
    connect(&m_media, &MediaValidator::verifyIdentityAndLiveness, &m_mlApi, &MlApiClient::sendVerificationFrame);
    connect(&m_media, &MediaValidator::frameCaptured, &m_mlApi, &MlApiClient::sendFrameForContinuousAnalysis);

    // 3. ML API -> Validator
    connect(&m_mlApi, &MlApiClient::challengeReceived, &m_media, &MediaValidator::onChallengeReceived);
    connect(&m_mlApi, &MlApiClient::verificationFinished, &m_media, &MediaValidator::onVerificationFinished);

    // 4. Core API -> Video uploads
    connect(&m_coreApi, &CoreApiClient::videoUploadedSuccessfully, this, &MediaViewModel::videoUploadFinished);
}

void MediaViewModel::startVerificationSession() {
    // Токен вже має бути встановлений в m_mlApi перед цим викликом (при логіні)
    m_media.startVerificationSession();
}

void MediaViewModel::uploadExamVideoChunk(const QString &filePath, int chunkIndex) {
    QString sessionIdStr = QString::number(m_sessionId);
    m_coreApi.uploadVideoChunk(sessionIdStr, chunkIndex, filePath);
}

QString MediaViewModel::generateTempVideoPath() {
    QString tempDir = QStandardPaths::writableLocation(QStandardPaths::TempLocation);
    QString fileName = "exam_" + QUuid::createUuid().toString(QUuid::WithoutBraces) + ".mp4";
    return "file:///" + QDir(tempDir).filePath(fileName);
}

bool MediaViewModel::isVirtualCamera(const QString &cameraName) {
    return m_media.isVirtualCamera(cameraName);
}

void MediaViewModel::simulateUserActionCompleted() {
    m_media.simulateUserActionCompleted();
}

void MediaViewModel::setVideoSink(QVideoSink *sink) {
    m_media.setVideoSink(sink);
}

void MediaViewModel::startContinuousProctoring() {
    m_media.startContinuousProctoring();
}

void MediaViewModel::stopContinuousProctoring() {
    m_media.stopContinuousProctoring();
}

void MediaViewModel::setAuthToken(const QString &token) {
    m_mlApi.setAuthToken(token);
    m_coreApi.setAuthToken(token);
}

void MediaViewModel::setSessionId(int sessionId) {
    m_sessionId = sessionId;
    m_mlApi.setSessionId(sessionId);
}
