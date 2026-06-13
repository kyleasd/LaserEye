using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.GameData;
using StardewValley.GameData.Shops;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace LaserEye;

/// <summary>The mod entry point.</summary>
internal sealed class ModEntry : Mod
{
    /// <summary>虚空炸弹（即巨型炸弹）破坏半径（体力 ≥ 60% 时使用，最强）。</summary>
    private const int VoidBombRadius = 7;

    /// <summary>普通炸弹破坏半径（体力 ≥ 30% 时使用）。</summary>
    private const int NormalBombRadius = 5;

    /// <summary>樱桃炸弹破坏半径（体力 ≥ 10% 时使用，最弱）。</summary>
    private const int CherryBombRadius = 3;

    /// <summary>鼠标停在同一格时，重复爆炸的间隔（毫秒）。</summary>
    private const int DestroyCooldownMs = 400;

    /// <summary>每次发射激光（爆炸）消耗的体力。</summary>
    private const float StaminaCostPerShot = 4f;

    /// <summary>镭射眼饰品在 Data/Trinkets 中的 ID。</summary>
    private const string LaserEyeTrinketId = "Kyle.LaserEye_LaserEyeBox";

    /// <summary>齐先生核桃房齐钻商店 ID。</summary>
    private const string QiGemShopId = "QiGemShop";

    /// <summary>齐钻物品 ID。</summary>
    private const string QiGemItemId = "(O)858";

    /// <summary>在齐钻商店购买配方所需齐钻数量。</summary>
    private const int CompoundNo5RecipeQiGemCost = 18;

    /// <summary>镭射眼饰品的完整物品 ID。</summary>
    private const string LaserEyeTrinketQualifiedId = "(TR)" + LaserEyeTrinketId;

    /// <summary>饰品图标资源（5号化合物，16×16）。</summary>
    private const string TrinketIconPath = "assets/CompoundNo5.png";

    /// <summary>饰品图标在游戏中的资产名。</summary>
    private const string TrinketIconAssetName = "Mods/Kyle.LaserEye/CompoundNo5";

    /// <summary>「全胜姿态」buff 的唯一 ID。</summary>
    private const string AllWinStanceBuffId = "Kyle.LaserEye/AllWinStance";

    /// <summary>「全胜姿态」buff 图标资源路径（16×16）。</summary>
    private const string BuffIconPath = "assets/BuffsIcons.png";

    private Texture2D? _buffIcon;

    private const float MainBeamThickness = 7f;
    private const float CoreBeamThickness = 2.5f;
    private const float OuterGlowBaseThickness = 14f;

    private static readonly Color OuterGlowColor = new(255, 30, 30, 180);
    private static readonly Color MainBeamColor = new(255, 45, 45, 220);
    private static readonly Color CoreBeamColor = new(255, 255, 210, 255);

    /// <summary>LooseSprites/Cursors 中的光晕贴图区域。</summary>
    private static readonly Rectangle CursorGlowSource = new(21, 1695, 41, 41);

    private const float EyeGlowBaseScale = 0.11f;
    private const float ImpactGlowBaseScale = 0.14f;

    /// <summary>弯曲光束的分段数。</summary>
    private const int BeamCurveSegments = 18;

    /// <summary>正弦摆动幅度占光束长度的比例。</summary>
    private const float BeamSineAmplitudeRatio = 0.14f;

    /// <summary>沿光束方向的正弦波周期数。</summary>
    private const float BeamSineFrequency = 1f;

    /// <summary>正弦摆动角速度（弧度/秒）。</summary>
    private const float BeamSineSpeed = 7f;

    /// <summary>直线延伸阶段时长（秒），从眼眶缓慢射至鼠标。</summary>
    private const float LaserExtendDurationSeconds = 0.2f;

    // 朝上/下时：两根线之间的水平间距，以及整体相对身体中心的偏移（OffsetY 负值 = 屏幕向上）
    private const float VerticalEyeSpacing = 20f;
    private const float VerticalEyeOffsetX = 32f;
    private const float VerticalEyeOffsetY = -16f;

    // 朝左/右时：双眼相对脸侧的偏移（X 随朝向翻转），以及两眼垂直间距
    private const float HorizontalEyeOffsetX = 32f;
    private const float HorizontalEyeOffsetY = -24f;
    private const float HorizontalEyeSpacing = 14f;
    /// <summary>侧脸两眼前后错开的深度（朝右时近眼更靠右）。</summary>
    private const float HorizontalEyeDepthOffset = 4f;

    /// <summary>联机爆炸请求的 SMAPI 消息类型。</summary>
    private const string LaserExplodeMessageType = "laser-explode";

    /// <summary>联机镭射光束绘制的 SMAPI 消息类型。</summary>
    private const string LaserVisualMessageType = "laser-visual";

    /// <summary>联机爆炸特效（火球/音效）的 SMAPI 消息类型。</summary>
    private const string LaserExplodeVisualMessageType = "laser-explode-visual";

    /// <summary>远端镭射状态超过此 tick 数未更新则清除。</summary>
    private const int RemoteLaserVisualTimeoutTicks = 30;

    /// <summary>每次爆炸时在鼠标位置散开的火球数量。</summary>
    private const int FireballScatterCount = 10;

    /// <summary>TileSheets\Fireball 图集：4×4 共 16 帧，每帧 32×32，从左到右、从上到下播放。</summary>
    private const string FireballTexturePath = "TileSheets\\Fireball";

    private const int FireballFrameSize = 32;
    private const int FireballFrameCount = 16;
    private const float FireballFrameIntervalMs = 50f;

    private Vector2 _lastDestroyCenterTile = new(-9999, -9999);
    private int _destroyCooldownRemainingMs;

