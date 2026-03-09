using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ReactApp1.Server.Data;
using ReactApp1.Server.Data.Repositories;
using ReactApp1.Server.Services;
using Stripe;

namespace ReactApp1.Server.UnitTest;

/// <summary>
/// Shared test fixture for integration tests. Sets up a real DI container with:
/// - SQLite in-memory database (real EF Core, real repositories)
/// - Real service implementations wired through DI
/// - Mocked external boundaries (Stripe payments, SMS)
///
/// Usage: implement IClassFixture&lt;IntegrationTestFixture&gt; on your test class,
/// then resolve services via GetService&lt;T&gt;().
/// </summary>
public class IntegrationTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly SqliteConnection _connection;

    /// <summary>Mocked Stripe payment intent service. Set up expectations in tests that exercise payments.</summary>
    public Mock<PaymentIntentService> MockPaymentIntentService { get; }

    /// <summary>Mocked Stripe refund service. Set up expectations in tests that exercise refunds.</summary>
    public Mock<RefundService> MockRefundService { get; }

    public IntegrationTestFixture()
    {
        // SQLite in-memory database — stays alive as long as the connection is open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        MockPaymentIntentService = new Mock<PaymentIntentService>();
        MockRefundService = new Mock<RefundService>();

        var services = new ServiceCollection();

        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(_connection));

        // Logging
        services.AddLogging(builder => builder.AddConsole());

        // Repositories — all real implementations backed by the SQLite database.
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
        services.AddScoped<ISharedSearchesRepository, SharedSearchesRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IFullOrderRepository, FullOrderRepository>();
        services.AddScoped<IFullOrderServiceRepository, FullOrderServiceRepository>();
        services.AddScoped<IGiftCardRepository, GiftCardRepository>();
        services.AddScoped<IWorkingHoursRepository, WorkingHoursRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();
        services.AddScoped<ITaxRepository, TaxRepository>();
        services.AddScoped<IFullOrderTaxRepository, FullOrderTaxRepository>();
        services.AddScoped<IFullOrderServiceTaxRepository, FullOrderServiceTaxRepository>();

        // Services — real implementations using real repositories.
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEstablishmentService, EstablishmentService>();
        services.AddScoped<ISharedSearchesService, SharedSearchesService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IGiftCardService, GiftCardService>();
        services.AddScoped<IServiceService, ServiceService>();
        services.AddScoped<IWorkingHoursService, WorkingHoursService>();
        services.AddScoped<ITaxService, TaxesService>();
        services.AddScoped<IDiscountService, ReactApp1.Server.Services.DiscountService>();

        // Stripe — mocked. These are external payment boundaries, not tested in integration tests.
        services.AddScoped(_ => MockPaymentIntentService.Object);
        services.AddScoped(_ => MockRefundService.Object);
        services.AddScoped<IPaymentService, PaymentService>();

        // Reservation service — real implementation with a dummy SMS API key.
        // The SMS client is constructed internally but won't send real messages in tests.
        services.AddScoped<IReservationService>(provider =>
        {
            var reservationRepository = provider.GetRequiredService<IReservationRepository>();
            var logger = provider.GetRequiredService<ILogger<OrderService>>();
            return new ReservationService(reservationRepository, logger, "test-api-key");
        });

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();

        // Create the database schema from the EF Core model.
        var dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
    }

    /// <summary>Resolves a service from the DI container.</summary>
    public T GetService<T>() where T : notnull =>
        _scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// Creates a mock IPrincipal for authenticated test scenarios.
    /// The principal has identity "TestUser" and the "MasterAdmin" role.
    /// </summary>
    public static IPrincipal CreateTestPrincipal(string username = "TestUser", string role = "MasterAdmin")
    {
        var mockIdentity = new Mock<IIdentity>();
        mockIdentity.Setup(i => i.Name).Returns(username);
        mockIdentity.Setup(i => i.IsAuthenticated).Returns(true);

        var mockPrincipal = new Mock<IPrincipal>();
        mockPrincipal.Setup(p => p.Identity).Returns(mockIdentity.Object);
        mockPrincipal.Setup(p => p.IsInRole(It.IsAny<string>()))
            .Returns((string r) => r == role);

        return mockPrincipal.Object;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
        _connection.Close();
    }
}
