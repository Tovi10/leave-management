using System.ComponentModel.DataAnnotations;
using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Controllers;

// NOTE: This controller was written quickly for a POC.
// It does data access, business logic and validation all in one place.
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveDbContext _db;

    public LeaveRequestsController(LeaveDbContext db)
    {
        _db = db;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        var all = _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
        return Ok(all);
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        // Build a quick query to filter by the employee name.
        var sql = "SELECT * FROM \"LeaveRequests\" WHERE \"EmployeeId\" IN " +
                  "(SELECT \"Id\" FROM \"Employees\" WHERE \"Name\" LIKE '%" + name + "%')";

        var results = _db.LeaveRequests
            .FromSqlRaw(sql)
            .ToList();

        return Ok(results);
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(
            dto,
            new ValidationContext(dto),
            validationResults,
            validateAllProperties: true);

        if (validationResults.Count > 0)
        {
            var errors = validationResults
                .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                    .Select(member => new { member, result.ErrorMessage }))
                .GroupBy(error => error.member)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage ?? "Invalid value.").ToArray());
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return NotFound("Employee not found");

        var days = (dto.EndDate!.Value - dto.StartDate!.Value).Days + 1;
        if (days <= 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(dto.EndDate)] = ["Leave duration must be greater than zero days."]
            }));
        }

        // How many vacation days has the employee already used this year?
        var used = _db.LeaveRequests
            .Where(r => r.EmployeeId == dto.EmployeeId
                        && r.Type == LeaveType.Vacation
                        && r.Status == LeaveStatus.Approved)
            .Sum(r => r.Days);

        // Make sure the request does not exceed the quota.
        if (dto.Type == LeaveType.Vacation && days > employee.AnnualQuota - used)
        {
            return BadRequest("Not enough vacation balance");
        }

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

        return Ok(request);
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var request = await _db.LeaveRequests
            .FromSqlInterpolated(
                $"SELECT * FROM \"LeaveRequests\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (request is null)
            return NotFound("Leave request not found");

        if (request.Status != LeaveStatus.Pending)
            return Conflict("Leave request is already approved or rejected");

        request.Status = LeaveStatus.Approved;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(request);
    }

}
