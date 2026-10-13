#include "environment_detector.h"
#include <QNetworkInterface>
#include <QSettings>
#include <QMediaDevices>
#include <QCameraDevice>
#include <QAudioDevice>
#include <QScreen>
#include <QGuiApplication>
#include <QFile>
#include <QDebug>

#ifdef _WIN32
#include <windows.h>
#include <intrin.h>
#endif

EnvironmentDetector::EnvironmentDetector(QObject* parent) : QObject(parent) {}

bool EnvironmentDetector::checkVirtualMac() const {
    // Відомі OUI префікси віртуальних мережевих карт
    QStringList vmPrefixes = {
        "08:00:27", // VirtualBox
        "00:05:69", // VMware
        "00:0C:29", // VMware
        "00:1C:14", // VMware
        "00:50:56", // VMware
        "00:15:5D", // Microsoft Hyper-V
        "52:54:00", // QEMU / KVM
        "00:1C:42"  // Parallels
    };

    for (const auto& iface : QNetworkInterface::allInterfaces()) {
        QString mac = iface.hardwareAddress().toUpper();
        for (const auto& prefix : vmPrefixes) {
            if (mac.startsWith(prefix)) {
                m_detectedVmInfo += QString("[MAC Prefix: %1] ").arg(prefix);
                return true;
            }
        }
    }
    return false;
}

bool EnvironmentDetector::checkRegistry() const {
#ifdef _WIN32
    // 1. Перевірка BIOS та системних виробників
    QSettings bios("HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System\\BIOS", QSettings::NativeFormat);
    QString vendor = bios.value("SystemManufacturer").toString().toLower();
    QString product = bios.value("SystemProductName").toString().toLower();
    QString biosVer = bios.value("BIOSVersion").toString().toLower();
    QString baseBoard = bios.value("BaseBoardProduct").toString().toLower();

    QString combined = vendor + " " + product + " " + biosVer + " " + baseBoard;

    QStringList badKeywords = {
        "vmware", "virtualbox", "innotek", "qemu", "kvm", "bochs",
        "parallels", "virtual machine", "hyper-v", "xen"
    };

    for (const QString &kw : badKeywords) {
        if (combined.contains(kw)) {
            m_detectedVmInfo += QString("[BIOS String: %1] ").arg(kw);
            return true;
        }
    }

    // 2. Перевірка SCSI / Disk контролера
    QSettings scsi("HKEY_LOCAL_MACHINE\\HARDWARE\\DEVICEMAP\\Scsi\\Scsi Port 0\\Scsi Bus 0\\Target Id 0\\Logical Unit Id 0", QSettings::NativeFormat);
    QString scsiId = scsi.value("Identifier").toString().toLower();
    if (scsiId.contains("vbox") || scsiId.contains("vmware") || scsiId.contains("qemu") || scsiId.contains("virtio")) {
        m_detectedVmInfo += QString("[SCSI Disk: %1] ").arg(scsiId);
        return true;
    }
#endif
    return false;
}

bool EnvironmentDetector::checkCPUID(QString &hypervisorVendor) const {
#ifdef _WIN32
    int cpuInfo[4] = {0};
    __cpuid(cpuInfo, 1);
    // 31-й біт ECX вказує на наявність гіпервізора (Hypervisor Present Bit)
    bool hypervisorPresent = (cpuInfo[2] & (1 << 31)) != 0;

    if (hypervisorPresent) {
        // Отримуємо назву вендора гіпервізора з leaf 0x40000000
        int hvInfo[4] = {0};
        __cpuid(hvInfo, 0x40000000);

        char vendorStr[13] = {0};
        memcpy(vendorStr + 0, &hvInfo[1], 4);
        memcpy(vendorStr + 4, &hvInfo[2], 4);
        memcpy(vendorStr + 8, &hvInfo[3], 4);

        hypervisorVendor = QString::fromLatin1(vendorStr).trimmed();
        m_detectedVmInfo += QString("[CPUID Hypervisor: %1] ").arg(hypervisorVendor);
        return true;
    }
#endif
    return false;
}

