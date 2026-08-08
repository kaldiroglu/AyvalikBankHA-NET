namespace AyvalikBankHA.Api.Domain.Model;

/// <summary>
/// Base type for every way the account domain can refuse an operation.
///
/// <para>Before this existed the domain threw raw <see cref="InvalidOperationException"/> from 23
/// places, all meaning different things, and the application layer recovered the meaning by
/// <b>matching on the exception message</b> — <c>when (e.Message.Contains("frozen"))</c>. Rewording
/// a message silently changed the HTTP status. Catching the BCL type also converted genuine defects
/// into 4xx business responses.</para>
///
/// <para>It extends <see cref="InvalidOperationException"/> deliberately: a refusal really is about
/// the object's state, and because <c>catch (AccountRuleViolation)</c> does <b>not</b> catch a plain
/// <c>InvalidOperationException</c>, precision is gained at the catch site without invalidating
/// existing tests that assert on the base type. Mirrors AyvalikBankHA-JAVA Refactorings.md entry 4.</para>
///
/// <para>C# has no <c>permits</c> clause, so the compiler cannot prove a switch over these subtypes
/// is exhaustive the way Java can. The translation switch therefore carries a discard arm that
/// throws, turning a missing case into a loud runtime failure rather than a silent fallthrough.</para>
/// </summary>
public abstract class AccountRuleViolation(string message) : InvalidOperationException(message);

/// <summary>The account's lifecycle state forbids the operation — frozen, closed, or an invalid transition.</summary>
public sealed class AccountNotActiveException(string message) : AccountRuleViolation(message);

/// <summary>The balance, plus any overdraft allowance, cannot cover the requested debit.</summary>
public sealed class InsufficientBalanceException(string message) : AccountRuleViolation(message);

/// <summary>The account product's own rules forbid the operation (locked principal, not matured, already accrued).</summary>
public sealed class OperationNotPermittedException(string message) : AccountRuleViolation(message);

/// <summary>The amount exceeds the per-transaction cap carried by the customer's tier.</summary>
public sealed class TransactionLimitExceededException(string message) : AccountRuleViolation(message);
