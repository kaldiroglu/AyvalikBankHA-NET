using AyvalikBankHA.Api.Application.Service;
using AyvalikBankHA.Api.Domain.Model;
using AyvalikBankHA.Api.Domain.Port.In;
using AyvalikBankHA.Api.Domain.Port.Out;
using AyvalikBankHA.Api.Domain.Service;
using FluentAssertions;
using NSubstitute;

namespace AyvalikBankHA.Tests;

/// <summary>
/// Any authenticated customer could previously operate on any account given its id, and set any
/// other customer's password. UnauthorizedAccessException was mapped to 403 but never thrown by
/// production code. Mirrors AyvalikBankHA-JAVA Refactorings.md entry 3.
/// </summary>
public class AccountOwnershipTests
{
    private readonly IAccountRepositoryPort accounts = Substitute.For<IAccountRepositoryPort>();
    private readonly ICustomerRepositoryPort customers = Substitute.For<ICustomerRepositoryPort>();
    private readonly ITransactionRepositoryPort transactions = Substitute.For<ITransactionRepositoryPort>();
    private readonly ISettingsRepositoryPort settings = Substitute.For<ISettingsRepositoryPort>();
    private readonly AccountApplicationService service;

    public AccountOwnershipTests() =>
        service = new AccountApplicationService(accounts, customers, transactions, settings, new TransferDomainService());

    private CheckingAccount GivenAccountOwnedBy(Guid owner)
    {
        var account = CheckingAccount.Open(owner, Currency.USD);
        accounts.FindByIdAsync(account.Id).Returns(account);
        return account;
    }

    [Fact]
    public async Task Deposit_into_another_customers_account_is_rejected()
    {
        var account = GivenAccountOwnedBy(Guid.NewGuid());
        var intruder = Guid.NewGuid();

        var act = async () => await service.DepositAsync(
            new IDepositMoneyUseCase.Command(intruder, account.Id, TransactionAmount.Of(100m, Currency.USD)));

        await act.Should().ThrowAsync<Api.Application.Exception.UnauthorizedAccessException>();
        await accounts.DidNotReceive().SaveAsync(Arg.Any<Account>());
    }

    [Fact]
    public async Task Withdrawal_from_another_customers_account_is_rejected()
    {
        var account = GivenAccountOwnedBy(Guid.NewGuid());
        var intruder = Guid.NewGuid();

        var act = async () => await service.WithdrawAsync(
            new IWithdrawMoneyUseCase.Command(intruder, account.Id, TransactionAmount.Of(10m, Currency.USD)));

        await act.Should().ThrowAsync<Api.Application.Exception.UnauthorizedAccessException>();
        await accounts.DidNotReceive().SaveAsync(Arg.Any<Account>());
    }

    [Fact]
    public async Task Transfer_out_of_another_customers_account_is_rejected()
    {
        var source = GivenAccountOwnedBy(Guid.NewGuid());
        var intruder = Guid.NewGuid();
        var target = GivenAccountOwnedBy(intruder);

        var act = async () => await service.TransferAsync(
            new ITransferMoneyUseCase.Command(intruder, source.Id, target.Id, TransactionAmount.Of(10m, Currency.USD)));

        await act.Should().ThrowAsync<Api.Application.Exception.UnauthorizedAccessException>();
        await accounts.DidNotReceive().SaveAsync(Arg.Any<Account>());
    }

    [Fact]
    public async Task Reading_another_customers_balance_is_rejected()
    {
        var account = GivenAccountOwnedBy(Guid.NewGuid());

        var act = async () => await service.GetBalanceAsync(Guid.NewGuid(), account.Id);

        await act.Should().ThrowAsync<Api.Application.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Reading_another_customers_transactions_is_rejected()
    {
        var account = GivenAccountOwnedBy(Guid.NewGuid());

        var act = async () => await service.GetTransactionsAsync(Guid.NewGuid(), account.Id);

        await act.Should().ThrowAsync<Api.Application.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Listing_another_customers_accounts_is_rejected()
    {
        var act = async () => await service.ListAccountsAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<Api.Application.Exception.UnauthorizedAccessException>();
    }

    [Fact]
    public async Task The_transfer_target_is_deliberately_not_ownership_checked()
    {
        var sender = Guid.NewGuid();
        var source = CheckingAccount.Open(sender, Currency.USD);
        source.Deposit(TransactionAmount.Of(500m, Currency.USD));
        var target = CheckingAccount.Open(Guid.NewGuid(), Currency.USD);
        accounts.FindByIdAsync(source.Id).Returns(source);
        accounts.FindByIdAsync(target.Id).Returns(target);
        customers.FindByIdAsync(sender).Returns(Customer.Create("Sender", "s@x.dev", "hash"));
        settings.GetTransferFeePercentAsync().Returns(1.0m);

        await service.TransferAsync(
            new ITransferMoneyUseCase.Command(sender, source.Id, target.Id, TransactionAmount.Of(100m, Currency.USD)));

        target.Balance.Amount.Should().Be(100m);
    }
}
