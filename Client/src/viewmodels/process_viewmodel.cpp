#include "process_viewmodel.h"
#include <QDebug>

ProcessViewModel::ProcessViewModel(QObject *parent) : QObject(parent) {
    connect(&m_scanner, &ProcessScanner::snapshotReady,
            this, &ProcessViewModel::snapshotReady);

    runFullScan();
}

void ProcessViewModel::runFullScan() {
    m_suspiciousApps   = m_scanner.getSuspiciousProcesses();
    m_spoofedProcesses = m_scanner.getSpoofedProcesses();
    m_isVmDetected     = m_envDetector.isVirtualMachine();
    m_vmDetails        = m_envDetector.getVmDetails();
    m_virtualCameras   = m_envDetector.getVirtualCameras();
    m_multipleMonitors = m_envDetector.hasMultipleMonitors();
    m_suspiciousAudio  = m_envDetector.getSuspiciousAudioDevices();

    // Безпечно тільки якщо немає заборонених додатків, підроблених процесів, VM або віртуальних камер
    m_isSafe = m_suspiciousApps.isEmpty() &&
               m_spoofedProcesses.isEmpty() &&
               !m_isVmDetected &&
               m_virtualCameras.isEmpty() &&
               !m_multipleMonitors;

    qDebug() << "[ProcessViewModel] Full environment scan completed."
             << "Safe:" << m_isSafe
             << "Suspicious apps:" << m_suspiciousApps.size()
             << "Spoofed processes:" << m_spoofedProcesses.size()
             << "VM detected:" << m_isVmDetected
             << "Virtual cameras:" << m_virtualCameras.size()
             << "Multiple monitors:" << m_multipleMonitors;

    emit scanCompleted();
}

void ProcessViewModel::requestProcessSnapshot() {
    m_scanner.requestSnapshotAsync();
}

QStringList ProcessViewModel::getSuspiciousProcesses() {
    return m_scanner.getSuspiciousProcesses();
}

bool ProcessViewModel::saveSnapshotToFile(const QVariantList &snapshot) {
    return m_scanner.saveSnapshotToFile(snapshot);
}

void ProcessViewModel::reportSecurityViolation(int sessionId, const QString &violationType, const QString &details) {
    if (sessionId <= 0) return;
    QString metadata = QString("{\"details\":\"%1\"}").arg(details);
    m_coreApi.reportViolation(sessionId, violationType, 1.0, 0, metadata);
    emit securityViolationDetected(violationType, details);
}