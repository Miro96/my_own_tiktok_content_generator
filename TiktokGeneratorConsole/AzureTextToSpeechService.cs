using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TiktokGeneratorConsole.Models.Audio;

namespace TiktokGeneratorConsole;

public class AzureTextToSpeechService
{
    private readonly string _apiKey;
    private readonly string _region;
    private readonly string _outputFilePath;
    private readonly string _optimizedContentPath;
    private readonly string _srtFilePath;
    private readonly string _language;
    private readonly string _ffmpegPath;
    public AzureTextToSpeechService(IConfiguration configuration, string outputFilePath)
    {
        _outputFilePath = outputFilePath;
        _optimizedContentPath = configuration["VideoGeneration:OptimizedContentPath"];
        _apiKey = configuration["AzureSettings:Speech:Key"];
        _region = configuration["AzureSettings:Speech:Region"];
        _srtFilePath = _optimizedContentPath + "/subtitles.srt";
        _language = configuration["AzureSettings:Speech:Language"];
        _ffmpegPath = configuration["Ffmpeg:Path"];
    }

    public async Task ConvertTextToSpeechAsync(string text)
    {
        var speechConfig = SpeechConfig.FromSubscription(_apiKey, _region);

        // Задаем язык для озвучивания (например, русский)
        speechConfig.SpeechSynthesisVoiceName = "en-US-AndrewMultilingualNeural"; // Выберите подходящий голос из Azure Portal

        // Указываем, куда сохранить аудио
        using var audioConfig = AudioConfig.FromWavFileOutput(_outputFilePath);

        // Инициализируем синтезатор
        using var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig);

        try
        {
            Console.WriteLine("Начинается синтез текста в речь...");
            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine($"Speech create successfully. Path to file: {_outputFilePath}");
            }
            else
            {
                Console.WriteLine($"Speech create failed: {result.Reason}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
        }
    }
    
    public async Task<List<SpeechSegment>> RecognizeSpeechAsync()
    {
        var speechConfig = SpeechConfig.FromSubscription(_apiKey, _region);
        // Установите язык распознавания, например, "ru-RU"
        speechConfig.SpeechRecognitionLanguage = _language;

        using var audioConfig = AudioConfig.FromWavFileInput(_outputFilePath);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var results = new List<SpeechSegment>();
        var stopRecognition = new TaskCompletionSource<int>();

        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                // Offset и Duration предоставляются в тиках (100 нс)
                results.Add(new SpeechSegment
                {
                    Text = e.Result.Text,
                    Offset = e.Result.OffsetInTicks,
                    Duration = e.Result.Duration.Ticks
                });
            }
        };

        recognizer.Canceled += (s, e) =>
        {
            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"Ошибка распознавания: {e.ErrorCode}, {e.ErrorDetails}");
            }
            stopRecognition.TrySetResult(0);
        };

        recognizer.SessionStopped += (s, e) => { stopRecognition.TrySetResult(0); };

        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
        // Ждем окончания сессии
        await stopRecognition.Task.ConfigureAwait(false);
        await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

        return results;
    }

    public void GenerateSrtFile(List<SubtitleChunk> subtitles)
    {
        using var sw = new StreamWriter(_srtFilePath, false, System.Text.Encoding.UTF8);
        int counter = 1;
        foreach (var sub in subtitles)
        {
            string start = TicksToSrtTime(sub.Start);
            string end = TicksToSrtTime(sub.End);

            sw.WriteLine(counter.ToString());
            sw.WriteLine($"{start} --> {end}");
            sw.WriteLine(sub.Text);
            sw.WriteLine(); // пустая строка
            counter++;
        }
    }

    private static string TicksToSrtTime(long ticks)
    {
        // 1 tick = 100 ns; 10^7 ticks = 1 second
        double totalSeconds = ticks / 10_000_000.0;
        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        int milliseconds = (int)((totalSeconds - Math.Floor(totalSeconds)) * 1000);

        return $"{hours:00}:{minutes:00}:{seconds:00},{milliseconds:000}";
    }
    public List<SubtitleChunk> SplitIntoChunks(List<SpeechSegment> segments, int wordsPerChunk)
    {
        var chunks = new List<SubtitleChunk>();

        foreach (var seg in segments)
        {
            var words = seg.Text.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) 
                continue;

            double totalWords = words.Length;
            double totalDurationTicks = seg.Duration;

            long currentStart = seg.Offset;
            double accumulatedRatio = 0.0;
            int currentIndex = 0;

            while (currentIndex < words.Length)
            {
                // Берём отрезок в 5 слов или меньше, если слов меньше осталось
                int chunkSize = Math.Min(wordsPerChunk, words.Length - currentIndex);
                var chunkWords = words.Skip(currentIndex).Take(chunkSize).ToArray();
                
                double chunkRatio = chunkSize / totalWords;
                double chunkDurationTicks = totalDurationTicks * chunkRatio;

                long chunkStart = currentStart;
                long chunkEnd = chunkStart + (long)chunkDurationTicks;

                // Создаём подпись
                chunks.Add(new SubtitleChunk
                {
                    Start = chunkStart,
                    End = chunkEnd,
                    Text = string.Join(" ", chunkWords)
                });

                // Сдвигаем старт для следующего куска
                currentStart = chunkEnd;
                currentIndex += chunkSize;
            }
        }

        return chunks;
    }

    public void EmbedSubtitlesFFmpeg(string inputVideo, string outputVideo)
    {
        // Пример со стилями: Arial, размер 28, полужирный, обводка, тень, белый цвет
        // force_style='FontName=Arial,FontSize=28,Bold=1,Outline=2,Shadow=1,PrimaryColour=&H00FFFFFF&'
        // Обратите внимание на использование кавычек:
        // -vf "subtitles=_srtFilePath:force_style='...'"
        // -y для перезаписи файла без запроса
        var arguments = $"-y -i \"{inputVideo}\" -vf \"subtitles={_srtFilePath}:force_style='FontName=Arial,FontSize=16,Bold=1,Outline=1,Shadow=1,Alignment=2,MarginL=50,MarginR=50,MarginV=60,PrimaryColour=&H00FFFFFF&'\" -c:a copy \"{outputVideo}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) Console.WriteLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
    }
}