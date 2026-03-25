#define MyAppName "Revit MCP Server"
#define MyAppVersion "1.0"
#define MyAppPublisher "Revit MCP"
#define RevitAddinsDir "{userappdata}\Autodesk\Revit\Addins\2025"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={#RevitAddinsDir}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=RevitMcpInstaller
Compression=lzma
SolidCompression=yes
; SetupIconFile=compiler:SetupClassicIcon.ico
Uninstallable=yes
UninstallDisplayIcon={app}\RevitPlugin.dll

[Files]
; Revit Plugin DLLs and Addin definition
Source: "..\RevitPlugin\bin\Release\net8.0-windows\RevitPlugin.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\RevitPlugin\RevitPlugin.addin"; DestDir: "{app}"; Flags: ignoreversion

; Other compiled DLL dependencies (Costura.Fody handles some, but just in case)
Source: "..\RevitPlugin\bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion

; The Node Server bundled output
Source: "..\McpServer\dist\index.js"; DestDir: "{app}"; Flags: ignoreversion

[Code]
// Custom code can go here
