import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { CreateLeaveRequest, Employee } from '../models/leave-request.model';
import { LeaveRequestService } from '../services/leave-request.service';

function validateLeaveRange(control: AbstractControl): ValidationErrors | null {
  const startDate = control.get('startDate')?.value as string;
  const endDate = control.get('endDate')?.value as string;

  if (!startDate || !endDate) {
    return null;
  }

  if (startDate > endDate) {
    return { dateOrder: true };
  }

  const start = Date.parse(`${startDate}T00:00:00Z`);
  const end = Date.parse(`${endDate}T00:00:00Z`);
  if (!Number.isFinite(start) || !Number.isFinite(end)) {
    return { invalidDate: true };
  }

  const days = Math.floor((end - start) / 86_400_000) + 1;
  return days > 0 ? null : { positiveDays: true };
}

@Component({
  selector: 'app-leave-request-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-request-form.component.html',
  styleUrls: ['./leave-request-form.component.css']
})
export class LeaveRequestFormComponent {
  private readonly destroyRef = inject(DestroyRef);

  @Input() employees: Employee[] = [];
  @Output() created = new EventEmitter<void>();

  submitAttempted = false;
  submitError = '';

  readonly requestForm = new FormGroup({
    employeeId: new FormControl<number | null>(null, Validators.required),
    type: new FormControl<number | null>(null, Validators.required),
    startDate: new FormControl('', { nonNullable: true, validators: Validators.required }),
    endDate: new FormControl('', { nonNullable: true, validators: Validators.required })
  }, { validators: validateLeaveRange });

  constructor(private readonly leaveRequestService: LeaveRequestService) {}

  get leaveDays(): number | null {
    const { startDate, endDate } = this.requestForm.getRawValue();
    if (!startDate || !endDate) {
      return null;
    }

    const start = Date.parse(`${startDate}T00:00:00Z`);
    const end = Date.parse(`${endDate}T00:00:00Z`);
    if (!Number.isFinite(start) || !Number.isFinite(end)) {
      return null;
    }

    const days = Math.floor((end - start) / 86_400_000) + 1;
    return days > 0 ? days : null;
  }

  createRequest(): void {
    this.submitAttempted = true;
    this.submitError = '';
    if (this.requestForm.invalid) {
      this.requestForm.markAllAsTouched();
      return;
    }

    const request: CreateLeaveRequest = {
      employeeId: this.requestForm.controls.employeeId.value!,
      type: this.requestForm.controls.type.value!,
      startDate: this.requestForm.controls.startDate.value,
      endDate: this.requestForm.controls.endDate.value
    };

    this.leaveRequestService.createLeaveRequest(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.requestForm.reset();
          this.submitAttempted = false;
          this.created.emit();
        },
        error: (error: HttpErrorResponse) => {
          const problem = this.getProblemDetails(error.error);
          const validationMessages = problem?.['errors'];
          const validationMessage = validationMessages && typeof validationMessages === 'object'
            ? Object.values(validationMessages).flat().find(
              (message): message is string => typeof message === 'string'
            )
            : undefined;
          this.submitError = validationMessage
            ?? this.getProblemMessage(problem, 'detail')
            ?? this.getProblemMessage(problem, 'title')
            ?? 'Could not create the leave request. Please try again.';
        }
      });
  }

  private getProblemDetails(body: unknown): Record<string, unknown> | null {
    return body !== null && typeof body === 'object' && !Array.isArray(body)
      ? body as Record<string, unknown>
      : null;
  }

  private getProblemMessage(problem: Record<string, unknown> | null, key: string): string | undefined {
    const value = problem?.[key];
    return typeof value === 'string' ? value : undefined;
  }
}
