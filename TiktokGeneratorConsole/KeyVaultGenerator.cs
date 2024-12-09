using TiktokGeneratorConsole.Models;
using static System.Environment;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace TiktokGeneratorConsole;

public static class KeyVaultGenerator
{
    public static void GetApiKey(AzureSettings azureSettings)
    {
        // Get Azure AI services key from keyvault using the service principal credentials
        var keyVaultUri = new Uri($"https://{azureSettings.KeyVault.KeyVault}.vault.azure.net/");
        ClientSecretCredential credential = new ClientSecretCredential(azureSettings.KeyVault.TenantId, azureSettings.KeyVault.AppId, azureSettings.KeyVault.AppPassword);
        var keyVaultClient = new SecretClient(keyVaultUri, credential);
        KeyVaultSecret secretKey = keyVaultClient.GetSecret("AI-Service-Key");
        azureSettings.AzureAiKey = secretKey.Value;
    }
}