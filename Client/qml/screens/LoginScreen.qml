import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: loginPage
    signal loginSuccess()

    background: Rectangle { color: "white" }

    property string errorText: ""

    Connections {
        target: ExamVM

        function onLoginSucceeded(token) {
            loginPage.loginSuccess()
        }

        function onLoginFailed(error) {
            errorText = error
            loginBtn.enabled = true
            loginBtn.text = "LOGIN"
        }
    }

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
            id: serverAddress
            text: ExamVM.serverUrl
            placeholderText: "Адреса сервера ExamGuardian"
            Layout.fillWidth: true
        }
        TextField {
            id: username
            placeholderText: "Login"
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

        // Повідомлення про помилку
        Label {
            visible: loginPage.errorText.length > 0
            text: "⚠ " + loginPage.errorText
            color: "#D32F2F"
            font.pixelSize: 13
            Layout.fillWidth: true
            wrapMode: Text.WordWrap
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 5
            CheckBox { id: agreeCheck }
            Text {
                text: "I agree to the <font color='#007bff'><u>rules</u></font>"
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
            id: loginBtn
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
                loginPage.errorText = ""
                loginBtn.text    = "Входимо..."
                loginBtn.enabled = false
                if (!ExamVM.configureServer(serverAddress.text)) { errorText = "Некоректна адреса сервера"; loginBtn.enabled = true; return }
                ExamVM.login(username.text, password.text)
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
            Label {
                width: availableWidth
                wrapMode: Text.Wrap
                text: "<b>1. Process Analysis:</b> SHA-256 hashes of all running programs will be collected.<br>" +
                      "<b>2. Blocking:</b> System keys Alt+Tab and Win will be disabled.<br>" +
                      "<b>3. Monitoring:</b> Camera, microphone and screen recording will be active throughout the exam. Video may remain on this PC until delivery is confirmed."
            }
        }
    }
}
