#!/bin/sh
# Сборка: сначала модуль для игры, затем окно трейнера (модуль встраивается в .exe).
set -e
cd "$(dirname "$0")"
dotnet build Mod -c Release
dotnet publish App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -o publish
echo "Готово: publish/TrainerByEMPEROR.exe"
