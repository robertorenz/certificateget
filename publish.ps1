# Builds a self-contained, compressed single-file CertificateGet.exe into .\run
# (no .NET runtime needed on the target machine).
dotnet publish "$PSScriptRoot\CertificateGet\CertificateGet.csproj" -c Release -r win-x64 `
    --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o "$PSScriptRoot\run"
