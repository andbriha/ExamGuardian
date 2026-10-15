import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: checkPage
    signal proceedToExam(var detectedApps)

    background: Rectangle { color: "#F8FAFC" }

    Component.onCompleted: {
        ProcessVM.runFullScan()
    }

    Connections {
        enabled: checkPage.StackView.status === StackView.Active || checkPage.StackView.status === StackView.Activating
        target: ProcessVM
        function onSnapshotReady(snapshot) {
            ProcessVM.saveSnapshotToFile(snapshot)
            loadingIndicator.running = false
            ProcessVM.runFullScan()
            if (ProcessVM.isEnvironmentSafe) checkPage.proceedToExam([])
        }
    }

    ScrollView {
        anchors.fill: parent
        contentWidth: parent.width

        ColumnLayout {
            anchors.horizontalCenter: parent.horizontalCenter
            width: Math.min(parent.width - 40, 720)
            spacing: 16

            Item { height: 10 }

            // Заголовок
            RowLayout {
                spacing: 12
                Layout.alignment: Qt.AlignHCenter

                Text { text: "🛡️"; font.pixelSize: 28 }
                Label {
                    text: "Валідація безпеки робочого середовища"
                    font.pixelSize: 22
                    font.bold: true
                    color: "#0F172A"
                }
            }

            Label {
                text: "Перед початком іспиту система автоматично перевіряє цілісність процесів, заборонені програми та відсутність віртуалізації."
                font.pixelSize: 13
                color: "#64748B"
                wrapMode: Text.Wrap
                Layout.fillWidth: true
                horizontalAlignment: Text.AlignHCenter
            }

            // Інформаційний чекліст
            Rectangle {
                Layout.fillWidth: true
                color: "white"
                radius: 10
                border.color: "#E2E8F0"
                Layout.preferredHeight: checklistCol.implicitHeight + 24

                ColumnLayout {
                    id: checklistCol
                    anchors.fill: parent
                    anchors.margins: 14
                    spacing: 10

                    // 1. Процеси
                    RowLayout {
                        spacing: 10
                        Text {
                            text: ProcessVM.suspiciousApps.length === 0 ? "✅" : "❌"
                            font.pixelSize: 16
                        }
                        Label {
                            text: "Заборонене ПЗ (Discord, Telegram, AnyDesk тощо):"
                            font.bold: true
                            font.pixelSize: 13
                            color: "#1E293B"
                            Layout.fillWidth: true
                        }
                        Label {
                            text: ProcessVM.suspiciousApps.length === 0 ? "Чисто" : (ProcessVM.suspiciousApps.length + " знайдено")
                            font.bold: true
                            color: ProcessVM.suspiciousApps.length === 0 ? "#16A34A" : "#DC2626"
                        }
                    }

                    // 2. Підробка процесів
                    RowLayout {
                        spacing: 10
                        Text {
                            text: ProcessVM.spoofedProcesses.length === 0 ? "✅" : "❌"
                            font.pixelSize: 16
                        }
                        Label {
                            text: "Контроль маскування процесів (Anti-Spoofing / SHA-256):"
                            font.bold: true
                            font.pixelSize: 13
                            color: "#1E293B"
                            Layout.fillWidth: true
                        }
                        Label {
                            text: ProcessVM.spoofedProcesses.length === 0 ? "Підтверджено" : "Підозріла активність"
                            font.bold: true
                            color: ProcessVM.spoofedProcesses.length === 0 ? "#16A34A" : "#DC2626"
                        }
                    }

                    // 3. Віртуальні машини
                    RowLayout {
                        spacing: 10
                        Text {
                            text: !ProcessVM.isVmDetected ? "✅" : "❌"
                            font.pixelSize: 16
                        }
                        Label {
                            text: "Віртуалізація / контейнери (VM, Hyper-V, Docker, WSL):"
                            font.bold: true
                            font.pixelSize: 13
                            color: "#1E293B"
                            Layout.fillWidth: true
                        }
                        Label {
                            text: !ProcessVM.isVmDetected ? "Не виявлено" : "Допуск заблоковано"
                            font.bold: true
                            color: !ProcessVM.isVmDetected ? "#16A34A" : "#DC2626"
                        }
                    }

                    // 4. Віртуальні камери
                    RowLayout {
                        spacing: 10
                        Text {
                            text: ProcessVM.virtualCameras.length === 0 ? "✅" : "❌"
                            font.pixelSize: 16
                        }
                        Label {
                            text: "Віртуальні відеокамери (OBS Virtual Camera / ManyCam):"
                            font.bold: true
                            font.pixelSize: 13
                            color: "#1E293B"
                            Layout.fillWidth: true
                        }
                        Label {
                            text: ProcessVM.virtualCameras.length === 0 ? "Чисто" : "Виявлено"
                            font.bold: true
                            color: ProcessVM.virtualCameras.length === 0 ? "#16A34A" : "#DC2626"
                        }
                    }

                    // 5. Монітори
                    RowLayout {
                        spacing: 10
                        Text {
                            text: !ProcessVM.multipleMonitors ? "✅" : "❌"
                            font.pixelSize: 16
                        }
                        Label {
                            text: "Конфігурація дисплеїв:"
                            font.bold: true
                            font.pixelSize: 13
                            color: "#1E293B"
                            Layout.fillWidth: true
                        }
                        Label {
                            text: !ProcessVM.multipleMonitors ? "1 монітор" : "Кілька екранів заборонено"
                            font.bold: true
                            color: !ProcessVM.multipleMonitors ? "#16A34A" : "#DC2626"
                        }
                    }
                }
            }

            // Блок червоного попередження якщо є проблеми
            Rectangle {
                Layout.fillWidth: true
                visible: !ProcessVM.isEnvironmentSafe
                color: "#FEF2F2"
                radius: 10
                border.color: "#FECACA"
                Layout.preferredHeight: errorsCol.implicitHeight + 24

                ColumnLayout {
                    id: errorsCol
                    anchors.fill: parent
                    anchors.margins: 16
                    spacing: 8

                    Label {
                        text: "⛔ ДОПУСК ЗАБЛОКОВАНО: Виявлено порушення вимог безпеки"
                        font.bold: true
                        font.pixelSize: 14
                        color: "#B91C1C"
                    }

                    // Список заборонених програм
                    Repeater {
                        model: ProcessVM.suspiciousApps
                        delegate: Label {
                            text: "• Запущена заборонена програма: " + modelData
                            color: "#991B1B"
                            font.pixelSize: 13
                        }
                    }

                    // Список підроблених процесів
                    Repeater {
                        model: ProcessVM.spoofedProcesses
                        delegate: Label {
                            text: "• Виявлено підозрілий маскований процес: " + modelData
                            color: "#991B1B"
                            font.pixelSize: 13
                        }
                    }

                    // Попередження про VM
                    Label {
                        visible: ProcessVM.isVmDetected
                        text: "• Віртуалізація активна або перевірку не завершено: " + ProcessVM.vmDetails
                        color: "#991B1B"
                        font.pixelSize: 13
                        wrapMode: Text.Wrap
                        Layout.fillWidth: true
                    }

                    // Попередження про віртуальні камери
                    Repeater {
                        model: ProcessVM.virtualCameras
                        delegate: Label {
                            text: "• Виявлено віртуальний пристрій відеозахвату: " + modelData
                            color: "#991B1B"
                            font.pixelSize: 13
                        }
                    }

                    // Попередження про 2 монітори
                    Label {
                        visible: ProcessVM.multipleMonitors
                        text: "• Підключено більше одного монітора. Будь ласка, вимкніть додаткові екрани."
                        color: "#991B1B"
                        font.pixelSize: 13
                    }

                    Label {
                        text: "Закрийте вказані програми та служби віртуалізації. Активний Hyper-V/VBS також блокує допуск. Після усунення причин повторіть перевірку."
                        font.pixelSize: 12
                        font.bold: true
                        color: "#B91C1C"
                    }
                }
            }

            // Блок успішної перевірки
            Rectangle {
                Layout.fillWidth: true
                visible: ProcessVM.isEnvironmentSafe
                color: "#ECFDF5"
                radius: 10
                border.color: "#A7F3D0"
                height: 52

                RowLayout {
                    anchors.centerIn: parent
                    spacing: 8
                    Text { text: "🎉"; font.pixelSize: 18 }
                    Label {
                        text: (typeof DevVM !== "undefined" && DevVM.active.length) ? "DEV — blocking overridden. Review the detected findings above." : "Усі перевірки пройдено успішно! Ви можете перейти до перевірки камери."
                        color: "#047857"
                        font.bold: true
                        font.pixelSize: 14
                    }
                }
            }

            // Кнопка оновлення списку
            Button {
                text: "🔄 Оновити та перевірити знову"
                Layout.fillWidth: true
                Layout.preferredHeight: 44
                onClicked: ProcessVM.runFullScan()
            }

            BusyIndicator {
                id: loadingIndicator
                running: false
                visible: running
                Layout.alignment: Qt.AlignHCenter
            }

            // Кнопка початку — СУВОРО ЗАБЛОКОВАНА, якщо середовище не безпечне!
            Button {
                id: startBtn
                Layout.fillWidth: true
                Layout.preferredHeight: 50
                visible: !loadingIndicator.running
                enabled: ProcessVM.isEnvironmentSafe

                text: ProcessVM.isEnvironmentSafe ? "Перейти до біометричної перевірки →" : "⛔ Усуньте порушення для продовження"

                background: Rectangle {
                    color: !ProcessVM.isEnvironmentSafe ? "#94A3B8" : (startBtn.down ? "#1D4ED8" : "#2563EB")
                    radius: 8
                }

                contentItem: Text {
                    text: parent.text
                    font.bold: true
                    font.pixelSize: 15
                    color: "white"
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                }

                onClicked: {
                    loadingIndicator.running = true
                    ProcessVM.requestProcessSnapshot()
                }
            }

            Item { height: 20 }
        }
    }
}
