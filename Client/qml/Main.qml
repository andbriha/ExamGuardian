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
    title: "ExamGuardian"
    function bypass(control) { return typeof DevVM !== "undefined" && DevVM.active.indexOf(control)>=0 }
    onActiveChanged: { if (isExamActive && !active && !bypass("focusEnforcement")) ExamVM.reportFocusLoss() }

    property int pendingVideoUploads: 0
    property string uploadStatus: ""
    property bool recordingPrepared: false
    property string technicalStatus: ""
    property string lastEnvironmentObservation: ""
    property string analysisStatus: "Очікування аналізу"
    property string screenVideoPath: ""
    property double cameraChunkStart: 0
    property double screenChunkStart: 0
    property bool cameraStopped: false
    property bool screenStopped: false
    property bool finalizationQueued: false
    property bool cameraFault: false
    property bool screenFault: false
    Connections {
        target: MediaVM
        function onEvidenceFinalized(status) { uploadStatus = status === "Delivered" ? "Передачу записів підтверджено. Висновок надасть викладач." : "Докази доставлено частково. Потрібна перевірка викладача." }
        function onAnalysisStatusChanged(message) { analysisStatus = message }
        function onPendingUploadsChanged(count) { pendingVideoUploads = count; uploadStatus = count === 0 ? "Черга передачі відео порожня" : "Матеріалів у черзі: " + count }
        function onVideoUploadFailed(message) { uploadStatus = message }
        function onRecordingReady(nextIndex) { mainWindow.startRecording(nextIndex) }
    }
    // --- ГЛОБАЛЬНАЯ КАМЕРА И ЗАПИСЬ ---
        property string currentVideoPath: ""
        property int currentChunkIndex: 1
        property bool isExamActive: false

        MediaDevices { id: globalMediaDevices }
        AudioInput { id: examAudioInput; device: globalMediaDevices.defaultAudioInput; muted: false }

        Timer {
            id: chunkTimer
            interval: 60000 // 60 000 мс = 1 минута (для тестов. Потом поставишь 180000 для 3 минут)
            repeat: true
            running: isExamActive
            onTriggered: {
                console.log("[SYSTEM] Time limit reached. Cutting video chunk...")
                // Останавливаем текущую запись. Это автоматически вызовет onRecorderStateChanged!
                globalRecorder.stop()
                screenRecorder.stop()
            }
        }

        Timer {
            id: processMonitorTimer
            interval: 5000 // Перевіряємо кожні 5 сек чи запущено Discord/Telegram/тощо
            repeat: true
            running: isExamActive
            onTriggered: {
                ProcessVM.runFullScan()
                let observation = JSON.stringify({spoofed:ProcessVM.spoofedProcesses,cameras:ProcessVM.virtualCameras,monitors:ProcessVM.multipleMonitors,vm:ProcessVM.isVmDetected})
                if (observation !== lastEnvironmentObservation) {
                    if (ProcessVM.multipleMonitors) ExamVM.reportEnvironmentObservation("multiple_monitors","Multiple displays detected")
                    if (ProcessVM.virtualCameras.length) ExamVM.reportEnvironmentObservation("virtual_camera",ProcessVM.virtualCameras.join(", "))
                    if (ProcessVM.spoofedProcesses.length) ExamVM.reportEnvironmentObservation("spoofed_process",ProcessVM.spoofedProcesses.join(", "))
                    if (ProcessVM.isVmDetected) ExamVM.reportEnvironmentObservation("virtual_machine",ProcessVM.vmDetails)
                    lastEnvironmentObservation=observation
                }
                let apps = ProcessVM.suspiciousApps
                if (apps && apps.length > 0) {
                    console.log("[SYSTEM] Prohibited application detected during exam:", apps[0])
                    ExamVM.reportProhibitedProcess(apps[0])
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
            audioInput: examAudioInput
            recorder: MediaRecorder {
                id: globalRecorder
                onErrorOccurred: function(error, errorString) { cameraFault = true; mainWindow.technicalStatus = "Запис камери не підтверджено: " + errorString }
                onRecorderStateChanged: {
                    if (recorderState === MediaRecorder.StoppedState && currentVideoPath !== "") {
                        console.log("[SYSTEM] Video chunk saved. Sending chunk #" + currentChunkIndex)

                        // 1. Отправляем готовый кусок на сервер в фоне
                        let cleanPath = currentVideoPath.replace("file:///", "")
                        MediaVM.uploadExamVideoChunk(currentVideoPath, currentChunkIndex, "camera", cameraChunkStart, globalRecorder.duration / 1000.0)

                        // 2. Если экзамен продолжается -> мгновенно начинаем писать новый кусок!
                        if (isExamActive && !cameraFault) {
                            currentChunkIndex++
                            cameraChunkStart = ExamVM.elapsedSeconds()
                            currentVideoPath = MediaVM.beginVideoChunk("camera", currentChunkIndex, cameraChunkStart)
                            globalRecorder.outputLocation = currentVideoPath
                            globalRecorder.record()
                        } else { cameraStopped = true; maybeFinalizeEvidence() }
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
    CaptureSession {
        screenCapture: ScreenCapture {
            id: screenCapture
            screen: mainWindow.screen
            active: mainWindow.isExamActive
            onErrorOccurred: function(error, errorString) { screenFault = true; mainWindow.technicalStatus = "Запис екрана недоступний: " + errorString }
        }
        recorder: MediaRecorder {
            id: screenRecorder
            videoFrameRate: 5
            onErrorOccurred: function(error, errorString) { screenFault = true; mainWindow.technicalStatus = "Запис екрана не підтверджено: " + errorString }
            onRecorderStateChanged: {
                if (recorderState !== MediaRecorder.StoppedState || screenVideoPath === "") return
                MediaVM.uploadExamVideoChunk(screenVideoPath, screenChunkIndex, "screen", screenChunkStart, screenRecorder.duration / 1000.0)
                if (isExamActive && !screenFault) {
                    screenChunkIndex++
                    screenChunkStart = ExamVM.elapsedSeconds()
                    screenVideoPath = MediaVM.beginVideoChunk("screen", screenChunkIndex, screenChunkStart)
                    screenRecorder.outputLocation = screenVideoPath
                    screenRecorder.record()
                } else { screenStopped = true; maybeFinalizeEvidence() }
            }
        }
    }
    property int screenChunkIndex: 1
    function maybeFinalizeEvidence() {
        if (!isExamActive && cameraStopped && screenStopped && !finalizationQueued) {
            finalizationQueued = true
            MediaVM.finalizeEvidence(currentChunkIndex, screenChunkIndex)
        }
    }
    StackView {
        id: stackView
        anchors.fill: parent
        initialItem: LoginScreen {
            onLoginSuccess: stackView.push(examSelectionComponent)
        }
    }

    function enterExamMode() {
        if (!recordingPrepared) MediaVM.prepareRecording()
    }
    function startRecording(nextIndex) {
        if (isExamActive) return
        ProcessVM.runFullScan()
        if (!ExamVM.setKeyBlock(true)) return
        recordingPrepared = true
        MediaVM.startContinuousProctoring(Math.floor(ExamVM.elapsedSeconds()))
        isExamActive = true
        currentChunkIndex = nextIndex
        cameraChunkStart = ExamVM.elapsedSeconds()
        currentVideoPath = MediaVM.beginVideoChunk("camera", currentChunkIndex, cameraChunkStart)
        screenChunkIndex = nextIndex
        screenChunkStart = ExamVM.elapsedSeconds()
        screenVideoPath = MediaVM.beginVideoChunk("screen", screenChunkIndex, screenChunkStart)
        if (currentVideoPath === "" || screenVideoPath === "") { technicalStatus = "Запис неможливий: перевірте вільне місце та права запису."; exitExamMode(); return }
        screenRecorder.outputLocation = screenVideoPath
        screenRecorder.record()
        globalRecorder.outputLocation = currentVideoPath
        globalRecorder.record()
        if (!bypass("focusEnforcement")) {
            mainWindow.flags = Qt.Window | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint
            mainWindow.showFullScreen()
        }
    }

        function exitExamMode() {
            if (!isExamActive) return
            ExamVM.setKeyBlock(false)
            MediaVM.stopContinuousProctoring()

            isExamActive = false // Останавливаем логику нарезки


            globalRecorder.stop()
            screenRecorder.stop()

            if (globalRecorder.recorderState === MediaRecorder.StoppedState) cameraStopped = true
            if (screenRecorder.recorderState === MediaRecorder.StoppedState) screenStopped = true
            maybeFinalizeEvidence()
            turnOffCamera()

            mainWindow.flags = Qt.Window
            mainWindow.showNormal()
            mainWindow.width = 1024
            mainWindow.height = 768
            console.log("Exam finished: Final chunk sending, Camera off")
        }
    Component {
        id: examSelectionComponent
        ExamSelectionScreen { onExamSelected: stackView.push(processCheckComponent) }
    }
    Component {
        id: processCheckComponent
        ProcessCheckScreen {
            onProceedToExam: function(detectedApps) {
                stackView.push(agreementComponent)
            }
        }
    }

    Component {
        id: agreementComponent
        AgreementScreen { onAccepted: stackView.push(mediaCheckComponent) }
    }

    Component {
        id: mediaCheckComponent
        MediaCheckScreen {
            onMediaCheckPassed: {
                ProcessVM.runFullScan()
                if (ProcessVM.virtualizationBlocked) { stackView.push(processCheckComponent); return }
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
