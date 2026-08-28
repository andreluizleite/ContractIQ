using ContractIQ.Domain.Contracts;

namespace ContractIQ.Application.Common.Models;

public sealed record MoneyDto(decimal Amount, string Currency)
{
    internal static MoneyDto FromDomain(Money money) => new(money.Amount, money.Currency);
}
