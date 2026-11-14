import QtQml
QtObject {
    property var questions: [{id:1,text:"Оберіть правильну відповідь",options:[{text:"Варіант A",orderIndex:0},{text:"Варіант B",orderIndex:1}]}]
    property var savedAnswers: ({})
    property int durationSeconds: 1800
    property string examTitle: "Перевірка комфортного іспиту"
    property string serverUrl: "https://school.example"
    property var selectedExam: ({allowSpeech:false,flagHeadMovement:true,allowedObjects:["book"]})
    property bool virtualizationBlocked: true
    property bool isExamActive: true
    property bool cameraNeedsAttention: false
    property bool microphoneNeedsAttention: false
    property string technicalStatus: ""
    property string uploadStatus: "Матеріалів у черзі: 12"
    property int pendingVideoUploads: 12
    signal examLoaded()
    signal submitFinished(bool success, string message)
    signal examLoadFailed(string message)
    signal answersSaved(bool success)
    signal sessionTerminated()
    signal prohibitedProcessAlert(string processName)
    function loadExam() {}
    function saveAnswers(answers) {}
    function heartbeat() {}
    function submitAnswers(answers) {}
    function enterExamMode() {}
    function exitExamMode() {}
}
