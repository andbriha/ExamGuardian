import { of, throwError } from 'rxjs';
import { ViolationDetailComponent } from './violation-detail';

describe('Manual review', () => {
  function setup() {
    const api = jasmine.createSpyObj('observations',['markAsReviewed']);
    const location = jasmine.createSpyObj('location',['back']);
    const cdr = jasmine.createSpyObj('changes',['detectChanges']);
    const component = new ViolationDetailComponent({} as never,location,api,{} as never,{} as never,cdr);
    component.currentViolation = {id:7,examSessionId:3,violationType:'looking_down',confidence:0.7,relativeTimestampSeconds:2,timestamp:'2026-10-14T12:00:00Z',isReviewed:false,reviewDecision:'Pending',eventId:'test'};
    component.reviewNotes='Context checked';
    return {component,api,location};
  }
  it('persists distinct confirmation and dismissal with the reason', () => {
    const {component,api}=setup(); api.markAsReviewed.and.returnValue(of(undefined));
    component.confirmViolation(); expect(api.markAsReviewed).toHaveBeenCalledWith(7,'Confirmed','Context checked');
    component.rejectViolation(); expect(api.markAsReviewed).toHaveBeenCalledWith(7,'Dismissed','Context checked');
  });
  it('does not claim success or navigate away when the save failed', () => {
    const {component,api,location}=setup(); api.markAsReviewed.and.returnValue(throwError(() => new Error('offline')));
    component.confirmViolation(); expect(component.reviewError).toContain('не збережено'); expect(location.back).not.toHaveBeenCalled();
  });
});
