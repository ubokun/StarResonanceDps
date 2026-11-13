@echo off
rem This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

setlocal
set "script=%~dp0release.sh"
set "script=%script:\=/%"
for /f "delims=" %%g in ('where git 2^>nul') do (
  if exist "%%~dpg..\bin\bash.exe" (
    "%%~dpg..\bin\bash.exe" "%script%" %*
    exit /b
  )
)
>&2 echo Git Bash (Git for Windows) was not found.
exit /b 1
