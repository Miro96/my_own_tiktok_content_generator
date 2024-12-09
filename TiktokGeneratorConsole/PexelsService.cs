using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace TiktokGeneratorConsole;

public class PexelsService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public PexelsService(IConfiguration configuration)
    {
        _apiKey = configuration["Pexels:ApiKey"];
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", _apiKey);
    }

    public async Task<List<PexelsVideo>> SearchVerticalVideosAsync(string query, int perPage = 15)
    {
        string url = $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(query)}&per_page={perPage}&orientation=portrait";
        HttpResponseMessage response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error video search: {response.StatusCode}");
            return new List<PexelsVideo>();
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PexelsVideoResponse>(responseContent, new JsonSerializerOptions(){PropertyNameCaseInsensitive = true});

        var videoDetails = new List<PexelsVideo>();
        foreach (var video in result.Videos)
        {
            // Try to get HD video, if not available, get the first one
            var hdVideo = video.VideoFiles.FirstOrDefault(v => v.Quality == "hd") ?? video.VideoFiles.First();
            if (hdVideo != null)
            {
                videoDetails.Add(new PexelsVideo
                {
                    Id = video.Id,
                    Url = hdVideo.Link,
                    Duration = video.Duration
                });
            }
        }

        return videoDetails;
    }

    public async Task DownloadVideoAsync(string videoUrl, string outputPath)
    {
        Console.WriteLine($"Download by URL: {videoUrl}...");
        using var response = await _httpClient.GetAsync(videoUrl);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error by downloading: {response.StatusCode}");
            return;
        }

        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fileStream);

        Console.WriteLine($"Video saved in: {outputPath}");
    }
}

public class PexelsVideoResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("videos")]
    public List<PexelsVideo> Videos { get; set; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }

    [JsonPropertyName("next_page")]
    public string NextPage { get; set; }
}

public class PexelsVideo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("user")]
    public PexelsUser User { get; set; }

    [JsonPropertyName("video_files")]
    public List<VideoFile> VideoFiles { get; set; }

    [JsonPropertyName("video_pictures")]
    public List<VideoPicture> VideoPictures { get; set; }
}

public class PexelsUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class VideoFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("quality")]
    public string Quality { get; set; }

    [JsonPropertyName("file_type")]
    public string FileType { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("fps")]
    public double Fps { get; set; }

    [JsonPropertyName("link")]
    public string Link { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }
}

public class VideoPicture
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nr")]
    public int Nr { get; set; }

    [JsonPropertyName("picture")]
    public string Picture { get; set; }
}