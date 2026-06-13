using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace LaserEye;

/// <summary>向 Generic Mod Config Menu 注册本 mod 的配置项。</summary>
internal static class GmcmIntegration
{
    private static readonly string[] TrajectoryValues =
    [
        nameof(LaserTrajectoryType.Curved),
        nameof(LaserTrajectoryType.Straight),
    ];

    internal static void Register(ModEntry mod, IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += (_, _) => RegisterMenu(mod, helper);

        // reload_mods 后 GameLaunched 不会再次触发，需立即重新注册。
        if (Context.IsWorldReady)
            RegisterMenu(mod, helper);
    }

    private static void RegisterMenu(ModEntry mod, IModHelper helper)
    {
        IGenericModConfigMenuApi? api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Unregister(mod.ModManifest);

        api.Register(
            mod: mod.ModManifest,
            reset: () => mod.ResetConfig(),
            save: () => helper.WriteConfig(mod.Config),
            titleScreenOnly: false);

        api.SetTitleScreenOnlyForNextOptions(mod.ModManifest, false);

        api.AddSectionTitle(
            mod: mod.ModManifest,
            text: () => "操作",
            tooltip: () => "调整发射镭射的按键。");

        api.AddKeybind(
            mod: mod.ModManifest,
            getValue: () => mod.Config.FireKey,
            setValue: value => mod.Config.FireKey = value,
            name: () => "发射按键",
            tooltip: () => "按住此按键时发射镭射，默认为鼠标左键。Android 上改为按住屏幕发射。",
            fieldId: "FireKey");

        api.SetTitleScreenOnlyForNextOptions(mod.ModManifest, false);

        api.AddSectionTitle(
            mod: mod.ModManifest,
            text: () => "镭射弹道",
            tooltip: () => "调整装备 5 号化合物后的激光视觉效果。");

        api.AddTextOption(
            mod: mod.ModManifest,
            getValue: () => mod.Config.Trajectory.ToString(),
            setValue: value => mod.Config.Trajectory = Enum.Parse<LaserTrajectoryType>(value, ignoreCase: true),
            name: () => "弹道类型",
            tooltip: () => "曲线：两束正弦摆动激光，中途交叉；直线：从眼眶直射鼠标。",
            allowedValues: TrajectoryValues,
            formatAllowedValue: FormatTrajectoryName,
            fieldId: "Trajectory");
    }

    private static string FormatTrajectoryName(string value)
    {
        return value switch
        {
            nameof(LaserTrajectoryType.Curved) => "曲线",
            nameof(LaserTrajectoryType.Straight) => "直线",
            _ => value,
        };
    }
}
