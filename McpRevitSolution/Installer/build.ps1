$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$solutionDir = Split-Path -Parent $scriptDir

Write-Host "Compiling RevitPlugin (Release)..."
dotnet build "$solutionDir\RevitPlugin\RevitPlugin.csproj" -c Release

Write-Host "Preparing Node Server..."
Set-Location "$solutionDir\McpServer"
npm install

Write-Host "Bundling Node Server using @vercel/ncc..."
# Use npx @vercel/ncc to single-file bundle the server
npx @vercel/ncc build index.js -o dist -m

Write-Host "Compiling Inno Setup Installer..."
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$iscc = $null
foreach ($path in $isccPaths) {
    if (Test-Path $path) {
        $iscc = $path
        break
    }
}

if ($null -eq $iscc) {
    Write-Host "Inno Setup Compiler not found. Please install Inno Setup 6."
    exit 1
}

Set-Location $scriptDir
& $iscc "RevitMcpInstaller.iss"

Write-Host "✅ Build complete! Installer is in the Output folder."
