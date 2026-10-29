@echo off
rem Builds both apps into dist\ (each run replaces the previous files):
rem   dist\TaskWidget.exe  - PC widget, runs on any Windows 10/11 x64
rem   dist\DayTasks.apk    - Android app (Android 8.0+), signed with android\release.keystore
rem "build.bat nopause" does the same without waiting for a key at the end (release.bat uses it).
cd /d "%~dp0"
if not exist dist mkdir dist

echo === PC widget ===
dotnet publish src\TaskWidget\TaskWidget.csproj -p:PublishProfile=Portable -nologo || goto :fail

echo.
echo === Android app ===
if not defined JAVA_HOME set "JAVA_HOME=%LOCALAPPDATA%\AndroidDev\jdk17"
if not defined ANDROID_HOME set "ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk"
if not exist "%JAVA_HOME%\bin\java.exe" (
  echo JDK 17 not found at %JAVA_HOME% - skipping the APK. See README.
  goto :done
)
call android\gradlew.bat -p android assembleRelease --project-cache-dir "%~dp0.build\gradle-cache" --console=plain -q || goto :fail
copy /y ".build\android\app\outputs\apk\release\app-release.apk" "dist\DayTasks.apk" >nul || goto :fail

:done
echo.
echo Done:
dir /b dist
if /i not "%~1"=="nopause" pause
exit /b 0

:fail
echo.
echo Build failed. If TaskWidget is running, close it and try again.
if /i not "%~1"=="nopause" pause
exit /b 1
