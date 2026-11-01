import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { CreateLeaveRequest, Employee, LeaveRequest } from '../models/leave-request.model';

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

  private readonly apiUrl = 'http://localhost:5080/api/leave-requests';

  constructor(private http: HttpClient) {}

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

    this.http.post<LeaveRequest>(this.apiUrl, request).subscribe({
      next: () => {
        this.requestForm.reset();
        this.submitAttempted = false;
        this.created.emit();
      },
      error: (error: HttpErrorResponse) => {
        const problem = error.error as {
          errors?: Record<string, string[]>;
          detail?: string;
          title?: string;
        };
        const validationMessages = problem?.errors ? Object.values(problem.errors).flat() : [];
        this.submitError = validationMessages[0]
          ?? problem?.detail
          ?? problem?.title
          ?? 'Could not create the leave request. Please try again.';
      }
    });
  }
}