bool EnvironmentDetector::checkVmDriverFiles() const {
#ifdef _WIN32
    QStringList driverPaths = {
        "C:\\Windows\\System32\\drivers\\VBoxMouse.sys",
        "C:\\Windows\\System32\\drivers\\VBoxGuest.sys",
        "C:\\Windows\\System32\\drivers\\VBoxSF.sys",
        "C:\\Windows\\System32\\drivers\\vmmouse.sys",
        "C:\\Windows\\System32\\drivers\\vmhgfs.sys",
        "C:\\Windows\\System32\\drivers\\vmbus.sys"
    };

    for (const QString &path : driverPaths) {
        if (QFile::exists(path)) {
            m_detectedVmInfo += QString("[Driver File: %1] ").arg(path);
            return true;
        }
    }
#endif
    return false;
}

bool EnvironmentDetector::checkDiskSignatures() const {
#ifdef _WIN32
    QSettings diskEnum("HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Disk\\Enum", QSettings::NativeFormat);
    int count = diskEnum.value("Count").toInt();
    for (int i = 0; i < count; ++i) {
        QString diskName = diskEnum.value(QString::number(i)).toString().toLower();
        if (diskName.contains("vbox") || diskName.contains("vmware") || diskName.contains("qemu") || diskName.contains("virtual")) {
            m_detectedVmInfo += QString("[Disk Signature: %1] ").arg(diskName);
            return true;
        }
    }
#endif
    return false;
}

int EnvironmentDetector::calculateVmScore() const {
    m_detectedVmInfo.clear();
    int score = 0;

    QString hvVendor;
    if (checkCPUID(hvVendor)) score += 55;
    if (checkRegistry()) score += 35;
    if (checkVmDriverFiles()) score += 30;
    if (checkVirtualMac()) score += 20;
    if (checkDiskSignatures()) score += 25;

    return score;
}

bool EnvironmentDetector::isVirtualMachine() const {
    int score = calculateVmScore();
    return score >= 45;
}

QString EnvironmentDetector::getVmDetails() const {
    if (m_detectedVmInfo.isEmpty()) {
        calculateVmScore();
    }
    return m_detectedVmInfo.trimmed();
}

QStringList EnvironmentDetector::getVirtualCameras() const {
    QStringList badCams;
    QStringList blacklist = {
        "obs", "virtual", "manycam", "splitcam", "xsplit",
        "droidcam", "iriun", "epoccam", "camo", "vcam"
    };

    for (const auto& cam : QMediaDevices::videoInputs()) {
        QString name = cam.description().toLower();
        for (const QString &kw : blacklist) {
            if (name.contains(kw)) {
                badCams.append(cam.description());
                break;
            }
        }
    }

    return badCams;
}

bool EnvironmentDetector::hasMultipleMonitors() const {
    return QGuiApplication::screens().count() > 1;
}

QStringList EnvironmentDetector::getSuspiciousAudioDevices() const {
    QStringList suspicious;
    QStringList audioBlacklist = {
        "cable input", "cable output", "vb-audio", "virtual audio",
        "voicemeeter", "line 1 (virtual)", "screaming bee"
    };

    for (const auto& dev : QMediaDevices::audioInputs()) {
        QString name = dev.description().toLower();
        for (const QString &kw : audioBlacklist) {
            if (name.contains(kw)) {
                suspicious.append(QString("Вхід: %1").arg(dev.description()));
                break;
            }
        }
    }

    for (const auto& dev : QMediaDevices::audioOutputs()) {
        QString name = dev.description().toLower();
        for (const QString &kw : audioBlacklist) {
            if (name.contains(kw)) {
                suspicious.append(QString("Вихід: %1").arg(dev.description()));
                break;
            }
        }
    }

    return suspicious;
}
