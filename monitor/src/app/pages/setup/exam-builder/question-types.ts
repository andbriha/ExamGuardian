export type QuestionKind = 'singleChoice' | 'trueFalse';
export interface DraftQuestion {
  kind?: QuestionKind;
  text: string;
  points: number;
  correctIndex: number;
  options: { text: string }[];
}
export function createQuestion(kind: QuestionKind = 'singleChoice'): DraftQuestion {
  return {kind, text:'', points:1, correctIndex:0,
    options: (kind === 'trueFalse' ? ['Правда', 'Неправда'] : ['', '', '', '']).map(text => ({text}))};
}
export function importedQuestion(question: {text:string; points:number; correctIndex:number; options:string[]}): DraftQuestion {
  const labels = question.options.map(option => option.trim().toLowerCase());
  const trueFalse = labels.length === 2 && [['true','false'],['правда','неправда'],['так','ні']].some(pair => pair.every(label => labels.includes(label)));
  return {...question,kind:trueFalse ? 'trueFalse' : 'singleChoice',options:question.options.map(text => ({text}))};
}