    /// <summary>处于菜单/暂停等不可发射状态后，需先松开发射键才能再次发射，避免误触发。</summary>
    private bool _requireFireKeyRelease;

    /// <summary>防止同一帧内重复触发喝牛奶回体。</summary>
    private int _lastMilkStaminaBonusTick = -1;

    /// <summary>饮用牛奶/羊奶后等待吃喝动画结束再额外回体（值为最大体力的比例）。</summary>
    private float? _pendingMilkStaminaBonusRatio;

    /// <summary>上一帧是否处于吃喝动画中，用于检测动画结束。</summary>
    private bool _wasEating;

    /// <summary>光束直线延伸进度（0 = 眼眶，1 = 鼠标）。</summary>
    private float _laserExtendProgress;

    /// <summary>上一帧是否正在向其他玩家同步镭射绘制状态。</summary>
    private bool _wasSyncingLaserVisual;

    /// <summary>其他玩家在当前地点的镭射绘制状态。</summary>
    private readonly Dictionary<long, RemoteLaserVisual> _remoteLaserVisuals = new();

    /// <summary>当前是否为 Android 平台（移动端使用触屏点击发射）。</summary>
    private static readonly bool IsMobilePlatform = OperatingSystem.IsAndroid();

    /// <summary>Mod 配置。</summary>
    private ModConfig _config = null!;

    /// <summary>当前 mod 配置（供 GMCM 读写）。</summary>
    internal ModConfig Config => _config;

    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _buffIcon = helper.ModContent.Load<Texture2D>(BuffIconPath);

        GmcmIntegration.Register(this, helper);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
        helper.Events.Player.InventoryChanged += OnInventoryChanged;
        helper.Events.Player.Warped += (_, _) =>
        {
            ResetDestroyState();
            _pendingMilkStaminaBonusRatio = null;
            _wasEating = false;
            StopSyncingLaserVisual();
        };

