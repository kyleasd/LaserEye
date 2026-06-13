namespace LaserEye;

using StardewModdingAPI;

/// <summary>镭射弹道类型。</summary>
public enum LaserTrajectoryType
{
    /// <summary>正弦曲线（默认，两束光中途交叉摆动）。</summary>
    Curved,

    /// <summary>直线（从眼眶直射鼠标）。</summary>
    Straight,
}

/// <summary>Mod 配置类，对应 Mods/LaserEye/config.json。</summary>
public class ModConfig
{
    /// <summary>镭射弹道效果。可选 Curved（曲线）或 Straight（直线）。</summary>
    public LaserTrajectoryType Trajectory { get; set; } = LaserTrajectoryType.Curved;

    /// <summary>发射镭射时按住的按键，默认为鼠标左键。</summary>
    public SButton FireKey { get; set; } = SButton.MouseLeft;
}
