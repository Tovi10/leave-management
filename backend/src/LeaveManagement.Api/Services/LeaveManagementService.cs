using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Services;

public enum CreateLeaveRequestStatus
{
    Created,
    EmployeeNotFound,
    InsufficientVacationBalance
}

public enum ApproveLeaveRequestStatus
{
    Approved,
    NotFound,
    NotPending
}

public sealed record CreateLeaveRequestResult(
    CreateLeaveRequestStatus Status,
    LeaveRequest? Request = null);

public sealed record ApproveLeaveRequestResult(
    ApproveLeaveRequestStatus Status,
    LeaveRequest? Request = null);

public sealed class LeaveManagementService
{
    private readonly LeaveDbContext _db;

    public LeaveManagementService(LeaveDbContext db)
    {
        _db = db;
    }

    public List<Employee> GetEmployees()
    {
        return _db.Employees.ToList();
    }

    public List<LeaveRequest> GetLeaveRequests()
    {
        return _db.LeaveRequests
            .Include(request => request.Employee)
            .OrderByDescending(request => request.StartDate)
            .ToList();
    }

    public List<LeaveRequest> SearchLeaveRequests(string name)
    {
        return _db.LeaveRequests
            .Where(request => _db.Employees
                .Where(employee => employee.Name.Contains(name))
                .Select(employee => employee.Id)
                .Contains(request.EmployeeId))
            .ToList();
    }

    public CreateLeaveRequestResult CreateLeaveRequest(CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee is null)
            return new CreateLeaveRequestResult(CreateLeaveRequestStatus.EmployeeNotFound);

        var days = (dto.EndDate!.Value - dto.StartDate!.Value).Days + 1;
        var used = _db.LeaveRequests
            .Where(request => request.EmployeeId == dto.EmployeeId
                              && request.Type == LeaveType.Vacation
                              && request.Status == LeaveStatus.Approved)
            .Sum(request => request.Days);

        if (dto.Type == LeaveType.Vacation && days > employee.AnnualQuota - used)
            return new CreateLeaveRequestResult(
                CreateLeaveRequestStatus.InsufficientVacationBalance);

        var request = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type!.Value,
            StartDate = dto.StartDate.Value,
            EndDate = dto.EndDate.Value,
            Days = days,
            Status = LeaveStatus.Pending
        };

        _db.LeaveRequests.Add(request);
        _db.SaveChanges();

        return new CreateLeaveRequestResult(CreateLeaveRequestStatus.Created, request);
    }

    public async Task<ApproveLeaveRequestResult> ApproveLeaveRequest(
        int id,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var request = _db.Database.IsNpgsql()
            ? await _db.LeaveRequests
                .FromSqlInterpolated(
                    $"SELECT * FROM \"LeaveRequests\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : await _db.LeaveRequests.SingleOrDefaultAsync(
                leaveRequest => leaveRequest.Id == id,
                cancellationToken);

        if (request is null)
            return new ApproveLeaveRequestResult(ApproveLeaveRequestStatus.NotFound);

        if (request.Status != LeaveStatus.Pending)
            return new ApproveLeaveRequestResult(ApproveLeaveRequestStatus.NotPending);

        request.Status = LeaveStatus.Approved;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ApproveLeaveRequestResult(ApproveLeaveRequestStatus.Approved, request);
    }
}
