Option Explicit
' Keep the Windows environment setup and synchronous exit status, but do not
' show its console behind installer/tool dialogs. MO2 owns its own GUI windows.
Dim shell, result
Set shell = CreateObject("WScript.Shell")
result = shell.Run("cmd.exe /d /c .mo2-frontend-launch.cmd", 0, True)
WScript.Quit result
