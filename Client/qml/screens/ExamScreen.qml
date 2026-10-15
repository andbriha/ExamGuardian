import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: root
    background: Rectangle { color: "#F5F7FA" }
    signal examFinished()

    // Дані завантажуються через ExamVM
    property bool isLoaded:       false
    property var  questions:      ExamVM.questions
    property int  currentIndex:   0
    property int  totalSeconds:   ExamVM.durationSeconds
    property var  answers:        ({})  // { questionId: selectedIndex }

    // Таймер
    Timer {
        id: examTimer
        interval: 1000
        repeat:   true
        running:  isLoaded && root.totalSeconds > 0
        onTriggered: {
            root.totalSeconds = ExamVM.durationSeconds
            if (root.totalSeconds <= 0) submitExam()
        }
    }

    function formatTime(secs) {
        let m = Math.floor(secs / 60)
        let s = secs % 60
        return String(m).padStart(2, "0") + ":" + String(s).padStart(2, "0")
    }

    property bool resultSuccess: false
    property string saveStatus: ""
    property string loadError: ""
    property string resultMessage: ""

    function submitExam() {
        examTimer.running = false
        ExamVM.submitAnswers(root.answers)
    }

    Connections {
        target: ExamVM
        function onExamLoaded() {
            root.questions    = ExamVM.questions
            root.totalSeconds = ExamVM.durationSeconds
            root.answers = ExamVM.savedAnswers
            root.isLoaded     = true
            if (root.totalSeconds > 0) mainWindow.enterExamMode()
            else submitExam()
        }
        function onSubmitFinished(success, message) {
            resultSuccess = success
            if (success) mainWindow.exitExamMode()
            resultMessage = message
            resultDialog.open()
        }
        function onExamLoadFailed(message) { root.loadError = message }
        function onAnswersSaved(success) { root.saveStatus = success ? "Відповіді збережено на сервері" : "Немає підтвердження збереження. Перевірте з’єднання." }
        function onSessionTerminated() { mainWindow.exitExamMode(); resultSuccess = true; resultMessage = "Спробу завершено сервером або викладачем."; resultDialog.open() }
        function onProhibitedProcessAlert(processName) {
            root.prohibitedAppAlert = "⚠️ УВАГА: Виявлено роботу забороненої програми (" + processName + ")! Спостереження потребує перевірки викладача. Стан доставки показано окремо."
            appAlertTimer.restart()
        }
    }

    property string prohibitedAppAlert: ""

    Timer {
        id: appAlertTimer
        interval: 7000
        onTriggered: root.prohibitedAppAlert = ""
    }

    Timer { interval: 5000; repeat: true; running: root.isLoaded && mainWindow.isExamActive; onTriggered: { ExamVM.saveAnswers(root.answers); ExamVM.heartbeat() } }
    Component.onCompleted: {
        ExamVM.loadExam()
    }

    ColumnLayout {
        enabled: !ProcessVM.virtualizationBlocked
        visible: !ProcessVM.virtualizationBlocked
        anchors.fill: parent
        anchors.margins: 40
        spacing: 16

        // Попередження про виявлену заборонену програму (Discord, Telegram тощо)
        Rectangle {
            Layout.fillWidth: true
            height: 44
            visible: root.prohibitedAppAlert.length > 0
            color: "#D32F2F"
            radius: 8

            RowLayout {
                anchors.fill: parent
                anchors.margins: 12
                spacing: 8
                Text { text: "🛑"; font.pixelSize: 16 }
                Label {
                    text: root.prohibitedAppAlert
                    color: "white"
                    font.bold: true
                    font.pixelSize: 13
                    Layout.fillWidth: true
                }
            }
        }

        Label { text: root.loadError; visible: text.length > 0; color: "#b91c1c"; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Button { text: "Повторити підключення"; visible: root.loadError.length > 0; onClicked: { root.loadError = ""; ExamVM.loadExam() } }
        Label { text: root.saveStatus; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        // Топ-панель
        Rectangle {
            Layout.fillWidth: true
            height: 60
            color: "white"
            radius: 8
            border.color: "#E2E8F0"

            RowLayout {
                anchors.fill:    parent
                anchors.margins: 15
                spacing: 15

                Label {
                    text: ExamVM.examTitle
                    font.pixelSize: 16
                    font.bold: true
                    color: "#2D3748"
                    elide: Text.ElideRight
                    Layout.fillWidth: true
                }

                Label {
                    text: root.formatTime(root.totalSeconds)
                    font.pixelSize: 18
                    font.bold: true
                    color: root.totalSeconds < 300 ? "#E53E3E" : "#2D3748"
                }

                Button {
                    text: "Завершити іспит"
                    palette.button: "#E53E3E"
                    palette.buttonText: "white"
                    onClicked: confirmDialog.open()
                }
            }
        }

        Label { text: mainWindow.analysisStatus; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: mainWindow.technicalStatus; visible: text.length > 0; color: "#b91c1c"; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: mainWindow.uploadStatus; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Button { text: "Закрити клієнт"; visible: root.resultSuccess && !mainWindow.isExamActive; enabled: mainWindow.pendingVideoUploads === 0; onClicked: Qt.quit() }
        Button { text: "Вийти зі збереженою чергою на цьому ПК"; visible: root.resultSuccess && !mainWindow.isExamActive && mainWindow.pendingVideoUploads > 0; onClicked: Qt.quit() }
        // Лічильник питань
        Label {
            text: "Питання " + (root.currentIndex + 1) +
                  " з " + root.questions.length
            font.pixelSize: 13
            color: "#718096"
        }

        // Блок питання
        Rectangle {
            Layout.fillWidth: true
            height: questionLabel.implicitHeight + 40
            color: "white"
            radius: 8
            border.color: "#E2E8F0"

            Label {
                id: questionLabel
                textFormat: Text.PlainText
                anchors { fill: parent; margins: 20 }
                text: root.questions.length > 0
                      ? root.questions[root.currentIndex].text
                      : "Завантаження..."
                font.pixelSize: 16
                wrapMode: Text.WordWrap
                color: "#2D3748"
            }
        }

        // Варіанти відповідей
        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: "transparent"

            Column {
                width: parent.width * 0.75
                spacing: 12

                Repeater {
                    model: root.questions.length > 0
                           ? root.questions[root.currentIndex].options
                           : []

                    delegate: RadioDelegate {
                        enabled: mainWindow.isExamActive && !root.resultSuccess
                        width:         parent.width
                        text:          modelData.text
                        font.pixelSize: 15
                        checked: {
                            if (!root.questions || root.questions.length <= root.currentIndex) return false;
                            let q = root.questions[root.currentIndex];
                            return q && root.answers[q.id] === modelData.orderIndex;
                        }
                        onClicked: {
                            if (!root.questions || root.questions.length <= root.currentIndex) return;
                            let q = root.questions[root.currentIndex];
                            if (!q) return;
                            let updated = Object.assign({}, root.answers);
                            updated[q.id] = modelData.orderIndex;
                            root.answers = updated;
                            ExamVM.saveAnswers(root.answers);
                        }
                    }
                }
            }
        }

        // Навігація
        RowLayout {
            Layout.fillWidth: true

            Button {
                text: "← Назад"
                enabled: root.currentIndex > 0
                onClicked: root.currentIndex--
            }

            Item { Layout.fillWidth: true }

            Label {
                text: Object.keys(root.answers).length +
                      " / " + root.questions.length + " відповідей"
                color: "#718096"
                font.pixelSize: 14
                font.bold: true
            }

            Item { Layout.fillWidth: true }

            Button {
                text: "Далі →"
                visible: root.currentIndex < root.questions.length - 1
                highlighted: true
                onClicked: root.currentIndex++
            }

            Button {
                text: "Здати іспит ✓"
                visible: root.currentIndex >= root.questions.length - 1
                palette.button: "#2E7D32"
                palette.buttonText: "white"
                highlighted: true
                onClicked: confirmDialog.open()
            }
        }
    }

    Dialog {
        id: confirmDialog
        anchors.centerIn: parent
        title: "Завершити іспит?"
        modal: true
        standardButtons: Dialog.Yes | Dialog.No
        Label {
            text: "Відповіли на " + Object.keys(root.answers).length +
                  " з " + root.questions.length + " питань.\nПродовжити?"
            wrapMode: Text.WordWrap
        }
        onAccepted: root.submitExam()
    }

    Dialog {
        id: resultDialog
        anchors.centerIn: parent
        width: 420
        title: "Результати тестування"
        modal: true
        closePolicy: Popup.NoAutoClose
        standardButtons: Dialog.Ok

        ColumnLayout {
            spacing: 15
            anchors.fill: parent

            Label {
                text: "📋 Підсумок"
                font.pixelSize: 18
                font.bold: true
                color: "#2E7D32"
            }

            Label {
                text: root.resultMessage
                font.pixelSize: 16
                wrapMode: Text.WordWrap
                Layout.fillWidth: true
            }

            Label {
                text: "Стан передачі відеоматеріалів буде показано окремо після завершення."
                font.pixelSize: 12
                color: "#718096"
                wrapMode: Text.WordWrap
                Layout.fillWidth: true
            }
        }

        onAccepted: { if (root.resultSuccess) root.examFinished(); else if (root.totalSeconds > 0) examTimer.running = true }
    }
    Rectangle {
        anchors.fill: parent
        z: 100
        color: "#FFF1F2"
        visible: root.isLoaded && ProcessVM.virtualizationBlocked && !root.resultSuccess
        MouseArea { anchors.fill: parent }
        ColumnLayout {
            anchors.centerIn: parent
            width: Math.min(parent.width - 48, 720)
            spacing: 20
            Label {
                text: "Виконання завдань заблоковано: віртуалізація"
                font.pixelSize: 22
                font.bold: true
                color: "#991B1B"
                wrapMode: Text.WordWrap
                Layout.fillWidth: true
            }
            Label {
                text: "Виявлено активний гіпервізор, контейнерне середовище або неможливо завершити перевірку. Закрийте відповідне ПЗ та служби. Перевірка повторюється автоматично. Час іспиту та запис продовжуються."
                wrapMode: Text.WordWrap
                Layout.fillWidth: true
            }
            ScrollView {
                Layout.fillWidth: true
                Layout.preferredHeight: 150
                clip: true
                TextArea { text: ProcessVM.vmDetails; readOnly: true; wrapMode: TextEdit.Wrap; selectByMouse: true }
            }
            Button { text: "Завершити іспит і надіслати збережені відповіді"; onClicked: root.submitExam() }
        }
    }

}
