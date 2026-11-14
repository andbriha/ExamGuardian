#include <QtTest>
#include "vm_policy.h"
#include "virtualization_runtime.h"

class VmPolicyTest : public QObject {
    Q_OBJECT
private slots:
    void hyperVHostIsBlockedWithoutGuestEvidence() {
        VmEvidence e;
        QCOMPARE(vmEvidenceScore(e), 0);
        e.hypervisor = e.microsoftHypervisor = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
    }
    void maskedCpuStillHasGuestEvidence() {
        VmEvidence e;
        e.firmware = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
        e.firmware = false;
        e.devices = e.runningGuestDrivers = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
        e.devices = false;
        e.runningGuestService = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
    }
    void hyperVGuestIsNotExempt() {
        VmEvidence e;
        e.hypervisor = e.microsoftHypervisor = e.devices = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
    }
    void renamedFirmwareDoesNotHideKnownHypervisor() {
        VmEvidence e;
        e.hypervisor = e.knownGuestHypervisor = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
    }
    void runtimesAndFailedProbesBlockIndependently() {
        VmEvidence e;
        e.activeRuntime = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
        e.activeRuntime = false;
        e.runtimeProbeFailed = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
        e.runtimeProbeFailed = false;
        e.runningGuestService = true;
        QVERIFY(vmEvidenceScore(e) >= 50);
    }
    void runtimeNamesIncludeWslAndContainers() {
        for (const auto *name : {"Docker Desktop.exe", "COM.DOCKER.BACKEND.EXE", "wslhost.exe", "vmmemWSL", "qemu-system-x86_64.exe", "containerd.exe", "podman.exe"})
            QVERIFY2(isVirtualizationRuntimeProcess(name), name);
        QVERIFY(!isVirtualizationRuntimeProcess("explorer.exe"));
        QVERIFY(!isVirtualizationRuntimeProcess("docker-notes.exe"));
        QVERIFY(isVirtualizationRuntimeService("LxssManagerUser_1234"));
        QVERIFY(isVirtualizationRuntimeService("com.docker.service"));
        QVERIFY(!isVirtualizationRuntimeService("Dnscache"));
    }
    void staleFilesAreNotRunningDrivers() {
        VmEvidence e;
        e.driverFiles = e.disk = e.network = true;
        QVERIFY(vmEvidenceScore(e) < 50);
    }
};
QTEST_GUILESS_MAIN(VmPolicyTest)
#include "vm_policy_test.moc"
