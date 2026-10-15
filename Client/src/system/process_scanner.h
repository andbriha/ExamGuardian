#ifndef PROCESS_SCANNER_H
#define PROCESS_SCANNER_H

#include <QObject>
#include <QVariantList>
#include <QStringList>
#include <QHash>
#include <QDateTime>

class ProcessScanner : public QObject {
    Q_OBJECT
public:
    explicit ProcessScanner(QObject *parent = nullptr);

    void requestSnapshotAsync();
    QStringList getSuspiciousProcesses();
    QStringList getSpoofedProcesses();
    bool saveSnapshotToFile(const QVariantList &snapshot);
    QString calculateFileHash(const QString &filePath);

signals:
    void snapshotReady(QVariantList snapshot);

private:
    struct CacheEntry {
        QString hash;
        QDateTime lastModified;
    };

    QHash<QString, CacheEntry> m_hashCache;

    QVariantList buildProcessSnapshot();
    QString getPeInternalName(const QString &filePath);
    bool isPathSpoofed(const QString &processName, const QString &fullPath);
};

#endif // PROCESS_SCANNER_H
