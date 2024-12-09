namespace TiktokGeneratorConsole;

public class CreateSubtitle
{
    public void Execute(string script)
    {
        using (StreamWriter writer = new StreamWriter("subtitles.srt"))
        {
            writer.WriteLine("1");
            writer.WriteLine("00:00:00,000 --> 00:00:10,000");
            writer.WriteLine(script);
        }
    }
}