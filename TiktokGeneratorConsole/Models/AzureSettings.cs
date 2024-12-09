namespace TiktokGeneratorConsole.Models;

public class AzureSettings
{
    public OpenAiSettings OpenAI { get; set; }
    
    public SpeechSettings Speech { get; set; }
    
    public KeyVaultSettings KeyVault { get; set; }

    public string AzureAiKey { get; set; } = "";
}

public class OpenAiSettings
{
    public string ApiKey { get; set; }
    
    public string ApiEndpoint { get; set; }
}

public class SpeechSettings
{
    public string Key { get; set; }
    
    public string Region { get; set; }
}

public class KeyVaultSettings
{
    public string TenantId { get; set; }
    
    public string KeyVault { get; set; }
    
    public string AppId { get; set; }
    
    public string AppPassword { get; set; }
}