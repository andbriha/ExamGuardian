#ifndef MEDIA_VALIDATOR_H
#define MEDIA_VALIDATOR_H

#include <QObject>
#include <QString>

class MediaValidator : public QObject {
    Q_OBJECT
public:
    explicit MediaValidator(QObject *parent = nullptr);

    bool isVirtualCamera(const QString &cameraName);
    void startLivenessSession();
    void simulateUserActionCompleted();

signals:
    // Сигнали про стан перевірки на живу людину
    void instructionReceived(QString instruction);
    void checkResult(bool passed, QString message);
};

#endif // MEDIA_VALIDATOR_H