        helper.ConsoleCommands.Add(
            "give_lasereye",
            "获得镭射眼饰品（5号化合物）。",
            (_, __) => Game1.player.addItemByMenuIfNecessary(ItemRegistry.Create(LaserEyeTrinketQualifiedId)));
    }

    /// <summary>将配置重置为默认值（供 GMCM 调用）。</summary>
    internal void ResetConfig()
    {
        _config = new ModConfig();
    }

    /// <summary>加载饰品贴图，并注册饰品、制作配方与齐钻商店条目。</summary>
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(TrinketIconAssetName))
        {
            e.LoadFromModFile<Texture2D>(TrinketIconPath, AssetLoadPriority.Medium);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Trinkets"))
        {
            e.Edit(asset =>
            {
                asset.AsDictionary<string, TrinketData>().Data[LaserEyeTrinketId] = new TrinketData
                {
                    DisplayName = "5号化合物",
                    Description = "听说这瓶药剂可以让你获得神秘力量。",
                    Texture = "Mods\\Kyle.LaserEye\\CompoundNo5",
                    SheetIndex = 0,
                    TrinketEffectClass = typeof(LaserEyeTrinketEffect).AssemblyQualifiedName,
                    DropsNaturally = false,
                    CanBeReforged = false,
                };
            });
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
        {
            e.Edit(asset =>
            {
                // 配方键必须与饰品 Item.Name/BaseName 一致，否则 LearnRecipe 与商店预览都会匹配失败（回退为火把）。
                // 910=放射性矿锭, 337=铱锭, 186=大瓶牛奶, 438=大瓶羊奶, 74=五彩碎片
                asset.AsDictionary<string, string>().Data[LaserEyeTrinketId] =
                    "(O)910 18 (O)337 18 (O)186 18 (O)438 18 (O)74 1/Home/(TR)Kyle.LaserEye_LaserEyeBox/false/null/";
            });
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
        {
            e.Edit(asset =>
            {
                var shops = asset.AsDictionary<string, ShopData>().Data;
                if (!shops.TryGetValue(QiGemShopId, out ShopData? qiShop) || qiShop.Items == null)
                    return;

                qiShop.Items.Add(new ShopItemData
                {
                    Id = $"{LaserEyeTrinketQualifiedId} (Recipe)",
                    ItemId = LaserEyeTrinketQualifiedId,
                    TradeItemId = QiGemItemId,
                    TradeItemAmount = CompoundNo5RecipeQiGemCost,
                    Price = -1,
                    AvailableStock = -1,
                    IsRecipe = true,
                });
            });
        }
    }

    /// <summary>按下发射键且鼠标在快捷栏上时立即屏蔽激光，避免与 UpdateTicked 时序竞争。</summary>
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !IsFireButton(e.Button))
            return;

        if (UsesMouseLeftFireKey() && IsMouseOverToolbar())
            _requireFireKeyRelease = true;
    }

    /// <summary>Apply vanilla bomb destruction at the mouse when left-clicking (throttled).</summary>
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        UpdateAllWinStanceBuff();
        UpdatePendingMilkStaminaBonus();
        PruneStaleRemoteLaserVisuals();

        if (!CanFireLaser())
        {
            // 进入菜单/暂停等状态后，要求松开发射键才能再次发射。
            _requireFireKeyRelease = true;
            ResetDestroyState();
            StopSyncingLaserVisual();
            return;
        }

        if (!IsFireKeyPressed())
            _requireFireKeyRelease = false;

        if (_destroyCooldownRemainingMs > 0)
            _destroyCooldownRemainingMs -= (int)Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;

        if (!IsWearingLaserEyeTrinket() || !IsFireKeyPressed() || _requireFireKeyRelease)
        {
            ResetDestroyState();
            StopSyncingLaserVisual();
            return;
        }

        if (UsesMouseLeftFireKey() && IsMouseOverToolbar())
        {
            _requireFireKeyRelease = true;
            ResetDestroyState();
            StopSyncingLaserVisual();
            return;
        }

        UpdateLaserExtendProgress();

        var location = Game1.currentLocation;
        if (location == null)
        {
            StopSyncingLaserVisual();
            return;
        }

        var mouseWorld = GetMouseWorldPosition();
        BroadcastLaserVisual(location, mouseWorld, active: true);

        if (_laserExtendProgress < 1f)
            return;

        var centerTile = GetMouseTile(mouseWorld);
        if (centerTile == _lastDestroyCenterTile && _destroyCooldownRemainingMs > 0)
            return;

        int bombRadius = GetBombRadiusByStamina();
        PlayExplosionSound();
        SpawnFireballScatter(mouseWorld, location);
        BroadcastLaserExplodeVisual(mouseWorld, location);
        if (Game1.IsMasterGame)
            ApplyBombDestruction(centerTile, location, bombRadius, Game1.player);
        else if (Context.IsMultiplayer)
            SendLaserExplodeMessage(centerTile, location, bombRadius);

        MobileCompat.TryRumble(1f, 300 + bombRadius * 100);
        ConsumeStamina();
        _lastDestroyCenterTile = centerTile;
        _destroyCooldownRemainingMs = DestroyCooldownMs;
    }

    private bool ShouldDrawLocalLaser()
    {
        return CanFireLaser()
            && !_requireFireKeyRelease
            && IsWearingLaserEyeTrinket()
            && IsFireKeyPressed()
            && !(UsesMouseLeftFireKey() && IsMouseOverToolbar());
    }

    private void StopSyncingLaserVisual()
    {
        if (!_wasSyncingLaserVisual)
            return;

        GameLocation? location = Game1.currentLocation;
        if (location != null)
            BroadcastLaserVisual(location, Vector2.Zero, active: false);

        _wasSyncingLaserVisual = false;
    }

    private void BroadcastLaserVisual(GameLocation location, Vector2 targetWorld, bool active)
    {
        if (!Context.IsMultiplayer)
            return;

        Helper.Multiplayer.SendMessage(
            new LaserVisualMessage
            {
                Active = active,
                TargetWorldX = targetWorld.X,
                TargetWorldY = targetWorld.Y,
                ExtendProgress = _laserExtendProgress,
                Trajectory = _config.Trajectory.ToString(),
                LocationName = GetLocationKey(location),
            },
            LaserVisualMessageType,
            modIDs: [ModManifest.UniqueID]);

        _wasSyncingLaserVisual = active;
    }

    private void BroadcastLaserExplodeVisual(Vector2 worldPosition, GameLocation location)
    {
        if (!Context.IsMultiplayer)
            return;

        Helper.Multiplayer.SendMessage(
            new LaserExplodeVisualMessage
            {
                WorldX = worldPosition.X,
                WorldY = worldPosition.Y,
                LocationName = GetLocationKey(location),
            },
            LaserExplodeVisualMessageType,
            modIDs: [ModManifest.UniqueID]);
    }

    private void PruneStaleRemoteLaserVisuals()
    {
        if (_remoteLaserVisuals.Count == 0)
            return;

        int now = Game1.ticks;
        List<long>? expired = null;

        foreach ((long playerId, RemoteLaserVisual visual) in _remoteLaserVisuals)
        {
            if (!visual.Active || now - visual.LastUpdateTick > RemoteLaserVisualTimeoutTicks)
                (expired ??= new List<long>()).Add(playerId);
        }

        if (expired == null)
            return;

        foreach (long playerId in expired)
            _remoteLaserVisuals.Remove(playerId);
    }

    /// <summary>客机将爆炸请求发给主持农场，由主机执行地图破坏。</summary>
    private void SendLaserExplodeMessage(Vector2 centerTile, GameLocation location, int radius)
    {
        Helper.Multiplayer.SendMessage(
            new LaserExplodeMessage
            {
                CenterTileX = centerTile.X,
                CenterTileY = centerTile.Y,
                Radius = radius,
                LocationName = GetLocationKey(location),
            },
            LaserExplodeMessageType,
            modIDs: [ModManifest.UniqueID],
            playerIDs: [Game1.MasterPlayer.UniqueMultiplayerID]);
    }

    /// <summary>处理联机消息：客机爆炸请求（仅主机）与其它玩家的视觉同步。</summary>
    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != ModManifest.UniqueID || !Context.IsWorldReady)
            return;

        if (e.Type == LaserVisualMessageType)
        {
            HandleLaserVisualMessage(e);
            return;
        }

        if (e.Type == LaserExplodeVisualMessageType)
        {
            HandleLaserExplodeVisualMessage(e);
            return;
        }

        if (e.Type != LaserExplodeMessageType || !Game1.IsMasterGame)
            return;

        LaserExplodeMessage message = e.ReadAs<LaserExplodeMessage>();
        Farmer? farmer = Game1.GetPlayer(e.FromPlayerID);
        if (farmer == null || !IsWearingLaserEyeTrinket(farmer))
            return;

        GameLocation? location = Game1.getLocationFromName(message.LocationName);
        if (location == null || farmer.currentLocation != location)
            return;

        var centerTile = new Vector2(message.CenterTileX, message.CenterTileY);
        ApplyBombDestruction(centerTile, location, message.Radius, farmer);
    }

    private void HandleLaserVisualMessage(ModMessageReceivedEventArgs e)
    {
        if (e.FromPlayerID == Game1.player.UniqueMultiplayerID)
            return;

        LaserVisualMessage message = e.ReadAs<LaserVisualMessage>();
        if (!message.Active)
        {
            _remoteLaserVisuals.Remove(e.FromPlayerID);
            return;
        }

        if (!Enum.TryParse(message.Trajectory, ignoreCase: true, out LaserTrajectoryType trajectory))
            trajectory = LaserTrajectoryType.Curved;

        _remoteLaserVisuals[e.FromPlayerID] = new RemoteLaserVisual
        {
            TargetWorld = new Vector2(message.TargetWorldX, message.TargetWorldY),
            ExtendProgress = message.ExtendProgress,
            Trajectory = trajectory,
            LocationName = message.LocationName,
            LastUpdateTick = Game1.ticks,
            Active = true,
        };
    }

    private void HandleLaserExplodeVisualMessage(ModMessageReceivedEventArgs e)
    {
        if (e.FromPlayerID == Game1.player.UniqueMultiplayerID)
            return;

        LaserExplodeVisualMessage message = e.ReadAs<LaserExplodeVisualMessage>();
        GameLocation? location = Game1.currentLocation;
        if (location == null || GetLocationKey(location) != message.LocationName)
            return;

        var worldPosition = new Vector2(message.WorldX, message.WorldY);
        PlayExplosionSound();
        SpawnFireballScatter(worldPosition, location);
    }

    private static string GetLocationKey(GameLocation location)
    {
        return string.IsNullOrEmpty(location.uniqueName.Value) ? location.Name : location.uniqueName.Value;
    }

    /// <summary>
    /// 是否允许发射激光：世界已就绪、游戏未暂停、没有打开任何菜单/背包，且体力大于 0。
    /// </summary>
    private static bool CanFireLaser()
    {
        if (!Context.IsWorldReady || Game1.paused)
            return false;

        // 打开背包、菜单或处于过场/小游戏等状态时不允许发射。
        if (Game1.activeClickableMenu != null || !Context.IsPlayerFree)
            return false;

        // 处于淡入淡出、切换新一天、事件、小游戏等过渡状态（如睡觉前的过渡）时不允许发射。
        if (Game1.fadeToBlack || Game1.globalFade || Game1.newDay || Game1.eventUp
            || Game1.currentMinigame != null || !Game1.shouldTimePass())
            return false;

        // 体力为 0 时不能发射。
        if (Game1.player.Stamina <= 0f)
            return false;

        return true;
    }

    /// <summary>
    /// 按当前体力占最大体力的百分比选择爆炸半径：
    /// ≥60% 虚空炸弹，≥30% 普通炸弹，否则樱桃炸弹。
    /// </summary>
    private static int GetBombRadiusByStamina()
    {
        float maxStamina = Game1.player.MaxStamina;
        float percent = maxStamina <= 0f ? 0f : Game1.player.Stamina / maxStamina;

        if (percent >= 0.60f)
            return VoidBombRadius;
        if (percent >= 0.30f)
            return NormalBombRadius;
        return CherryBombRadius;
    }

    private void ResetDestroyState()
    {
        _lastDestroyCenterTile = new(-9999, -9999);
        _destroyCooldownRemainingMs = 0;
        _laserExtendProgress = 0f;
    }

    private void UpdateLaserExtendProgress()
    {
        float elapsedSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
        _laserExtendProgress = System.Math.Min(1f, _laserExtendProgress + elapsedSeconds / LaserExtendDurationSeconds);
    }

    /// <summary>原版炸弹式地图破坏（仅主持农场执行）。</summary>
    private static void ApplyBombDestruction(Vector2 centerTile, GameLocation location, int radius, Farmer farmer)
    {
        if (!Game1.IsMasterGame)
            return;

        location.explode(centerTile, radius, farmer, damageFarmers: false, destroyObjects: true);
        DestroyResourceClumps(centerTile, location, radius, farmer);
        DestroyCrabPotsInArea(centerTile, location, radius);
    }

    private static Rectangle GetExplosionPixelArea(Vector2 centerTile, int radius)
    {
        int left = (int)((centerTile.X - radius) * Game1.tileSize);
        int top = (int)((centerTile.Y - radius) * Game1.tileSize);
        int size = (radius * 2 + 1) * Game1.tileSize;
        return new Rectangle(left, top, size, size);
    }

    /// <summary>
    /// 原版 explode 不会摧毁蟹笼（destroyObject 要求 CanBeGrabbed）。
    /// 镭射爆炸时显式清除范围内蟹笼，并掉落笼内收获物与诱饵。
    /// </summary>
    private static void DestroyCrabPotsInArea(Vector2 centerTile, GameLocation location, int radius)
    {
        Rectangle area = GetExplosionPixelArea(centerTile, radius);
        List<Vector2>? tilesToRemove = null;

        foreach ((Vector2 tile, StardewValley.Object obj) in location.objects.Pairs)
        {
            if (obj is not CrabPot crabPot)
                continue;

            if (!obj.GetBoundingBoxAt((int)tile.X, (int)tile.Y).Intersects(area))
                continue;

            DestroyCrabPot(crabPot, tile, location);
            (tilesToRemove ??= new List<Vector2>()).Add(tile);
        }

        if (tilesToRemove == null)
            return;

        foreach (Vector2 tile in tilesToRemove)
            location.objects.Remove(tile);
    }

    private static void DestroyCrabPot(CrabPot crabPot, Vector2 tile, GameLocation location)
    {
        Vector2 dropPos = tile * Game1.tileSize + crabPot.directionOffset.Value + new Vector2(32f, 32f);

        if (crabPot.heldObject.Value != null)
            location.debris.Add(new Debris(crabPot.heldObject.Value.getOne(), dropPos));

        if (crabPot.bait.Value != null)
            location.debris.Add(new Debris(crabPot.bait.Value.getOne(), dropPos + new Vector2(16f, 0f)));

        crabPot.performRemoveAction();
    }

    /// <summary>
    /// 爆炸不会伤害 ResourceClump（GiantCrop、树桩、原木、巨石、陨石）。
    /// 对范围内的 clump 模拟斧头砍伐直至摧毁；GiantCrop 会按原版逻辑掉落产物。
    /// </summary>
    private static void DestroyResourceClumps(Vector2 centerTile, GameLocation location, int radius, Farmer farmer)
    {
        if (location.resourceClumps == null || location.resourceClumps.Count == 0)
            return;

        Rectangle area = GetExplosionPixelArea(centerTile, radius);

        for (int i = location.resourceClumps.Count - 1; i >= 0; i--)
        {
            ResourceClump clump = location.resourceClumps[i];
            if (!clump.getBoundingBox().Intersects(area))
                continue;

            Tool tool = CreateLaserTool(clump, farmer);
            if (TryDestroyClump(clump, location, tool) && i < location.resourceClumps.Count && location.resourceClumps[i] == clump)
                location.resourceClumps.RemoveAt(i);
        }
    }

    /// <summary>镭射破坏 ResourceClump 时使用满级虚拟工具，不读取玩家实际装备等级。</summary>
    private static Tool CreateLaserTool(ResourceClump clump, Farmer farmer)
    {
        Tool tool = RequiresPickaxe(clump)
            ? new Pickaxe { UpgradeLevel = 4 }
            : new Axe { UpgradeLevel = 4 };
        tool.lastUser = farmer;
        return tool;
    }

    private static bool RequiresPickaxe(ResourceClump clump)
    {
        int index = clump.parentSheetIndex.Value;
        return index == ResourceClump.boulderIndex
            || index == ResourceClump.meteoriteIndex
            || index == ResourceClump.quarryBoulderIndex;
    }

    private static bool TryDestroyClump(ResourceClump clump, GameLocation location, Tool tool)
    {
        if (clump.Location == null)
            clump.Location = location;

        Vector2 toolTile = clump.Tile + new Vector2(clump.width.Value / 2f, clump.height.Value / 2f);

        // GiantCrop 覆写了 performToolAction，不检查 swingTicker，可连续砍到 health <= 0。
        if (clump is GiantCrop)
        {
            for (int hit = 0; hit < 20; hit++)
            {
                if (clump.performToolAction(tool, 1, toolTile))
                    return true;
            }

            return false;
        }

        // 其它 ResourceClump 同一 swingTicker 只会生效一次，每次命中前递增。
        for (int hit = 0; hit < 50; hit++)
        {
            tool.swingTicker++;
            if (clump.performToolAction(tool, 1, toolTile))
                return true;
        }

        return false;
    }

    private static void PlayExplosionSound()
    {
        Game1.playSound("explosion", out _);
    }

    /// <summary>在指定世界坐标生成向四周随机散开的火球动画，随后淡出消失。</summary>
    private static void SpawnFireballScatter(Vector2 worldPosition, GameLocation location)
    {
        var sprites = new List<TemporaryAnimatedSprite>(FireballScatterCount);
        var random = Game1.random;

        for (int i = 0; i < FireballScatterCount; i++)
        {
            float angle = (float)(random.NextDouble() * System.Math.PI * 2);
            float speed = random.Next(4, 9);
            var motion = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * speed;
            var position = worldPosition + new Vector2(random.Next(-12, 13), random.Next(-12, 13));
            float scale = 1.8f + (float)random.NextDouble() * 0.8f;

            sprites.Add(new TemporaryAnimatedSprite(
                FireballTexturePath,
                new Rectangle(0, 0, FireballFrameSize, FireballFrameSize),
                FireballFrameIntervalMs,
                FireballFrameCount,
                1,
                position,
                flicker: false,
                flipped: random.NextDouble() < 0.5,
                layerDepth: (position.Y + FireballFrameSize) / 10000f,
                alphaFade: 0.028f,
                Color.White,
                scale,
                0f,
                0f,
                0f)
            {
                motion = motion,
                acceleration = motion * -0.04f,
                delayBeforeAnimationStart = random.Next(0, 40),
            });
        }

        location.TemporarySprites.AddRange(sprites);
    }

    /// <summary>发射一次激光消耗体力，体力不会降到 0 以下。</summary>
    private static void ConsumeStamina()
    {
        Game1.player.Stamina = System.Math.Max(0f, Game1.player.Stamina - StaminaCostPerShot);
    }

    /// <summary>PC 上使用配置的发射键；Android 上按住屏幕（MouseLeft）发射。</summary>
    private bool IsFireKeyPressed()
    {
        if (IsMobilePlatform)
            return Helper.Input.IsDown(SButton.MouseLeft);

        return Helper.Input.IsDown(_config.FireKey);
    }

    private bool IsFireButton(SButton button)
    {
        if (IsMobilePlatform)
            return button == SButton.MouseLeft;

        return button == _config.FireKey;
    }

    private bool UsesMouseLeftFireKey()
    {
        return !IsMobilePlatform && _config.FireKey == SButton.MouseLeft;
    }

    /// <summary>鼠标是否在屏幕底部快捷栏区域（PC 近似判定，不引用 Toolbar/makeSafeMarginY 以兼容 Android）。</summary>
    private static bool IsMouseOverToolbar()
    {
        if (IsMobilePlatform || !Game1.displayHUD || Game1.activeClickableMenu != null)
            return false;

        Game1.PushUIMode();
        try
        {
            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            int uiHeight = Game1.uiViewport.Height;
            int uiWidth = Game1.uiViewport.Width;

            // 12 格快捷栏：水平居中 768px，高度约 128px（与原版布局接近）。
            if (mouseY < uiHeight - 128)
                return false;

            int left = uiWidth / 2 - 384;
            int right = uiWidth / 2 + 384;
            return mouseX >= left && mouseX < right;
        }
        finally
        {
            Game1.PopUIMode();
        }
    }

    /// <summary>Draw laser lines after the world; when facing up, redraw the player on top.</summary>
    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        DrawRemoteLaserLines(e.SpriteBatch);

        if (!ShouldDrawLocalLaser())
        {
            ResetDestroyState();
            return;
        }

        if (IsMouseOverToolbar())
        {
            ResetDestroyState();
            return;
        }

        var mouseWorld = GetMouseWorldPosition();
        DrawLaserLines(e.SpriteBatch, Game1.player, mouseWorld, _laserExtendProgress, _config.Trajectory);

        if (Game1.player.FacingDirection == 0)
            Game1.player.draw(e.SpriteBatch);
    }

    private void DrawRemoteLaserLines(SpriteBatch batch)
    {
        if (_remoteLaserVisuals.Count == 0)
            return;

        GameLocation? currentLocation = Game1.currentLocation;
        if (currentLocation == null)
            return;

        string locationKey = GetLocationKey(currentLocation);

        foreach ((long playerId, RemoteLaserVisual visual) in _remoteLaserVisuals)
        {
            if (!visual.Active || visual.LocationName != locationKey)
                continue;

            Farmer? farmer = Game1.GetPlayer(playerId);
            if (farmer == null || farmer.currentLocation != currentLocation || !IsWearingLaserEyeTrinket(farmer))
                continue;

            DrawLaserLines(batch, farmer, visual.TargetWorld, visual.ExtendProgress, visual.Trajectory);

            if (farmer.FacingDirection == 0)
                farmer.draw(batch);
        }
    }

    /// <summary>玩家是否装备了镭射眼饰品。</summary>
    private static bool IsWearingLaserEyeTrinket()
    {
        return IsWearingLaserEyeTrinket(Game1.player);
    }

    private static bool IsWearingLaserEyeTrinket(Farmer farmer)
    {
        foreach (Trinket? trinket in farmer.trinketItems)
        {
            if (trinket?.QualifiedItemId == LaserEyeTrinketQualifiedId)
                return true;
        }

        return false;
    }

    /// <summary>是否装备 5 号化合物（全胜姿态的唯一触发条件）。</summary>
    private static bool IsAllWinStanceActive()
    {
        return IsWearingLaserEyeTrinket();
    }

    /// <summary>
    /// 仅装备 5 号化合物时为玩家施加「全胜姿态」buff；卸下时立即移除。
    /// 独立于激光发射条件，保证打开菜单等状态下 buff 仍然保留。
    /// </summary>
    private void UpdateAllWinStanceBuff()
    {
        if (!Context.IsWorldReady)
            return;

        var player = Game1.player;
        bool active = IsAllWinStanceActive();
        bool hasBuff = player.hasBuff(AllWinStanceBuffId);

        if (active && !hasBuff)
            player.applyBuff(CreateAllWinStanceBuff());
        else if (!active && hasBuff)
            player.buffs.Remove(AllWinStanceBuffId);
    }

    /// <summary>构建「全胜姿态」buff（持续到手动移除）。</summary>
    private Buff CreateAllWinStanceBuff()
    {
        return new Buff(
            id: AllWinStanceBuffId,
            source: "LaserEye",
            displaySource: "5号化合物",
            duration: Buff.ENDLESS,
            iconTexture: _buffIcon,
            iconSheetIndex: 0,
            displayName: "全胜姿态",
            description: "恐怖的超自然力量。喝牛奶或羊奶可恢复更多体力。");
    }

    /// <summary>装备/卸下饰品时同步 buff；检测到饮用牛奶时先标记，等动画结束再回体。</summary>
    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (!e.IsLocalPlayer || !Context.IsWorldReady)
            return;

        UpdateAllWinStanceBuff();

        Farmer player = e.Player;
        if (!IsAllWinStanceActive())
            return;

        if (!player.isEating && player.itemToEat == null)
            return;

        foreach (ItemStackSizeChange change in e.QuantityChanged)
        {
            if (change.OldSize <= change.NewSize)
                continue;

            TryMarkPendingMilkStaminaBonus(player, change.Item);
            return;
        }

        foreach (Item item in e.Removed)
            TryMarkPendingMilkStaminaBonus(player, item);
    }

    /// <summary>吃喝动画结束后（与原版 doneEating 同步）再按奶类额外恢复体力。</summary>
    private void UpdatePendingMilkStaminaBonus()
    {
        if (!Context.IsWorldReady)
            return;

        Farmer player = Game1.player;
        bool isEating = player.isEating;

        if (_wasEating && !isEating && _pendingMilkStaminaBonusRatio.HasValue)
        {
            float ratio = _pendingMilkStaminaBonusRatio.Value;
            _pendingMilkStaminaBonusRatio = null;
            if (IsAllWinStanceActive())
                ApplyMilkStaminaBonus(player, ratio);
        }

        _wasEating = isEating;
    }

    private void TryMarkPendingMilkStaminaBonus(Farmer player, Item? item)
    {
        Item? milkItem = player.itemToEat ?? item;
        if (milkItem == null || !IsDrinkableMilk(milkItem))
            return;

        if (player.itemToEat != null && !IsDrinkableMilk(player.itemToEat))
            return;

        _pendingMilkStaminaBonusRatio = GetMilkStaminaBonusRatio(milkItem);
    }

    private void ApplyMilkStaminaBonus(Farmer player, float maxStaminaRatio)
    {
        if (Game1.ticks == _lastMilkStaminaBonusTick)
            return;

        _lastMilkStaminaBonusTick = Game1.ticks;

        float bonus = player.MaxStamina * maxStaminaRatio;
        float oldStamina = player.Stamina;
        player.Stamina = System.Math.Min(player.MaxStamina, player.Stamina + bonus);

        if (player.Stamina <= oldStamina)
            return;

        Game1.addHUDMessage(new HUDMessage(
            Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3116", (int)(player.Stamina - oldStamina)),
            4));
    }

    /// <summary>大瓶牛奶/羊奶恢复一半最大体力，小瓶恢复三分之一。</summary>
    private static float GetMilkStaminaBonusRatio(Item item)
    {
        return item.HasContextTag("large_milk_item") ? 0.5f : 1f / 3f;
    }

    private static bool IsDrinkableMilk(Item item)
    {
        return item.HasContextTag("cow_milk_item") || item.HasContextTag("goat_milk_item");
    }

    /// <summary>绘制多层镭射光束：先直线延伸至鼠标，再切换为正弦交叉或直线。</summary>
    private static void DrawLaserLines(
        SpriteBatch batch,
        Farmer player,
        Vector2 targetWorld,
        float extendProgress,
        LaserTrajectoryType trajectory)
    {
        var tileSize = Game1.tileSize;
        var bodyCenter = player.Position + new Vector2(0f, -tileSize / 2f);
        float pulse = GetLaserPulse();

        if (extendProgress < 1f)
        {
            DrawExtendingLaserLines(batch, bodyCenter, targetWorld, pulse, extendProgress, player.FacingDirection);
            return;
        }

        GetEyePositions(bodyCenter, player.FacingDirection, out Vector2 eyeA, out Vector2 eyeB);

        if (trajectory == LaserTrajectoryType.Curved)
        {
            DrawLaserBeam(batch, eyeA, targetWorld, pulse, 0f);
            DrawLaserBeam(batch, eyeB, targetWorld, pulse, MathF.PI);
        }
        else
        {
            DrawStraightLaserBeam(batch, eyeA, targetWorld, pulse);
            DrawStraightLaserBeam(batch, eyeB, targetWorld, pulse);
        }

        DrawImpactGlow(batch, targetWorld, pulse);
    }

    private static void GetEyePositions(Vector2 bodyCenter, int facingDirection, out Vector2 eyeA, out Vector2 eyeB)
    {
        switch (facingDirection)
        {
            case 0: // 朝上
            case 2: // 朝下
            {
                var pairCenter = bodyCenter + new Vector2(VerticalEyeOffsetX, VerticalEyeOffsetY);
                eyeA = pairCenter + new Vector2(-VerticalEyeSpacing / 2f, 0f);
                eyeB = pairCenter + new Vector2(VerticalEyeSpacing / 2f, 0f);
                break;
            }
            case 1: // 朝右：眼位在脸右侧
            {
                var pairCenter = bodyCenter + new Vector2(HorizontalEyeOffsetX, HorizontalEyeOffsetY);
                eyeA = pairCenter + new Vector2(HorizontalEyeDepthOffset, HorizontalEyeSpacing / 2f);
                eyeB = pairCenter + new Vector2(HorizontalEyeDepthOffset, HorizontalEyeSpacing / 2f);
                break;
            }
            case 3: // 朝左：镜像到脸左侧
            {
                var pairCenter = bodyCenter + new Vector2(-HorizontalEyeOffsetX + 55f, HorizontalEyeOffsetY);
                eyeA = pairCenter + new Vector2(HorizontalEyeDepthOffset, HorizontalEyeSpacing / 2f);
                eyeB = pairCenter + new Vector2(HorizontalEyeDepthOffset, HorizontalEyeSpacing / 2f);
                break;
            }
            default:
                eyeA = eyeB = bodyCenter;
                break;
        }
    }

    /// <summary>延伸阶段：直线从眼眶缓慢射向鼠标。</summary>
    private static void DrawExtendingLaserLines(
        SpriteBatch batch,
        Vector2 bodyCenter,
        Vector2 targetWorld,
        float pulse,
        float extendProgress,
        int facingDirection)
    {
        GetEyePositions(bodyCenter, facingDirection, out Vector2 eyeA, out Vector2 eyeB);
        Vector2 tipA = Vector2.Lerp(eyeA, targetWorld, extendProgress);
        Vector2 tipB = Vector2.Lerp(eyeB, targetWorld, extendProgress);
        DrawStraightLaserBeam(batch, eyeA, tipA, pulse);
        DrawStraightLaserBeam(batch, eyeB, tipB, pulse);
        DrawImpactGlow(batch, tipA, pulse);
        DrawImpactGlow(batch, tipB, pulse);
    }

    /// <summary>直线三层光束 + 眼眶光晕。</summary>
    private static void DrawStraightLaserBeam(SpriteBatch batch, Vector2 worldStart, Vector2 worldEnd, float pulse)
    {
        float outerThickness = OuterGlowBaseThickness * (0.85f + pulse * 0.3f);
        Color outerColor = OuterGlowColor * (0.55f + pulse * 0.35f);

        DrawWorldLineLayer(batch, worldStart, worldEnd, outerColor, outerThickness);
        DrawWorldLineLayer(batch, worldStart, worldEnd, MainBeamColor, MainBeamThickness);
        DrawWorldLineLayer(batch, worldStart, worldEnd, CoreBeamColor, CoreBeamThickness);
        DrawEyeGlow(batch, worldStart, pulse);
    }

    private static float GetLaserPulse()
    {
        double seconds = Game1.currentGameTime.TotalGameTime.TotalSeconds;
        return 0.65f + 0.35f * (float)System.Math.Sin(seconds * 10.0);
    }

    /// <summary>单束镭射：正弦摆动三层光束 + 起点眼眶光晕。</summary>
    private static void DrawLaserBeam(SpriteBatch batch, Vector2 worldStart, Vector2 worldEnd, float pulse, float sinePhase)
    {
        float outerThickness = OuterGlowBaseThickness * (0.85f + pulse * 0.3f);
        Color outerColor = OuterGlowColor * (0.55f + pulse * 0.35f);

        DrawSineWaveLineLayer(batch, worldStart, worldEnd, outerColor, outerThickness, sinePhase);
        DrawSineWaveLineLayer(batch, worldStart, worldEnd, MainBeamColor, MainBeamThickness, sinePhase);
        DrawSineWaveLineLayer(batch, worldStart, worldEnd, CoreBeamColor, CoreBeamThickness, sinePhase);
        DrawEyeGlow(batch, worldStart, pulse);
    }

    /// <summary>沿光束做正弦摆动（波纹从眼眶流向鼠标）；相位差 π 的两束会在中途交叉。</summary>
    private static void DrawSineWaveLineLayer(
        SpriteBatch batch,
        Vector2 worldStart,
        Vector2 worldEnd,
        Color color,
        float thickness,
        float sinePhase)
    {
        Vector2 delta = worldEnd - worldStart;
        float length = delta.Length();
        if (length <= 0f)
            return;

        Vector2 perpendicular = new(-delta.Y, delta.X);
        if (perpendicular.LengthSquared() > 0f)
            perpendicular.Normalize();

        float amplitude = length * BeamSineAmplitudeRatio;
        double time = Game1.currentGameTime.TotalGameTime.TotalSeconds;

        Vector2 previous = worldStart;
        for (int i = 1; i <= BeamCurveSegments; i++)
        {
            float t = i / (float)BeamCurveSegments;
            float envelope = (float)System.Math.Sin(t * System.Math.PI);
            float wave = (float)System.Math.Sin(
                t * System.Math.PI * 2 * BeamSineFrequency - time * BeamSineSpeed + sinePhase);
            Vector2 point = worldStart + delta * t + perpendicular * (amplitude * wave * envelope);
            DrawWorldLineLayer(batch, previous, point, color, thickness);
            previous = point;
        }
    }

    /// <summary>落点光斑（爆炸火球仍由 UpdateTicked 单独生成）。</summary>
    private static void DrawImpactGlow(SpriteBatch batch, Vector2 worldPos, float pulse)
    {
        Vector2 screen = Game1.GlobalToLocal(Game1.viewport, worldPos);
        float layerDepth = worldPos.Y / 10000f;
        float scale = ImpactGlowBaseScale * (0.9f + pulse * 0.35f);
        var origin = new Vector2(CursorGlowSource.Width / 2f, CursorGlowSource.Height / 2f);

        batch.Draw(
            Game1.mouseCursors,
            screen,
            CursorGlowSource,
            Color.Red * (0.45f + pulse * 0.25f),
            0f,
            origin,
            scale * 1.6f,
            SpriteEffects.None,
            layerDepth);

        batch.Draw(
            Game1.mouseCursors,
            screen,
            CursorGlowSource,
            Color.White * (0.35f + pulse * 0.2f),
            0f,
            origin,
            scale,
            SpriteEffects.None,
            layerDepth - 0.0001f);
    }

    /// <summary>眼眶起点光晕。</summary>
    private static void DrawEyeGlow(SpriteBatch batch, Vector2 worldPos, float pulse)
    {
        Vector2 screen = Game1.GlobalToLocal(Game1.viewport, worldPos);
        float layerDepth = worldPos.Y / 10000f - 0.0002f;
        float scale = EyeGlowBaseScale * (0.85f + pulse * 0.4f);
        var origin = new Vector2(CursorGlowSource.Width / 2f, CursorGlowSource.Height / 2f);

        batch.Draw(
            Game1.mouseCursors,
            screen,
            CursorGlowSource,
            Color.Red * (0.5f + pulse * 0.3f),
            0f,
            origin,
            scale * 1.4f,
            SpriteEffects.None,
            layerDepth);

        batch.Draw(
            Game1.mouseCursors,
            screen,
            CursorGlowSource,
            Color.White * (0.75f + pulse * 0.25f),
            0f,
            origin,
            scale * 0.75f,
            SpriteEffects.None,
            layerDepth - 0.0001f);
    }

    private static Vector2 GetMouseTile(Vector2 worldPosition)
    {
        return new Vector2(
            (int)(worldPosition.X / Game1.tileSize),
            (int)(worldPosition.Y / Game1.tileSize));
    }

    /// <summary>Mouse position in world pixel coordinates.</summary>
    private static Vector2 GetMouseWorldPosition()
    {
        var mouse = Game1.getMousePosition();
        return new Vector2(mouse.X + Game1.viewport.X, mouse.Y + Game1.viewport.Y);
    }

    /// <summary>绘制单层光束线段。</summary>
    private static void DrawWorldLineLayer(SpriteBatch batch, Vector2 worldStart, Vector2 worldEnd, Color color, float thickness)
    {
        Vector2 start = Game1.GlobalToLocal(Game1.viewport, worldStart);
        Vector2 end = Game1.GlobalToLocal(Game1.viewport, worldEnd);
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length <= 0f)
            return;

        float angle = (float)System.Math.Atan2(delta.Y, delta.X);
        batch.Draw(
            Game1.staminaRect,
            start,
            sourceRectangle: null,
            color: color,
            rotation: angle,
            origin: new Vector2(0f, 0.5f),
            scale: new Vector2(length, thickness),
            effects: SpriteEffects.None,
            layerDepth: 1f);
    }

    private sealed class RemoteLaserVisual
    {
        public Vector2 TargetWorld;
        public float ExtendProgress;
        public LaserTrajectoryType Trajectory;
        public string LocationName = "";
        public int LastUpdateTick;
        public bool Active;
    }
}
