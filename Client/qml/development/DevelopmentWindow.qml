import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

Item {
    anchors.fill: parent
    z: 10000
    Component.onCompleted: Qt.callLater(function() { tools.open() })
    Column {
        anchors.right: parent.right
        anchors.top: parent.top
        spacing: 4
        Label {
            padding: 8
            width: Math.min(500, parent.parent.width)
            text: "DEV BUILD — " + (DevVM.active.length ? "overrides: " + DevVM.active.join(", ") : "no overrides")
            wrapMode: Text.WordWrap
            color: "#7c2d12"
            background: Rectangle {color: "#ffedd5"}
        }
        Button {text: "DEV controls"; onClicked: tools.open()}
    }
    Shortcut {sequence: "Ctrl+Shift+D"; onActivated: tools.open()}
    Popup {
        id: tools
        x: Math.max(0,(parent.width-width)/2)
        y: 30
        width: Math.min(520,parent.width)
        height: Math.min(650,parent.height-50)
        modal: false
        closePolicy: Popup.CloseOnEscape
        ScrollView {
            anchors.fill: parent
            contentWidth: availableWidth
            ColumnLayout {
                width: parent.width
                Label {text:"DEV controls"; font.bold:true;font.pixelSize:20}
                Label {text:DevVM.status;wrapMode:Text.WordWrap;Layout.fillWidth:true}
                Label {text:"These switches only disable local client restrictions. Login, identity verification, recording and AI remain unchanged.";wrapMode:Text.WordWrap;Layout.fillWidth:true}
                Repeater {
                    model: DevVM.controls
                    CheckBox {
                        required property string modelData
                        text: ({vmGate: "Disable VM check", processGate: "Disable process blocking", displayGate: "Disable display check", virtualDeviceGate: "Disable virtual device check", keyboardHooks: "Disable keyboard restrictions", focusEnforcement: "Disable focus restrictions"})[modelData]
                        checked: DevVM.active.indexOf(modelData)>=0
                        enabled: DevVM.available && !DevVM.frozen
                        onClicked: DevVM.setOverride(modelData,checked)
                    }
                }
                Label {visible:DevVM.frozen;text:"Controls are frozen for this attempt."}
                RowLayout {
                    Button {text:"Reset overrides";enabled:!DevVM.frozen;onClicked:DevVM.reset()}
                    Button {text:"Enable all";enabled:DevVM.available&&!DevVM.frozen;onClicked:DevVM.enableAll()}
                    Button {text:"Close";onClicked:tools.close()}
                }
            }
        }
    }
}
