import { StudentDto } from '../../../services/student.service';
import { ExamSessionDto } from '../../../services/session.service';

export interface GroupResultRow { student: StudentDto; latest?: ExamSessionDto; attempts: ExamSessionDto[]; }

export function groupResults(students: StudentDto[], sessions: ExamSessionDto[], group: string, examId: number): GroupResultRow[] {
  return students.filter(s => (s.group || '') === group).sort((a, b) => a.fullName.localeCompare(b.fullName, 'uk')).map(student => {
    const attempts = sessions.filter(s => s.studentId === student.id && s.examId === examId)
      .sort((a, b) => Date.parse(b.startTime) - Date.parse(a.startTime) || b.id - a.id);
    return {student, latest: attempts[0], attempts};
  });
}

export function resultVerdict(session?: ExamSessionDto): string {
  if (!session) return 'Не розпочато';
  if (!session.isFinished) return 'Триває';
  if (session.identityVerificationSkipped) return 'DEV: тестова спроба';
  return ({Accepted: 'Прийнято', Rejected: 'Відхилено', Inconclusive: 'Недостатньо доказів'} as Record<string, string>)[session.integrityVerdict] || 'Очікує рішення';
}
