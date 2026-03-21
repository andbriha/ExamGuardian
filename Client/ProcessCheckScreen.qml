import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Page {
    id: checkPage
    signal proceedToExam(var detectedApps)

    background: Rectangle { color: "white" }

    property var detectedApps: []

    function scanProcesses() {
        detectedApps = Proctoring.getSuspiciousProcesses()
    }

    Component.onCompleted: scanProcesses()

    Connections {
        target: Proctoring
        function onSnapshotReady(snapshot) {
            Proctoring.saveSnapshotToFile(snapshot)
            loadingIndicator.running = false
            checkPage.proceedToExam(detectedApps)
        }
    }

    ColumnLayout {
        anchors.centerIn: parent
        width: 600
        spacing: 20

        Label {
            text: "ℹ️ Exam Rules"
            font.pixelSize: 24
            font.bold: true
            color: "#0277BD"
            Layout.alignment: Qt.AlignHCenter
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 140
            color: "#F5F5F5"
            radius: 8
            border.color: "#E0E0E0"

            Label {
                anchors.fill: parent
                anchors.margins: 15
                wrapMode: Text.Wrap
                font.pixelSize: 14
                color: "#333"
                textFormat: Text.RichText
                text: "During the exam, the use of the following programs is <b>strictly prohibited</b>:<br>" +
                      "• <b>Messengers:</b> Telegram, Discord, Skype, Viber, WhatsApp<br>" +
                      "• <b>Remote Access:</b> TeamViewer, AnyDesk<br>" +
                      "• <b>Screen Recording:</b> OBS Studio and similar<br><br>" +
                      "<i>The system does not close programs automatically to avoid data loss. It is your responsibility to close these programs.</i>"
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: detectedApps.length > 0 ? 150 : 0
            visible: detectedApps.length > 0
            color: "#FFF3E0"
            radius: 8
            border.color: "#FFB74D"
            clip: true

            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 15
                spacing: 10

                Label {
                    text: "⚠️ You have prohibited programs running:"
                    font.bold: true
                    color: "#D84315"
                }

                ListView {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    model: detectedApps
                    clip: true
                    delegate: RowLayout {
                        width: parent.width
                        spacing: 10
                        Text { text: "🛑"; font.pixelSize: 16 }
                        Label {
                            text: modelData
                            font.pixelSize: 15
                            font.bold: true
                            color: "#D84315"
                            Layout.fillWidth: true
                        }
                    }
                }

                Label {
                    text: "Please close them and click 'Update List'."
                    font.pixelSize: 13
                    color: "#D84315"
                }
            }
        }

        Label {
            visible: detectedApps.length === 0
            Layout.fillWidth: true
            horizontalAlignment: Text.AlignHCenter
            text: "✅ No prohibited programs detected. You may begin."
            font.bold: true
            color: "#2E7D32"
            font.pixelSize: 16
        }

        Button {
            text: "🔄 Update List"
            Layout.fillWidth: true
            flat: true
            visible: detectedApps.length > 0
            onClicked: scanProcesses()
        }

        BusyIndicator {
            id: loadingIndicator
            running: false
            visible: running
            Layout.alignment: Qt.AlignHCenter
        }

        Button {
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            highlighted: true
            visible: !loadingIndicator.running

            text: detectedApps.length > 0 ? "I ignore the warning, start exam" : "Start Exam"

            background: Rectangle {
                color: parent.down ? "#01579B" : (detectedApps.length > 0 ? "#D32F2F" : "#0288D1")
                radius: 5
            }

            contentItem: Text {
                text: parent.text
                font.bold: true
                color: "white"
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
            }

            onClicked: {
                loadingIndicator.running = true

                Proctoring.requestProcessSnapshot()
            }
        }
    }
}
