// OnReloadEvent.cs
public class OnReloadEvent
{
    public bool IsReloading { get; }
    public float Progress { get; }  // 0 → 1

    public OnReloadEvent(bool isReloading, float progress)
    {
        IsReloading = isReloading;
        Progress = progress;
    }
}