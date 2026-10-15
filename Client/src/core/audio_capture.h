#pragma once
#include <QObject>
#include <QAudioSource>
#include <QMediaDevices>
#include <QElapsedTimer>
#include <QtEndian>
#include <cstring>
#include <algorithm>

// PCM for VAD is separate from the microphone track in the evidence recording.
class AudioCapture : public QObject {
    Q_OBJECT
public:
    explicit AudioCapture(QObject *parent = nullptr) : QObject(parent) {}
    bool isActive() const { return m_source != nullptr; }
    void start(int sessionOffset, int batchSeconds = 5) {
        stop(); m_batchSeconds = std::clamp(batchSeconds, 1, 5); m_samplesProcessed = 0; m_offset = sessionOffset; m_clock.start();
        const auto device = QMediaDevices::defaultAudioInput();
        if (device.isNull()) { emit unavailable("Мікрофон недоступний"); return; }
        m_format = device.preferredFormat();
        m_source = new QAudioSource(device,m_format,this);
        auto *input = m_source->start();
        if (!input) { stop(); emit unavailable("Не вдалося відкрити аудіоканал"); return; }
        connect(m_source,&QAudioSource::stateChanged,this,[this](QAudio::State state) {
            if (state == QAudio::StoppedState && m_source && m_source->error() != QAudio::NoError) emit unavailable("Аудіоаналіз перервано");
        });
        connect(input,&QIODevice::readyRead,this,[this,input,source=m_source] {
            if (source != m_source) return;
            m_buffer += input->readAll();
            const int bytesPerBatch = m_format.sampleRate()*m_format.bytesPerFrame()*m_batchSeconds;
            while (bytesPerBatch > 0 && m_buffer.size() >= bytesPerBatch) {
                QByteArray pcm; pcm.resize(16000*m_batchSeconds*2);
                for (int n=0; n<16000*m_batchSeconds; ++n) {
                    const int sourceFrame = qint64(n)*m_format.sampleRate()/16000;
                    float sample=0;
                    for (int c=0; c<m_format.channelCount(); ++c) {
                        const char *data=m_buffer.constData()+sourceFrame*m_format.bytesPerFrame()+c*m_format.bytesPerSample();
                        switch(m_format.sampleFormat()) {
                        case QAudioFormat::Float: { float value; std::memcpy(&value,data,4); sample+=value; break; }
                        case QAudioFormat::Int16: { qint16 value; std::memcpy(&value,data,2); sample+=value/32768.0f; break; }
                        case QAudioFormat::Int32: { qint32 value; std::memcpy(&value,data,4); sample+=value/2147483648.0f; break; }
                        case QAudioFormat::UInt8: sample+=(static_cast<unsigned char>(*data)-128)/128.0f; break;
                        default: break;
                        }
                    }
                    const qint16 value = qToLittleEndian(static_cast<qint16>(std::clamp(sample/m_format.channelCount(),-1.0f,0.9999f)*32768));
                    std::memcpy(pcm.data()+n*2,&value,2);
                }
                m_buffer.remove(0,bytesPerBatch);
                emit captured(pcm,m_offset+int(m_samplesProcessed/m_format.sampleRate()));
                m_samplesProcessed += qint64(m_format.sampleRate())*m_batchSeconds;
            }
        });
    }
    void stop() { if(m_source) { m_source->disconnect(this); m_source->stop(); m_source->deleteLater(); m_source=nullptr; } m_buffer.clear(); }
signals:
    void captured(const QByteArray &pcm,int relativeSeconds);
    void unavailable(const QString &message);
private:
    QAudioSource *m_source=nullptr;
    QAudioFormat m_format;
    QByteArray m_buffer;
    QElapsedTimer m_clock;
    int m_offset=0;
    int m_batchSeconds=5;
    qint64 m_samplesProcessed=0;
};
