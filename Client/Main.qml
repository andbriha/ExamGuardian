import QtQuick
import QtQuick.Controls
import QtQuick.Window

ApplicationWindow {
    id: mainWindow
    visible: true
    width: 1024
    height: 768
    visibility: Window.Windowed
    title: "ExamGuardian"

    StackView {
        id: stackView
        anchors.fill: parent
        initialItem: LoginScreen {
            onLoginSuccess: stackView.push(processCheckComponent)
        }
    }

    function enterExamMode() {
            Proctoring.setKeyBlock(true)
            mainWindow.flags = Qt.Window | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint
            mainWindow.showFullScreen()
            console.log("Exam Mode Activated: Full Screen & Keys Blocked")
        }

        function exitExamMode() {
            Proctoring.setKeyBlock(false)
            mainWindow.flags = Qt.Window
            mainWindow.showNormal()
            mainWindow.width = 1024
            mainWindow.height = 768
            console.log("Exam finished: keys unblocked")
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
        Page {
            Button {
                anchors.centerIn: parent
                text: "Finish Exam"
                onClicked: {
                    mainWindow.exitExamMode()
                    Qt.quit()
                }
            }
        }
    }
}
