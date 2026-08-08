using AyvalikBankHA.Api.Application.Exception;
using AyvalikBankHA.Api.Application.Service;
using AyvalikBankHA.Api.Domain.Model;
using AyvalikBankHA.Api.Domain.Port.In;
using AyvalikBankHA.Api.Domain.Port.Out;
using AyvalikBankHA.Api.Domain.Service;
using FluentAssertions;
using NSubstitute;

namespace AyvalikBankHA.Tests;

/// <summary>
/// Orchestration tests for the application service: repository interaction, exception translation
/// and fee/limit policy. Ported from AyvalikBankHA-JAVA's AccountApplicationServiceTest.
///
/// These cannot be replaced by the shared HTTP contract suite: they assert which port was called
/// and which exception type was raised, not just the resulting status code.
/// </summary>
public class AccountApplicationServiceTests
{
    private readonly IAccountRepositoryPort _accounts = Substitute.For<IAccountRepositoryPort>();
    private readonly ICustomerRepositoryPort _customers = Substitute.For<ICustomerRepositoryPort>();
    private readonly ITransactionRepositoryPort _transactions = Substitute.For<ITransactionRepositoryPort>();
    private readonly ISettingsRepositoryPort _settings = Substitute.For<ISettingsRepositoryPort>();
    private readonly AccountApplicationService _service;

    public AccountApplicationServiceTests()
    {
        _service = new AccountApplicationService(
            _accounts, _customers, _transactions, _settings, new TransferDomainService());
        _accounts.SaveAsync(Arg.Any<Account>()).Returns(ci => ci.Arg<Account>());
        _transactions.SaveAsync(Arg.Any<Transaction>()).Returns(ci => ci.Arg<Transaction>());
    }

    private Guid GivenCustomer(CustomerTier tier = CustomerTier.STANDARD)
    {
        var c = Customer.Create("X", $"{Guid.NewGuid()}@x.dev", "hash");
        c.ChangeTier(tier);
        _customers.ExistsByIdAsync(c.Id).Returns(true);
        _customers.FindByIdAsync(c.Id).Returns(c);
        return c.Id;
    }

    private CheckingAccount GivenChecking(Guid owner, decimal balance = 0m)
    {
        var a = CheckingAccount.Open(owner, Currency.USD);
        if (balance > 0) a.Deposit(new Money(balance, Currency.USD));
        _accounts.FindByIdAsync(a.Id).Returns(a);
        return a;
    }

    // ── opening ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Opens_checking_for_an_existing_customer()
    {
        var owner = GivenCustomer();

        var a = await _service.OpenCheckingAsync(
            new IOpenCheckingAccountUseCase.Command(owner, Currency.USD, new Money(0m, Currency.USD)));

        a.OwnerId.Should().Be(owner);
        await _accounts.Received(1).SaveAsync(Arg.Any<Account>());
    }

    [Fact]
    public async Task Opens_savings_for_an_existing_customer()
    {
        var owner = GivenCustomer();

        var a = await _service.OpenSavingsAsync(
            new IOpenSavingsAccountUseCase.Command(owner, Currency.USD, 0.05m));

        a.Should().BeOfType<SavingsAccount>();
    }

    [Fact]
    public async Task Opens_time_deposit_for_an_existing_customer()
    {
        var owner = GivenCustomer();

        var a = await _service.OpenTimeDepositAsync(new IOpenTimeDepositAccountUseCase.Command(
            owner, Currency.USD, new Money(1000m, Currency.USD),
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 0.05m));

        a.Should().BeOfType<TimeDepositAccount>();
    }

    [Fact]
    public async Task Rejects_opening_an_account_for_a_missing_customer()
    {
        var unknown = Guid.NewGuid();
        _customers.ExistsByIdAsync(unknown).Returns(false);

        var act = () => _service.OpenCheckingAsync(
            new IOpenCheckingAccountUseCase.Command(unknown, Currency.USD, new Money(0m, Currency.USD)));

        await act.Should().ThrowAsync<CustomerNotFoundException>();
    }

