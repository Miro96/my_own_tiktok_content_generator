using System.Net.Http.Headers;

namespace TiktokGeneratorConsole;

public class RunwayMLService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public RunwayMLService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("X-Runway-Version", "2024-11-06");
    }

    public async Task AnimateImageAsync(string inputImagePath, string motionType, int duration, string outputVideoPath)
    {
        var url = "https://api.dev.runwayml.com/v1/image_to_video";

        using var imageStream = File.OpenRead(inputImagePath);
        using var content = new MultipartFormDataContent
        {
            { new StreamContent(imageStream), "image", Path.GetFileName(inputImagePath) },
            { new StringContent(motionType), "motion" },
            { new StringContent(duration.ToString()), "duration" },
            { new StringContent("1080p"), "resolution" } // Устанавливаем разрешение
        };

        Console.WriteLine("Отправка запроса к Runway ML...");
        var response = await _httpClient.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            var videoBytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(outputVideoPath, videoBytes);
            Console.WriteLine($"Видео успешно создано: {outputVideoPath}");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Ошибка анимации: {response.StatusCode} {errorContent}");
        }
    }
}