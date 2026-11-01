import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LeaveRequest } from '../models/leave-request.model';
import { LeaveRequestFormComponent } from './leave-request-form.component';
import { LeaveRequestsComponent } from './leave-requests.component';

describe('LeaveRequestFormComponent validation', () => {
  let httpTestingController: HttpTestingController;
  let component: LeaveRequestFormComponent;
  let fixture: ReturnType<typeof TestBed.createComponent<LeaveRequestFormComponent>>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeaveRequestFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LeaveRequestFormComponent);
    component = fixture.componentInstance;
    component.employees = [{ id: 1, name: 'Test Employee', annualQuota: 20 }];
    fixture.detectChanges();

  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('prevents submission when the start date is later than the end date', () => {
    component.requestForm.setValue({
      employeeId: 1,
      type: 0,
      startDate: '2026-03-02',
      endDate: '2026-03-01'
    });

    component.createRequest();
    fixture.detectChanges();

    expect(component.requestForm.errors?.['dateOrder']).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('End date must be on or after the start date.');
    httpTestingController.expectNone(request => request.method === 'POST');
  });

  it('prevents submission when leave type is missing', () => {
    component.requestForm.setValue({
      employeeId: 1,
      type: null,
      startDate: '2026-03-01',
      endDate: '2026-03-01'
    });

    component.createRequest();
    fixture.detectChanges();

    expect(component.requestForm.controls.type.hasError('required')).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Please select a leave type.');
    httpTestingController.expectNone(request => request.method === 'POST');
  });

  it('submits an equal-date one-day leave request', () => {
    component.requestForm.setValue({
      employeeId: 1,
      type: 1,
      startDate: '2026-03-01',
      endDate: '2026-03-01'
    });

    expect(component.leaveDays).toBe(1);
    component.createRequest();

    const request = httpTestingController.expectOne('http://localhost:5080/api/leave-requests');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      employeeId: 1,
      type: 1,
      startDate: '2026-03-01',
      endDate: '2026-03-01'
    });
    request.flush({ id: 2 });
    expect(component.requestForm.pristine).toBeTrue();
  });
});

describe('LeaveRequestsComponent approval', () => {
  const apiUrl = 'http://localhost:5080/api/leave-requests';
  let httpTestingController: HttpTestingController;
  let fixture: ReturnType<typeof TestBed.createComponent<LeaveRequestsComponent>>;
  let component: LeaveRequestsComponent;

  const pendingRequest: LeaveRequest = {
    id: 1,
    employeeId: 2,
    type: 0,
    startDate: '2026-03-01',
    endDate: '2026-03-01',
    status: 0,
    days: 1
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeaveRequestsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LeaveRequestsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    httpTestingController.expectOne(apiUrl).flush([pendingRequest]);
    httpTestingController.expectOne('http://localhost:5080/api/employees').flush([]);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('updates only the approved request and shows success feedback', () => {
    component.approve(pendingRequest.id);
    fixture.detectChanges();

    const approval = httpTestingController.expectOne(`${apiUrl}/1/approve`);
    expect(approval.request.method).toBe('POST');
    approval.flush({ ...pendingRequest, status: 1 });
    fixture.detectChanges();

    expect(component.requests[0].status).toBe(1);
    expect(component.approvalFeedback.get(1)?.message).toBe('Leave request approved successfully.');
    expect(fixture.nativeElement.textContent).toContain('Approved');
    expect(fixture.nativeElement.textContent).toContain('Leave request approved successfully.');
    httpTestingController.expectNone(request => request.method === 'GET' && request.url === apiUrl);
  });

  it('disables the relevant button and prevents duplicate submissions while pending', () => {
    component.approve(pendingRequest.id);
    component.approve(pendingRequest.id);
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('.approve-button');
    expect(button.disabled).toBeTrue();
    expect(button.textContent).toContain('Approving...');

    const approval = httpTestingController.expectOne(`${apiUrl}/1/approve`);
    approval.flush({ ...pendingRequest, status: 1 });
  });

  it('shows the backend error and preserves the request when approval fails', () => {
    component.approve(pendingRequest.id);
    const approval = httpTestingController.expectOne(`${apiUrl}/1/approve`);
    approval.flush('Leave request is already approved or rejected', {
      status: 409,
      statusText: 'Conflict'
    });
    fixture.detectChanges();

    expect(component.requests[0].status).toBe(0);
    expect(component.approvalFeedback.get(1)?.kind).toBe('error');
    expect(fixture.nativeElement.textContent).toContain(
      'Leave request is already approved or rejected'
    );
    expect(fixture.nativeElement.querySelector('.approve-button').disabled).toBeFalse();
    httpTestingController.expectNone(request => request.method === 'GET' && request.url === apiUrl);
  });

  it('extracts validation messages from structured API errors', () => {
    component.approve(pendingRequest.id);
    const approval = httpTestingController.expectOne(`${apiUrl}/1/approve`);
    approval.flush(
      { errors: { status: ['The request cannot be approved in its current state.'] } },
      { status: 400, statusText: 'Bad Request' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'The request cannot be approved in its current state.'
    );
    expect(component.requests[0].status).toBe(0);
  });

  it('extracts messages from JSON text bodies as well as parsed JSON objects', () => {
    component.approve(pendingRequest.id);
    httpTestingController.expectOne(`${apiUrl}/1/approve`).flush(
      '{"detail":"The request is no longer pending."}',
      { status: 409, statusText: 'Conflict' }
    );
    fixture.detectChanges();

    expect(component.approvalFeedback.get(1)?.message).toBe(
      'The request is no longer pending.'
    );
  });

  it('preserves plain text in Angular response wrappers and falls back for an empty body', () => {
    component.approve(pendingRequest.id);
    httpTestingController.expectOne(`${apiUrl}/1/approve`).flush(
      { error: new SyntaxError('Invalid JSON'), text: 'Approval failed in plain text.' },
      { status: 500, statusText: 'Internal Server Error' }
    );
    fixture.detectChanges();
    expect(component.approvalFeedback.get(1)?.message).toBe('Approval failed in plain text.');

    component.approve(pendingRequest.id);
    httpTestingController.expectOne(`${apiUrl}/1/approve`).flush(null, {
      status: 409,
      statusText: 'Conflict'
    });
    fixture.detectChanges();
    expect(component.approvalFeedback.get(1)?.message).toBe(
      'This leave request has already been approved or rejected.'
    );
  });
});
