import { groupResults, resultVerdict } from './group-results.model';
import { ExamSessionDto } from '../../../services/session.service';

describe('Whole group results', () => {
  const students = [
    {id:'a', fullName:'Anna', login:'anna', role:'Student', group:'A'},
    {id:'b', fullName:'Bohdan', login:'bohdan', role:'Student', group:'A'},
    {id:'c', fullName:'Other', login:'other', role:'Student', group:'B'}
  ];
  it('includes absent students, isolates groups and uses the latest attempt even if unfinished', () => {
    const sessions = [
      {id:1, studentId:'a', examId:1, startTime:'2026-01-01', isFinished:true, integrityVerdict:'Accepted'},
      {id:2, studentId:'a', examId:1, startTime:'2026-01-02', isFinished:false},
      {id:3, studentId:'c', examId:1, startTime:'2026-01-03', isFinished:true}
    ] as ExamSessionDto[];
    const rows = groupResults(students, sessions, 'A', 1);
    expect(rows.length).toBe(2); expect(rows[0].latest?.id).toBe(2);
    expect(rows[0].attempts.length).toBe(2); expect(rows[1].latest).toBeUndefined();
    expect(resultVerdict(rows[0].latest)).toBe('Триває'); expect(resultVerdict(rows[1].latest)).toBe('Не розпочато');
  });
  it('never presents a development bypass as an accepted result', () => {
    expect(resultVerdict({isFinished:true, identityVerificationSkipped:true, integrityVerdict:'Accepted'} as ExamSessionDto)).toContain('DEV');
  });
});
