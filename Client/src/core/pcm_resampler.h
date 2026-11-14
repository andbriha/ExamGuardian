#pragma once
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <vector>

// Causal, windowed-sinc polyphase conversion; history survives capture batches.
// 64 source-sample delay is constant and never grows with the number of packets.
class PcmResampler {
public:
    void reset(int rate) {
        m_rate = rate; m_inputCount = 0; m_outputCount = 0; m_history.clear();
        const double cutoff = .45 * std::min(1.0, 16000.0 / rate);
        constexpr double pi = 3.14159265358979323846;
        for (int phase=0; phase<Phases; ++phase) {
            double sum=0;
            for (int k=0; k<Taps; ++k) {
                const double x = k + double(phase)/Phases - 64.0;
                const double sinc = std::abs(x)<1e-9 ? 2*cutoff : std::sin(2*pi*cutoff*x)/(pi*x);
                const double window = .42 - .5*std::cos(2*pi*k/(Taps-1)) + .08*std::cos(4*pi*k/(Taps-1));
                sum += m_coefficients[phase][k] = sinc * window;
            }
            for (auto &value : m_coefficients[phase]) value /= sum;
        }
    }
    std::vector<float> process(const std::vector<float> &input) {
        if (input.empty()) return {};
        std::vector<float> samples=m_history;
        const auto base=m_inputCount-static_cast<std::int64_t>(m_history.size());
        samples.insert(samples.end(),input.begin(),input.end());
        m_inputCount += input.size();
        std::vector<float> output;
        // Integer phase accumulator avoids timing drift at 44.1 kHz.
        while (m_outputCount*m_rate < m_inputCount*16000) {
            const auto numerator=m_outputCount*m_rate;
            const auto index=numerator/16000;
            const int phase=int((numerator%16000)*Phases/16000);
            double value=0;
            for (int k=0;k<Taps;++k) {
                const auto offset=index-k-base;
                if (offset>=0 && offset<static_cast<std::int64_t>(samples.size()))
                    value += samples[static_cast<size_t>(offset)]*m_coefficients[phase][k];
            }
            output.push_back(static_cast<float>(value)); ++m_outputCount;
        }
        const auto keep=std::min(samples.size(),size_t(Taps));
        m_history.assign(samples.end()-keep,samples.end());
        return output;
    }
private:
    static constexpr int Taps=129, Phases=256;
    int m_rate=16000;
    std::int64_t m_inputCount=0, m_outputCount=0;
    std::vector<float> m_history;
    std::array<std::array<double,Taps>,Phases> m_coefficients{};
};
