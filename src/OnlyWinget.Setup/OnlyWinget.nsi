!define PRODUCT_NAME "OnlyWinget"
!define PUBLISHER "OnlyWinget"
!define REGKEY "SOFTWARE\OnlyWinget"
!define UNINSTALL_REGKEY "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget"

!ifndef PRODUCT_VERSION
  !define PRODUCT_VERSION "1.0.2"
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
RequestExecutionLevel admin
SetCompressor /SOLID lzma



Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${OUT_FILE}"
InstallDir "$PROGRAMFILES64\OnlyWinget"
InstallDirRegKey HKLM "${REGKEY}" "InstallDir"

!include "MUI2.nsh"

!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "Assets\WixUIBanner.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "Assets\WixUIBanner.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "Assets\WixUIDialog.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "Assets\WixUIDialog.bmp"
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LICENSE_RTF}"
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

Section "MainSection" SEC01
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteRegStr HKLM "${REGKEY}" "InstallDir" "$INSTDIR"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\OnlyWinget"
  CreateShortcut "$SMPROGRAMS\OnlyWinget\OnlyWinget.lnk" "$INSTDIR\OnlyWinget.exe" "" "$INSTDIR\OnlyWinget.exe" 0
  CreateShortcut "$SMPROGRAMS\OnlyWinget\Uninstall.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0
  CreateShortcut "$DESKTOP\OnlyWinget.lnk" "$INSTDIR\OnlyWinget.exe" "" "$INSTDIR\OnlyWinget.exe" 0

  WriteRegStr HKLM "${UNINSTALL_REGKEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKLM "${UNINSTALL_REGKEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKLM "${UNINSTALL_REGKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTALL_REGKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINSTALL_REGKEY}" "DisplayIcon" '"$INSTDIR\OnlyWinget.exe"'
  WriteRegStr HKLM "${UNINSTALL_REGKEY}" "InstallLocation" '"$INSTDIR"'
  WriteRegDWORD HKLM "${UNINSTALL_REGKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_REGKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM OnlyWinget.exe'

  Delete "$DESKTOP\OnlyWinget.lnk"
  Delete "$SMPROGRAMS\OnlyWinget\OnlyWinget.lnk"
  Delete "$SMPROGRAMS\OnlyWinget\Uninstall.lnk"
  RMDir "$SMPROGRAMS\OnlyWinget"

  RMDir /r "$INSTDIR"

  DeleteRegKey HKLM "${UNINSTALL_REGKEY}"
  DeleteRegKey HKLM "${REGKEY}"
SectionEnd
