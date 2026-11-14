import { observationPriority, episodeLabel, decisionLabel } from './observation-presentation';
import { ViolationAlertDto } from './violation.service';

describe('Observation presentation', () => {
  it('does not turn high detector confidence into an urgent alert', () => {
    expect(observationPriority({violationType:'cell_phone',confidence:1} as ViolationAlertDto)).toBe('Warning');
    expect(observationPriority({violationType:'looking_down',confidence:1} as ViolationAlertDto)).toBe('Observation');
    expect(observationPriority({violationType:'analysis_unavailable',confidence:0} as ViolationAlertDto)).toBe('Intervention');
  });
  it('distinguishes an innocent real event from a model error', () => {
    expect(decisionLabel('NoViolation')).not.toBe(decisionLabel('Dismissed'));
    expect(decisionLabel('InsufficientEvidence')).toBe('Недостатньо даних');
  });
  it('describes coverage gaps without claiming continuous visibility', () => {
    const metadata=JSON.stringify({episode:{start:1,last_observed:9,samples:3,status:'coverage_gap'}});
    expect(episodeLabel({metadata} as ViolationAlertDto)).toContain('пропуск даних');
    expect(episodeLabel({metadata:'bad json'} as ViolationAlertDto)).toBe('');
  });
});
