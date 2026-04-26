namespace AyvalikBankHA.Api.Domain.Model;

// State pattern. Each state owns its valid transitions and operability check.
// Stateless singletons.
public abstract class AccountState
{
    public static AccountState Of(AccountStatus s) => s switch
    {
        AccountStatus.ACTIVE => ActiveState.Instance,
        AccountStatus.FROZEN => FrozenState.Instance,
        AccountStatus.CLOSED => ClosedState.Instance,
        _ => ActiveState.Instance
    };

    public abstract AccountStatus Status { get; }
    public abstract AccountState Freeze();
    public abstract AccountState Unfreeze();
    public abstract AccountState Close();
    public abstract void RequireOperable();
    public virtual bool IsTerminal => false;
}

public sealed class ActiveState : AccountState
{
    public static readonly ActiveState Instance = new();
    private ActiveState() { }
    public override AccountStatus Status => AccountStatus.ACTIVE;
    public override AccountState Freeze() => FrozenState.Instance;
    public override AccountState Unfreeze() => throw new InvalidOperationException("Account is not frozen");
    public override AccountState Close() => ClosedState.Instance;
    public override void RequireOperable() { /* active accounts are operable */ }
}

public sealed class FrozenState : AccountState
{
    public static readonly FrozenState Instance = new();
    private FrozenState() { }
    public override AccountStatus Status => AccountStatus.FROZEN;
    public override AccountState Freeze() => throw new InvalidOperationException("Account is already frozen");
    public override AccountState Unfreeze() => ActiveState.Instance;
    public override AccountState Close() => ClosedState.Instance;
    public override void RequireOperable() => throw new InvalidOperationException("Account is frozen");
}

public sealed class ClosedState : AccountState
{
    public static readonly ClosedState Instance = new();
    private ClosedState() { }
    public override AccountStatus Status => AccountStatus.CLOSED;
    public override AccountState Freeze() => throw new InvalidOperationException("Cannot freeze a closed account");
    public override AccountState Unfreeze() => throw new InvalidOperationException("Cannot unfreeze a closed account");
    public override AccountState Close() => throw new InvalidOperationException("Account is already closed");
    public override void RequireOperable() => throw new InvalidOperationException("Account is closed");
    public override bool IsTerminal => true;
}
