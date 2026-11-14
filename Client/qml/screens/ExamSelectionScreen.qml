import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: page
    signal examSelected()
    property string errorText: ""
    property bool loaded: false
    property int chosenId: 0
    property double nowMs: ExamVM.catalogTimeMs()
    property string updatedAt: ""
    property var chosenExam: {
        for (let exam of ExamVM.availableExams) if (exam.id === chosenId) return exam
        return null
    }
    function startMs(exam) { return Date.parse(exam.scheduledStartTime) }
    function endMs(exam) { return Date.parse(exam.scheduledEndTime) }
    function canStart(exam) { return !!exam && nowMs >= startMs(exam) && nowMs < endMs(exam) && !errorText }
    function dateText(value) { return Qt.formatDateTime(new Date(value), "dd.MM.yyyy HH:mm t") }
    function statusText(exam) {
        if (nowMs < startMs(exam)) {
            let minutes = Math.ceil((startMs(exam) - nowMs) / 60000)
            return minutes > 60 ? "Заплановано" : "Початок через " + minutes + " хв."
        }
        return nowMs < endMs(exam) ? "Можна розпочати" : "Час іспиту завершився"
    }
    Component.onCompleted: ExamVM.loadAvailableExams()
    Timer { interval: 5000; repeat: true; running: page.visible; onTriggered: ExamVM.loadAvailableExams() }
    Timer { interval: 1000; repeat: true; running: page.visible; onTriggered: page.nowMs = ExamVM.catalogTimeMs() }
    Connections {
        target: ExamVM
        function onCatalogLoaded() {
            page.loaded = true
            page.errorText = ""
            page.nowMs = ExamVM.catalogTimeMs()
            page.updatedAt = Qt.formatTime(new Date(), "HH:mm:ss")
            if (!page.chosenExam) page.chosenId = 0
        }
        function onCatalogLoadFailed(message) { page.errorText = message }
    }
    background: Rectangle { color: "#f3f6fa" }
    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 28
        spacing: 14
        Label { text: "Ваші іспити"; font.pixelSize: 28; font.bold: true; color: "#152b40" }
        Label { text: "Заплановані та доступні іспити вашої групи. Час показано у часовому поясі цього пристрою."; wrapMode: Text.WordWrap; Layout.fillWidth: true; color: "#475569" }
        RowLayout {
            Layout.fillWidth: true
            Label { text: ExamVM.catalogLoading ? "Оновлення…" : "Автооновлення кожні 5 с" + (page.updatedAt ? " · Оновлено " + page.updatedAt : ""); color: "#64748b"; Layout.fillWidth: true; wrapMode: Text.WordWrap }
            Button { text: "Оновити"; enabled: !ExamVM.catalogLoading; onClicked: ExamVM.loadAvailableExams() }
        }
        Label { text: page.errorText; visible: text.length > 0; color: "#b91c1c"; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { visible: page.loaded && !exams.count; text: "Вам ще не призначено майбутніх або доступних іспитів. Нові іспити з’являться автоматично. Якщо очікуєте іспит, уточніть вашу групу у викладача."; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        ListView {
            id: exams
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            spacing: 12
            model: ExamVM.availableExams
            ScrollBar.vertical: ScrollBar {}
            delegate: ItemDelegate {
                id: card
                required property var modelData
                width: ListView.view.width
                implicitHeight: details.implicitHeight + 32
                padding: 16
                onClicked: page.chosenId = modelData.id
                background: Rectangle { radius: 8; color: "white"; border.width: page.chosenId === card.modelData.id ? 2 : 1; border.color: page.chosenId === card.modelData.id ? "#0284c7" : "#dce4ed" }
                contentItem: ColumnLayout {
                    id: details
                    spacing: 8
                    Label { text: card.modelData.title; font.pixelSize: 20; font.bold: true; color: "#0f172a"; wrapMode: Text.WordWrap; Layout.fillWidth: true }
                    Label { text: page.statusText(card.modelData); color: page.canStart(card.modelData) ? "#047857" : "#475569"; font.bold: true }
                    Label { text: "Початок: " + page.dateText(card.modelData.scheduledStartTime) + "\nЗавершення: " + page.dateText(card.modelData.scheduledEndTime); wrapMode: Text.WordWrap; Layout.fillWidth: true; color: "#334155" }
                    Label { text: "Тривалість: " + card.modelData.durationMinutes + " хв · Прохідний бал: " + card.modelData.passingScore + "/100"; wrapMode: Text.WordWrap; Layout.fillWidth: true; color: "#475569" }
                    Label { visible: !!card.modelData.description; text: card.modelData.description || ""; wrapMode: Text.WordWrap; Layout.fillWidth: true; color: "#475569" }
                }
            }
        }
        Label { text: page.chosenExam ? (page.canStart(page.chosenExam) ? "Далі — перевірка особи. Пізній вхід не подовжує час іспиту." : "Обраний іспит ще не розпочався або вже завершився. Перевірте розклад і з’єднання.") : "Оберіть іспит зі списку."; wrapMode: Text.WordWrap; Layout.fillWidth: true; color: "#475569" }
        Button {
            text: "Перейти до перевірки особи"
            enabled: page.canStart(page.chosenExam)
            highlighted: true
            onClicked: { ExamVM.selectExam(page.chosenId); MediaVM.setExamId(page.chosenId); page.examSelected() }
        }
    }
}
