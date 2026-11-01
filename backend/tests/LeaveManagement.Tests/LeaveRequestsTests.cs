using System.ComponentModel.DataAnnotations;
using LeaveManagement.Api.Data;
using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Xunit.Sdk;
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

        var controller = new LeaveRequestsController(new LeaveManagementService(db));

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
        var controller = new LeaveRequestsController(new LeaveManagementService(db));

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
        var controller = new LeaveRequestsController(new LeaveManagementService(db));

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

    [Fact]
    public void Create_StartDateAfterEndDate_IsRejectedByDtoValidation()
    {
        var dto = new CreateLeaveRequestDto
        {
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 3),
            EndDate = new DateTime(2026, 3, 1)
        };

        var errors = Validate(dto);
        Assert.Contains(errors, error =>
            error.ErrorMessage == "End date must be on or after start date.");
    }

    [Fact]
    public void Create_MissingLeaveType_IsRejectedByDtoValidation()
    {
        var dto = new CreateLeaveRequestDto
        {
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 1)
        };

        var errors = Validate(dto);
        Assert.Contains(errors, error => error.ErrorMessage == "Leave type is required.");
    }

    [Fact]
    public void Create_InvalidLeaveType_IsRejectedByDtoValidation()
    {
        var dto = new CreateLeaveRequestDto
        {
            Type = (LeaveType)99,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 1)
        };
        var errors = Validate(dto);
        Assert.Contains(errors, error => error.ErrorMessage == "Select a valid leave type.");
        Assert.Contains(errors, error => error.ErrorMessage == "Select a valid leave type.");
    }

    [Fact]
    public void Create_OneDayLeaveRequest_SucceedsWithOneDay()
    {
        using var db = _database.NewDb();
        var employee = new Employee { Name = "Test Employee", AnnualQuota = 20 };
        db.Employees.Add(employee);
        db.SaveChanges();
        var controller = new LeaveRequestsController(new LeaveManagementService(db));

        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = employee.Id,
            Type = LeaveType.Sick,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 1)
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, Assert.Single(db.LeaveRequests).Days);
    }

    [Fact]
    public async Task Approve_PendingRequest_ApprovesSuccessfully()
    {
        using var db = _database.NewDb();
        var request = CreateLeaveRequest(db);
        var controller = new LeaveRequestsController(new LeaveManagementService(db));

        var result = await controller.Approve(request.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await db.Entry(request).ReloadAsync();
        Assert.Equal(LeaveStatus.Approved, request.Status);
    }

    [Fact]
    public async Task Approve_NonexistentRequest_ReturnsNotFound()
    {
        using var db = _database.NewDb();
        var controller = new LeaveRequestsController(new LeaveManagementService(db));

        var result = await controller.Approve(42, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Approve_AlreadyApprovedRequest_ReturnsConflict()
    {
        using var db = _database.NewDb();
        var request = CreateLeaveRequest(db, LeaveStatus.Approved);
        var controller = new LeaveRequestsController(new LeaveManagementService(db));

        var result = await controller.Approve(request.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(LeaveStatus.Approved, request.Status);
    }

    [Fact]
    public async Task Approve_AlreadyRejectedRequest_ReturnsConflict()
    {
        using var db = _database.NewDb();
        var request = CreateLeaveRequest(db, LeaveStatus.Rejected);
        var controller = new LeaveRequestsController(new LeaveManagementService(db));

        var result = await controller.Approve(request.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(LeaveStatus.Rejected, request.Status);
    }

    [Fact]
    public async Task Approve_ConcurrentAttempts_OnlyOneSucceeds()
    {
        if (TestDatabase.UseSqlite)
            throw SkipException.ForSkip("Concurrent approval locking is PostgreSQL-specific.");

        using var setupDb = _database.NewDb();
        var request = CreateLeaveRequest(setupDb);
        var databaseName = setupDb.Database.GetDbConnection().Database;

        using var firstDb = _database.NewDb(databaseName);
        using var secondDb = _database.NewDb(databaseName);
        Assert.True(await firstDb.LeaveRequests.AnyAsync(r => r.Id == request.Id));
        Assert.True(await secondDb.LeaveRequests.AnyAsync(r => r.Id == request.Id));
        var firstController = new LeaveRequestsController(new LeaveManagementService(firstDb));
        var secondController = new LeaveRequestsController(new LeaveManagementService(secondDb));
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var firstAttempt = Task.Run(async () =>
        {
            await start.Task;
            return await firstController.Approve(request.Id, CancellationToken.None);
        });
        var secondAttempt = Task.Run(async () =>
        {
            await start.Task;
            return await secondController.Approve(request.Id, CancellationToken.None);
        });
        start.SetResult();

        var results = await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Single(results.OfType<OkObjectResult>());
        Assert.Single(results.OfType<ConflictObjectResult>());

        using var verifyDb = _database.NewDb(databaseName);
        var savedRequest = await verifyDb.LeaveRequests
            .AsNoTracking()
            .SingleAsync(r => r.Id == request.Id);
        Assert.Equal(LeaveStatus.Approved, savedRequest.Status);
    }

    private static LeaveRequest CreateLeaveRequest(
        LeaveDbContext db,
        LeaveStatus status = LeaveStatus.Pending)
    {
        var employee = new Employee { Name = "Test Employee", AnnualQuota = 20 };
        db.Employees.Add(employee);
        db.SaveChanges();

        var request = new LeaveRequest
        {
            EmployeeId = employee.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 1),
            Days = 1,
            Status = status
        };
        db.LeaveRequests.Add(request);
        db.SaveChanges();
        return request;
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

    private static List<ValidationResult> Validate(CreateLeaveRequestDto dto)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
        return errors;
    }
}
