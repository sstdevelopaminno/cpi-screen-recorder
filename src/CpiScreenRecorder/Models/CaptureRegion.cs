namespace CpiScreenRecorder.Models;

public sealed record CaptureRegion(int X, int Y, int Width, int Height)
{
    public static CaptureRegion Empty { get; } = new(0, 0, 0, 0);
    public bool IsValid => Width >= 32 && Height >= 32;
    public string Label => IsValid ? $"{Width} × {Height} px  ·  X {X}, Y {Y}" : "ยังไม่ได้เลือกพื้นที่";
}
