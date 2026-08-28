namespace ContractIQ.Domain.Contracts;

public sealed record Money
{
    public const int DecimalPlaces = 2;

    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        string normalizedCurrency = currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(IsAsciiLetter))
        {
            throw new ArgumentException(
                "Currency must contain exactly three ASCII letters, such as USD or BRL.",
                nameof(currency));
        }

        Amount = decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero);
        Currency = normalizedCurrency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Multiply(decimal multiplier) => new(Amount * multiplier, Currency);

    private static bool IsAsciiLetter(char character) =>
        character is >= 'A' and <= 'Z';
}
