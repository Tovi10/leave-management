using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagement.Api.Controllers;

// Read-only lookup so the UI can offer an employee picker.
[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly LeaveManagementService _service;

    public EmployeesController(LeaveManagementService service)
    {
        _service = service;
    }

    // GET /api/employees
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_service.GetEmployees());
    }
}
