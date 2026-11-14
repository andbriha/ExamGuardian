import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GroupCredentialsComponent } from './group-credentials';

describe('Group credentials confirmation', () => {
  beforeEach(() => TestBed.configureTestingModule({imports:[GroupCredentialsComponent], providers:[provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()]}));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('downloads saved passwords using GET without rotating them', () => {
    const c=TestBed.createComponent(GroupCredentialsComponent).componentInstance;
    c.group='A';c.members=[{id:'one',fullName:'One',login:'one',role:'Student',group:'A'}];
    spyOn(URL,'createObjectURL').and.returnValue('blob:test');spyOn(HTMLAnchorElement.prototype,'click');
    c.download(false,'pdf');const request=TestBed.inject(HttpTestingController).expectOne(r=>r.url.includes('format=pdf'));
    expect(request.request.method).toBe('GET');expect(request.request.params.get('group')).toBe('A');request.flush(new Blob(['pdf']));expect(c.filename).toBe('credentials-A.pdf');
  });
  it('does not reset passwords without confirmation, or after changing groups', () => {
    const component = TestBed.createComponent(GroupCredentialsComponent).componentInstance;
    component.group = 'A'; component.members = [{id:'one',fullName:'One',login:'one',role:'Student',group:'A'}];
    component.download(true);
    component.prepare(); component.group = 'B'; component.download(true);
    TestBed.inject(HttpTestingController).expectNone(r => r.method === 'POST');
    expect(component.busy).toBeFalse();
  });
  it('sends the confirmed membership, prevents duplicate requests and keeps a download link', () => {
    const component = TestBed.createComponent(GroupCredentialsComponent).componentInstance;
    component.group = 'A'; component.members = [{id:'one',fullName:'One',login:'one',role:'Student',group:'A'}];
    spyOn(URL, 'createObjectURL').and.returnValue('blob:test'); spyOn(HTMLAnchorElement.prototype, 'click');
    component.prepare(); component.download(true); component.download(true);
    const request = TestBed.inject(HttpTestingController).expectOne(r => r.method === 'POST');
    expect(request.request.body).toEqual({group:'A',studentIds:['one']});
    request.flush(new Blob(['username,password\none,secret']));
    expect(component.result).toBe('blob:test'); expect(component.busy).toBeFalse(); expect(component.confirmation).toBeFalse();
  });
});
