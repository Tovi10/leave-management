using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagement.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveManagementService _service;

    public LeaveRequestsController(LeaveManagementService service)
    {
        _service = service;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_service.GetLeaveRequests());
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        return Ok(_service.SearchLeaveRequests(name));
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        var result = _service.CreateLeaveRequest(dto);
        return result.Status switch
        {
            CreateLeaveRequestStatus.Created => Ok(result.Request),
            CreateLeaveRequestStatus.EmployeeNotFound => NotFound("Employee not found"),
            CreateLeaveRequestStatus.InsufficientVacationBalance =>
                BadRequest("Not enough vacation balance"),
            _ => throw new InvalidOperationException(
                $"Unexpected leave request creation status: {result.Status}.")
        };
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var result = await _service.ApproveLeaveRequest(id, cancellationToken);
        return result.Status switch
        {
            ApproveLeaveRequestStatus.Approved => Ok(result.Request),
            ApproveLeaveRequestStatus.NotFound => NotFound("Leave request not found"),
            ApproveLeaveRequestStatus.NotPending =>
                Conflict("Leave request is already approved or rejected"),
            _ => throw new InvalidOperationException(
                $"Unexpected leave request approval status: {result.Status}.")
        };
    }

}
