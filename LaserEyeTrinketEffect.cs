using StardewValley.Objects.Trinkets;

namespace LaserEye;

/// <summary>镭射眼饰品的效果类；实际逻辑由 <see cref="ModEntry"/> 处理。</summary>
internal sealed class LaserEyeTrinketEffect : TrinketEffect
{
    public LaserEyeTrinketEffect(Trinket trinket)
        : base(trinket)
    {
    }
}
