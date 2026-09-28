# CertificateGet Agent

A small service for your servers (Windows or Linux). The CertificateGet app sends it each renewed certificate, and the agent:

1. writes the files you configured, with the names you choose, into **one or more folders** (one per app instance),
2. can import the certificate into **TSplus** (15 or later),
3. **restarts** the Windows services or systemd units that use it, or runs commands (for example `systemctl reload haproxy`),
4. keeps **backups** of the files it replaced, in its own folder, never next to the live files,
5. reports every step back to the app's activity log.

The full manual (install walkthrough, `agent.json` templates for NetTalk, HAProxy, Cockpit and TSplus, and a reference generated from the sources) is in [`docs/`](../docs) and linked from the [main README](../README.md#agent-manual).

## Install

**Windows** (PowerShell as administrator). Copy `CertificateGet.Agent.exe` to a folder such as `C:\Program Files\CertificateGet Agent\`, then run:

```powershell
cd "C:\Program Files\CertificateGet Agent"
.\CertificateGet.Agent.exe install          # optional: install 9443
```

**Linux**:

```bash
sudo mkdir -p /opt/certificateget-agent && sudo cp CertificateGet.Agent /opt/certificateget-agent/
cd /opt/certificateget-agent && sudo chmod +x CertificateGet.Agent
sudo ./CertificateGet.Agent install
sudo ufw allow 9443/tcp                                                          # Ubuntu/Debian with ufw
sudo firewall-cmd --permanent --add-port=9443/tcp && sudo firewall-cmd --reload  # RHEL/Rocky/Alma/Fedora
```

On SELinux systems (RHEL/Rocky/Alma/Fedora), `install` labels the program `bin_t`. Without that, a file moved from `/tmp` fails to start with `status=203/EXEC`. When updating, copy the new file with `cp` rather than `mv`, or run `sudo restorecon -v /opt/certificateget-agent/CertificateGet.Agent`.

`install` creates `agent.json`, a random **API key** (shown once), and a TLS certificate. It registers the service to start automatically (Windows service `CertificateGetAgent`, systemd unit `certificateget-agent`) and, on Windows, opens the firewall port.

The output shows the **URL**, **API key** and **fingerprint** to enter in the app.

## Configure

Edit `agent.json` next to the executable. Changes apply to the next deployment without a restart, except `Port`, which needs a service restart. Run `check` to validate the file.

A **slot** is what a certificate in the app points to. Each slot has **destinations**:

| Field | Meaning |
|---|---|
| `Name` | Label shown in the app's log |
| `Kind` | `Files` (default), `TSplus` (TSplus 15+, PFX import) or `TSplusJks` (TSplus versions that use `cert.jks`) |
| `Folder` | Where the files go; created if it does not exist |
| `Files` | List of `{ "Source": "...", "FileName": "..." }` |
| `RestartServices` | Windows service names, or systemd units on Linux, restarted after writing. Each is restarted once, even when several destinations list it. |
| `RestartPrograms` | Windows only: desktop programs (`.exe`) to close and start again. See below. |
| `Commands` | Run after writing (`cmd.exe` on Windows, `/bin/sh` on Linux). `{folder}` is replaced with the destination folder. A non-zero exit code fails the step. |
| `TsplusCertFolder` | `TSplus` only. Defaults to `C:\Program Files (x86)\TSplus\UserDesktop\files\cert` |
| `TsplusFolder` | `TSplusJks` only. TSplus install folder, default `C:\Program Files (x86)\TSplus` |

The slot's own `Commands` run once, after all destinations.

**Sources:**

| Source | What it is |
|---|---|
| `fullchain` | Certificate + intermediates |
| `key` | Private key |
| `combined` | Full chain + key (HAProxy) |
| `combined-keyfirst` | Key + full chain |
| `cer`, `crt` | Certificate only (PEM) |
| `der` | Certificate only (binary DER) |
| `chain` | Intermediates only |
| `fullchain-root` | Full chain + root |
| `pfx`, `p12` | PKCS#12, using the certificate's PFX password from the app |
| `encrypted-key` | Key encrypted with the PFX password |
| `p7b` | PKCS#7 |
| `k8s` | Kubernetes TLS secret |
| `jks` | Java KeyStore (`cert.jks`), password from the app's Settings (default `secret`) |

See [`examples/agent.windows.json`](examples/agent.windows.json) and [`examples/agent.linux-haproxy.json`](examples/agent.linux-haproxy.json).

### NetTalk

NetTalk loads its certificate at start-up. Point a destination at each instance's certificate folder, write `fullchain` and `key` with the file names that instance is configured with, and list the instance's Windows service in `RestartServices`. If an instance runs as a desktop app rather than a service, use a `Commands` entry to restart it.

### Programs that are not services (Windows)

If an app runs as a normal program instead of a Windows service, add it under `RestartPrograms`:

```json
"RestartPrograms": [
  { "Path": "C:\\Apps\\Api\\ApiServer.exe" }
]
```

For each running copy of that exe, the agent:

1. notes its exact command line and the user session (desktop) it is running in,
2. asks it to close normally, like clicking its close button,
3. ends it forcefully if it is still running after `StopTimeoutSeconds` (default 20),
4. starts it again **on the same desktop, as the same user, with the same command line**,
5. checks that it is still running 3 seconds later.

Several copies of the same exe with different parameters each come back with their own parameters. Each program is restarted only once, even when several destinations list it.

| Field | Default | Meaning |
|---|---|---|
| `Path` | (required) | Full path of the `.exe` |
| `Arguments` | `""` | Used only when the program was **not** running, so there is no command line to reuse |
| `WorkingFolder` | exe folder | Start-in folder for the new process |
| `StopTimeoutSeconds` | `20` | How long to wait for a normal close |
| `StartIn` | `SameSession` | `SameSession`: the desktop it was running on, or the console desktop if it was not running. `Console`: always the console desktop. `Background`: session 0 as the agent's account, with no visible window. |

A user must be logged on, locally or through a disconnected RDP session, for the program to be started on a desktop. On servers that run such apps unattended, set up automatic logon, or use `Background` if the app does not need a visible window.

### TSplus

Add a destination with `"Kind": "TSplus"`. The agent then:

1. writes the PFX and a temporary `certpassword.txt`,
2. runs TSplus's `CertificateManager.exe /add <pfx>`,
3. deletes both files.

This requires TSplus 15 or later.

For TSplus versions that use **`cert.jks`**, add `{ "Name": "TSplus", "Kind": "TSplusJks" }` instead. The agent then:

1. backs up the current `C:\Program Files (x86)\TSplus\Clients\webserver\cert.jks`,
2. writes the new `cert.jks` (JKS keystore, password `secret`, full chain),
3. runs `UserDesktop\files\AdminTool.exe /webrestart` to restart the TSplus web server.

Set `TsplusFolder` if TSplus is installed somewhere else. If the certificate has no PFX password in the app, the agent wraps the PFX with a random one, because TSplus needs a password.

### HAProxy

On the HAProxy machine, write `combined` into the `crt` directory, then run `haproxy -c -f /etc/haproxy/haproxy.cfg && systemctl reload haproxy` as the destination command. The config is checked first, so a bad file never takes HAProxy down.

### Cockpit

Cockpit uses the **last `.cert` or `.crt` file in alphabetical order** in `/etc/cockpit/ws-certs.d`, with the key in a `.key` file of the same name. If you already have a certificate file there, such as `reddinassessments.cert`, overwrite it with `combined`. A `.cert` file holds the chain and the key together. Otherwise, write `fullchain` as `90-letsencrypt.crt` and `key` as `90-letsencrypt.key`; they sort after Cockpit's own `0-self-signed.cert`.

The key must be readable by the `cockpit-ws` group (`root:cockpit-ws`, mode 640), because `cockpit-tls` runs as that user. Use this command so the certificate always loads and a failed start can never leave Cockpit down:

```
chgrp cockpit-ws {folder}/reddinassessments.cert && chmod 640 {folder}/reddinassessments.cert && restorecon -F {folder}/reddinassessments.cert ; systemctl reset-failed cockpit.socket cockpit ; systemctl restart cockpit.socket ; systemctl try-restart cockpit
```

Check which certificate Cockpit uses with `sudo /usr/lib/cockpit/cockpit-certificate-ensure --check` (Debian/Ubuntu) or `sudo /usr/libexec/cockpit-certificate-ensure --check` (RHEL/Fedora).

See [`examples/agent.linux-haproxy.json`](examples/agent.linux-haproxy.json) for HAProxy and Cockpit together.

## Security

- **TLS only.** The app pins the agent's certificate fingerprint on first connect, after you confirm it matches `info`, and refuses a different certificate later.
- **API key** is sent in the `X-Api-Key` header. The agent stores only its SHA-256 hash, and failed attempts are logged and slowed down.
- **`AllowedIps`** restricts which machines may connect. Loopback is always allowed.
- **Private-key files** are written with mode 600 on Linux when they are new. When the agent replaces an existing file, it keeps that file's owner, group, mode and SELinux label. For example, Cockpit's `root:cockpit-ws 640` stays as it is.
- **Protect the agent folder.** It contains `agent.json`, the agent's TLS key and the backups. Keep it readable by administrators or root only.

## Commands

```
CertificateGet.Agent install [port]   create config/key/TLS cert, register and start the service
CertificateGet.Agent uninstall        remove the service (keeps agent.json and backups)
CertificateGet.Agent newkey           new API key (update it in the app)
CertificateGet.Agent info             URL, fingerprint, slots
CertificateGet.Agent check            validate agent.json
CertificateGet.Agent run              run in the console (for testing)
```

The log is `agent.log` next to the executable.
