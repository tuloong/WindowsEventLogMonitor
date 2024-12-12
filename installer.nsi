Unicode True

!include "MUI2.nsh"
!include "FileFunc.nsh"

; Product Information
!define PRODUCT_NAME "WindowsEventLogMonitor"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "ZKSoft"
!define PRODUCT_WEB_SITE "http://www.zksoft.cc"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\${PRODUCT_NAME}.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_GUID "{EAC67F01-A1D3-4097-B7D1-66EB6D27074B}"

; Basic Settings
SetCompressor lzma
RequestExecutionLevel admin

; Version Information
VIProductVersion "${PRODUCT_VERSION}.0"
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "Comments" "WindowsEventLogMonitor Installer"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "WindowsEventLogMonitor-Setup.exe"
InstallDir "$PROGRAMFILES64\${PRODUCT_NAME}"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""

; Interface Settings
!define MUI_ABORTWARNING
!define MUI_ICON "WindowsEventLogMonitor\Resources\app.ico"
!define MUI_UNICON "WindowsEventLogMonitor\Resources\app.ico"

; Install Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; Uninstall Pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Language
!insertmacro MUI_LANGUAGE "English"

Section "MainSection" SEC01
    SetOutPath "$INSTDIR"
    
    ; Copy program files
    File /r "WindowsEventLogMonitor\bin\Release\net8.0-windows\win-x64\publish\*.*"
    
    ; Create shortcuts with icons
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\WindowsEventLogMonitor.exe" "" "$INSTDIR\Resources\app.ico"
    CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\WindowsEventLogMonitor.exe" "" "$INSTDIR\Resources\app.ico"
    
    ; Write uninstall information
    WriteUninstaller "$INSTDIR\uninst.exe"
    WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\WindowsEventLogMonitor.exe"
    WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
    WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\WindowsEventLogMonitor.exe"
    WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
    WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "ProductID" "${PRODUCT_GUID}"
SectionEnd

Section "Uninstall"
  ; Remove program files
    Delete "$INSTDIR\MESESI.exe"
    Delete "$INSTDIR\*.dll"
    RMDir /r "$INSTDIR\Resources"
    Delete "$INSTDIR\uninst.exe"
    
    ; Remove shortcuts
    Delete "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk"
    Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
    RMDir "$SMPROGRAMS\${PRODUCT_NAME}"
    
    ; Remove install directory
    RMDir "$INSTDIR"
    
    ; Remove registry keys
    DeleteRegKey HKLM "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
SectionEnd