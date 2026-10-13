#ifndef INPUT_BLOCKER_H
#define INPUT_BLOCKER_H

#include <QObject>
#include <QGuiApplication>
#include <QString>

#ifdef Q_OS_WIN
#include <windows.h>
#endif

class InputBlocker : public QObject {
    Q_OBJECT
public:
    explicit InputBlocker(QObject *parent = nullptr);
    ~InputBlocker();

    void setKeyBlock(bool enable);

private:
    QString getActiveWindowTitle();

#ifdef Q_OS_WIN
    static HHOOK hKeyboardHook;
    static LRESULT CALLBACK KeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);
#endif
};

#endif // INPUT_BLOCKER_H
