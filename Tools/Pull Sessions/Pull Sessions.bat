@echo off
setlocal EnableExtensions
title DroneDDW - Pull session files

rem Copies the DroneDDW app's session files (Sessions.csv, Deleted Sessions.csv) from a Quest plugged in by USB to
rem Documents\DroneDDW Sessions\<date time>\ and opens that folder. Every pull gets its own folder, so nothing is
rem overwritten, and nothing on the headset is changed. Same as the Unity menu DroneDDW > Admin Menu > Pull Session
rem Files From Headset (AdminMenuHeadsetFiles.cs); keep the package and folder in step with it.

set "PACKAGE=nl.fontys.dronedw"
set "REMOTE=/sdcard/Android/data/%PACKAGE%/files/Sessions"
set "ADB=%~dp0adb\adb.exe"

if not exist "%ADB%" (
	echo adb.exe is missing. Keep the "adb" folder next to this file.
	goto :fail
)

echo Looking for the headset...
"%ADB%" start-server >nul 2>&1
set "STATE="
for /f "skip=1 tokens=1,2" %%a in ('""%ADB%" devices"') do (
	if not defined STATE set "STATE=%%b"
	if "%%b"=="device" set "STATE=device"
)

if not defined STATE (
	echo.
	echo No headset found.
	echo  - Plug the Quest into this laptop with a USB-C cable that carries data, not just power.
	echo  - Put the headset on: the first time, it asks "Allow USB debugging?". Tick "Always allow
	echo    from this computer" and press Allow. Then run this again.
	echo  - Still nothing? See README.txt about the Oculus ADB driver.
	goto :fail
)
if "%STATE%"=="unauthorized" (
	echo.
	echo The headset hasn't allowed this laptop yet.
	echo Put the headset on, tick "Always allow from this computer" and press Allow. Then run this again.
	goto :fail
)
if not "%STATE%"=="device" (
	echo.
	echo The headset is connected but not ready ^(%STATE%^). Unplug it, plug it back in and run this again.
	goto :fail
)

for /f "usebackq delims=" %%d in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')"`) do set "DOCS=%%d"
for /f "usebackq delims=" %%t in (`powershell -NoProfile -Command "Get-Date -Format 'yyyy-MM-dd HH.mm.ss'"`) do set "STAMP=%%t"
set "LOCAL=%DOCS%\DroneDDW Sessions\%STAMP%"
mkdir "%LOCAL%" 2>nul

echo Copying the session files...
mkdir "%LOCAL%\CSV" 2>nul
"%ADB%" pull "%REMOTE%/." "%LOCAL%\CSV"
if errorlevel 1 (
	rmdir /s /q "%LOCAL%" 2>nul
	echo.
	echo Couldn't copy the session files. If no session has been saved on this headset yet, there is nothing to copy.
	goto :fail
)

rem Excel copies (.xlsx, with sized columns and real dates) in Excel\, next to CSV\, which keeps the files as the
rem headset wrote them for R, SPSS, Python and the like
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0adb\Make Excel Files.ps1" -Folder "%LOCAL%"
if errorlevel 1 (
	echo.
	echo Couldn't make the Excel copies. The files as they came from the headset are in:
	echo %LOCAL%\CSV
	start "" explorer "%LOCAL%"
	goto :fail
)

echo.
echo Done. Excel files are in the Excel folder, the CSV files ^(for R, SPSS, Python^) in the CSV folder:
echo %LOCAL%
start "" explorer "%LOCAL%"
"%ADB%" kill-server >nul 2>&1
echo.
pause
exit /b 0

:fail
rem Stop adb's background server, so the folder can be moved or deleted afterwards
"%ADB%" kill-server >nul 2>&1
echo.
pause
exit /b 1
