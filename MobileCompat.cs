using System.Reflection;

namespace LaserEye;

/// <summary>Android 兼容：避免编译期引用 PC 专有 API（如 Rumble），否则 SMAPI 加载时会报 no longer compatible。</summary>
internal static class MobileCompat
{
    internal static void TryRumble(float intensity, int milliseconds)
    {
        try
        {
            Type? rumble = Type.GetType("StardewValley.Rumble, Stardew Valley");
            rumble?
                .GetMethod("rumbleAndFade", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { intensity, (float)milliseconds });
        }
        catch
        {
            // 移动端或无 Rumble 类时忽略。
        }
    }
}
