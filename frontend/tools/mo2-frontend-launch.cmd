@echo off
set "QT_QPA_PLATFORM=windows:nowmpointer"
set "MO2_FRONTEND_HOST=1"
"%~dp0ModOrganizer.exe"
exit /b %errorlevel%
