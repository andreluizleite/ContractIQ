namespace ContractIQ.Application.Assistant;

public enum AssistantLanguage
{
    English,
    PortugueseBrazil,
}

public static class AssistantLanguageParser
{
    public static bool TryParse(string? value, out AssistantLanguage language)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "en":
                language = AssistantLanguage.English;
                return true;
            case "pt-br":
                language = AssistantLanguage.PortugueseBrazil;
                return true;
            default:
                language = default;
                return false;
        }
    }

    public static string ToCode(this AssistantLanguage language) => language switch
    {
        AssistantLanguage.English => "en",
        AssistantLanguage.PortugueseBrazil => "pt-BR",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };
}
