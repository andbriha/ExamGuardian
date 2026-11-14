#pragma once
#include <QByteArray>

// Preserve contiguous microphone samples while ASR is busy. Never silently
// join speech across an overflow or a failed upload.
class VerificationAudioBuffer {
public:
    bool append(const QByteArray &pcm) {
        if (m_data.size() + pcm.size() > 320000) {
            clear();
            m_data = pcm.right(64000);
            return false;
        }
        m_data.append(pcm);
        return true;
    }
    QByteArray take() { auto chunk = m_data.left(64000); m_data.remove(0, chunk.size()); return chunk; }
    void clear() { m_data.clear(); m_reset = true; }
    bool takeReset() { bool reset = m_reset; m_reset = false; return reset; }
    bool empty() const { return m_data.isEmpty(); }
private:
    QByteArray m_data;
    bool m_reset = true;
};
