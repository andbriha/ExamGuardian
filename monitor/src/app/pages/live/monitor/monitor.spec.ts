import { of, throwError } from 'rxjs';
import { MonitorComponent } from './monitor';

describe('Monitor session integrity', () => {
  it('does not invent sessions or attach another exam observations to an empty exam', () => {
    const students=jasmine.createSpyObj('students',['getAll']);
    const component=new MonitorComponent({} as never,{getById: () => of({id:5,title:'Exam',durationMinutes:60})} as never,students,
      {getUnreviewed: () => of([{id:1,examSessionId:999,violationType:'test',confidence:1,relativeTimestampSeconds:0,timestamp:'2026-10-14T12:00:00Z'}])} as never,
      {getByExam: () => of([])} as never,{detectChanges: () => {}} as never);
    component.examId='5'; component.loadData();
    expect(component.students).toEqual([]); expect(component.alerts).toEqual([]); expect(students.getAll).not.toHaveBeenCalled();
  });
});

describe('Monitor evidence playback', () => {
  function create() { return new MonitorComponent({} as never,{} as never,{} as never,{} as never,{} as never,{detectChanges: () => {},markForCheck: () => {}} as never); }
  it('waits for the new clip duration before seeking to an observation', () => {
    const component=create();
    component.videoChunks=[{chunkIndex:1,url:'first',metadata:{startOffsetSeconds:0,durationSeconds:5}},{chunkIndex:2,url:'second',metadata:{startOffsetSeconds:5,durationSeconds:60}}] as never;
    const video={duration:60,currentTime:0,playbackRate:1,play: () => Promise.resolve(),pause: () => {}};
    component.videoRef={nativeElement:video} as never; component.duration=5;
    component.seekToViolation({relativeTimestampSeconds:45} as never);
    expect(video.currentTime).toBe(0);
    component.onMetadataLoaded(); expect(video.currentTime).toBe(40);
  });
  it('does not carry a pending observation seek into a manually selected clip', () => {
    const component=create();
    component.videoChunks=[{chunkIndex:1,url:'first'},{chunkIndex:2,url:'second'}] as never;
    const video={duration:60,currentTime:0,playbackRate:1,play: () => Promise.resolve()};
    component.videoRef={nativeElement:video} as never;
    component.seekToSeconds(40); component.selectChunk(2); component.onMetadataLoaded();
    expect(video.currentTime).toBe(0);
  });
});

describe('Monitor signed video access', () => {
  it('renews a failed URL once, preserving the position and pause state', () => {
    const service={getVideoChunks:jasmine.createSpy().and.returnValue(of([{chunkIndex:1,url:'renewed'}]))};
    const component=new MonitorComponent({} as never,{} as never,{} as never,service as never,{} as never,{detectChanges: () => {},markForCheck: () => {}} as never);
    component.selectedSession={sessionId:7} as never; component.currentVideoUrl='expired';
    const video={duration:60,currentTime:23,playbackRate:1,play: () => Promise.resolve(),pause:jasmine.createSpy()};
    component.videoRef={nativeElement:video} as never;
    component.onVideoError(); expect(component.currentVideoUrl).toBe('renewed');
    component.onMetadataLoaded(); expect(video.currentTime).toBe(23); expect(video.pause).toHaveBeenCalled();
    component.onVideoError(); expect(service.getVideoChunks).toHaveBeenCalledTimes(1); expect(component.evidenceError).not.toBe('');
  });
});

describe('Queue recovery', () => {
  it('keeps the last queue on failure and separates waiting observations', () => {
    const api={getUnreviewed:jasmine.createSpy().and.returnValue(throwError(() => new Error('offline')))};
    const c=new MonitorComponent({} as never,{} as never,{} as never,api as never,{} as never,{detectChanges:()=>{}} as never);
    c.alerts=[{id:'1',studentName:'Anna',type:'Book',raw:{reviewDecision:'Pending'}},{id:'2',studentName:'Anna',type:'Voice',raw:{reviewDecision:'InsufficientEvidence'}}] as never;
    c.refreshAlerts(); expect(c.alerts.length).toBe(2); expect(c.alertsError).not.toBe('');
    expect(c.visibleAlerts.map(a=>a.id)).toEqual(['1']); c.queueFilter='waiting'; expect(c.visibleAlerts.map(a=>a.id)).toEqual(['2']);
    api.getUnreviewed.and.returnValue(of([])); c.refreshAlerts(); expect(c.alertsError).toBe(''); expect(c.alerts.length).toBe(0);
  });
});
