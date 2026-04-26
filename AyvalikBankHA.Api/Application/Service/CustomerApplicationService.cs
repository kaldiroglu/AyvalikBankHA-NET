using AyvalikBankHA.Api.Application.Exception;
using AyvalikBankHA.Api.Domain.Model;
using AyvalikBankHA.Api.Domain.Port.In;
using AyvalikBankHA.Api.Domain.Port.Out;
using AyvalikBankHA.Api.Domain.Service;

namespace AyvalikBankHA.Api.Application.Service;

public class CustomerApplicationService(
    ICustomerRepositoryPort customerRepository,
    IPasswordHasherPort passwordHasher,
    PasswordValidationService passwordValidationService) :
    ICreateCustomerUseCase,
    IDeleteCustomerUseCase,
    IListCustomersUseCase,
    IChangePasswordUseCase,
    IChangeCustomerTierUseCase
{
    public async Task<Customer> CreateCustomerAsync(ICreateCustomerUseCase.Command cmd)
    {
        try { passwordValidationService.Validate(cmd.RawPassword); }
        catch (ArgumentException e) { throw new InvalidPasswordException(e.Message); }
        var hash = passwordHasher.Hash(cmd.RawPassword);
        var customer = Customer.Create(cmd.Name, cmd.Email, hash);
        return await customerRepository.SaveAsync(customer);
    }

    public async Task DeleteCustomerAsync(Guid customerId)
    {
        if (!await customerRepository.ExistsByIdAsync(customerId))
            throw new CustomerNotFoundException($"Customer not found: {customerId}");
        await customerRepository.DeleteByIdAsync(customerId);
    }

    public Task<List<Customer>> ListCustomersAsync() => customerRepository.FindAllAsync();

    public async Task ChangePasswordAsync(IChangePasswordUseCase.Command cmd)
    {
        try { passwordValidationService.Validate(cmd.RawNewPassword); }
        catch (ArgumentException e) { throw new InvalidPasswordException(e.Message); }
        var customer = await customerRepository.FindByIdAsync(cmd.CustomerId)
            ?? throw new CustomerNotFoundException($"Customer not found: {cmd.CustomerId}");
        if (passwordHasher.Matches(cmd.RawNewPassword, customer.CurrentPasswordHash))
            throw new PasswordReusedException("New password must differ from the current one");
        customer.ChangePassword(passwordHasher.Hash(cmd.RawNewPassword));
        await customerRepository.SaveAsync(customer);
    }

    public async Task ChangeCustomerTierAsync(IChangeCustomerTierUseCase.Command cmd)
    {
        var customer = await customerRepository.FindByIdAsync(cmd.CustomerId)
            ?? throw new CustomerNotFoundException($"Customer not found: {cmd.CustomerId}");
        customer.ChangeTier(cmd.Tier);
        await customerRepository.SaveAsync(customer);
    }
}
