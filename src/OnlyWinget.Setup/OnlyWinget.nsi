!define PRODUCT_NAME "OnlyWinget"
!define PUBLISHER "OnlyWinget"
!define REGKEY "SOFTWARE\OnlyWinget"
!define UNINSTALL_REGKEY "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget"

!ifndef PRODUCT_VERSION
  !define PRODUCT_VERSION "1.0.0"
!endif

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\..\artifacts\installer\win-x64\publish"
!endif

!ifndef OUT_FILE
  !define OUT_FILE "..\..\artifacts\dist\OnlyWinget\Release\OnlyWinget-${PRODUCT_VERSION}-setup.exe"
!endif

!ifndef APP_ICON
  !define APP_ICON "..\OnlyWinget\Assets\OnlyWinget.ico"
!endif

!ifndef LICENSE_RTF
  !define LICENSE_RTF "License.rtf"
!endif

Unicode true
SetCompressor /SOLID lzma

; MultiUser Configuration
!define MULTIUSER_EXECUTIONLEVEL Highest
!define MULTIUSER_MUI
!define MULTIUSER_INSTALLMODE_COMMANDLINE
!define MULTIUSER_USE_PROGRAMFILES64
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "${REGKEY}"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallDir"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "${REGKEY}"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallDir"
!define MULTIUSER_INSTALLMODE_INSTDIR "OnlyWinget"

!include "MultiUser.nsh"
!include "MUI2.nsh"
!include "x64.nsh"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${OUT_FILE}"

!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "Assets\HeaderBanner.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "Assets\HeaderBanner.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "Assets\WelcomeDialog.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "Assets\WelcomeDialog.bmp"
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LICENSE_RTF}"
!insertmacro MULTIUSER_PAGE_INSTALLMODE
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\OnlyWinget.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Avvia OnlyWinget / Launch OnlyWinget"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "Italian"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  !insertmacro MULTIUSER_INIT
FunctionEnd

Function un.onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  !insertmacro MULTIUSER_UNINIT
FunctionEnd

Section "MainSection" SEC01
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteRegStr SHCTX "${REGKEY}" "InstallDir" "$INSTDIR"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\OnlyWinget"
  CreateShortcut "$SMPROGRAMS\OnlyWinget\OnlyWinget.lnk" "$INSTDIR\OnlyWinget.exe" "" "$INSTDIR\OnlyWinget.exe" 0
  CreateShortcut "$SMPROGRAMS\OnlyWinget\Uninstall.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0
  CreateShortcut "$DESKTOP\OnlyWinget.lnk" "$INSTDIR\OnlyWinget.exe" "" "$INSTDIR\OnlyWinget.exe" 0

  WriteRegStr SHCTX "${UNINSTALL_REGKEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr SHCTX "${UNINSTALL_REGKEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr SHCTX "${UNINSTALL_REGKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr SHCTX "${UNINSTALL_REGKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr SHCTX "${UNINSTALL_REGKEY}" "DisplayIcon" '"$INSTDIR\OnlyWinget.exe"'
  WriteRegStr SHCTX "${UNINSTALL_REGKEY}" "InstallLocation" '"$INSTDIR"'
  WriteRegDWORD SHCTX "${UNINSTALL_REGKEY}" "NoModify" 1
  WriteRegDWORD SHCTX "${UNINSTALL_REGKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  nsExec::Exec 'taskkill /F /IM OnlyWinget.exe'

  Delete "$DESKTOP\OnlyWinget.lnk"
  Delete "$SMPROGRAMS\OnlyWinget\OnlyWinget.lnk"
  Delete "$SMPROGRAMS\OnlyWinget\Uninstall.lnk"
  RMDir "$SMPROGRAMS\OnlyWinget"

  RMDir /r "$INSTDIR"

  DeleteRegKey SHCTX "${UNINSTALL_REGKEY}"
  DeleteRegKey SHCTX "${REGKEY}"
SectionEnd
