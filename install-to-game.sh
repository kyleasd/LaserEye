#!/bin/bash
set -e

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME_MODS="/d/Program Files (x86)/Steam/steamapps/common/Stardew Valley/Mods/LaserEye"
SOURCE="$ROOT_DIR/bin/ModDeploy/LaserEye"

if [ ! -d "$SOURCE" ]; then
    echo "请先运行: dotnet build -c Release"
    exit 1
fi

if tasklist 2>/dev/null | grep -qi "StardewModdingAPI"; then
    echo "请先完全关闭游戏/SMAPI，再运行本脚本。"
    exit 1
fi

echo "正在安装到: $GAME_MODS"
mkdir -p "$GAME_MODS"
cp -rf "$SOURCE/"* "$GAME_MODS/"

# 清理旧版遗留贴图
rm -f "$GAME_MODS/assets/IridiumLargeMilk.png" "$GAME_MODS/assets/CompoundNo. 5.PNG" 2>/dev/null || true

echo "安装完成。请启动游戏，用 give_lasereye 重新拿一个饰品查看新图标。"
