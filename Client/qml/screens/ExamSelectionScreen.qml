import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    signal examSelected()
    property string errorText: ""
    Component.onCompleted: ExamVM.loadAvailableExams()
    Connections {
        target: ExamVM
        function onExamLoadFailed(message) { errorText = message }
    }
    ColumnLayout {
        anchors.centerIn: parent
        width: Math.min(parent.width - 48, 560)
        spacing: 20
        Label { text: "Оберіть іспит"; font.pixelSize: 26 }
        Label { text: "Перевірка особи та спроба будуть пов’язані з обраним іспитом."; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        ComboBox {
            id: exams
            Layout.fillWidth: true
            model: ExamVM.availableExams
            textRole: "title"
            valueRole: "id"
        }
        Label {
            Layout.fillWidth: true; wrapMode: Text.WordWrap
            text: exams.currentIndex >= 0 ? "Тривалість: " + ExamVM.availableExams[exams.currentIndex].durationMinutes + " хв. Прохідний бал: " + ExamVM.availableExams[exams.currentIndex].passingScore + "/100. Мовлення: " + (ExamVM.availableExams[exams.currentIndex].allowSpeech ? "дозволено" : "позначається для перевірки") + "." : ""
        }
        Label { visible: exams.count === 0; text: "Доступних іспитів немає. Оновіть список або зверніться до викладача."; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: errorText; visible: text.length > 0; color: "#b91c1c"; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Button { text: "Оновити список"; onClicked: ExamVM.loadAvailableExams() }
        Button {
            text: "Продовжити"; enabled: exams.currentIndex >= 0; highlighted: true
            onClicked: { ExamVM.selectExam(exams.currentValue); MediaVM.setExamId(exams.currentValue); examSelected() }
        }
    }
}
