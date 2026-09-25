# Builds self-contained, compressed single-file executables into .\run (no .NET runtime needed on the target machines):
#   run\CertificateGet.exe                       the desktop app
#   run\agent-windows\CertificateGet.Agent.exe   agent for Windows servers (NetTalk, TSplus, IIS...)
#   run\agent-linux\CertificateGet.Agent         agent for Linux servers (HAProxy, nginx...)
$common = @('-c', 'Release', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:EnableCompressionInSingleFile=true', '-p:DebugType=none', '-nologo')

dotnet publish "$PSScriptRoot\CertificateGet\CertificateGet.csproj" -r win-x64 @common -o "$PSScriptRoot\run"

$agents = @(
    @{ Rid = 'win-x64';   Dir = 'agent-windows'; Example = 'agent.windows.json' },
    @{ Rid = 'linux-x64'; Dir = 'agent-linux';   Example = 'agent.linux-haproxy.json' }
)
foreach ($a in $agents) {
    $out = Join-Path $PSScriptRoot "run\$($a.Dir)"
    dotnet publish "$PSScriptRoot\CertificateGet.Agent\CertificateGet.Agent.csproj" -r $a.Rid @common -o $out
    Copy-Item "$PSScriptRoot\CertificateGet.Agent\README.md" (Join-Path $out 'README.md') -Force
    Copy-Item "$PSScriptRoot\CertificateGet.Agent\examples\$($a.Example)" (Join-Path $out 'agent.example.json') -Force
}
