using ReactApp1.Server.Data;
using ReactApp1.Server.Models;
using ReactApp1.Server.Models.Enums;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.IntegrationTest;

/// <summary>
/// Example integration test demonstrating the expected pattern.
/// Uses IClassFixture to share the DI container and database across tests.
/// </summary>
public class ExampleIntegrationTest : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ExampleIntegrationTest(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenOrder_WithValidEmployeeAndEstablishment_CreatesEmptyOrderWithOpenStatus()
    {
        // Arrange: an establishment and an employee must exist.
        var dbContext = _fixture.GetService<AppDbContext>();

        var establishment = new Establishment
        {
            EstablishmentAddressId = 0,
            Type = 1
        };
        dbContext.Establishments.Add(establishment);
        await dbContext.SaveChangesAsync();

        var employee = new Employee
        {
            Title = (int)TitleEnum.Server,
            EstablishmentId = establishment.EstablishmentId,
            AddressId = 0,
            FirstName = "Test",
            LastName = "Employee",
            Email = "test@example.com"
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var employeeAddress = new EmployeeAddress
        {
            Country = "LT",
            City = "Vilnius",
            Street = "Test St",
            StreetNumber = "1",
            EmployeeId = employee.EmployeeId
        };
        dbContext.EmployeeAddresses.Add(employeeAddress);
        await dbContext.SaveChangesAsync();

        employee.AddressId = employeeAddress.AddressId;
        await dbContext.SaveChangesAsync();

        var orderService = _fixture.GetService<IOrderService>();

        // Act - open a new order.
        var result = await orderService.OpenOrder(employee.EmployeeId, establishment.EstablishmentId);

        // Assert - the returned composite object wraps an order with Open status and no items/services/payments.
        Assert.NotNull(result);
        Assert.NotNull(result.Order);
        Assert.Equal((int)OrderStatusEnum.Open, result.Order.Status);
        Assert.Equal(employee.EmployeeId, result.Order.CreatedByEmployeeId);
        Assert.True(result.Items is null || result.Items.Count == 0);
        Assert.True(result.Services is null || result.Services.Count == 0);
        Assert.True(result.Payments is null || result.Payments.Count == 0);
    }
}
