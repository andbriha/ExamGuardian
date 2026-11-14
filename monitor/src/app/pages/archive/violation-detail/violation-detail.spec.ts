import { of, throwError, Subject } from 'rxjs';
import { ViolationDetailComponent } from './violation-detail';

describe('Manual review', () => {
  function setup() {
    const api = jasmine.createSpyObj('observations',['markAsReviewed','getEvidence','getById']);
    const location = jasmine.createSpyObj('location',['back','replaceState']);
    const cdr = jasmine.createSpyObj('changes',['detectChanges']);
    const component = new ViolationDetailComponent({} as never,location,api,{} as never,{} as never,cdr);
    component.currentViolation = {id:7,examSessionId:3,violationType:'looking_down',confidence:0.7,relativeTimestampSeconds:2,timestamp:'2026-10-14T12:00:00Z',isReviewed:false,reviewDecision:'Pending',eventId:'test'};
    api.getEvidence.and.returnValue(of(new Blob()));
    api.getById.and.returnValue(of({reviewHistoryJson:'[]'}));
    component.reviewNotes='Context checked';
    return {component,api,location};
  }
  it('persists distinct confirmation and dismissal with the reason', () => {
    const {component,api}=setup(); api.markAsReviewed.and.returnValue(of(undefined));
    component.confirmViolation(); expect(api.markAsReviewed).toHaveBeenCalledWith(7,'Confirmed','Context checked',undefined);
    component.rejectViolation(); expect(api.markAsReviewed).toHaveBeenCalledWith(7,'Dismissed','Context checked',undefined);
    component.noViolation(); expect(api.markAsReviewed).toHaveBeenCalledWith(7,'NoViolation','Context checked',undefined);
    component.insufficientEvidence(); expect(api.markAsReviewed).toHaveBeenCalledWith(7,'InsufficientEvidence','Context checked',undefined);
  });
  it('does not claim success or navigate away when the save failed', () => {
    const {component,api,location}=setup(); api.markAsReviewed.and.returnValue(throwError(() => new Error('offline')));
    component.confirmViolation(); expect(component.reviewError).toContain('не збережено'); expect(location.back).not.toHaveBeenCalled();
  });
  it('requires a reason instead of confirming with one click', () => {
    const {component,api}=setup(); component.reviewNotes='  ';
    component.confirmViolation(); expect(api.markAsReviewed).not.toHaveBeenCalled();
    expect(component.reviewError).toContain('Поясніть');
  });
  it('reviews the selected event even when its recording is missing and restores per-event drafts', () => {
    const {component,api}=setup(); api.markAsReviewed.and.returnValue(of(undefined));
    const first=component.currentViolation!;
    const second={...first,id:8,reviewNotes:'',eventId:'second'};
    component.seekToViolation(second);
    expect(component.currentViolation?.id).toBe(8);
    expect(component.reviewNotes).toBe('');
    component.reviewNotes='Second reason'; component.noViolation();
    expect(api.markAsReviewed).toHaveBeenCalledWith(8,'NoViolation','Second reason',undefined);
    component.seekToViolation(first); expect(component.reviewNotes).toBe('Context checked');
    component.seekToViolation(second); expect(component.reviewNotes).toBe('Second reason');
  });
  it('does not switch the review target while saving and stays after success', () => {
    const {component,api,location}=setup(); const saved=new Subject<void>(); api.markAsReviewed.and.returnValue(saved);
    component.confirmViolation(); component.seekToViolation({...component.currentViolation!,id:8});
    expect(component.currentViolation?.id).toBe(7);
    saved.next(); expect(location.back).not.toHaveBeenCalled(); expect(component.reviewSuccess).toContain('#7');
  });
  it('ignores evidence arriving for a previous selection', () => {
    const {component,api}=setup(); const old=new Subject<Blob>(); api.getEvidence.and.returnValue(old);
    component.selectObservation({...component.currentViolation!,id:8});
    api.getEvidence.and.returnValue(of(new Blob()));
    component.selectObservation({...component.currentViolation!,id:9}); const image=component.evidenceImageUrl;
    old.next(new Blob()); expect(component.evidenceImageUrl).toBe(image);
    component.ngOnDestroy();
  });

  it('advances only after saving and skips events waiting for evidence', () => {
    const {component,api}=setup(); api.markAsReviewed.and.returnValue(of(undefined));
    const first=component.currentViolation!;
    component.sessionViolations=[first,{...first,id:8,reviewDecision:'InsufficientEvidence'},{...first,id:9,reviewNotes:''}];
    component.selectedDecision='NoViolation'; component.saveSelected(true);
    expect(api.markAsReviewed).toHaveBeenCalledWith(7,'NoViolation','Context checked',undefined);
    expect(component.currentViolation?.id).toBe(9); expect(component.reviewNotes).toBe('');
  });
  it('ignores an obsolete video source response after switching streams', () => {
    const {component,api}=setup(); const camera=new Subject<unknown>();
    api.getVideoChunks=jasmine.createSpy().and.returnValues(camera,of([{chunkIndex:1,url:'screen',metadata:{startOffsetSeconds:0,durationSeconds:60,stream:'screen'}}]));
    component.changeStream('camera'); component.changeStream('screen');
    camera.next([{chunkIndex:1,url:'old-camera',metadata:{startOffsetSeconds:0,durationSeconds:60}}]);
    expect(component.currentVideoUrl).toBe('screen'); expect(component.selectedStream).toBe('screen');
  });

});
