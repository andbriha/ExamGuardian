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
    // 5: Success
    // 6: Error state (requires retry)
    property string audioStatus: "Мікрофон: очікування"
    property int step: 0
    property int actionNumber: 0
    property string serverInstruction: ""
    property string verificationPhase: "prepare"
    property string errorMessage: ""
    property string progressHint: "Подивіться прямо в камеру"

    Timer {
        id: autoProceedTimer
        interval: 1200
        repeat: false
        running: false
        onTriggered: {
            if (mediaPage.StackView.status === StackView.Active) mediaPage.mediaCheckPassed()
        }
    }

    Connections {
        enabled: mediaPage.StackView.status === StackView.Active || mediaPage.StackView.status === StackView.Activating
        target: MediaVM
        function onVerificationAudioStatus(message) { audioStatus = message }

        function onVerificationError(message) { step = 6; errorMessage = message; progressHint = "" }
        function onVerificationPhaseChanged(phase) { verificationPhase = phase }
        function onInstructionReceived(instruction) {
            verificationPhase = "prepare"
            actionNumber += 1
            serverInstruction = instruction
            errorMessage = ""
            progressHint = "Спочатку дивіться прямо та розслабте обличчя. Дочекайтеся підказки."
            step = 3 // Режим виконання завдання
        }

        function onCheckResult(passed, message) {
            if (passed) {
                step = 5 // Успіх
                errorMessage = ""
                progressHint = message + " Переходимо до іспиту..."
                autoProceedTimer.start()
            } else {
                progressHint = message
            }
        }

        function onProgressUpdated(hint) {
            progressHint = hint
        }
    }

    Component.onDestruction: MediaVM.stopVerificationSession()

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
                visible: step === 3

                Label {
                    anchors.centerIn: parent
                    width: parent.width - 24
                    wrapMode: Text.WordWrap
                    horizontalAlignment: Text.AlignHCenter
                    text: "📝 Дія " + actionNumber + "/3: " + (verificationPhase === "prepare" ? "Спочатку дивіться прямо в камеру" : verificationPhase === "return" ? "Поверніться прямо та розслабте обличчя" : serverInstruction)
                    color: "#FFEB3B"
                    font.pixelSize: 16
                    font.bold: true
                }
            }
        }

        // Банер помилки верифікації
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: visible ? Math.max(76, errorDetails.implicitHeight + 24) : 0
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
                    id: errorDetails
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
                            actionNumber = 0
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
            Layout.preferredHeight: visible ? 76 : 0
            visible: progressHint.length > 0 && step !== 1
            color: step === 5 ? "#E8F5E9" : "#FFF8E1"
            border.color: step === 5 ? "#81C784" : "#FFD54F"
            radius: 8
            clip: true

            Label {
                anchors.centerIn: parent
                width: parent.width - 24
                wrapMode: Text.WordWrap
                horizontalAlignment: Text.AlignHCenter
                text: (step === 5 ? "" : "💡 ") + progressHint
                color: step === 5 ? "#2E7D32" : "#F57F17"
                font.pixelSize: 14
                font.bold: true
            }
        }

        Label {
            Layout.fillWidth: true
            text: step === 2 ? "Підключаємо перевірку…" : "Кадри та звук аналізуються автоматично. Натискати нічого не потрібно."
            wrapMode: Text.WordWrap
            horizontalAlignment: Text.AlignHCenter
            color: "#546E7A"
            visible: step === 2 || step === 3
        }
        Label {
            Layout.fillWidth: true
            text: mediaPage.audioStatus
            wrapMode: Text.WordWrap
            horizontalAlignment: Text.AlignHCenter
            color: "#546E7A"
            visible: step === 3
        }
    }
}
