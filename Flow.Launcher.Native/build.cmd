@echo off
cd /d "%~dp0"
echo Building flow_launcher_native in release mode...
cargo build --release
if %errorlevel% neq 0 (
    echo Cargo build failed!
    exit /b %errorlevel%
)
echo Copying flow_launcher_native.dll to output folders...
copy /y target\release\flow_launcher_native.dll ..\Output\Release\
copy /y target\release\flow_launcher_native.dll ..\Output\Debug\
copy /y target\release\flow_launcher_native.dll ..\Flow.Launcher.Infrastructure\
echo flow_launcher_native.dll successfully built and deployed!
