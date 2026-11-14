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
#include <winver.h>
#endif

ProcessScanner::ProcessScanner(QObject *parent) : QObject(parent) {}

QString ProcessScanner::calculateFileHash(const QString &filePath) {
    QFileInfo fileInfo(filePath);
    if (!fileInfo.exists()) return "N/A";

    QDateTime modifiedTime = fileInfo.lastModified();

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

QString ProcessScanner::getPeInternalName(const QString &filePath) {
#ifdef Q_OS_WIN
    std::wstring wPath = filePath.toStdWString();
    DWORD handle = 0;
    DWORD size = GetFileVersionInfoSizeW(wPath.c_str(), &handle);
    if (size == 0) return "";

    QByteArray buffer(size, 0);
    if (!GetFileVersionInfoW(wPath.c_str(), handle, size, buffer.data())) {
        return "";
    }

    struct LANGANDCODEPAGE {
        WORD wLanguage;
        WORD wCodePage;
    } *lpTranslate;

    UINT cbTranslate = 0;
    if (!VerQueryValueW(buffer.data(), L"\\VarFileInfo\\Translation", (LPVOID*)&lpTranslate, &cbTranslate) || cbTranslate == 0) {
        return "";
    }

    wchar_t subBlock[128];
    LPVOID lpBuffer = nullptr;
    UINT dwBytes = 0;
    QString combined;

    // Перевіряємо поля: FileDescription, ProductName, OriginalFilename, CompanyName
    const wchar_t* fields[] = { L"FileDescription", L"ProductName", L"OriginalFilename", L"CompanyName" };
    for (const wchar_t* field : fields) {
        swprintf_s(subBlock, L"\\StringFileInfo\\%04x%04x\\%s",
                   lpTranslate[0].wLanguage, lpTranslate[0].wCodePage, field);
        if (VerQueryValueW(buffer.data(), subBlock, &lpBuffer, &dwBytes) && dwBytes > 0) {
            combined += " " + QString::fromWCharArray((const wchar_t*)lpBuffer);
        }
    }

    return combined.toLower().trimmed();
#else
    Q_UNUSED(filePath);
    return "";
#endif
}

bool ProcessScanner::isPathSpoofed(const QString &processName, const QString &fullPath) {
    QString pLower = processName.toLower();
    QString pathLower = fullPath.toLower().replace("/", "\\");

    // Критичні системні процеси Windows, які мають запускатися ТІЛЬКИ з System32 або Windows
    QStringList systemProcesses = {
        "svchost.exe", "csrss.exe", "lsass.exe", "services.exe",
        "winlogon.exe", "smss.exe", "taskhostw.exe", "conhost.exe",
        "dwm.exe", "sihost.exe"
    };

    if (systemProcesses.contains(pLower)) {
        if (!pathLower.contains("\\windows\\system32\\") &&
            !pathLower.contains("\\windows\\syswow64\\") &&
            !pathLower.contains("\\windows\\systemapps\\")) {
            return true; // Процес маскується під системний, але запущений з користувацької папки!
        }
    }

    // Процеси з підробленими іменами, запущені з Temp, Downloads тощо
    if (pathLower.contains("\\appdata\\local\\temp\\") || pathLower.contains("\\downloads\\")) {
        if (pLower == "explorer.exe" || pLower == "calculator.exe" || pLower == "notepad.exe") {
            return true;
        }
    }

    return false;
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
                    details["spoofed"] = isPathSpoofed(name, fullPath);
                }
                CloseHandle(hProcess);
            }
            if (!details.isEmpty()) processes.append(details);
        } while (Process32NextW(hSnapshot, &pe));
    }
    CloseHandle(hSnapshot);
#elif defined(Q_OS_LINUX)
    for (const auto &pid : QDir("/proc").entryList(QDir::Dirs | QDir::NoDotAndDotDot)) {
        bool numeric = false; pid.toUInt(&numeric); if (!numeric) continue;
        const auto path = QFileInfo("/proc/" + pid + "/exe").symLinkTarget();
        if (path.isEmpty()) continue;
        processes.append(QVariantMap{{"name", QFileInfo(path).fileName()}, {"path", path}, {"hash", calculateFileHash(path)}});
    }
#endif

    return processes;
}

bool ProcessScanner::saveSnapshotToFile(const QVariantList &snapshot) {
    QJsonArray jsonArray = QJsonArray::fromVariantList(snapshot);
    QJsonDocument doc(jsonArray);

    QString filePath = QCoreApplication::applicationDirPath() + "/exam_protocol.json";

    QFile file(filePath);
    if (!file.open(QIODevice::WriteOnly | QIODevice::Text)) {
        qDebug() << "Failed to create file:" << filePath;
        return false;
    }

    file.write(doc.toJson(QJsonDocument::Indented));
    file.close();
    qDebug() << "Process protocol saved at:" << filePath;
    return true;
}

