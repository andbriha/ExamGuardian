import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: agreementPage
    signal accepted()

    background: Rectangle { color: "white" }

    ColumnLayout {
        anchors.centerIn: parent
        width: Math.min(600, parent.width - 40)
        spacing: 20

        Label {
            text: "Умови перевірки та запису"
            font.pixelSize: 24
            font.bold: true
            Layout.alignment: Qt.AlignHCenter
        }

        ScrollView {
            Layout.fillWidth: true
            Layout.preferredHeight: 200

            TextArea {
                readOnly: true
                background: Rectangle { color: "#f8f9fa"; radius: 8; border.color: "#dee2e6" }
                padding: 15
                wrapMode: Text.Wrap

                textFormat: Text.RichText

                text: "<b>1. Перевірка особи:</b><br>" +
                      "Після згоди камера використовується для перевірки особи та завдання з рухом голови. Кадри передаються налаштованому серверу.<br><br>" +
                      "<b>2. Запис спроби:</b><br>" +
                      "Під час іспиту записуються камера, мікрофон та екран. Матеріали й спостереження доступні викладачу для розбору.<br><br>" +
                      "<b>3. Середовище:</b><br>" +
                      "Клієнт перевіряє відомі програми, камери, дисплеї та індикатори віртуального середовища. Під час спроби обмежуються деякі системні комбінації клавіш; втрата фокусу фіксується як спостереження.<br><br>" +
                      "<b>4. Рішення:</b><br>" +
                      "Автоматичний сигнал не є висновком про порушення. Викладач переглядає контекст; результат відповідей зберігається окремо."

            }
        }

        Label { text: "Сервер: " + ExamVM.serverUrl; textFormat: Text.PlainText; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: "Мовлення: " + (ExamVM.selectedExam.allowSpeech ? "дозволено" : "позначається для перевірки") + ". Рухи голови: " + (ExamVM.selectedExam.flagHeadMovement ? "позначаються" : "не позначаються"); wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: "Книги: " + ((ExamVM.selectedExam.allowedObjects || []).indexOf("book") >= 0 ? "дозволено" : "позначаються") + ". Додатковий ноутбук: " + ((ExamVM.selectedExam.allowedObjects || []).indexOf("laptop") >= 0 ? "дозволено" : "позначається"); wrapMode: Text.WordWrap; Layout.fillWidth: true }
        CheckBox {
            id: checkAgree
            text: "Я розумію умови, надаю дозволи та погоджуюсь"
            font.pixelSize: 14
        }

        Button {
            text: "РОЗПОЧАТИ ПЕРЕВІРКУ"
            enabled: checkAgree.checked
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            highlighted: true

            background: Rectangle {
                color: parent.enabled ? (parent.down ? "#1e7e34" : "#28a745") : "#e0e0e0"
                radius: 5
            }
            contentItem: Text {
                text: parent.text
                font.bold: true
                color: parent.enabled ? "white" : "#9e9e9e"
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
            }

            onClicked: agreementPage.accepted()
        }

        Button {
            text: "Відмовитись і вийти"
            Layout.fillWidth: true
            flat: true
            onClicked: Qt.quit()
        }
    }
}
