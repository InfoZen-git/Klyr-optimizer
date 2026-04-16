; ============================================================
;  InfoZen – Script NSIS v2.1
; ============================================================

Unicode True

!define APP_NAME      "InfoZen"
!define APP_VERSION   "2.1.0"
!define APP_PUBLISHER "Octave"
!define APP_EXE       "InfoZen.exe"
!define INSTALL_DIR   "$PROGRAMFILES64\InfoZen"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\InfoZen"

Name              "${APP_NAME} ${APP_VERSION}"
OutFile           "InfoZen_Setup_v${APP_VERSION}.exe"
InstallDir        "${INSTALL_DIR}"
InstallDirRegKey  HKLM "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor     /SOLID lzma

!include "MUI2.nsh"
!include "WinVer.nsh"

!define MUI_ICON              "..\Assets\Icons\icon.ico"
!define MUI_UNICON            "..\Assets\Icons\icon.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Installation d'InfoZen ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TEXT  "InfoZen Optimiseur PC$\r$\n$\r$\nCet assistant va installer InfoZen sur votre ordinateur.$\r$\n$\r$\nFermez toutes les applications avant de continuer."
!define MUI_FINISHPAGE_RUN    "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Lancer InfoZen"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "French"

; ── Installation ─────────────────────────────────────────────
Section "InfoZen" SecMain
    SectionIn RO
    SetOutPath "$INSTDIR"
    File /r "..\publish\*.*"

    CreateShortcut "$DESKTOP\InfoZen.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

    CreateDirectory "$SMPROGRAMS\InfoZen"
    CreateShortcut  "$SMPROGRAMS\InfoZen\InfoZen.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
    CreateShortcut  "$SMPROGRAMS\InfoZen\Desinstaller.lnk" "$INSTDIR\Uninstall.exe"

    WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayName"     "${APP_NAME}"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify"        1
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair"        1
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "EstimatedSize"   20000

    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

; ── Désinstallation ──────────────────────────────────────────
Section "Uninstall"
    RMDir  /r "$INSTDIR"
    Delete "$DESKTOP\InfoZen.lnk"
    RMDir  /r "$SMPROGRAMS\InfoZen"
    DeleteRegKey HKLM "${UNINSTALL_KEY}"
    MessageBox MB_ICONINFORMATION "InfoZen a ete desinstalle.$\r$\nVos logs dans Documents\InfoZen\ ont ete conserves." /SD IDOK
SectionEnd

; ── Vérif Windows 10 minimum (WinVer.nsh) ────────────────────
Function .onInit
    ${IfNot} ${AtLeastWin10}
        MessageBox MB_ICONSTOP "InfoZen necessite Windows 10 ou superieur." /SD IDOK
        Abort
    ${EndIf}
FunctionEnd
