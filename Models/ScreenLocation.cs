namespace CSharpVAmp.Models;

public class ScreenLocation
{
    public int Index { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Free { get; set; } = true;
}

