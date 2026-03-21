#include "proctoring_module.h"

ProctoringModule::ProctoringModule(QObject *parent) : QObject(parent) {

    connect(&m_scanner, &ProcessScanner::snapshotReady,
            this, &ProctoringModule::snapshotReady);

    connect(&m_media, &MediaValidator::instructionReceived,
            this, &ProctoringModule::livenessInstructionReceived);

    connect(&m_media, &MediaValidator::checkResult,
            this, &ProctoringModule::livenessCheckResult);
}

ProctoringModule::~ProctoringModule() {

}


void ProctoringModule::setKeyBlock(bool enable) {
    m_inputBlocker.setKeyBlock(enable);
}

void ProctoringModule::requestProcessSnapshot() {
    m_scanner.requestSnapshotAsync();
}

QStringList ProctoringModule::getSuspiciousProcesses() {
    return m_scanner.getSuspiciousProcesses();
}

bool ProctoringModule::saveSnapshotToFile(const QVariantList &snapshot) {
    return m_scanner.saveSnapshotToFile(snapshot);
}

bool ProctoringModule::isVirtualCamera(const QString &cameraName) {
    return m_media.isVirtualCamera(cameraName);
}

void ProctoringModule::startLivenessSession() {
    m_media.startLivenessSession();
}

void ProctoringModule::simulateUserActionCompleted() {
    m_media.simulateUserActionCompleted();
}
