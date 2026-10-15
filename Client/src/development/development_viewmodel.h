#pragma once
#include <QObject>
#include <QStringList>

class DevelopmentViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool available READ available NOTIFY changed)
    Q_PROPERTY(bool frozen READ frozen NOTIFY changed)
    Q_PROPERTY(QStringList controls READ controls NOTIFY changed)
    Q_PROPERTY(QStringList active READ active NOTIFY changed)
    Q_PROPERTY(QString status READ status NOTIFY changed)
public:
    explicit DevelopmentViewModel(QObject *parent=nullptr);
    static DevelopmentViewModel *current;
    bool available() const {return true;}
    bool frozen() const {return m_frozen;}
    QStringList controls() const {return m_controls;}
    QStringList active() const {return m_active;}
    QString status() const {return m_status;}
    bool bypass(const QString &control) const {return m_active.contains(control);}
    void authenticate(const QString &token);
    void started(int id);
    Q_INVOKABLE void setOverride(const QString &control,bool enabled);
    Q_INVOKABLE void reset();
    Q_INVOKABLE void enableAll();
signals:
    void changed();
private:
    QString m_status;
    QStringList m_controls,m_active;
    bool m_frozen=false;
};
