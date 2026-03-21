import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtMultimedia

Page {
    id: mediaPage
    signal mediaCheckPassed()

    background: Rectangle { color: "white" }

    // Screen states
    // 0: Initialization, 1: Virtual camera (error), 2: Waiting for server, 3: Executing task, 4: Server verification, 5: Success
    property int step: 0
    property string serverInstruction: ""

    MediaDevices {
        id: mediaDevices
    }

    CaptureSession {
        id: captureSession
        camera: Camera {
            id: camera
            cameraDevice: mediaDevices.defaultVideoInput
            active: true
        }
        audioInput: AudioInput {
            id: audioInput
            device: mediaDevices.defaultAudioInput
            muted: false
        }
        videoOutput: videoOutput
    }

    // Listening to signals from C++ backend (our stubs)
    Connections {
        target: Proctoring

        function onLivenessInstructionReceived(instruction) {
            serverInstruction = instruction
            step = 3 // Transition to task execution mode
        }

        function onLivenessCheckResult(passed, message) {
            if (passed) {
                step = 5 // Success
            } else {
                serverInstruction = "Verification error: " + message
                step = 3 // Let the user try again
            }
        }
    }

    Component.onCompleted: {
        let camName = mediaDevices.defaultVideoInput.description
        if (Proctoring.isVirtualCamera(camName)) {
            step = 1 // Blocked: virtual camera detected
        } else {
            step = 0 // All good, ready to start verification
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

        // Video block
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
                visible: step !== 1 // Hide video if it's a virtual camera
            }

            Rectangle {
                anchors.bottom: parent.bottom
                anchors.left: parent.left
                anchors.right: parent.right
                height: 60
                color: "#CC000000" // Semi-transparent black
                visible: step === 3 || step === 4

                Label {
                    anchors.centerIn: parent
                    text: step === 3 ? "📝 Task: " + serverInstruction : "⏳ Neural network is analyzing video..."
                    color: step === 3 ? "#FFEB3B" : "white"
                    font.pixelSize: 16
                    font.bold: true
                }
            }
        }

        // Technical camera info (Hardware ID)
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 60
            color: step === 1 ? "#FFEBEE" : "#F5F5F5"
            border.color: step === 1 ? "#D32F2F" : "#E0E0E0"
            radius: 8

            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 10
                spacing: 2

                Label {
                    text: "<b>Device:</b> " + (mediaDevices.defaultVideoInput.description || "Not found")
                    color: step === 1 ? "#D32F2F" : "#333"
                }
                Label {
                    // Display system camera ID
                    text: "<b>Hardware ID:</b> " + mediaDevices.defaultVideoInput.id
                    font.pixelSize: 11
                    color: "#757575"
                }
            }
        }

        Label {
            visible: step === 1
            text: "❌ Virtual cameras (OBS, ManyCam) are strictly prohibited!"
            color: "#D32F2F"
            font.bold: true
            Layout.alignment: Qt.AlignHCenter
        }

        Button {
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            highlighted: true
            visible: step !== 1

            text: {
                if (step === 0) return "Connect to proctoring server"
                if (step === 2) return "Connecting..."
                if (step === 3) return "I have completed the task"
                if (step === 4) return "Verifying..."
                if (step === 5) return "✅ Start Exam"
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
                    step = 2 // Waiting for server
                    Proctoring.startLivenessSession()
                } else if (step === 3) {
                    step = 4 // Sent, waiting for analysis
                    Proctoring.simulateUserActionCompleted()
                } else if (step === 5) {
                    // Success! Proceeding to exam
                    camera.active = false
                    mediaPage.mediaCheckPassed()
                }
            }
        }
    }

    Component.onDestruction: {
        camera.active = false
    }
}
