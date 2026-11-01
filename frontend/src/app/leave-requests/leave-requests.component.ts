import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { Employee, LeaveRequest } from '../models/leave-request.model';
import { LeaveRequestService } from '../services/leave-request.service';
import { LeaveRequestFormComponent } from './leave-request-form.component';

interface ApprovalFeedback {
  kind: 'success' | 'error';
  message: string;
}

@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, LeaveRequestFormComponent],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  requests: LeaveRequest[] = [];
  employees: Employee[] = [];
  loading = false;
  readonly approvingRequestIds = new Set<number>();
  readonly approvalFeedback = new Map<number, ApprovalFeedback>();

  constructor(private readonly leaveRequestService: LeaveRequestService) {}

  ngOnInit(): void {
    this.load();
    this.leaveRequestService.getEmployees()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((employees) => this.employees = employees);
  }

  load(): void {
    this.loading = true;
    this.leaveRequestService.getLeaveRequests()
      .pipe(
        finalize(() => this.loading = false),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((requests) => this.requests = requests);
  }

  approve(id: number): void {
    if (this.approvingRequestIds.has(id)) {
      return;
    }

    this.approvingRequestIds.add(id);
    this.approvalFeedback.delete(id);

    this.leaveRequestService.approveLeaveRequest(id)
      .pipe(
        finalize(() => this.approvingRequestIds.delete(id)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (approvedRequest) => {
          this.requests = this.requests.map((request) =>
            request.id === id ? { ...request, status: approvedRequest.status } : request
          );
          this.approvalFeedback.set(id, {
            kind: 'success',
            message: 'Leave request approved successfully.'
          });
        },
        error: (error: HttpErrorResponse) => {
          this.approvalFeedback.set(id, {
            kind: 'error',
            message: this.getApprovalErrorMessage(error)
          });
        }
      });
  }

  private getApprovalErrorMessage(error: HttpErrorResponse): string {
    const responseMessage = this.extractResponseMessage(error.error);
    if (responseMessage) {
      return responseMessage;
    }

    if (error.status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
    if (error.status === 400) {
      return 'The approval request was invalid. Please try again.';
    }
    if (error.status === 401) {
      return 'You must sign in before approving this request.';
    }
    if (error.status === 403) {
      return 'You do not have permission to approve this request.';
    }
    if (error.status === 404) {
      return 'This leave request could not be found. Refresh the list and try again.';
    }
    if (error.status === 409) {
      return 'This leave request has already been approved or rejected.';
    }
    if (error.status >= 500) {
      return 'The server could not approve this request. Please try again later.';
    }
    return `Could not approve the leave request (HTTP ${error.status}). Please try again.`;
  }

  private extractResponseMessage(body: unknown): string | null {
    if (typeof body === 'string') {
      try {
        return this.extractResponseMessage(JSON.parse(body) as unknown) ?? (body.trim() ? body : null);
      } catch (error) {
        if (!(error instanceof SyntaxError)) {
          throw error;
        }
        return body.trim() ? body : null;
      }
    }

    if (!body || typeof body !== 'object' || Array.isArray(body)) {
      return null;
    }

    const problem = body as Record<string, unknown>;
    const errors = problem['errors'];
    if (errors && typeof errors === 'object' && !Array.isArray(errors)) {
      for (const value of Object.values(errors as Record<string, unknown>)) {
        const messages = Array.isArray(value) ? value : [value];
        const message = messages.find(
          (item): item is string => typeof item === 'string' && item.trim().length > 0
        );
        if (message) {
          return message.trim();
        }
      }
    }

    for (const key of ['detail', 'message', 'title', 'text', 'error']) {
      const message = this.extractResponseMessage(problem[key]);
      if (message) {
        return message;
      }
    }

    return JSON.stringify(body);
  }

  isApproving(id: number): boolean {
    return this.approvingRequestIds.has(id);
  }

  typeLabel(type: number): string {
    if (type == 0) return 'Vacation';
    if (type == 1) return 'Sick';
    return 'Unpaid';
  }

  statusLabel(status: number): string {
    if (status == 0) return 'Pending';
    if (status == 1) return 'Approved';
    return 'Rejected';
  }
}
