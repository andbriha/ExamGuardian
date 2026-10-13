#include "input_blocker.h"
#include <QDebug>
#include <QWindow>
#include <QGuiApplication>
#include <QProcess>

#ifdef Q_OS_WIN
HHOOK InputBlocker::hKeyboardHook = nullptr;

LRESULT CALLBACK InputBlocker::KeyboardProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode == HC_ACTION) {
        KBDLLHOOKSTRUCT *p = (KBDLLHOOKSTRUCT *)lParam;
        bool isWin = (p->vkCode == VK_LWIN || p->vkCode == VK_RWIN);
        bool isAltTab = (p->vkCode == VK_TAB && (p->flags & LLKHF_ALTDOWN));

        if (isWin || isAltTab) return 1;
    }
    return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
}
#endif

InputBlocker::InputBlocker(QObject *parent) : QObject(parent) {}

InputBlocker::~InputBlocker() {
    setKeyBlock(false);
}

QString InputBlocker::getActiveWindowTitle() {
#ifdef Q_OS_WIN
    HWND hwnd = GetForegroundWindow();
    if (hwnd) {
        wchar_t windowTitle[256];
        GetWindowTextW(hwnd, windowTitle, 256);
        return QString::fromWCharArray(windowTitle);
    }
    return "Unknown Window";
#elif defined(Q_OS_LINUX)
    QProcess process;
    process.start("xdotool", QStringList() << "getactivewindow" << "getwindowname");
    if (process.waitForFinished(1000)) {
        QString output = process.readAllStandardOutput().trimmed();
        if (!output.isEmpty()) {
            return output;
        }
    }
    return "Unknown Window (можливо Wayland)";
#endif
    return "";
}

void InputBlocker::setKeyBlock(bool enable) {
#ifdef Q_OS_WIN
    if (enable && !hKeyboardHook) {
        hKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardProc, GetModuleHandle(NULL), 0);
        if (hKeyboardHook) {
            qDebug() << "[OK] Windows Keyboard blocked!";
        } else {
            qDebug() << "[ERROR] Failed to install hook!";
        }
    } else if (!enable && hKeyboardHook) {
        UnhookWindowsHookEx(hKeyboardHook);
        hKeyboardHook = nullptr;
        qDebug() << "[OK] Windows Keyboard unblocked!";
    }
#elif defined(Q_OS_LINUX)
    QWindowList windows = qApp->topLevelWindows();

    if (enable) {
        qDebug() << "[LINUX] Activating total keyboard grab...";
        for (QWindow *window : windows) {
            window->setKeyboardGrabEnabled(true);
        }

        // ХАК ДЛЯ UBUNTU/GNOME: Временно отключаем системную реакцию на клавишу Super
        qDebug() << "[LINUX] Disabling GNOME Super key...";
        QProcess::execute("gsettings", {"set", "org.gnome.mutter", "overlay-key", "''"});
        QProcess::execute("gsettings", {"set", "org.gnome.desktop.wm.keybindings", "panel-main-menu", "[]"});

    } else {
        qDebug() << "[LINUX] Releasing keyboard grab...";
        for (QWindow *window : windows) {
            window->setKeyboardGrabEnabled(false);
        }

        // ВОЗВРАЩАЕМ КАК БЫЛО: Сбрасываем настройки клавиши Super к значениям по умолчанию
        qDebug() << "[LINUX] Restoring GNOME Super key...";
        QProcess::execute("gsettings", {"reset", "org.gnome.mutter", "overlay-key"});
        QProcess::execute("gsettings", {"reset", "org.gnome.desktop.wm.keybindings", "panel-main-menu"});
    }
#endif
}
