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
    bool saveSnapshotToFile(const QVariantList &snapshot);

signals:
    // Сигнал, який спрацює, коли фоновий потік закінчить роботу
    void snapshotReady(QVariantList snapshot);

private:
    struct CacheEntry {
        QString hash;
        QDateTime lastModified;
    };

    QHash<QString, CacheEntry> m_hashCache;

    QString calculateFileHash(const QString &filePath);
    QVariantList buildProcessSnapshot(); // Метод для фонового потоку
};

#endif // PROCESS_SCANNER_H
