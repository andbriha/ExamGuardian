#include "process_scanner.h"
#include <QCryptographicHash>
#include <QFile>
#include <QJsonDocument>
#include <QJsonArray>
#include <QJsonObject>
#include <QCoreApplication>
#include <QDir>
#include <QDebug>
#include <QFileInfo>
#include <QStandardPaths>
#include <QtConcurrent/QtConcurrent>

#ifdef Q_OS_WIN
#include <windows.h>
#include <tlhelp32.h>
#include <psapi.h>
#endif

ProcessScanner::ProcessScanner(QObject *parent) : QObject(parent) {}

QString ProcessScanner::calculateFileHash(const QString &filePath) {
    QFileInfo fileInfo(filePath);
    if (!fileInfo.exists()) return "N/A";

    QDateTime modifiedTime = fileInfo.lastModified();

    // Розумне кешування
    if (m_hashCache.contains(filePath)) {
        if (m_hashCache[filePath].lastModified == modifiedTime) {
            return m_hashCache[filePath].hash;
        }
    }

    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly)) return "N/A";

    QCryptographicHash hash(QCryptographicHash::Sha256);
    if (hash.addData(&file)) {
        QString result = hash.result().toHex();
        m_hashCache[filePath] = {result, modifiedTime};
        return result;
    }
    return "Error";
}

void ProcessScanner::requestSnapshotAsync() {
    QFuture<QVariantList> future = QtConcurrent::run(&ProcessScanner::buildProcessSnapshot, this);

    QFutureWatcher<QVariantList> *watcher = new QFutureWatcher<QVariantList>(this);
    connect(watcher, &QFutureWatcher<QVariantList>::finished, this, [this, watcher]() {
        emit snapshotReady(watcher->result());
        watcher->deleteLater();
    });

    watcher->setFuture(future);
}

QVariantList ProcessScanner::buildProcessSnapshot() {
    QVariantList processes;

#ifdef Q_OS_WIN
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) return processes;

    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(PROCESSENTRY32W);

    if (Process32FirstW(hSnapshot, &pe)) {
        do {
            QVariantMap details;
            QString name = QString::fromWCharArray(pe.szExeFile);

            HANDLE hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pe.th32ProcessID);
            if (hProcess) {
                wchar_t path[MAX_PATH];
                DWORD size = MAX_PATH;
                if (QueryFullProcessImageNameW(hProcess, 0, path, &size)) {
                    QString fullPath = QString::fromWCharArray(path);
                    details["name"] = name;
                    details["path"] = fullPath;
                    details["hash"] = calculateFileHash(fullPath);
                }
                CloseHandle(hProcess);
            }
            if (!details.isEmpty()) processes.append(details);
        } while (Process32NextW(hSnapshot, &pe));
    }
    CloseHandle(hSnapshot);

#elif defined(Q_OS_LINUX)
    QDir procDir("/proc");
    QStringList entries = procDir.entryList(QDir::Dirs | QDir::NoDotAndDotDot);

    for (const QString &entry : entries) {
        bool isNumeric;
        entry.toInt(&isNumeric);

        if (isNumeric) {
            QString exeSymlink = QString("/proc/%1/exe").arg(entry);
            QFileInfo fileInfo(exeSymlink);

            if (fileInfo.exists() && fileInfo.isSymLink()) {
                QString fullPath = fileInfo.symLinkTarget();
                QString name = QFileInfo(fullPath).fileName();

                QVariantMap details;
                details["name"] = name;
                details["path"] = fullPath;
                details["hash"] = calculateFileHash(fullPath);
                processes.append(details);
            }
        }
    }
#endif

    return processes;
}

bool ProcessScanner::saveSnapshotToFile(const QVariantList &snapshot) {
    QJsonArray jsonArray = QJsonArray::fromVariantList(snapshot);
    QJsonDocument doc(jsonArray);

    QString dirPath = QStandardPaths::writableLocation(QStandardPaths::DocumentsLocation);
    QString filePath = QCoreApplication::applicationDirPath() + "/exam_protocol.json";

    QFile file(filePath);
    if (!file.open(QIODevice::WriteOnly | QIODevice::Text)) {
        qDebug() << "Failed to create file:" << filePath;
        return false;
    }

    file.write(doc.toJson(QJsonDocument::Indented));
    file.close();
    qDebug() << "File successfully saved at:" << filePath;
    return true;
}

QStringList ProcessScanner::getSuspiciousProcesses() {
    QStringList foundApps;

#ifdef Q_OS_WIN
    QStringList blacklist = {
        "telegram.exe", "discord.exe", "skype.exe", "viber.exe", "whatsapp.exe",
        "teamviewer.exe", "anydesk.exe", "radmin.exe", "obs64.exe", "obs32.exe", "fraps.exe"
    };

    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) return foundApps;

    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(PROCESSENTRY32W);

    if (Process32FirstW(hSnapshot, &pe)) {
        do {
            QString processName = QString::fromWCharArray(pe.szExeFile).toLower();
            if (blacklist.contains(processName) && !foundApps.contains(processName)) {
                foundApps.append(processName);
            }
        } while (Process32NextW(hSnapshot, &pe));
    }
    CloseHandle(hSnapshot);

#elif defined(Q_OS_LINUX)
    QStringList blacklist = {
        "telegram-desktop", "discord", "skypeforlinux", "viber",
        "teamviewer", "anydesk", "obs"
    };

    QDir procDir("/proc");
    QStringList entries = procDir.entryList(QDir::Dirs | QDir::NoDotAndDotDot);

    for (const QString &entry : entries) {
        bool isNumeric;
        entry.toInt(&isNumeric);

        if (isNumeric) {
            QString exeSymlink = QString("/proc/%1/exe").arg(entry);
            QFileInfo fileInfo(exeSymlink);

            if (fileInfo.exists() && fileInfo.isSymLink()) {
                QString fullPath = fileInfo.symLinkTarget();
                QString processName = QFileInfo(fullPath).fileName().toLower();

                if (blacklist.contains(processName) && !foundApps.contains(processName)) {
                    foundApps.append(processName);
                }
            }
        }
    }
#endif

    return foundApps;
}