    // ── deposit / withdraw ────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_credits_the_account_and_records_a_transaction()
    {
        var owner = GivenCustomer();
        var a = GivenChecking(owner);

        var tx = await _service.DepositAsync(
            new IDepositMoneyUseCase.Command(owner, a.Id, new Money(200m, Currency.USD)));

        tx.Type.Should().Be(TransactionType.DEPOSIT);
        a.Balance.Amount.Should().Be(200m);
        await _transactions.Received(1).SaveAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Deposit_into_a_missing_account_is_not_found()
    {
        var id = Guid.NewGuid();
        _accounts.FindByIdAsync(id).Returns((Account?)null);

        var act = () => _service.DepositAsync(
            new IDepositMoneyUseCase.Command(Guid.NewGuid(), id, new Money(100m, Currency.USD)));

        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    [Fact]
    public async Task Withdrawal_beyond_the_balance_is_rejected()
    {
        var owner = GivenCustomer();
        var a = GivenChecking(owner, 100m);

        var act = () => _service.WithdrawAsync(
            new IWithdrawMoneyUseCase.Command(owner, a.Id, new Money(500m, Currency.USD)));

        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task Withdrawal_from_a_frozen_account_reports_not_operable()
    {
        var owner = GivenCustomer();
        var a = GivenChecking(owner, 500m);
        a.Freeze();

        var act = () => _service.WithdrawAsync(
            new IWithdrawMoneyUseCase.Command(owner, a.Id, new Money(10m, Currency.USD)));

        await act.Should().ThrowAsync<AccountNotOperableException>();
    }

    // ── transfer and fees ─────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_between_one_customers_own_accounts_is_free()
    {
        var owner = GivenCustomer();
        var src = GivenChecking(owner, 500m);
        var tgt = GivenChecking(owner);
        _settings.GetTransferFeePercentAsync().Returns(1.0m);

        await _service.TransferAsync(new ITransferMoneyUseCase.Command(
            owner, src.Id, tgt.Id, new Money(200m, Currency.USD)));

        src.Balance.Amount.Should().Be(300m);   // no fee
        tgt.Balance.Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Transfer_between_different_customers_deducts_the_fee()
    {
        var sender = GivenCustomer();
        var recipient = GivenCustomer();
        var src = GivenChecking(sender, 1000m);
        var tgt = GivenChecking(recipient);
        _settings.GetTransferFeePercentAsync().Returns(1.0m);

        await _service.TransferAsync(new ITransferMoneyUseCase.Command(
            sender, src.Id, tgt.Id, new Money(200m, Currency.USD)));

        src.Balance.Amount.Should().Be(798m);   // 200 + 1% fee
        tgt.Balance.Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Premium_tier_halves_the_transfer_fee()
    {
        var sender = GivenCustomer(CustomerTier.PREMIUM);
        var recipient = GivenCustomer();
        var src = GivenChecking(sender, 1000m);
        var tgt = GivenChecking(recipient);
        _settings.GetTransferFeePercentAsync().Returns(1.0m);

        await _service.TransferAsync(new ITransferMoneyUseCase.Command(
            sender, src.Id, tgt.Id, new Money(200m, Currency.USD)));

        src.Balance.Amount.Should().Be(799m);   // 1% x 0.5 = 1.00 fee
    }

    [Fact]
    public async Task Transfer_above_the_standard_tier_cap_is_rejected()
    {
        var sender = GivenCustomer();
        var recipient = GivenCustomer();
        var src = GivenChecking(sender, 10000m);
        var tgt = GivenChecking(recipient);

        var act = () => _service.TransferAsync(new ITransferMoneyUseCase.Command(
            sender, src.Id, tgt.Id, new Money(5001m, Currency.USD)));

        await act.Should().ThrowAsync<LimitExceededException>();
    }

    [Fact]
    public async Task Withdrawal_above_the_standard_tier_cap_is_rejected()
    {
        var owner = GivenCustomer();
        var a = GivenChecking(owner, 10000m);

        var act = () => _service.WithdrawAsync(
            new IWithdrawMoneyUseCase.Command(owner, a.Id, new Money(5001m, Currency.USD)));

        await act.Should().ThrowAsync<LimitExceededException>();
    }

    // ── status transitions ────────────────────────────────────────────────

    [Fact]
    public async Task Freezes_then_unfreezes_an_account()
    {
        var a = GivenChecking(GivenCustomer());

        await _service.FreezeAccountAsync(a.Id);
        a.Status.Should().Be(AccountStatus.FROZEN);

        await _service.UnfreezeAccountAsync(a.Id);
        a.Status.Should().Be(AccountStatus.ACTIVE);
    }

    [Fact]
    public async Task Closes_an_account()
    {
        var a = GivenChecking(GivenCustomer());

        await _service.CloseAccountAsync(a.Id);

        a.Status.Should().Be(AccountStatus.CLOSED);
    }

    [Fact]
    public async Task Freezing_a_closed_account_is_not_operable()
    {
        var a = GivenChecking(GivenCustomer());
        await _service.CloseAccountAsync(a.Id);

        var act = () => _service.FreezeAccountAsync(a.Id);

        await act.Should().ThrowAsync<AccountNotOperableException>();
    }

    [Fact]
    public async Task Freezing_a_missing_account_is_not_found()
    {
        var id = Guid.NewGuid();
        _accounts.FindByIdAsync(id).Returns((Account?)null);

        var act = () => _service.FreezeAccountAsync(id);

        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    // ── interest and maturity ─────────────────────────────────────────────

    [Fact]
    public async Task Accrues_interest_on_a_savings_account()
    {
        var owner = GivenCustomer();
        var s = SavingsAccount.Open(owner, Currency.USD, 0.12m);
        s.Deposit(new Money(1000m, Currency.USD));
        _accounts.FindByIdAsync(s.Id).Returns(s);

        var tx = await _service.AccrueInterestAsync(
            new IAccrueInterestUseCase.Command(s.Id, 2026, 4));

        tx.Type.Should().Be(TransactionType.INTEREST);
        s.Balance.Amount.Should().BeGreaterThan(1000m);
    }

    [Fact]
    public async Task Accruing_interest_on_a_non_savings_account_is_rejected()
    {
        var a = GivenChecking(GivenCustomer());

        var act = () => _service.AccrueInterestAsync(
            new IAccrueInterestUseCase.Command(a.Id, 2026, 4));

        await act.Should().ThrowAsync<InvalidAccountOperationException>();
    }

    [Fact]
    public async Task Maturing_a_non_time_deposit_is_rejected()
    {
        var a = GivenChecking(GivenCustomer());

        var act = () => _service.MatureAsync(new IMatureTimeDepositUseCase.Command(a.Id));

        await act.Should().ThrowAsync<InvalidAccountOperationException>();
    }

    // ── bank settings ─────────────────────────────────────────────────────

    [Fact]
    public async Task Stores_the_transfer_fee_percent()
    {
        await _service.SetTransferFeeAsync(new ISetTransferFeeUseCase.Command(2.5m));

        await _settings.Received(1).SetTransferFeePercentAsync(2.5m);
    }

    [Fact]
    public async Task Rejects_a_negative_transfer_fee_percent()
    {
        var act = () => _service.SetTransferFeeAsync(new ISetTransferFeeUseCase.Command(-0.01m));

        await act.Should().ThrowAsync<ArgumentException>();
        await _settings.DidNotReceive().SetTransferFeePercentAsync(Arg.Any<decimal>());
    }

    // ── refusal translation ───────────────────────────────────────────────

    [Fact]
    public async Task A_locked_time_deposit_reports_invalid_operation_not_a_state_problem()
    {
        var owner = GivenCustomer();
        var td = TimeDepositAccount.Open(owner, Currency.USD, new Money(1000m, Currency.USD),
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), 0.05m);
        _accounts.FindByIdAsync(td.Id).Returns(td);

        var act = () => _service.DepositAsync(
            new IDepositMoneyUseCase.Command(owner, td.Id, new Money(10m, Currency.USD)));

        await act.Should().ThrowAsync<InvalidAccountOperationException>();
    }

    // NOTE: AyvalikBankHA-JAVA has a test proving an unrelated IllegalStateException raised inside
    // the guarded region propagates as a defect rather than becoming a 422. It cannot be written
    // here: TransferDomainService's methods are not virtual, so NSubstitute cannot intercept them,
    // and Mockito's ability to mock non-final classes has no NSubstitute equivalent. Making the
    // method virtual purely for testability was rejected. The behaviour is still correct - the
    // service now catches AccountRuleViolation, not InvalidOperationException - it is simply not
    // directly asserted in this language.
}
