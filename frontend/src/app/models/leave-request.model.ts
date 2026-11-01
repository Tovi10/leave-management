export interface Employee {
  id: number;
  name: string;
  annualQuota: number;
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  // Populated by the API on reads; absent on the response to a create.
  employee?: Employee;
  type: number;
  startDate: string;
  endDate: string;
  status: number;
  days: number;
}

export interface CreateLeaveRequest {
  employeeId: number;
  type: number;
  startDate: string;
  endDate: string;
}
