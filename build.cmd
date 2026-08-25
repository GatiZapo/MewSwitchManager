@echo off
setlocal
set CONFIG=Release
if not "%1"=="" set CONFIG=%1

echo [MewSwitch Manager] Clean
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul
rmdir /s /q dist 2>nul

echo [MewSwitch Manager] Restore
dotnet restore MewSwitchManager.csproj || exit /b 1

for %%A in (win-x64 win-arm64 win-x86) do (
  echo [MewSwitch Manager] Publish %%A
  dotnet publish MewSwitchManager.csproj -c %CONFIG% -r %%A --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\%%A --no-restore || exit /b 1
)

echo.
echo Build complete. Artifacts are in dist\win-x64, dist\win-arm64 and dist\win-x86.
endlocal
