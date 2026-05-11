/// <summary>
/// Raised by RoomController every time enemy kill count or wave progress changes.
/// </summary>
public  class OnRoomStateChangedEvent
{
    /// <summary>Enemies killed in the current wave (resets to 0 each wave).</summary>
    public int EnemiesKilledThisWave;

    /// <summary>Total enemies per wave.</summary>
    public int EnemiesPerWave;

    /// <summary>Current wave (1-based).</summary>
    public int CurrentWave;

    /// <summary>Total waves. 0 = infinite.</summary>
    public int TotalWaves;
}
