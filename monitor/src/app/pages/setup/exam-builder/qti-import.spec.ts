import { ChangeDetectorRef } from '@angular/core';
import { ExamBuilderComponent } from './exam-builder';
import { GroupService } from '../../../services/group.service';
import { ExamService } from '../../../services/exam.service';

describe('QTI draft import', () => {
  it('creates and recognizes true/false without changing answer order', () => {
    const component = builder();component.addQuestion('trueFalse');
    expect(component.questions[1].options.map(o=>o.text)).toEqual(['Правда','Неправда']);
    component.addOption(component.questions[1]);expect(component.questions[1].options.length).toBe(2);
    component.qtiPreview={canImport:true,issues:[],notice:'',questions:[{text:'Statement',points:1,correctIndex:1,options:['False','True']}]};
    component.applyImport();const question=component.questions[component.questions.length-1];
    expect(question.kind).toBe('trueFalse');expect(question.options[question.correctIndex].text).toBe('True');
  });
  function builder() { return new ExamBuilderComponent({} as GroupService, {} as ExamService, {} as ChangeDetectorRef); }
  it('preserves authored questions and appends the imported answer mapping', () => {
    const component = builder(); component.questions[0].text = 'Existing question';
    component.qtiPreview = {canImport:true,issues:[],notice:'',questions:[{text:'Imported',points:1,correctIndex:1,options:['A','B']}]};
    component.applyImport();
    expect(component.questions.length).toBe(2); expect(component.questions[0].text).toBe('Existing question');
    expect(component.questions[1].correctIndex).toBe(1); expect(component.questions[1].options[1].text).toBe('B');
    expect(component.qtiPreview).toBeNull();
  });
  it('does not apply a partially supported package', () => {
    const component = builder();
    component.qtiPreview = {canImport:false,issues:[{file:'unsupported.xml',message:'Unsupported'}],notice:'',questions:[{text:'Imported',points:1,correctIndex:0,options:['A','B']}]};
    component.applyImport(); expect(component.questions[0].text).toBe(''); expect(component.qtiPreview).not.toBeNull();
  });
});
