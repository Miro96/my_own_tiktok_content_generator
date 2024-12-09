using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Images;

namespace TiktokGeneratorConsole;

public class AzureImageGenerator
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deploymentName;
    private readonly ImageClient _imageClient;

    public AzureImageGenerator(IConfiguration configuration)
    {
        var endpoint = new Uri(configuration["AzureSettings:OpenAI:ApiEndpoint-Dall-E"]);
        var apiKey = configuration["AzureSettings:OpenAI:ApiKey-Dall-E"];
        _deploymentName = configuration["AzureSettings:OpenAI:DeploymentName-Image"];
        _client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
        _imageClient = _client.GetImageClient(_deploymentName);
    }

    public async Task<string> GenerateImageAsync(string prompt, string outputFilePath)
    {
        Console.WriteLine("Image generation...");
        var response = await _imageClient.GenerateImageAsync(
            prompt,
            new ImageGenerationOptions()
            {
                Size = new GeneratedImageSize(1024, 1792),
                Style = new GeneratedImageStyle("natural")
            }
        );

        string imageUrl = response.Value.ImageUri.ToString();

        // Скачиваем изображение
        using (var client = new HttpClient())
        {
            var imageBytes = await client.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(outputFilePath, imageBytes);
        }

        Console.WriteLine($"Image saved in {outputFilePath}");
        return outputFilePath;
    }
}