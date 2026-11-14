#include "environment_detector.h"
#include "vm_policy.h"
#include "vm_windows.h"
#include <QDir>
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
#elif defined(Q_OS_LINUX) && (defined(__i386__) || defined(__x86_64__))
#include <cpuid.h>
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

#elif defined(Q_OS_LINUX)
    QString combined;
    for (const auto &name : {"sys_vendor", "product_name", "board_vendor"}) {
        QFile file(QString("/sys/class/dmi/id/") + name);
        if (file.open(QIODevice::ReadOnly)) combined += QString::fromUtf8(file.readAll()).toLower();
    }
    for (const auto &keyword : {"vmware", "virtualbox", "qemu", "kvm", "virtual machine", "xen"}) {
        if (combined.contains(keyword)) { m_detectedVmInfo += QString("[DMI: %1] ").arg(keyword); return true; }
    }
#endif
    return false;
}

bool EnvironmentDetector::checkCPUID(QString &hypervisorVendor) const {
#ifdef _WIN32
    int cpuInfo[4] = {0};
    __cpuid(cpuInfo, 1);
    // 31-й біт ECX вказує на наявність гіпервізора (Hypervisor Present Bit)
    bool hypervisorPresent = (cpuInfo[2] & (1u << 31)) != 0;

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
#elif defined(Q_OS_LINUX) && (defined(__i386__) || defined(__x86_64__))
    unsigned int a, b, c, d;
    __cpuid(1, a, b, c, d);
    if (c & (1u << 31)) {
        __cpuid(0x40000000, a, b, c, d);
        char vendor[13] = {}; memcpy(vendor, &b, 4); memcpy(vendor+4, &c, 4); memcpy(vendor+8, &d, 4);
        hypervisorVendor = QString::fromLatin1(vendor).trimmed();
        m_detectedVmInfo += QString("[CPUID: %1] ").arg(hypervisorVendor); return true;
    }
#endif
    return false;
}

bool EnvironmentDetector::checkVmDriverFiles() const {
#ifdef _WIN32
    const QString drivers = QDir(qEnvironmentVariable("SystemRoot", "C:/Windows")).filePath("System32/drivers/");
    QStringList driverPaths;
    for (const auto *name : {"VBoxMouse.sys", "VBoxGuest.sys", "VBoxSF.sys", "vmmouse.sys", "vmhgfs.sys"})
        driverPaths << drivers + QLatin1String(name);

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
    VmEvidence evidence;
    QString vendor;
    evidence.hypervisor = checkCPUID(vendor);
    evidence.microsoftHypervisor = vendor == "Microsoft Hv";
    evidence.knownGuestHypervisor = evidence.hypervisor &&
        (vendor.contains("VMware", Qt::CaseInsensitive) || vendor.contains("VBox", Qt::CaseInsensitive) ||
         vendor.contains("KVM", Qt::CaseInsensitive) || vendor.contains("Xen", Qt::CaseInsensitive) ||
         vendor.contains("prl", Qt::CaseInsensitive) || vendor.contains("TCG", Qt::CaseInsensitive));
    const auto native = collectWindowsVmEvidence();
    evidence.firmware = checkRegistry() || native.firmware;
    evidence.devices = native.devices;
    evidence.runningGuestDrivers = native.drivers;
    evidence.runningGuestService = native.services;
    evidence.activeRuntime = native.activeRuntime;
    evidence.runtimeProbeFailed = native.runtimeProbeFailed;
    evidence.driverFiles = checkVmDriverFiles();
    evidence.network = checkVirtualMac();
    evidence.disk = checkDiskSignatures();
    m_detectedVmInfo += native.details.join(' ');
    const int score = vmEvidenceScore(evidence);
    m_detectedVmInfo += QString(" [Strict virtualization policy: %1/50]").arg(score);
    return score;
}

bool EnvironmentDetector::isVirtualMachine() const {
    int score = calculateVmScore();
    return score >= 50;
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
