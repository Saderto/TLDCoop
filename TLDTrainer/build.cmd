@echo off
rem Сборка: сначала модуль для игры, затем окно трейнера (модуль встраивается в .exe).
cd /d "%~dp0"
dotnet build Mod -c Release || exit /b 1
dotnet publish App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish || exit /b 1
echo Готово: publish\TLDTrainer.exe