QStringList ProcessScanner::getSuspiciousProcesses() {
    QStringList foundApps;
#ifdef Q_OS_LINUX
    const QStringList blocked = {"discord", "telegram-desktop", "telegram", "anydesk", "teamviewer", "obs", "obs-studio", "rustdesk", "zoom", "slack", "skype", "vncviewer", "wireshark"};
    for (const auto &pid : QDir("/proc").entryList(QDir::Dirs | QDir::NoDotAndDotDot)) {
        bool numeric = false; pid.toUInt(&numeric); if (!numeric) continue;
        QFile nameFile("/proc/" + pid + "/comm");
        if (!nameFile.open(QIODevice::ReadOnly)) continue;
        const auto name = QString::fromUtf8(nameFile.readAll()).trimmed().toLower();
        const auto executable = QFileInfo("/proc/" + pid + "/exe").symLinkTarget();
        if (blocked.contains(name) || blocked.contains(QFileInfo(executable).fileName().toLower())) {
            if (!foundApps.contains(name)) foundApps.append(name);
        }
    }
#endif

#ifdef Q_OS_WIN
    QStringList blacklistNames = {
        // Месенджери
        "telegram.exe", "discord.exe", "skype.exe", "viber.exe", "whatsapp.exe",
        "teams.exe", "slack.exe", "zoom.exe",
        // Віддалене керування
        "teamviewer.exe", "anydesk.exe", "radmin.exe", "rustdesk.exe",
        "vncviewer.exe", "ammyy.exe", "parsec.exe", "sunloginclient.exe",
        // Запис екрана
        "obs64.exe", "obs32.exe", "fraps.exe", "bandicam.exe", "action.exe", "camtasia.exe",
        // Утиліти аналізу та читів
        "cheatengine.exe", "x64dbg.exe", "ida64.exe", "wireshark.exe",
        "httpdebugger.exe", "fiddler.exe", "charles.exe", "procexp.exe", "processhacker.exe"
    };

    // Ключові слова в сигнатурах/описах файлів PE
    QStringList peSignatureKeywords = {
        "discord", "telegram", "anydesk", "teamviewer", "skype",
        "viber", "whatsapp", "obs studio", "wireshark", "cheat engine",
        "rustdesk", "radmin"
    };

    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) return foundApps;

    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(PROCESSENTRY32W);

    if (Process32FirstW(hSnapshot, &pe)) {
        do {
            QString processName = QString::fromWCharArray(pe.szExeFile).toLower();
            QString fullPath;

            HANDLE hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pe.th32ProcessID);
            if (hProcess) {
                wchar_t path[MAX_PATH];
                DWORD size = MAX_PATH;
                if (QueryFullProcessImageNameW(hProcess, 0, path, &size)) {
                    fullPath = QString::fromWCharArray(path);
                }
                CloseHandle(hProcess);
            }

            // 1. Перевірка за назвою файлу
            if (blacklistNames.contains(processName) && !foundApps.contains(processName)) {
                foundApps.append(processName);
                continue;
            }

            // 2. Якщо файл перейменовано студентом — перевіряємо внутрішній PE-підпис та назву продукту!
            if (!fullPath.isEmpty()) {
                QString peInfo = getPeInternalName(fullPath);
                for (const QString &kw : peSignatureKeywords) {
                    if (peInfo.contains(kw)) {
                        QString detectedEntry = QString("%1 (перейменований %2)").arg(processName, kw);
                        if (!foundApps.contains(detectedEntry)) {
                            foundApps.append(detectedEntry);
                        }
                        break;
                    }
                }
            }

        } while (Process32NextW(hSnapshot, &pe));
    }
    CloseHandle(hSnapshot);
#endif

    return foundApps;
}

QStringList ProcessScanner::getSpoofedProcesses() {
    QStringList spoofedList;

#ifdef Q_OS_WIN
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) return spoofedList;

    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(PROCESSENTRY32W);

    if (Process32FirstW(hSnapshot, &pe)) {
        do {
            QString name = QString::fromWCharArray(pe.szExeFile);
            HANDLE hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pe.th32ProcessID);
            if (hProcess) {
                wchar_t path[MAX_PATH];
                DWORD size = MAX_PATH;
                if (QueryFullProcessImageNameW(hProcess, 0, path, &size)) {
                    QString fullPath = QString::fromWCharArray(path);
                    if (isPathSpoofed(name, fullPath)) {
                        spoofedList.append(QString("%1 [шлях: %2]").arg(name, fullPath));
                    }
                }
                CloseHandle(hProcess);
            }
        } while (Process32NextW(hSnapshot, &pe));
    }
    CloseHandle(hSnapshot);
#endif

    return spoofedList;
}
