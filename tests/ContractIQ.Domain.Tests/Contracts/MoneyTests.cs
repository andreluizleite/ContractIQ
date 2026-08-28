using ContractIQ.Domain.Contracts;
using Xunit;

namespace ContractIQ.Domain.Tests.Contracts;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_normalizes_currency()
    {
        var money = new Money(42m, " brl ");

        Assert.Equal("BRL", money.Currency);
        Assert.Equal(42m, money.Amount);
    }

    [Theory]
    [InlineData(1.005, 1.01)]
    [InlineData(-1.005, -1.01)]
    public void Constructor_rounds_midpoints_away_from_zero(
        decimal amount,
        decimal expectedAmount)
    {
        var money = new Money(amount, "USD");

        Assert.Equal(expectedAmount, money.Amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("US1")]
    [InlineData("BR$")]
    [InlineData("€UR")]
    public void Constructor_rejects_invalid_currency(string? currency)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Money(10m, currency!));
    }

    [Fact]
    public void Multiply_preserves_currency_and_applies_money_rounding()
    {
        var result = new Money(10.05m, "brl").Multiply(0.1m);

        Assert.Equal(new Money(1.01m, "BRL"), result);
    }
}
