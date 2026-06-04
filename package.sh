#!/bin/bash
set -e

echo "========================================"
echo "  LaserEye Mod 构建并启动"
echo "========================================"
echo ""

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME_DIR="/d/Program Files (x86)/Steam/steamapps/common/Stardew Valley"
SMAPI_EXE="$GAME_DIR/StardewModdingAPI.exe"
MOD_DIR="$GAME_DIR/Mods/LaserEye"
DEPLOY_DIR="$ROOT_DIR/bin/ModDeploy/LaserEye"

VERSION="$(grep -m1 '<Version>' "$ROOT_DIR/LaserEye.csproj" | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/')"
ZIP_PATH="$ROOT_DIR/bin/Release/net6.0/LaserEye $VERSION.zip"

cd "$ROOT_DIR"

echo "[1/3] 正在 Release 构建（输出到 bin/ModDeploy/LaserEye）..."
dotnet build -c Release
echo "编译成功！"
echo ""

echo "[2/3] 安装到游戏 Mods 目录..."
if tasklist 2>/dev/null | grep -qi "StardewModdingAPI"; then
    echo "游戏正在运行，跳过安装。请关闭游戏后运行: ./install-to-game.sh"
else
    mkdir -p "$MOD_DIR"
    cp -rf "$DEPLOY_DIR/"* "$MOD_DIR/"
    rm -f "$MOD_DIR/assets/IridiumLargeMilk.png" "$MOD_DIR/assets/CompoundNo. 5.PNG" 2>/dev/null || true
    echo "已安装到 $MOD_DIR"
fi
echo ""

echo "[3/3] 启动 SMAPI..."
if [ ! -f "$SMAPI_EXE" ]; then
    echo "找不到 SMAPI：$SMAPI_EXE"
    exit 1
fi
start "" "$SMAPI_EXE"
echo "游戏已启动！"
echo ""

echo "========================================"
echo "  完成！"
echo "  本地 Mod 包: $DEPLOY_DIR"
echo "  发布 zip: $ZIP_PATH"
echo "  手动安装: ./install-to-game.sh（需先关游戏）"
echo "========================================"
