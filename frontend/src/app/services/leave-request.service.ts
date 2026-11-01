import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateLeaveRequest, Employee, LeaveRequest } from '../models/leave-request.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class LeaveRequestService {
  private readonly apiUrl = `${environment.apiUrl}/leave-requests`;
  private readonly employeesUrl = `${environment.apiUrl}/employees`;

  constructor(private readonly http: HttpClient) {}

  getEmployees(): Observable<Employee[]> {
    return this.http.get<Employee[]>(this.employeesUrl);
  }

  getLeaveRequests(): Observable<LeaveRequest[]> {
    return this.http.get<LeaveRequest[]>(this.apiUrl);
  }

  createLeaveRequest(request: CreateLeaveRequest): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(this.apiUrl, request);
  }

  approveLeaveRequest(id: number): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(`${this.apiUrl}/${id}/approve`, {});
  }
}
