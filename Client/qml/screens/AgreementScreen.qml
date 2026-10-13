import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: agreementPage
    signal accepted()

    background: Rectangle { color: "white" }

    ColumnLayout {
        anchors.centerIn: parent
        width: 600
        spacing: 20

        Label {
            text: "Екзаменаційна угода"
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

                text: "<b>1. Контроль середовища:</b><br>" +
                      "Система отримає права адміністратора для блокування клавіатури (окрім дозволених клавіш) та примусового завершення заборонених програм (браузери, месенджери).<br><br>" +

                      "<b>2. Доступ до обладнання:</b><br>" +
                      "Ви надаєте дозвіл на використання <b>веб-камери</b> та <b>мікрофону</b> протягом усього іспиту. Ведеться безперервний запис.<br><br>" +

                      "<b>3. Персональні дані:</b><br>" +
                      "Ви погоджуєтесь на обробку ваших персональних даних (відео, аудіо, знімки екрану) з метою забезпечення академічної доброчесності."
            }
        }

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
