!ifndef ONLYWINGET_CLOSE_APPLICATION_INCLUDED
!define ONLYWINGET_CLOSE_APPLICATION_INCLUDED
!include "LogicLib.nsh"
!ifndef ONLYWINGET_CLOSE_SCRIPT
!define ONLYWINGET_CLOSE_SCRIPT "..\..\scripts\support\CloseOwnedApplication.ps1"
!endif

!macro OnlyWingetCloseInstallation
  InitPluginsDir
  File /oname=$PLUGINSDIR\CloseOwnedApplication.ps1 "${ONLYWINGET_CLOSE_SCRIPT}"
  nsExec::ExecToStack '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\CloseOwnedApplication.ps1" -ExecutablePath "$INSTDIR\OnlyWinget.exe"'
  Pop $0
  Pop $1
  ${If} $0 != 0
    DetailPrint "$1"
    MessageBox MB_OK|MB_ICONSTOP "Close this installation of OnlyWinget and retry. / Chiudi questa installazione di OnlyWinget e riprova." /SD IDOK
    SetErrorLevel 1
    Quit
  ${EndIf}
!macroend
!endif
