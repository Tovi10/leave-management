using LeaveManagement.Api.Data;
using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LeaveManagement.Tests;

public class LeaveRequestsTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _database;

    public LeaveRequestsTests(TestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void Create_WithinAvailableBalance_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = CreateEmployeeWithApprovedVacation(db, 15);

        var controller = new LeaveRequestsController(db);

        // Act: request 3 days with 5 days remaining.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, db.LeaveRequests.Count());
    }

    [Fact]
    public void Create_ExactlyRemainingBalance_Succeeds()
    {
        using var db = _database.NewDb();
        var emp = CreateEmployeeWithApprovedVacation(db, 17);
        var controller = new LeaveRequestsController(db);

        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, db.LeaveRequests.Count());
    }

    [Fact]
    public void Create_ExceedingAvailableBalance_ReturnsBadRequest()
    {
        using var db = _database.NewDb();
        var emp = CreateEmployeeWithApprovedVacation(db, 18);
        var controller = new LeaveRequestsController(db);

        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    private static Employee CreateEmployeeWithApprovedVacation(
        LeaveDbContext db,
        int usedDays)
    {
        var employee = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(employee);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employee.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, usedDays),
            Days = usedDays,
            Status = LeaveStatus.Approved
        });
        db.SaveChanges();

        return employee;
    }
}
