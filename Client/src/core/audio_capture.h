#pragma once
#include <QObject>
#include <QAudioSource>
#include <QMediaDevices>
#include <QElapsedTimer>
#include <QtEndian>
#include <cstring>
#include <algorithm>
#include "pcm_resampler.h"

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
        if (m_format.sampleRate() <= 0 || m_format.channelCount() <= 0 || m_format.sampleFormat() == QAudioFormat::Unknown) {
            emit unavailable("Формат мікрофона не підтримується"); return;
        }
        m_resampler.reset(m_format.sampleRate());
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
                const int sourceFrames = m_format.sampleRate()*m_batchSeconds;
                std::vector<float> mono(sourceFrames);
                for (int sourceFrame=0; sourceFrame<sourceFrames; ++sourceFrame) {
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
                    mono[sourceFrame] = std::isfinite(sample) ? sample/m_format.channelCount() : 0.0f;
                }
                const auto resampled=m_resampler.process(mono);
                QByteArray pcm; pcm.resize(resampled.size()*2);
                for (size_t n=0; n<resampled.size(); ++n) {
                    const qint16 value = qToLittleEndian(static_cast<qint16>(std::clamp(resampled[n],-1.0f,0.9999f)*32768));
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
    PcmResampler m_resampler;
    QByteArray m_buffer;
    QElapsedTimer m_clock;
    int m_offset=0;
    int m_batchSeconds=5;
    qint64 m_samplesProcessed=0;
};
