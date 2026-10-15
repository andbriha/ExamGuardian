#include <QSettings>
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

        const bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        const bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        const bool alt = (p->flags & LLKHF_ALTDOWN) != 0;
        const bool prohibited = (ctrl && p->vkCode == VK_ESCAPE) || (ctrl && shift && p->vkCode == VK_ESCAPE) || (alt && p->vkCode == VK_F4) || p->vkCode == VK_SNAPSHOT;
        if (isWin || isAltTab || prohibited) return 1;
    }
    return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
}
#endif

InputBlocker::InputBlocker(QObject *parent) : QObject(parent) {
#ifdef Q_OS_LINUX
    // Recover saved settings after an interrupted previous exam.
    setKeyBlock(false);
#endif
}

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

bool InputBlocker::setKeyBlock(bool enable) {
#ifdef Q_OS_WIN
    if (enable && !hKeyboardHook) hKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardProc, GetModuleHandle(NULL), 0);
    if (!enable && hKeyboardHook) { UnhookWindowsHookEx(hKeyboardHook); hKeyboardHook = nullptr; }
    return !enable || hKeyboardHook != nullptr;
#elif defined(Q_OS_LINUX)
    if (enable && QGuiApplication::platformName() != "xcb") return false;
    bool success = true;
    for (auto *window : qApp->topLevelWindows()) if (!window->setKeyboardGrabEnabled(enable) && enable) success = false;
    QSettings recovery("ExamGuardian", "InputRecovery");
    const QStringList schemas = {"org.gnome.mutter", "org.gnome.desktop.wm.keybindings"};
    const QStringList keys = {"overlay-key", "panel-main-menu"};
    for (int i = 0; i < keys.size(); ++i) {
        const QString storageKey = schemas[i] + "/" + keys[i];
        if (enable && !recovery.contains(storageKey)) {
            QProcess process; process.start("gsettings", {"get", schemas[i], keys[i]});
            if (process.waitForFinished(1000) && process.exitCode() == 0) {
                recovery.setValue(storageKey, QString::fromUtf8(process.readAllStandardOutput()).trimmed()); recovery.sync();
                QProcess::execute("gsettings", {"set", schemas[i], keys[i], i == 0 ? "''" : "[]"});
            }
        } else if (!enable && recovery.contains(storageKey)) {
            if (QProcess::execute("gsettings", {"set", schemas[i], keys[i], recovery.value(storageKey).toString()}) == 0) recovery.remove(storageKey);
        }
    }
    return success;
#else
    return !enable;
#endif
}
