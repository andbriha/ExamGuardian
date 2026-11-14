import { ViolationAlertDto } from './violation.service';

export function observationPriority(v: ViolationAlertDto): 'Observation' | 'Warning' | 'Intervention' {
  if (v.priority) return v.priority;
  if (['identity_uncertain', 'analysis_unavailable'].includes(v.violationType)) return 'Intervention';
  return ['cell_phone', 'book', 'laptop', 'multiple_faces', 'extra_person', 'face_missing', 'prohibited_app', 'voice_activity'].includes(v.violationType) ? 'Warning' : 'Observation';
}
export function priorityLabel(v: ViolationAlertDto): string {
  return {Observation: 'Спостереження', Warning: 'Попередження до перевірки', Intervention: 'Потребує уваги викладача'}[observationPriority(v)];
}
export function decisionLabel(decision: string): string {
  return ({Pending: 'Очікує перевірки', Confirmed: 'Порушення підтверджено', Dismissed: 'Помилка розпізнавання', NoViolation: 'Подія була, порушення немає', InsufficientEvidence: 'Недостатньо даних', LegacyReviewed: 'Потрібне уточнення рішення'} as Record<string,string>)[decision] || decision;
}
export function episodeLabel(v: ViolationAlertDto): string {
  try {
    const e = JSON.parse(v.metadata || '{}').episode;
    if (!e || !Number.isFinite(e.start) || !Number.isFinite(e.last_observed)) return '';
    const status = e.status === 'ended' ? 'завершено' : e.status === 'coverage_gap' ? 'перервано через пропуск даних' : 'останнє підтвердження';
    return `Епізод ${e.start.toFixed(1)}–${e.last_observed.toFixed(1)} с від початку · ${e.samples} спостережень · ${status}. Інтервал не доводить безперервність між кадрами.`;
  } catch { return ''; }
}
