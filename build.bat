@echo off
setlocal
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

rem Where the game is. Change this if the game lives somewhere else.
set "GAMEDIR=E:\SteamLibrary\steamapps\common\Probably Stolen"
set "MODDIR=%GAMEDIR%\Mods\AdmirerLona"
set "PROJ=%ROOT%\src\AdmirerLona"

set "DOTNET=%ROOT%\tools\dotnet-sdk\dotnet.exe"
if not exist "%DOTNET%" (
    echo [ERROR] Portable .NET SDK not found:
    echo         %DOTNET%
    echo         Re-extract tools\dotnet-sdk-8.0.427-win-x64.zip into tools\dotnet-sdk\.
    pause
    exit /b 1
)

set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_HOME=%ROOT%\.build\dotnet-home"
set "NUGET_PACKAGES=%ROOT%\.build\nuget"
set "TMP=%ROOT%\.build\temp"
set "TEMP=%ROOT%\.build\temp"
if not exist "%DOTNET_CLI_HOME%" mkdir "%DOTNET_CLI_HOME%"
if not exist "%NUGET_PACKAGES%" mkdir "%NUGET_PACKAGES%"
if not exist "%TMP%" mkdir "%TMP%"

cd /d "%PROJ%"
echo Building AdmirerLona (Release) ...
echo.
rem SkipInstall=true: the copy below does the install, so MSBuild's Copy task is not used.
"%DOTNET%" build -c Release -p:SkipInstall=true -p:GameDir="%GAMEDIR%" %*
set "RC=%ERRORLEVEL%"
echo.
if not "%RC%"=="0" (
    echo Build FAILED - read the errors above.
    pause
    exit /b %RC%
)

echo Installing into %MODDIR% ...
if not exist "%MODDIR%" mkdir "%MODDIR%"
copy /Y "%PROJ%\bin\Release\netstandard2.1\AdmirerLona.dll" "%MODDIR%\AdmirerLona.dll" >nul
if errorlevel 1 goto install_failed
copy /Y "%PROJ%\manifest.xml" "%MODDIR%\manifest.xml" >nul
if errorlevel 1 goto install_failed
if exist "%PROJ%\preview.png" copy /Y "%PROJ%\preview.png" "%MODDIR%\preview.png" >nul
for %%D in (Textures Sounds Localization LootTables Books) do (
    if exist "%PROJ%\%%D" (
        xcopy /E /I /Y /Q "%PROJ%\%%D" "%MODDIR%\%%D" >nul
    )
)

echo Build OK - installed into %MODDIR%
pause
exit /b 0

:install_failed
echo.
echo [ERROR] Could not copy the mod into the game folder.
echo         Close Probably Stolen and run build.bat again.
pause
exit /b 1
