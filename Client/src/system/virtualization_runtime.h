#pragma once
#include <QString>
#include <QStringList>

inline bool isVirtualizationRuntimeProcess(QString name) {
    name = name.toLower();
    if (name.endsWith(".exe")) name.chop(4);
    static const QStringList names = {
        "docker", "docker desktop", "dockerd", "com.docker.backend", "com.docker.build",
        "com.docker.proxy", "containerd", "containerd-shim", "containerd-shim-runc-v2",
        "podman", "podman machine", "rancher desktop", "nerdctl", "runc", "conmon",
        "wsl", "wslhost", "wslservice", "wslrelay", "vmmem", "vmmemwsl", "vmcompute",
        "vmwp", "vmms", "vmware-vmx", "vmware", "virtualbox", "virtualboxvm",
        "vboxheadless", "vboxsvc", "prl_vm_app", "qemu-kvm"
    };
    return names.contains(name) || name.startsWith("qemu-system-");
}

inline bool isVirtualizationRuntimeService(const QString &name) {
    static const QStringList names = {"com.docker.service", "docker", "containerd",
        "wslservice", "lxssmanager", "vmcompute", "vmms", "vmwarehostd", "vboxsds"};
    const auto lower = name.toLower();
    return names.contains(lower) || lower.startsWith("lxssmanageruser_");
}
