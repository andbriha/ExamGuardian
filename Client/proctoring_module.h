#ifndef PROCTORING_MODULE_H
#define PROCTORING_MODULE_H

#include <QObject>
#include "input_blocker.h"
#include "process_scanner.h"
#include "media_validator.h"

class ProctoringModule : public QObject {
    Q_OBJECT
public:
    explicit ProctoringModule(QObject *parent = nullptr);
    ~ProctoringModule();

    // --- МЕТОДИ ДЛЯ QML (ДЕЛЕГУВАННЯ) ---

    // 1. Управління блокуваннями
    Q_INVOKABLE void setKeyBlock(bool enable);

    // 2. Управління процесами
    Q_INVOKABLE void requestProcessSnapshot();
    Q_INVOKABLE QStringList getSuspiciousProcesses();
    Q_INVOKABLE bool saveSnapshotToFile(const QVariantList &snapshot);

    // 3. Управління камерою (Liveness)
    Q_INVOKABLE bool isVirtualCamera(const QString &cameraName);
    Q_INVOKABLE void startLivenessSession();
    Q_INVOKABLE void simulateUserActionCompleted();

signals:
    // --- СИГНАЛИ ДЛЯ QML ---
    void snapshotReady(QVariantList snapshot);
    void livenessInstructionReceived(QString instruction);
    void livenessCheckResult(bool passed, QString message);

private:
    // Екземпляри наших робочих модулів
    InputBlocker m_inputBlocker;
    ProcessScanner m_scanner;
    MediaValidator m_media;
};

#endif // PROCTORING_MODULE_H
