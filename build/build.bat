@echo off
cd /d "%~dp0..\src"
dotnet publish -c Release
echo.
echo 输出目录: src\bin\Release\net8.0-windows\win-x64\publish\
pause
