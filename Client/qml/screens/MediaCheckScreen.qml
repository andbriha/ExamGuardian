import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtMultimedia

Page {
    id: mediaPage
    signal mediaCheckPassed()

    background: Rectangle { color: "white" }

    // Screen states
    // 0: Initialization
    // 1: Virtual camera error
    // 2: Waiting for server / Fetching task
    // 3: Executing task
    // 4: Server verification
    // 5: Success
    // 6: Error state (requires retry)
    property int step: 0
    property string serverInstruction: ""
    property string errorMessage: ""
    property string progressHint: "Подивіться прямо в камеру"

    Timer {
        id: autoVerifyTimer
        interval: 1400
        repeat: true
        running: step === 3
        onTriggered: {
            MediaVM.simulateUserActionCompleted()
        }
    }

    Timer {
        id: autoProceedTimer
        interval: 1200
        repeat: false
        running: false
        onTriggered: {
            mediaPage.mediaCheckPassed()
        }
    }

    Connections {
        target: MediaVM

        function onInstructionReceived(instruction) {
            serverInstruction = instruction
            errorMessage = ""
            progressHint = "Подивіться прямо в камеру, потім: " + instruction
            step = 3 // Режим виконання завдання
        }

        function onCheckResult(passed, message) {
            if (passed) {
                step = 5 // Успіх
                errorMessage = ""
                progressHint = "✅ Верифікацію успішно пройдено! Переходимо до іспиту..."
                autoProceedTimer.start()
            } else {
                if (step === 4) {
                    step = 3
                }
                progressHint = message
            }
        }

        function onProgressUpdated(hint) {
            progressHint = hint
        }
    }

    Component.onCompleted: {
        mainWindow.turnOnCamera()
        globalCaptureSession.videoOutput = videoOutput

        let camName = globalMediaDevices.defaultVideoInput.description
        if (MediaVM.isVirtualCamera(camName)) {
            step = 1
        } else {
            step = 2
            MediaVM.setVideoSink(videoOutput.videoSink)
            MediaVM.startVerificationSession()
        }
    }

    ColumnLayout {
        anchors.centerIn: parent
        width: 600
        spacing: 15

        Label {
            text: "🎥 Identity Verification"
            font.pixelSize: 24
            font.bold: true
            color: "#0277BD"
            Layout.alignment: Qt.AlignHCenter
        }

        // Блок з відео
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 350
            color: "black"
            radius: 8
            clip: true

            VideoOutput {
                id: videoOutput
                anchors.fill: parent
                fillMode: VideoOutput.PreserveAspectCrop
                visible: step !== 1 // Приховуємо, якщо знайдена віртуальна камера
            }

            // Оверлей з інструкцією або статусом
            Rectangle {
                anchors.bottom: parent.bottom
                anchors.left: parent.left
                anchors.right: parent.right
                height: 60
                color: "#CC000000" // Напівпрозорий чорний
                visible: step === 3 || step === 4

                Label {
                    anchors.centerIn: parent
                    text: step === 3 ? "📝 Завдання: " + serverInstruction : "⏳ Нейромережа аналізує кадр..."
                    color: step === 3 ? "#FFEB3B" : "white"
                    font.pixelSize: 16
                    font.bold: true
                }
            }
        }

        // Банер помилки верифікації
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: visible ? 60 : 0
            visible: step === 6
            color: "#FFEBEE"
            border.color: "#EF9A9A"
            radius: 8
            clip: true

            RowLayout {
                anchors.fill: parent
                anchors.margins: 12
                spacing: 10

                Text {
                    text: "✕"
                    font.pixelSize: 18
                    font.bold: true
                    color: "#C62828"
                }

                Text {
                    Layout.fillWidth: true
                    text: errorMessage
                    color: "#B71C1C"
                    font.pixelSize: 14
                    wrapMode: Text.WordWrap
                }

                // Кнопка повторної спроби
                Text {
                    text: "Спробувати ще раз"
                    color: "#1565C0"
                    font.pixelSize: 14
                    font.underline: true
                    font.bold: true

                    MouseArea {
                        anchors.fill: parent
                        cursorShape: Qt.PointingHandCursor
                        onClicked: {
                            step = 2
                            errorMessage = ""
                            MediaVM.startVerificationSession() // Запит нового челенджу від бекенду
                        }
                    }
                }
            }
        }

        // Попередження про віртуальну камеру
        Label {
            visible: step === 1
            text: "❌ Virtual cameras (OBS, ManyCam) are strictly prohibited!"
            color: "#D32F2F"
            font.bold: true
            Layout.alignment: Qt.AlignHCenter
        }

        // Банер підказки (progress hint)
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: visible ? 50 : 0
            visible: progressHint.length > 0 && step !== 1
            color: step === 5 ? "#E8F5E9" : "#FFF8E1"
            border.color: step === 5 ? "#81C784" : "#FFD54F"
            radius: 8
            clip: true

            Label {
                anchors.centerIn: parent
                text: (step === 5 ? "" : "💡 ") + progressHint
                color: step === 5 ? "#2E7D32" : "#F57F17"
                font.pixelSize: 14
                font.bold: true
            }
        }

        // Головна кнопка управління
        Button {
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            highlighted: true
            visible: step !== 1 && step !== 6

            text: {
                if (step === 0) return "Connect to proctoring server"
                if (step === 2) return "Fetching task..."
                if (step === 3) return "I have completed the task"
                if (step === 4) return "Verifying identity & liveness..."
                if (step === 5) return "✅ Start Exam"
                return ""
            }

            enabled: step === 0 || step === 3 || step === 5

            background: Rectangle {
                color: parent.enabled ? (step === 5 ? "#2E7D32" : "#0288D1") : "#BDBDBD"
                radius: 5
            }

            contentItem: Text {
                text: parent.text
                font.bold: true
                color: "white"
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
            }

            onClicked: {
                if (step === 0) {
                    step = 2
                    MediaVM.startVerificationSession() // Новий метод, що ініціює подвійну перевірку
                } else if (step === 3) {
                    step = 4
                    MediaVM.simulateUserActionCompleted()
                } else if (step === 5) {
                    mediaPage.mediaCheckPassed()
                }
            }
        }
    }
}