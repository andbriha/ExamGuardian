#pragma once
#include <QStringList>

struct WindowsVmEvidence {
    bool firmware = false;
    bool devices = false;
    bool drivers = false;
    bool services = false;
    bool activeRuntime = false;
    bool runtimeProbeFailed = false;
    QStringList details;
};

WindowsVmEvidence collectWindowsVmEvidence();
