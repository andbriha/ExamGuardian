import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: agreementPage
    signal accepted()

    background: Rectangle { color: "white" }

    ScrollView {
        id: agreementScroll
        anchors.fill: parent
        anchors.margins: 24
        contentWidth: availableWidth
        clip: true
    ColumnLayout {
        width: Math.min(600, agreementScroll.availableWidth)
        x: Math.max(0, (agreementScroll.availableWidth - width) / 2)
        spacing: 20

        Label {
            text: "Умови перевірки та запису"
            font.pixelSize: 24
            font.bold: true
            Layout.alignment: Qt.AlignHCenter
        }

        ScrollView {
            id: termsScroll
            contentWidth: availableWidth
            Layout.fillWidth: true
            Layout.preferredHeight: 160

            TextArea {
                width: termsScroll.availableWidth
                readOnly: true
                background: Rectangle { color: "#f8f9fa"; radius: 8; border.color: "#dee2e6" }
                padding: 15
                wrapMode: Text.Wrap

                textFormat: Text.RichText

                text: "<b>1. Перевірка особи:</b><br>" +
                      "Після згоди камера використовується для перевірки особи та завдання з рухом голови. Кадри передаються налаштованому серверу.<br><br>" +
                      "<b>2. Запис спроби:</b><br>" +
                      "Під час іспиту записуються камера, мікрофон та екран. Матеріали й спостереження доступні викладачу для розбору. Під час іспиту сигнали прокторингу бачить лише викладач; студент отримує тільки потрібні технічні повідомлення.<br><br>" +
                      "<b>3. Середовище:</b><br>" +
                      "Клієнт перевіряє відомі програми, камери, дисплеї та індикатори віртуального середовища. Під час спроби обмежуються деякі системні комбінації клавіш; втрата фокусу фіксується як спостереження.<br><br>" +
                      "<b>4. Рішення:</b><br>" +
                      "Автоматичний сигнал не є висновком про порушення. Викладач переглядає контекст; результат відповідей зберігається окремо."

            }
        }

        Label { text: "Сервер: " + ExamVM.serverUrl; textFormat: Text.PlainText; wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: "Мовлення: " + (ExamVM.selectedExam.allowSpeech ? "дозволено" : "позначається для перевірки") + ". Рухи голови: " + (ExamVM.selectedExam.flagHeadMovement ? "позначаються" : "не позначаються"); wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: "Книги: " + ((ExamVM.selectedExam.allowedObjects || []).indexOf("book") >= 0 ? "дозволено" : "позначаються") + ". Додатковий ноутбук: " + ((ExamVM.selectedExam.allowedObjects || []).indexOf("laptop") >= 0 ? "дозволено" : "позначається"); wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: "Телефон: " + ((ExamVM.selectedExam.allowedObjects || []).indexOf("cell_phone") >= 0 ? "дозволено правилами цього іспиту" : "приберіть до початку іспиту"); wrapMode: Text.WordWrap; Layout.fillWidth: true }
        Label { text: "Чернетка та інші матеріали — за попередньо погодженими правилами викладача. До початку перевірте камеру й мікрофон та закрийте програми, зазначені на екрані перевірки середовища. Якщо потрібні особливі умови, узгодьте їх до старту."; wrapMode: Text.WordWrap; Layout.fillWidth: true }
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
}
