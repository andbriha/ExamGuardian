#pragma once
#include <QObject>
#include <QStringList>
#include <QVariantList>
#include "process_scanner.h"
#include "environment_detector.h"
#include "core_api_client.h"

class ProcessViewModel : public QObject {
    Q_OBJECT

    Q_PROPERTY(bool isEnvironmentSafe READ isEnvironmentSafe NOTIFY scanCompleted)
    Q_PROPERTY(QStringList suspiciousApps READ suspiciousApps NOTIFY scanCompleted)
    Q_PROPERTY(QStringList spoofedProcesses READ spoofedProcesses NOTIFY scanCompleted)
    Q_PROPERTY(bool isVmDetected READ isVmDetected NOTIFY scanCompleted)
    Q_PROPERTY(QString vmDetails READ vmDetails NOTIFY scanCompleted)
    Q_PROPERTY(QStringList virtualCameras READ virtualCameras NOTIFY scanCompleted)
    Q_PROPERTY(bool multipleMonitors READ multipleMonitors NOTIFY scanCompleted)
    Q_PROPERTY(QStringList suspiciousAudio READ suspiciousAudio NOTIFY scanCompleted)

public:
    explicit ProcessViewModel(QObject *parent = nullptr);

    Q_INVOKABLE void runFullScan();
    Q_INVOKABLE void requestProcessSnapshot();
    Q_INVOKABLE QStringList getSuspiciousProcesses();
    Q_INVOKABLE bool saveSnapshotToFile(const QVariantList &snapshot);
    Q_INVOKABLE void reportSecurityViolation(int sessionId, const QString &violationType, const QString &details);

    bool isEnvironmentSafe() const { return m_isSafe; }
    QStringList suspiciousApps() const { return m_suspiciousApps; }
    QStringList spoofedProcesses() const { return m_spoofedProcesses; }
    bool isVmDetected() const { return m_isVmDetected; }
    QString vmDetails() const { return m_vmDetails; }
    QStringList virtualCameras() const { return m_virtualCameras; }
    bool multipleMonitors() const { return m_multipleMonitors; }
    QStringList suspiciousAudio() const { return m_suspiciousAudio; }

signals:
    void scanCompleted();
    void snapshotReady(QVariantList snapshot);
    void securityViolationDetected(const QString &type, const QString &message);

private:
    ProcessScanner      m_scanner;
    EnvironmentDetector m_envDetector;
    CoreApiClient       m_coreApi;

    bool        m_isSafe = true;
    QStringList m_suspiciousApps;
    QStringList m_spoofedProcesses;
    bool        m_isVmDetected = false;
    QString     m_vmDetails;
    QStringList m_virtualCameras;
    bool        m_multipleMonitors = false;
    QStringList m_suspiciousAudio;
};
