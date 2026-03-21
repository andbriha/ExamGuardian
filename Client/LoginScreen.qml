import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: loginPage
    signal loginSuccess()

    background: Rectangle { color: "white" }

    ColumnLayout {
        anchors.centerIn: parent
        width: 350
        spacing: 15

        Label {
            text: "🔐 System Login"
            font.pixelSize: 24
            font.bold: true
            Layout.alignment: Qt.AlignHCenter
            color: "#333"
        }

        TextField {
            id: username
            placeholderText: "Login (Student ID)"
            Layout.fillWidth: true
            font.pixelSize: 16
        }

        TextField {
            id: password
            placeholderText: "Password"
            echoMode: TextInput.Password
            Layout.fillWidth: true
            font.pixelSize: 16
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 5

            CheckBox {
                id: agreeCheck
                checked: false
            }

            Text {
                text: "I agree to the <font color='#007bff'><u>rules</u></font> and data processing"
                color: "#555"
                font.pixelSize: 14
                textFormat: Text.RichText
                Layout.fillWidth: true
                wrapMode: Text.WordWrap

                MouseArea {
                    anchors.fill: parent
                    cursorShape: Qt.PointingHandCursor
                    onClicked: rulesDialog.open()
                }
            }
        }

        Button {
            text: "LOGIN"
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            highlighted: true

            enabled: username.text.length > 0 &&
                     password.text.length > 0 &&
                     agreeCheck.checked

            background: Rectangle {
                color: parent.enabled ? (parent.down ? "#1565C0" : "#1976D2") : "#E0E0E0"
                radius: 5
            }

            contentItem: Text {
                text: parent.text
                font.bold: true
                color: parent.enabled ? "white" : "#9E9E9E"
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
            }

            onClicked: {
                // Just logging in, nothing heavy here
                console.log("Login: " + username.text + " | Agreement Accepted: Yes")
                loginPage.loginSuccess()
            }
        }
    }

    Dialog {
        id: rulesDialog
        anchors.centerIn: parent
        width: parent.width * 0.9
        title: "Terms and Conditions"
        standardButtons: Dialog.Ok
        modal: true

        contentItem: ScrollView {
            contentWidth: availableWidth
            contentHeight: rulesText.implicitHeight
            clip: true

            Label {
                id: rulesText
                width: availableWidth
                wrapMode: Text.Wrap
                // Updated text with blocking warning
                text: "<b>Security Warning:</b><br><br>" +
                      "To prevent cheating, the system will perform the following actions:<br><br>" +
                      "<b>1. Process Analysis:</b> The names and digital signatures (hashes) of all running programs will be collected.<br>" +
                      "<b>2. Blocking:</b> System keys Alt+Tab and Win will be disabled.<br>" +
                      "<b>3. Monitoring:</b> Any attempt to switch windows or exit full-screen mode will be recorded in the exam log."
            }
        }
    }
}
