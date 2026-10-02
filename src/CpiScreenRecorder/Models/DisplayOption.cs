namespace CpiScreenRecorder.Models;

public sealed record DisplayOption(string FriendlyName, string DeviceName, int Width, int Height)
{
    public string Label
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FriendlyName) ? DeviceName : FriendlyName;
            var size = Width > 0 && Height > 0 ? $" · {Width}×{Height}" : string.Empty;
            return $"{name}{size} · {DeviceName}";
        }
    }
}
