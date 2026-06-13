namespace LaserEye;

/// <summary>联机时由客机发往主机的镭射爆炸请求。</summary>
internal sealed class LaserExplodeMessage
{
    public float CenterTileX { get; set; }
    public float CenterTileY { get; set; }
    public int Radius { get; set; }
    public string LocationName { get; set; } = "";
}
