# Builds a single-file CertificateGet.exe into .\publish (requires the .NET 9 Desktop Runtime on the target machine).
# Add -SelfContained to bundle the runtime (bigger file, no runtime needed).
param([switch]$SelfContained)

$sc = if ($SelfContained) { "true" } else { "false" }
dotnet publish "$PSScriptRoot\CertificateGet\CertificateGet.csproj" -c Release -r win-x64 `
    --self-contained $sc -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$PSScriptRoot\publish"
