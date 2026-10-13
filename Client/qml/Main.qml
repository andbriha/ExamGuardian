import QtQuick
import QtQuick.Controls
import QtQuick.Window
import QtMultimedia

ApplicationWindow {
    id: mainWindow
    visible: true
    width: 1024
    height: 768
    visibility: Window.Windowed
    title: "Online Proctoring System"

    // --- ГЛОБАЛЬНАЯ КАМЕРА И ЗАПИСЬ ---
        property string currentVideoPath: ""
        property int currentChunkIndex: 1
        property bool isExamActive: false

        MediaDevices { id: globalMediaDevices }

        Timer {
            id: chunkTimer
            interval: 60000 // 60 000 мс = 1 минута (для тестов. Потом поставишь 180000 для 3 минут)
            repeat: true
            running: isExamActive
            onTriggered: {
                console.log("[SYSTEM] Time limit reached. Cutting video chunk...")
                // Останавливаем текущую запись. Это автоматически вызовет onRecorderStateChanged!
                globalRecorder.stop()
            }
        }

        Timer {
            id: processMonitorTimer
            interval: 5000 // Перевіряємо кожні 5 сек чи запущено Discord/Telegram/тощо
            repeat: true
            running: isExamActive
            onTriggered: {
                let apps = ProcessVM.getSuspiciousProcesses()
                if (apps && apps.length > 0) {
                    console.log("[SYSTEM] Prohibited application detected during exam:", apps[0])
                    ExamVM.reportProhibitedProcess(apps[0])
                }
            }
        }
        Connections {
                target: MediaVM
                function onVideoUploadFinished() {
                    if (!isExamActive) {
                        console.log("[SYSTEM] Final upload complete. Shutting down.")
                        Qt.quit()
                    }
                }
            }
        CaptureSession {
            id: globalCaptureSession
            camera: Camera {
                id: globalCamera
                cameraDevice: globalMediaDevices.defaultVideoInput
                active: false
            }
            audioInput: AudioInput {
                device: globalMediaDevices.defaultAudioInput
                muted: false
            }
            recorder: MediaRecorder {
                id: globalRecorder
                onRecorderStateChanged: {
                    if (recorderState === MediaRecorder.StoppedState && currentVideoPath !== "") {
                        console.log("[SYSTEM] Video chunk saved. Sending chunk #" + currentChunkIndex)

                        // 1. Отправляем готовый кусок на сервер в фоне
                        let cleanPath = currentVideoPath.replace("file:///", "")
                        MediaVM.uploadExamVideoChunk(cleanPath, currentChunkIndex)

                        // 2. Если экзамен продолжается -> мгновенно начинаем писать новый кусок!
                        if (isExamActive) {
                            currentChunkIndex++
                            currentVideoPath = MediaVM.generateTempVideoPath()
                            globalRecorder.outputLocation = currentVideoPath
                            globalRecorder.record()
                        }
                    }
                }
            }
        }

        function turnOnCamera() {
            globalCamera.active = true
        }

        function turnOffCamera() {
            globalCamera.active = false
        }
    StackView {
        id: stackView
        anchors.fill: parent
        initialItem: LoginScreen {
            onLoginSuccess: stackView.push(processCheckComponent)
        }
    }

    function enterExamMode() {
            ExamVM.setKeyBlock(true)
            MediaVM.startContinuousProctoring()

            isExamActive = true // Запускаем логику нарезки
            currentChunkIndex = 1

            currentVideoPath = MediaVM.generateTempVideoPath()
            globalRecorder.outputLocation = currentVideoPath
            globalRecorder.record()

            mainWindow.flags = Qt.Window | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint
            mainWindow.showFullScreen()
            console.log("Exam Mode Activated: Recording started")
        }

        function exitExamMode() {
            ExamVM.setKeyBlock(false)
            MediaVM.stopContinuousProctoring()

            isExamActive = false // Останавливаем логику нарезки


            globalRecorder.stop()

            turnOffCamera()

            mainWindow.flags = Qt.Window
            mainWindow.showNormal()
            mainWindow.width = 1024
            mainWindow.height = 768
            console.log("Exam finished: Final chunk sending, Camera off")
        }
    Component {
        id: processCheckComponent
        ProcessCheckScreen {
            onProceedToExam: function(detectedApps) {
                stackView.push(mediaCheckComponent)
            }
        }
    }

    Component {
        id: mediaCheckComponent
        MediaCheckScreen {
            onMediaCheckPassed: {
                mainWindow.enterExamMode()
                stackView.push(examComponent)
            }
        }
    }

    Component {
            id: examComponent
            ExamScreen {
                onExamFinished: {
                    mainWindow.exitExamMode()
                }
            }
    }

}
