using System.Text.Json.Serialization;
using AyvalikBankHA.Api.Adapter.In.Web;
using AyvalikBankHA.Api.Adapter.Out.Persistence;
using AyvalikBankHA.Api.Adapter.Out.Persistence.Adapter;
using AyvalikBankHA.Api.Adapter.Out.Security;
using AyvalikBankHA.Api.Application.Service;
using AyvalikBankHA.Api.Config;
using AyvalikBankHA.Api.Domain.Port.In;
using AyvalikBankHA.Api.Domain.Port.Out;
using AyvalikBankHA.Api.Domain.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core (the persistence adapter's plumbing)
builder.Services.AddDbContext<BankDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Outbound adapters wire concrete implementations behind their ports
builder.Services.AddScoped<ICustomerRepositoryPort, CustomerPersistenceAdapter>();
builder.Services.AddScoped<IAccountRepositoryPort, AccountPersistenceAdapter>();
builder.Services.AddScoped<ITransactionRepositoryPort, TransactionPersistenceAdapter>();
builder.Services.AddScoped<ISettingsRepositoryPort, SettingsPersistenceAdapter>();
builder.Services.AddSingleton<IPasswordHasherPort, BCryptPasswordHasherAdapter>();

// Domain services (no Spring/JPA imports — registered as DI singletons)
builder.Services.AddSingleton<PasswordValidationService>();
builder.Services.AddSingleton<TransferDomainService>();

// Application services implement use-case interfaces
builder.Services.AddScoped<CustomerApplicationService>();
builder.Services.AddScoped(sp => sp.GetRequiredService<CustomerApplicationService>() as ICreateCustomerUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<CustomerApplicationService>() as IDeleteCustomerUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<CustomerApplicationService>() as IListCustomersUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<CustomerApplicationService>() as IChangePasswordUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<CustomerApplicationService>() as IChangeCustomerTierUseCase);

builder.Services.AddScoped<AccountApplicationService>();
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IOpenCheckingAccountUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IOpenSavingsAccountUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IOpenTimeDepositAccountUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IDepositMoneyUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IWithdrawMoneyUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as ITransferMoneyUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IGetBalanceUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IGetTransactionsUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IListAccountsUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IFreezeAccountUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IUnfreezeAccountUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as ICloseAccountUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IAccrueInterestUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IMatureTimeDepositUseCase);
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as ISetTransferFeeUseCase);

// Auth
builder.Services.AddAuthentication(BasicAuthHandler.SchemeName)
    .AddScheme<BasicAuthOptions, BasicAuthHandler>(BasicAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

// MVC + global exception handler
// Enums travel as strings ("USD", "PREMIUM"), matching the Java and Python implementations and
// the documented API. System.Text.Json otherwise expects numeric enum values on the way IN while
// the response DTOs already emit strings - an asymmetry that made this API unusable by any client
// written against the others. Pinned by AyvalikBankContractTests.
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Seed admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherPort>();
    await AdminSeeder.SeedAsync(db, hasher);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
