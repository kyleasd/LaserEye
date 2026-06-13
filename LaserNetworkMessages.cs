namespace LaserEye;

/// <summary>联机时同步镭射光束的绘制状态。</summary>
internal sealed class LaserVisualMessage
{
    public bool Active { get; set; }
    public float TargetWorldX { get; set; }
    public float TargetWorldY { get; set; }
    public float ExtendProgress { get; set; }
    public string Trajectory { get; set; } = nameof(LaserTrajectoryType.Curved);
    public string LocationName { get; set; } = "";
}

/// <summary>联机时同步爆炸火球与音效（不含地图破坏）。</summary>
internal sealed class LaserExplodeVisualMessage
{
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public string LocationName { get; set; } = "";
}
