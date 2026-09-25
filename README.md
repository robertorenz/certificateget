# CertificateGet

A Windows desktop app (C# / WPF, .NET 9) that gets free TLS certificates from **Let's Encrypt**, keeps every certificate in every common file format, and logs everything you do.

![Platform](https://img.shields.io/badge/platform-Windows-0F172A) ![.NET](https://img.shields.io/badge/.NET-9-2563EB) ![ACME](https://img.shields.io/badge/ACME-Let's%20Encrypt-0D9488)

## Features

- **Standard and wildcard certificates.** Wildcards (`*.example.com`) can also cover the bare domain.
- **Six ways to validate:**
  | Method | When to use it |
  |---|---|
  | HTTP, built-in web server | Run the app on the machine the domain points to; it briefly answers on port 80. No IIS needed. |
  | HTTP, web root folder | IIS, Apache or nginx already serves the site; the app writes the file into `.well-known/acme-challenge` (and a `web.config` for IIS). |
  | DNS, manual | Works anywhere and is required for wildcards. The app shows the TXT records with copy buttons and checks public DNS before validating. |
  | DNS, Cloudflare | Creates and removes the TXT records for you through the Cloudflare API. |
| DNS, Hostinger | Creates and removes the TXT records for you through the Hostinger API. |
| DNS, Constellix | Creates and removes the TXT records for you through the Constellix API (v4, API key + secret key). |
- **Every format, every time.** Each issuance writes:
  | File | Use |
  |---|---|
  | `name.pfx` | Certificate + chain + key, password protected (IIS, Azure, Exchange, Windows) |
  | `name.cer` | Certificate only, PEM |
  | `name.key` | Private key, PEM (PKCS#8 or traditional, you choose) |
  | `name-fullchain.pem` | Certificate + intermediates (nginx, Apache 2.4.8+) |
  | `name-combined.pem` | Full chain + private key in one file (HAProxy, Webmin, appliances) |
  | `name-chain.cer` | Intermediates only (Apache `SSLCertificateChainFile`) |
  | `name-der.cer` | Certificate only, binary DER (Java, some Windows tools) |

  Optional formats, chosen in Settings (for every new certificate) or ticked in Export (any time):

  | File | Use |
  |---|---|
  | `name.p7b` | PKCS#7: certificate + chain, no key (Windows intermediates, Java keytool, Tomcat, F5, Citrix, Palo Alto) |
  | `cert.pem`, `privkey.pem`, `chain.pem`, `fullchain.pem` | Certbot-style names (Linux guides, Synology, Home Assistant, Proxmox, Docker) |
  | `name-encrypted.key` | Private key encrypted with the PFX password (FortiGate, Sophos, Cisco, Apache with passphrase) |
  | `name-fullchain-root.pem` | Full chain plus the ISRG root (devices that validate the whole chain) |
  | `name-combined-keyfirst.pem` | Private key, then full chain (Postfix, lighttpd, Pound) |
  | `name.p12` | Same as the PFX with a .p12 extension (Java/Tomcat, macOS, Android) |
  | `name.crt` | Certificate only, PEM, .crt extension |
  | `name-k8s-secret.yaml` | Kubernetes `kubernetes.io/tls` Secret |

  Each file row on the Certificates page also has a **B64** button that copies the file as one Base64 line, for Azure Key Vault, web panels or CI secrets.
- **Stored and ready to reuse.** Every certificate keeps its settings and full issuance history. **Renew** reuses the settings with one click. **Export** writes any formats to a folder and can set a new PFX password or none.
- **Install in Windows.** Adds the certificate to the Local Machine or Current User store for IIS, RDP or SQL Server.
- **Deployment to your servers.** Each certificate can have deployment targets. After every issuance or renewal the app pushes the new files automatically; there is also a **Deploy now** button.
  - **CertificateGet Agent**: a small Windows or Linux service ([CertificateGet.Agent](CertificateGet.Agent/README.md)). It writes the files into any number of folders with the names each app instance expects, restarts services or runs commands, imports into **TSplus**, and keeps backups. Suited to NetTalk, TSplus and HAProxy servers.
  - **SFTP / SSH**: uploads files atomically and runs a command such as `haproxy -c … && systemctl reload haproxy`. Nothing to install on the server; presets for HAProxy and nginx.
- **Activity log.** Every request, challenge, validation, export, install and error is recorded with search, level filter and CSV export.
- **Staging and Production.** Test against Let's Encrypt staging without hitting rate limits, then switch to Production.
- **Secrets protected.** PFX passwords, ACME account keys and the Cloudflare token are encrypted with Windows DPAPI for your user account.
- Popups are modal dialogs, not message boxes. The interface uses a slate, blue and teal theme.

## Requirements

- Windows 10/11 or Windows Server 2016+
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) to build, or the .NET 9 Desktop Runtime to run
- For HTTP validation, the domain must point at the server and port 80 must be reachable from the internet
- For wildcards, you need access to the domain's DNS

## Build and run

```powershell
dotnet run --project CertificateGet
```

Self-contained, compressed single-file builds in `.\run`. No .NET runtime is needed on the target machines.

```powershell
.\publish.ps1
# run\CertificateGet.exe                      desktop app
# run\agent-windows\CertificateGet.Agent.exe  agent for Windows servers
# run\agent-linux\CertificateGet.Agent        agent for Linux servers
```

Run it **as administrator** when you want to install into the Local Machine certificate store, or when you write into a web root that only admins can modify.

## Quick start

1. Go to **New certificate** and choose **Standard** or **Wildcard**.
2. Enter the domains and pick a validation method.
3. Keep **Staging** for a first test run. Choose a key type (RSA 2048 is the most compatible) and, if you want one, a PFX password.
4. Click **Request certificate** and follow the progress on the right. For manual DNS, a dialog lists the TXT records to create.
5. When it works, request again with **Production** to get a trusted certificate.
6. On **Certificates**, open the folder, export, install into Windows or renew. Certificates close to expiry are shown in amber or red.

### Cloudflare token

Create the token in Cloudflare under **My Profile → API Tokens → Create Token**, with **Zone → Zone → Read** and **Zone → DNS → Edit** for the zones you need. Paste it in **Settings** and click **Test token**.

### Hostinger token

In hPanel, open **Account** (profile icon) → **API** and create a token. Paste it in **Settings** and click **Test token**; it lists the domains the token can see. The domain must use Hostinger's name servers. The app adds its TXT values without touching other records, and removes only its own values afterwards.

### Constellix keys

In the Constellix portal, open **Edit My Account → API Keys** and create a key. Copy the **API key** and the **secret key**, which is shown when the key is created. Paste both in **Settings** and click **Test keys**; it lists the domains in the account. Requests are signed with the current time, so the PC clock must be correct. The app merges its values into an existing `_acme-challenge` record if there is one, and afterwards removes only its own values.

## Where things are stored

Default location: `%LOCALAPPDATA%\CertificateGet\Store`. You can change it in Settings.

```
Store\
  activity.jsonl                         activity log (JSON lines)
  accounts\staging.json, production.json ACME account keys (DPAPI encrypted)
  certificates\
    wildcard.example.com_<id>\
      profile.json                       domains, method, options, history
      2026-09-24_231200\                 one folder per issuance, with all 7 files
```

Private keys are stored here unencrypted in `.key` and `.pem` files, like any web server would keep them. Keep this folder out of cloud-synced locations and protect it.

## Project layout

```
CertificateGet/
  Models/Models.cs              profiles, issuances, settings, log entries
  Services/AcmeService.cs       ACME order workflow (Certes 4), all challenge types
  Services/ChallengeHelpers.cs  built-in HTTP-01 server, DNS checker, Cloudflare client, IDnsProvider
  Services/HostingerDns.cs      Hostinger DNS API client
  Services/ConstellixDns.cs     Constellix DNS API v4 client (HMAC-signed requests)
  Services/DeployService.cs     deployment to SFTP servers and CertificateGet agents
CertificateGet.Agent/           the server agent (ASP.NET Core minimal API, Windows service / systemd)
  Services/CertificateStore.cs  key/CSR generation, file writing (PFX via Pkcs12Builder), export, Windows store install
  Services/AppServices.cs       settings, DPAPI helpers, activity log
  Views/                        Certificates, New certificate, Activity log, Settings pages
  UI/                           modal dialog, DNS records dialog, export dialog, converters
  Themes/Theme.xaml             colours and control styles
```

## Notes

- Let's Encrypt certificates are valid for up to 90 days, and Let's Encrypt is moving to shorter lifetimes. Renew when the app flags a certificate as expiring. Let's Encrypt no longer sends expiry emails.
- Production rate limits include 5 failed validations per hour per account and hostname, and 50 certificates per registered domain per week. Test with Staging.
- PFX encryption defaults to 3DES/SHA-1 so it imports on older Windows Server and appliances. Switch to AES-256 in Settings if all your targets support it.
