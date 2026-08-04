; ============================================================
;  Klyr – Script NSIS v2.3.0
;  Installeur self-contained .NET 10, bilingue FR/EN
; ============================================================

Unicode True

!define APP_NAME      "Klyr"
!define APP_VERSION   "2.5.0"
!define APP_PUBLISHER "InfoZen · Yahya"
!define APP_EXE       "Klyr.exe"
!define INSTALL_DIR   "$PROGRAMFILES64\Klyr"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Klyr"

Name              "${APP_NAME} ${APP_VERSION}"
OutFile           "Klyr_Setup_v${APP_VERSION}.exe"
InstallDir        "${INSTALL_DIR}"
InstallDirRegKey  HKLM "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor     /SOLID lzma

!include "MUI2.nsh"
!include "WinVer.nsh"

!define MUI_ICON              "..\Assets\Icons\icon.ico"
!define MUI_UNICON            "..\Assets\Icons\icon.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN    "$INSTDIR\${APP_EXE}"

; ── Pages installation ───────────────────────────────────────
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; ── Pages désinstallation ────────────────────────────────────
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; ── v2.2.0 : Multilangue (le user choisit FR ou EN au lancement,
;             ou la langue du système est utilisée automatiquement) ──
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "English"

; ── Textes localisés des pages NSIS ──────────────────────────
LangString DESC_Welcome ${LANG_FRENCH}  "Klyr Optimiseur PC v${APP_VERSION}$\r$\n$\r$\nCet assistant va installer Klyr sur votre ordinateur.$\r$\n$\r$\nNouveautés v2.5 :$\r$\n  • Profils 1-clic (Gaming / Perf max / Vie privée / Équilibré)$\r$\n  • Débloat UWP avancé (apps du Windows Store)$\r$\n  • Gros fichiers & doublons (suppression vers la Corbeille)$\r$\n  • Rapport de santé actionnable$\r$\n  • 6 modules / 44 optimisations + 11 outils avancés$\r$\n  • Monitoring matériel, mode tray, services, restauration$\r$\n$\r$\nFermez toutes les applications avant de continuer."
LangString DESC_Welcome ${LANG_ENGLISH} "Klyr PC Optimizer v${APP_VERSION}$\r$\n$\r$\nThis wizard will install Klyr on your computer.$\r$\n$\r$\nWhat's new in v2.5:$\r$\n  • One-click profiles (Gaming / Max perf / Privacy / Balanced)$\r$\n  • Advanced UWP debloat (Windows Store apps)$\r$\n  • Large files & duplicates (delete to Recycle Bin)$\r$\n  • Actionable health report$\r$\n  • 6 modules / 44 optimizations + 11 advanced tools$\r$\n  • Hardware monitoring, tray mode, services, restore$\r$\n$\r$\nClose all applications before continuing."

LangString DESC_RunKlyr ${LANG_FRENCH}  "Lancer Klyr"
LangString DESC_RunKlyr ${LANG_ENGLISH} "Launch Klyr"

LangString MSG_NeedWin10 ${LANG_FRENCH}  "Klyr necessite Windows 10 ou superieur."
LangString MSG_NeedWin10 ${LANG_ENGLISH} "Klyr requires Windows 10 or later."

LangString MSG_Uninstalled ${LANG_FRENCH}  "Klyr a ete desinstalle.$\r$\nVos logs dans Documents\Klyr\ ont ete conserves."
LangString MSG_Uninstalled ${LANG_ENGLISH} "Klyr has been uninstalled.$\r$\nYour logs in Documents\Klyr\ have been preserved."

; Appliquer les textes localisés aux macros MUI
!define MUI_WELCOMEPAGE_TITLE "Installation de Klyr ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TITLE_EN "Klyr ${APP_VERSION} Setup"
!define MUI_FINISHPAGE_RUN_TEXT "$(DESC_RunKlyr)"

; ── Installation ─────────────────────────────────────────────
Section "Klyr" SecMain
    SectionIn RO
    SetOutPath "$INSTDIR"

    ; Copie tout le contenu du dossier publish :
    ;   - Klyr.exe (single-file self-contained, runtime .NET 10 embarqué)
    ;   - en\Klyr.resources.dll (satellite anglais auto-chargé selon culture)
    ;   - dépendances natives (System.Management.dll, runtimes\*)
    File /r "..\publish\*.*"

    ; Raccourcis Bureau + Menu Démarrer
    CreateShortcut "$DESKTOP\Klyr.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

    CreateDirectory "$SMPROGRAMS\Klyr"
    CreateShortcut  "$SMPROGRAMS\Klyr\Klyr.lnk"              "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
    CreateShortcut  "$SMPROGRAMS\Klyr\Desinstaller Klyr.lnk" "$INSTDIR\Uninstall.exe"

    ; Entrées Programmes et fonctionnalités
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayName"     "${APP_NAME}"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr   HKLM "${UNINSTALL_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify"        1
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair"        1
    ; Taille estimée en KB (Klyr.exe ~44 Mo + en\Klyr.resources.dll ~27 Ko + runtimes)
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "EstimatedSize"   165000

    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

; ── Désinstallation ──────────────────────────────────────────
Section "Uninstall"
    RMDir  /r "$INSTDIR"
    Delete "$DESKTOP\Klyr.lnk"
    RMDir  /r "$SMPROGRAMS\Klyr"
    DeleteRegKey HKLM "${UNINSTALL_KEY}"
    MessageBox MB_ICONINFORMATION "$(MSG_Uninstalled)" /SD IDOK
SectionEnd

; ── Vérif Windows 10 minimum (WinVer.nsh) ────────────────────
Function .onInit
    ${IfNot} ${AtLeastWin10}
        MessageBox MB_ICONSTOP "$(MSG_NeedWin10)" /SD IDOK
        Abort
    ${EndIf}
FunctionEnd
