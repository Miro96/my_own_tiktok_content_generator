using System.Diagnostics;

namespace TiktokGeneratorConsole;

public class CreateVideo
{
    public void Execute()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = "-f lavfi -i color=c=white:s=1080x1920:d=10 -i output_audio.wav -c:v libx264 -c:a aac -strict experimental -vf subtitles=subtitles.srt final_video.mp4",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            process.WaitForExit();
        }
    }
}