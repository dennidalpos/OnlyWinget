!include "LogicLib.nsh"

!macro OnlyWingetRemoveLegacyProtocol KEY EXECUTABLE
  Push $0
  Push $1
  ReadRegStr $0 HKCU "${KEY}" ""
  ReadRegStr $1 HKCU "${KEY}\shell\open\command" ""
  ${If} $0 == "URL:OnlyWinget Protocol"
  ${AndIf} $1 == '"${EXECUTABLE}" "%1"'
    ClearErrors
    ReadRegStr $0 HKCU "${KEY}" "URL Protocol"
    ${IfNot} ${Errors}
      DeleteRegKey HKCU "${KEY}"
      ${If} ${Errors}
        DetailPrint "Could not remove the owned legacy OnlyWinget protocol registration."
        SetErrorLevel 1
      ${EndIf}
    ${EndIf}
  ${EndIf}
  Pop $1
  Pop $0
!macroend
