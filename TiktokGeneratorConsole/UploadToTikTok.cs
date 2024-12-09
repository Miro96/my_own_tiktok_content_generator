namespace TiktokGeneratorConsole;

public class UploadToTikTok
{
    public async Task Execute()
    {
        string accessToken = Environment.GetEnvironmentVariable("TIKTOK_ACCESS_TOKEN");
        string videoPath = "final_video.mp4";

        using (var client = new HttpClient())
        {
            using (var content = new MultipartFormDataContent())
            {
                content.Add(new StreamContent(File.OpenRead(videoPath)), "video", Path.GetFileName(videoPath));
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                HttpResponseMessage response = await client.PostAsync("https://open-api.tiktok.com/share/video/upload/", content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Видео успешно загружено!");
                }
                else
                {
                    Console.WriteLine($"Ошибка загрузки: {response.StatusCode}, {response.ReasonPhrase}");
                }
            }
        }
    }

}