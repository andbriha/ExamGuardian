#pragma once
#include <QObject>
#include <QStringList>
#include <QString>

class EnvironmentDetector : public QObject {
    Q_OBJECT
public:
    explicit EnvironmentDetector(QObject* parent = nullptr);

    bool isVirtualMachine() const;
    int calculateVmScore() const;
    QString getVmDetails() const;
    QStringList getVirtualCameras() const;
    bool hasMultipleMonitors() const;
    QStringList getSuspiciousAudioDevices() const;

private:
    bool checkVirtualMac() const;
    bool checkRegistry() const;
    bool checkCPUID(QString &hypervisorVendor) const;
    bool checkVmDriverFiles() const;
    bool checkDiskSignatures() const;

    mutable QString m_detectedVmInfo;
};
