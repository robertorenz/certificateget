# CertificateGet

A Windows desktop app (C# / WPF, .NET 9) that gets free TLS certificates from **Let's Encrypt** or **ZeroSSL**, keeps every certificate in every common file format, and logs everything you do.

![Platform](https://img.shields.io/badge/platform-Windows-0F172A) ![.NET](https://img.shields.io/badge/.NET-9-2563EB) ![ACME](https://img.shields.io/badge/ACME-Let's%20Encrypt-0D9488) ![License](https://img.shields.io/badge/license-MIT-15803D)

## Features

- **Standard and wildcard certificates.** Wildcards (`*.example.com`) can also cover the bare domain.
- **Nine ways to validate:**
  | Method | When to use it |
  |---|---|
  | HTTP, built-in web server | Run the app on the machine the domain points to; it briefly answers on port 80. No IIS needed. |
  | HTTP, web root folder | IIS, Apache or nginx already serves the site; the app writes the file into `.well-known/acme-challenge` (and a `web.config` for IIS). |
  | DNS, manual | Works anywhere and is required for wildcards. The app shows the TXT records with copy buttons and checks public DNS before validating. |
  | DNS, Cloudflare | Creates and removes the TXT records for you through the Cloudflare API. |
| DNS, Hostinger | Creates and removes the TXT records for you through the Hostinger API. |
| DNS, Constellix | Creates and removes the TXT records for you through the Constellix API (v4, API key + secret key). |
| DNS, acme-dns | Works with **any** DNS host. Once per domain you create a CNAME for `_acme-challenge`; after that the app only talks to the acme-dns server and never needs your DNS credentials. |
| DNS, DNS Made Easy | Creates and removes the TXT records for you through the DNS Made Easy API (v2.0, API key + secret key). |
| DNS, Namecheap | Creates and removes the TXT records for you through the Namecheap API. Every other record is written back unchanged. |
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
  | `name-fullchain-root.pem` | Full chain plus the root (devices that validate the whole chain) |
  | `name-combined-keyfirst.pem` | Private key, then full chain (Postfix, lighttpd, Pound) |
  | `name.p12` | Same as the PFX with a .p12 extension (Java/Tomcat, macOS, Android) |
  | `name.crt` | Certificate only, PEM, .crt extension |
  | `name-k8s-secret.yaml` | Kubernetes `kubernetes.io/tls` Secret |
  | `cert.jks` | Java KeyStore with key + full chain; password from Settings (default `secret`, which TSplus requires) |

  Each file row on the Certificates page also has a **B64** button that copies the file as one Base64 line, for Azure Key Vault, web panels or CI secrets.
- **Stored and ready to reuse.** Every certificate keeps its settings and full issuance history. **Renew** reuses the settings with one click. **Export** writes any formats to a folder and can set a new PFX password or none.
- **Install in Windows.** Adds the certificate to the Local Machine or Current User store for IIS, RDP or SQL Server.
- **Deployment to your servers.** Each certificate can have deployment targets. After every issuance or renewal the app pushes the new files automatically; there is also a **Deploy now** button.
  - **CertificateGet Agent**: a small Windows or Linux service ([CertificateGet.Agent](CertificateGet.Agent/README.md)). It writes the files into any number of folders with the names each app instance expects, restarts services or runs commands, imports into **TSplus**, and keeps backups. Suited to NetTalk, TSplus and HAProxy servers. See the [manual](#manual) for install steps and `agent.json` templates for NetTalk, HAProxy, Cockpit and TSplus.
  - **SFTP / SSH**: uploads files atomically and runs a command such as `haproxy -c … && systemctl reload haproxy`. Nothing to install on the server; presets for HAProxy and nginx.
- **Activity log.** Every request, challenge, validation, export, install and error is recorded with search, level filter and CSV export.
- **Staging and Production.** Test against Let's Encrypt staging without hitting rate limits, then switch to Production.
- **Let's Encrypt or ZeroSSL.** Choose the certificate authority per certificate; see [Certificate authorities](#certificate-authorities).
- **Secrets protected.** PFX passwords, ACME account keys, DNS API tokens and keys, and acme-dns passwords are encrypted with Windows DPAPI for your user account.
- **Help built in.** **Help** in the sidebar (or F1) opens the four-volume manual (the app and the agent) in your browser, in English or Spanish, from a copy inside the exe, so it works offline.
- Popups are modal dialogs, not message boxes. The interface uses a slate, blue and teal theme.

## Requirements

- Windows 10/11 or Windows Server 2016+
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) to build, or the .NET 9 Desktop Runtime to run
- For HTTP validation, the domain must point at the server and port 80 must be reachable from the internet
- For wildcards, you need access to the domain's DNS

## Download

Ready-built, self-contained executables (no .NET install needed) are on the [Releases page](https://github.com/robertorenz/certificateget/releases/latest): `CertificateGet.exe`, the Windows agent (`CertificateGet.Agent-win-x64.zip`) and the Linux agent (`CertificateGet.Agent-linux-x64.tar.gz`).

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

## Certificate authorities

Each certificate is requested from **Let's Encrypt** or **ZeroSSL**, chosen under **Certificate authority** on **New certificate**. The default for new certificates is in **Settings**.

| | Let's Encrypt | ZeroSSL |
|---|---|---|
| Test server | **Staging** (untrusted certificates, generous limits) | None: every request is a trusted certificate |
| Account | Created automatically; e-mail optional | Must be linked to a ZeroSSL account (External Account Binding) |
| Rate limits | Yes (e.g. 5 failed validations per hour, 50 certificates per registered domain per week) | No ACME rate limits |
| Time to issue | Seconds | Usually seconds, sometimes minutes; the app waits up to 10 |
| Chain | ISRG roots | Sectigo (USERTrust) roots |

Both issue free, trusted 90-day certificates, including wildcards. Use Let's Encrypt to test a new setup on Staging first; use ZeroSSL after hitting a Let's Encrypt rate limit, to see your certificates in a ZeroSSL dashboard, or as a second authority to fall back on.

### Setting up ZeroSSL

The first ZeroSSL request creates the ACME account and links it to a ZeroSSL account, in one of two ways:

- **With a ZeroSSL API key** (recommended): in the ZeroSSL dashboard open **Developer**, copy the **API Access Key** and paste it into **Settings → ACME accounts → ZeroSSL API key**. The certificates then appear in your ZeroSSL dashboard.
- **With the contact e-mail only**: leave the key empty and enter a contact e-mail. ZeroSSL creates an account for that address if it has none.

After that only the ACME account key (`accounts\zerossl.json`) is used. To move to another ZeroSSL account, change the key or e-mail and click **Reset accounts** in Settings (this resets the Let's Encrypt accounts too; they are recreated on the next request).

To move an existing certificate to the other authority, click **Renew**, change **Certificate authority** and request. The history, deployment targets and agents are unaffected; only `fullchain-root` ends in a different root.

### Cloudflare token

Create the token in Cloudflare under **My Profile → API Tokens → Create Token**, with **Zone → Zone → Read** and **Zone → DNS → Edit** for the zones you need. Paste it in **Settings** and click **Test token**.

### Hostinger token

In hPanel, open **Account** (profile icon) → **API** and create a token. Paste it in **Settings** and click **Test token**; it lists the domains the token can see. The domain must use Hostinger's name servers. The app adds its TXT values without touching other records, and removes only its own values afterwards.

### Constellix keys

In the Constellix portal, open **Edit My Account → API Keys** and create a key. Copy the **API key** and the **secret key**, which is shown when the key is created. Paste both in **Settings** and click **Test keys**; it lists the domains in the account. Requests are signed with the current time, so the PC clock must be correct. The app merges its values into an existing `_acme-challenge` record if there is one, and afterwards removes only its own values.

## Manual

CertificateGet has a four-volume manual covering the app and the agent, in English and Spanish, generated by `python docs/build-docs.py`. Each volume covers the app first, then the agent. The reference volume is read out of the sources: file formats, settings, validation methods and SFTP file types from the app, every `agent.json` field, command and endpoint from the agent. English goes to [`docs/`](docs) and Spanish to [`docs/es/`](docs/es). The app's chapters are in `docs/build-docs-app.py` (and `build-docs-app-es.py`), the agent's Spanish text in `docs/build-docs-es.py`. The build fails if a Spanish volume's headings drift from the English one, or if a format, setting, method or field has no description. The build also writes `docs/help/` and `docs/help/es/`, copies with relative links that are embedded in the app and opened by **Help** / F1 (with an English / Español switch); run the generator before building the app after changing the docs. Every page has a language switch to the same volume in the other language.

1. [Getting Started](https://claude.ai/artifact/6NUtSWhxsJHAgFg1bsgEjH): install the app, first certificate, using it; install the agent on Windows and Linux, connect it
2. [Programmer's Guide](https://claude.ai/artifact/Xk9wRJUG5rPquoFTwbCrFe): validation methods and DNS providers, Let's Encrypt and ZeroSSL, renewals, SFTP and agent deployment, storage and secrets; how an agent deployment runs, security, troubleshooting
3. [Template Guide](https://claude.ai/artifact/4NaQZxJRzNB1EgG6FPq4i7): recipes (IIS, wildcard with Cloudflare, HAProxy and nginx over SFTP); ready-made `agent.json` for NetTalk, HAProxy, Cockpit and TSplus
4. [Reference](https://claude.ai/artifact/WfYNLoBapj4dsyLWJMPU8s): every file format, validation method and setting of the app; every `agent.json` field, source, command and endpoint

En español:

1. [Primeros pasos](https://claude.ai/artifact/5G22xWR3cTTfuWabzd1den): instalar la aplicación, primer certificado; instalar el agente en Windows y Linux, conectarlo
2. [Guía del programador](https://claude.ai/artifact/1Md1dk2STxaDExvMtmxifP): métodos de validación, Let's Encrypt y ZeroSSL, renovaciones, despliegue, almacén; cómo funciona el agente
3. [Guía de plantillas](https://claude.ai/artifact/P99MLSHwS4nKgr7gTRCAKU): recetas en la aplicación; `agent.json` listos para NetTalk, HAProxy, Cockpit y TSplus
4. [Referencia](https://claude.ai/artifact/LV7XNKYtxXtAna9Xkw2CAJ): cada formato y ajuste de la aplicación; cada campo de `agent.json`, comando y endpoint

### acme-dns

Use this when your DNS host has no API, or when you do not want DNS credentials on this PC. [acme-dns](https://github.com/joohoi/acme-dns) is a tiny DNS server that only answers `_acme-challenge` TXT queries.

1. In **Settings → acme-dns server**, keep `https://auth.acme-dns.io` (the public server, fine for testing) or enter your own server. **Test server** checks that it answers.
2. Request a certificate with **DNS — acme-dns**. On the first request for a domain, the app registers it and shows the one CNAME to create, for example `_acme-challenge.example.com CNAME 8e5700ea-a4bf-41c7-8a77-e990661dcc6a.auth.acme-dns.io`. One CNAME covers both `example.com` and `*.example.com`. Remove any existing `_acme-challenge` TXT record with that name first.
3. Create the CNAME at your DNS host, click **Check DNS now** until it shows as visible, then **Continue validation**.

From then on, renewals need no DNS changes. The registrations and their CNAME targets are listed in Settings, with **Copy target** and **Remove**. For production, run your own acme-dns server: anyone holding a registration's credentials can get certificates for that domain.

### DNS Made Easy keys

In DNS Made Easy, open **Config → Account Information** and copy the **API key** and **secret key**. Paste both in **Settings** and click **Test keys**; it lists the managed domains. Requests are signed with the current time, so the PC clock must be correct. Each TXT value is its own record, and afterwards the app deletes only the records it created.

### Namecheap API access

1. In Namecheap, open **Profile → Tools → API Access** and turn it on. Namecheap only allows this for accounts that meet its criteria (for example a minimum number of domains or account balance).
2. Whitelist this PC's **public IPv4** address there.
3. In **Settings**, enter the **API user** (your Namecheap user name) and the **API key**. Leave **Client IP** empty to detect it automatically (via api.ipify.org), or enter the whitelisted address. Click **Test**; it lists your domains and shows the IP used.

The domain must use Namecheap BasicDNS or PremiumDNS. Namecheap's API can only replace a domain's whole record list, so for every change the app reads all records, adds or removes only its own `_acme-challenge` values, and writes every other record back exactly as it was, keeping the mail (MX) setting.

## Where things are stored

Default location: `%LOCALAPPDATA%\CertificateGet\Store`. You can change it in Settings.

```
Store\
  activity.jsonl                         activity log (JSON lines)
  accounts\staging.json, production.json Let's Encrypt ACME account keys (DPAPI encrypted)
  accounts\zerossl.json                  ZeroSSL ACME account key (DPAPI encrypted)
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
  Services/DnsMadeEasyDns.cs    DNS Made Easy API v2.0 client (HMAC-signed requests)
  Services/NamecheapDns.cs      Namecheap XML API client (read, merge and write back the host list)
  Services/AcmeDns.cs           acme-dns client: registration, TXT updates, CNAME delegation
  Services/HelpDocs.cs          extracts the embedded manual and opens it (Help / F1)
  Services/ZeroSsl.cs           ZeroSSL ACME directory and EAB credentials (API key or e-mail)
  Services/DeployService.cs     deployment to SFTP servers and CertificateGet agents
  Services/CertificateStore.cs  key/CSR generation, file writing (PFX via Pkcs12Builder), export, Windows store install
  Services/AppServices.cs       settings, DPAPI helpers, activity log
  Views/                        Certificates, New certificate, Activity log, Settings pages
  UI/                           modal dialog, DNS records dialog, export dialog, converters
  Themes/Theme.xaml             colours and control styles
CertificateGet.Agent/           the server agent (ASP.NET Core minimal API, Windows service / systemd)
  AgentConfig.cs                agent.json model, API key hash, TLS certificate
  Deployer.cs                   writes files, backups, TSplus import, service restarts, commands
  ProgramRestarter.cs           restarts desktop programs in the user's session (Windows)
  examples/                     agent.json for Windows (NetTalk + TSplus) and Linux (HAProxy + Cockpit)
docs/                           the manual (four HTML volumes, Spanish in docs/es) and the build-docs*.py scripts that generate them
  help/                         the same volumes with relative links, embedded in the app for Help / F1
```

## Notes

- ZeroSSL can take a few minutes to issue after validation; the app waits up to ten minutes.
- Let's Encrypt certificates are valid for up to 90 days, and Let's Encrypt is moving to shorter lifetimes. Renew when the app flags a certificate as expiring. Let's Encrypt no longer sends expiry emails.
- Production rate limits include 5 failed validations per hour per account and hostname, and 50 certificates per registered domain per week. Test with Staging.
- PFX encryption defaults to 3DES/SHA-1 so it imports on older Windows Server and appliances. Switch to AES-256 in Settings if all your targets support it.

## License

CertificateGet is released under the [MIT License](LICENSE). Copyright (c) 2026 Roberto Renz.
