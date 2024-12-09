using System.Diagnostics;
using System.Globalization;

public class VideoAudioMerger
{
    private readonly string _ffmpegPath;
    private readonly string _videos2Folder;
    private readonly string _downloadedVideosPath;

    public VideoAudioMerger(string ffmpegPath, string videos2Folder, string downloadedVideosPath)
    {
        _ffmpegPath = ffmpegPath;
        _videos2Folder = videos2Folder;
        _downloadedVideosPath = downloadedVideosPath;
    }

    public async Task MergeVideosWithAudioAsync(string[] videoPaths, string audioPath, string outputPath)
    {
        string tempDirectory = _videos2Folder;

        if (!Path.Exists(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }

        string[] normalizedVideos = await NormalizeVideosAsync(videoPaths);

        TimeSpan audioDuration = await GetAudioDuration(audioPath);

        string tempMergedFile = Path.Combine(tempDirectory, "merged_with_transitions.mp4");
        string transitionCommand = GenerateTransitionCommand(normalizedVideos, tempMergedFile, audioDuration);

        await ExecuteFFmpegCommand(transitionCommand);

        string finalCommand =
            $"-i \"{tempMergedFile}\" -i \"{audioPath}\" -c:v copy -c:a aac -shortest \"{outputPath}\"";
        await ExecuteFFmpegCommand(finalCommand);

        Console.WriteLine($"Финальное видео сохранено в {outputPath}");

    }

    private async Task<string[]> NormalizeVideosAsync(string[] videoPaths)
    {
        string outputDirectory = _videos2Folder;

        if (!Path.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            Console.WriteLine($"Создана папка для временных файлов: {outputDirectory}");
        }

        string[] normalizedVideos = new string[videoPaths.Length];
        for (int i = 0; i < videoPaths.Length; i++)
        {
            string outputFile = Path.Combine(outputDirectory, $"normalized_{i}.mp4");
            string scaleCommand =
                $"-i \"{videoPaths[i]}\" -vf \"scale=1080:1920,fps=fps=25\" -preset fast -c:a copy \"{outputFile}\"";
            await ExecuteFFmpegCommand(scaleCommand);
            normalizedVideos[i] = outputFile;
        }

        return normalizedVideos;
    }

    private async Task<double[]> GetNormalizedDurationsAsync(string[] normalizedVideoPaths)
    {
        double[] durations = new double[normalizedVideoPaths.Length];
        for (int i = 0; i < normalizedVideoPaths.Length; i++)
        {
            TimeSpan dur = await GetVideoDuration(normalizedVideoPaths[i]);
            durations[i] = dur.TotalSeconds;
        }

        return durations;
    }

    public async Task<TimeSpan> GetAudioDuration(string audioPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-i \"{audioPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string output = await process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        string durationLine = output.Substring(output.IndexOf("Duration: ") + 10, 11);
        return TimeSpan.ParseExact(durationLine, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);
    }

    private string GenerateTransitionCommand(string[] normalizedVideoPaths, string output, TimeSpan audioDuration)
    {
        string filterComplex = string.Empty;
        string inputFiles = string.Empty;
        double audioDurationSeconds = audioDuration.TotalSeconds;
        double fadeDuration = 1.0;

        // Calculate the durations of already normalized videos
        double[] singleClipDurations = GetNormalizedDurationsAsync(normalizedVideoPaths).Result;

        // Repeat clips until their total duration exceeds the length of the audio
        List<double> segmentDurations = new List<double>();
        double totalVideoDuration = 0.0;
        int inputIndex = 0;
        while (totalVideoDuration < audioDurationSeconds)
        {
            double segDur = singleClipDurations[inputIndex % singleClipDurations.Length];
            inputFiles += $"-i \"{normalizedVideoPaths[inputIndex % singleClipDurations.Length]}\" ";
            filterComplex += $"[{inputIndex}:v:0]setpts=PTS-STARTPTS,fps=25[stream{inputIndex}];";

            totalVideoDuration += segDur;
            segmentDurations.Add(segDur);

            inputIndex++;
        }

        // Now create the transition chain
// The first track just goes as is:
        string lastStream = "[stream0]";
        double outputLength = segmentDurations[0];

        for (int i = 1; i < segmentDurations.Count; i++)
        {
            double currentSegmentDuration = segmentDurations[i];

            // // Offset for xfade - current output time minus transition duration
            double offset = outputLength - fadeDuration;
            if (offset < 0) offset = 0; // на случай очень коротких роликов

            // Use xfade
            filterComplex +=
                $"{lastStream}[stream{i}]xfade=transition=fade:duration={fadeDuration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}:offset={offset.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}[stream{i}_out];";
            lastStream = $"[stream{i}_out]";

// After merging, the output clip length:
// outputLength = previous length + new segment length - fadeDuration
            outputLength = outputLength + currentSegmentDuration - fadeDuration;
        }

        filterComplex += $"{lastStream}format=yuv420p[final]";
// Do not use -t here to avoid trimming the result, we have enough segments.
// Use -shortest during the audio overlay stage to ensure the video is not longer than the audio.
        return $"{inputFiles}-filter_complex \"{filterComplex}\" -map \"[final]\" \"{output}\"";
    }

    private async Task<TimeSpan> GetVideoDuration(string videoPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-i \"{videoPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = await process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        string durationLine = output.Substring(output.IndexOf("Duration: ") + 10, 11);
        return TimeSpan.ParseExact(durationLine, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);
    }


    private async Task ExecuteFFmpegCommand(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-nostdin {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var standardOutputTask = Task.Run(async () =>
        {
            while (!process.StandardOutput.EndOfStream)
            {
                string line = await process.StandardOutput.ReadLineAsync();
                Console.WriteLine($"FFmpeg Output: {line}");
            }
        });

        var standardErrorTask = Task.Run(async () =>
        {
            while (!process.StandardError.EndOfStream)
            {
                string line = await process.StandardError.ReadLineAsync();
                Console.WriteLine($"FFmpeg Output: {line}");
            }
        });

        // Ожидание завершения процесса и чтения вывода
        await Task.WhenAll(standardOutputTask, standardErrorTask, process.WaitForExitAsync());

        // Проверка успешного завершения процесса
        if (process.ExitCode != 0)
        {
            throw new Exception("Execute erro FFmpeg command.");
        }
    }

    public void CleanUpDirectory(string directoryPath)
    {
        if (Path.Exists(directoryPath))
        {
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                try
                {
                    File.Delete(file);
                    Console.WriteLine($"Temporary file is deleted: {file}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cannot delete file {file}: {ex.Message}");
                }
            }
        }
    }
}