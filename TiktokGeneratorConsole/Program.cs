using Microsoft.Extensions.Configuration;

namespace TiktokGeneratorConsole;

class Program
{
    
    private static IConfigurationRoot _configuration;
    private static string DownloadedVideoPath;
    private static string GeneratedVideoPath;
    private static string OptimizedContentPath;
    private static string FfmpegPath;
    private static string AudioPath;
    
    private static VideoAudioMerger _merger;
    private static PexelsService _pexelsService;
    private static AzureOpenAIService _openAIService;
    
    public static void SetParameters()
    {
        IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
        _configuration = builder.Build();
        DownloadedVideoPath = _configuration["VideoGeneration:DownloadedVideoPath"];
        CreateDirectoryIfNotExists(DownloadedVideoPath);
        OptimizedContentPath = _configuration["VideoGeneration:OptimizedContentPath"];
        CreateDirectoryIfNotExists(OptimizedContentPath);
        GeneratedVideoPath = _configuration["VideoGeneration:GeneratedVideoPath"];
        CreateDirectoryIfNotExists(GeneratedVideoPath);
        FfmpegPath = _configuration["Ffmpeg:Path"];
        _merger = new VideoAudioMerger(FfmpegPath,
            OptimizedContentPath, DownloadedVideoPath);
        _openAIService = new AzureOpenAIService(_configuration);
        _pexelsService = new PexelsService(_configuration);
        AudioPath = OptimizedContentPath + "/output_audio.wav";
        
    }
    
    private static void CreateDirectoryIfNotExists(string? path)
    {
        if (path == null)
            return;
        if (!Path.Exists(path))
            Directory.CreateDirectory(path);
    }

    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Hi! Welcome to Tiktok Generator Console");

            SetParameters();
            
            Console.WriteLine("AI keys are loaded successfully");

            Console.WriteLine("Enter your prompt scenario:");
            string? prompt = Console.ReadLine();
            string systemPromptForScenario =
                _configuration["AzureSettings:SystemPromptForScenario"];
            string scenario = await _openAIService.GenerateChatScriptAsync(prompt, systemPromptForScenario);
            Console.WriteLine("Scenario: " + scenario);
            
            
            var textToSpeechService = new AzureTextToSpeechService(_configuration, AudioPath);
            await textToSpeechService.ConvertTextToSpeechAsync(scenario);

            string mainScenarioThing = await _openAIService.GenerateChatScriptAsync(scenario,
                "Pick from the text main word. You need to answer the question: What is this text about? Your answer should be in one word.");
            Console.WriteLine("Audio file is generated successfully");
            var resultPath = await CallVideoService(_pexelsService, mainScenarioThing, _configuration);
            Console.WriteLine("Tiktok video + audio is generated successfully");
            
            var transcript = await textToSpeechService.RecognizeSpeechAsync();
            var splittedSubtitles = textToSpeechService.SplitIntoChunks(transcript, 5);
            textToSpeechService.GenerateSrtFile(splittedSubtitles);
            textToSpeechService.EmbedSubtitlesFFmpeg(resultPath,
                GeneratedVideoPath + "/tiktok_with_subtitles_" + DateTime.Now.Ticks + ".mp4");

        }
        finally
        {
            _merger.CleanUpDirectory(DownloadedVideoPath);
            _merger.CleanUpDirectory(OptimizedContentPath);
        }
    }


    static async Task<string> CallVideoService(PexelsService pexelsService, string query, IConfigurationRoot configuration)
    {
        var videos = await pexelsService.SearchVerticalVideosAsync(query);

        if (videos.Count == 0)
        {
            Console.WriteLine("Video not found.");
            throw new FileNotFoundException("Video not found.");
        }

        Console.WriteLine("Videos:");
        foreach (var video in videos)
        {
            Console.WriteLine($"Duration: {video.Duration} secont");
            Console.WriteLine($"Video url: {video.Url}");
            Console.WriteLine("-------------------------------");
        }
        string outputPath = Path.Combine(DownloadedVideoPath, "vertical_video");
        
        var audioDurationTimeSpan = await _merger.GetAudioDuration(AudioPath);
        var audioDuration = audioDurationTimeSpan.Duration().TotalSeconds;
        var tempVideoCount = 0;
        for (var i = 0; i < videos.Count; i++)
        {
            if (audioDuration > 0)
            {
                tempVideoCount++;
                audioDuration = audioDuration - videos[i].Duration;
            }
            
        }

        var videosPathsForCompiler = new string[tempVideoCount];
        for (var i = 0; i < tempVideoCount; i++)
        {
            await pexelsService.DownloadVideoAsync(videos[i].Url, outputPath+i+".mp4");
            videosPathsForCompiler[i] = outputPath + i + ".mp4";
        }
        var outputVideoWithAudioPath = GeneratedVideoPath + "/tiktok"+ DateTime.Now.Ticks + ".mp4";
        
        
        await _merger.MergeVideosWithAudioAsync(videosPathsForCompiler, AudioPath, outputVideoWithAudioPath);

        return outputVideoWithAudioPath;
    }
}