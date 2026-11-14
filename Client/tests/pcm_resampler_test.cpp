#include "pcm_resampler.h"
#include <cmath>
#include <iostream>
#include <stdexcept>

static void check(bool condition) { if (!condition) throw std::runtime_error("PCM resampling regression"); }
static std::vector<float> tone(int rate, double hz) {
    std::vector<float> data(rate);
    for (int i=0;i<rate;++i) data[i]=float(.5*std::sin(2*3.14159265358979323846*hz*i/rate));
    return data;
}
static double rms(const std::vector<float>& v) {
    double sum=0; for (size_t i=1000;i<v.size();++i) sum+=v[i]*v[i];
    return std::sqrt(sum/(v.size()-1000));
}
int main() {
    for (int rate : {16000,44100,48000}) {
        PcmResampler converter; converter.reset(rate);
        const auto source=tone(rate,1000); const auto whole=converter.process(source);
        check(whole.size()==16000); check(rms(whole)>.34 && rms(whole)<.36);
        converter.reset(rate); std::vector<float> split;
        for(size_t offset=0;offset<source.size();offset+=137) {
            const auto end=std::min(offset+137,source.size());
            const auto chunk=converter.process(std::vector<float>(source.begin()+offset,source.begin()+end));
            split.insert(split.end(),chunk.begin(),chunk.end());
        }
        check(split==whole); // exact sample continuity, independent of packet boundaries
        if (rate>16000) { converter.reset(rate); check(rms(converter.process(tone(rate,12000)))<.01); }
    }
    std::cout << "PCM resampling: frequency rejection, gain, sample count and continuity passed\n";
}
