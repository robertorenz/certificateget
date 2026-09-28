# Builds the four volumes of the CertificateGet manual, the app first and the agent after it:
#
#   getting-started.html    install the app, first certificate; install the agent, connect the app
#   programmers-guide.html  validation, certificate authorities, deployment, storage; how the agent works
#   template-guide.html     recipes in the app; ready-made agent.json for NetTalk, HAProxy, Cockpit, TSplus
#   reference.html          file formats, settings, methods; every agent.json field, command and endpoint
#
# The reference volume is READ OUT OF THE SOURCES - AgentConfig.cs, ProgramRestarter.cs,
# Program.cs and Deployer.cs for the agent, CertificateStore.cs, Models.cs and DeployService.cs for
# the app - and the combined examples are read out of CertificateGet.Agent/examples, so a field,
# default or command here is the one in the build.  The app's chapters are in build-docs-app.py.
# Run from the repository root after changing the app or the agent:   python docs/build-docs.py
#
# Every volume is built twice: in English into docs/ and in Spanish into docs/es/. The Spanish
# text lives in docs/build-docs-es.py, which is loaded into this namespace and uses the same
# helpers; the build fails when a Spanish volume's headings differ from the English one's, or
# when a field, source, kind, command or endpoint has no Spanish description.
import io, re, html, sys, json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AGENT = os.path.join(ROOT, 'CertificateGet.Agent')

def src(name):
    return io.open(os.path.join(AGENT, name), encoding='utf-8-sig').read().replace('\r\n', '\n')

# ---------------------------------------------------------------- extract
def _extract_classes(text):
    """public class X { /// <summary>..</summary> public T Name { get; set; } = default; }"""
    out, cls, pend, in_sum = {}, None, [], False
    for raw in text.split('\n'):
        t = raw.strip()
        m = re.match(r'public (?:static )?class (\w+)', t)
        if m:
            cls = {'name': m.group(1), 'doc': ' '.join(pend).strip(), 'props': []}
            out[cls['name']] = cls; pend = []; continue
        if t.startswith('///'):
            s = t[3:].strip()
            s = re.sub(r'</?summary>', '', s).strip()
            s = s.replace('&lt;', '<').replace('&gt;', '>')
            if s: pend.append(s)
            continue
        m = re.match(r'public ([\w<>?, ]+?) (\w+) \{ get; set; \}(?: = (.+?);)?$', t)
        if m and cls is not None:
            cls['props'].append({'type': m.group(1), 'name': m.group(2),
                                 'default': (m.group(3) or '').strip(), 'doc': ' '.join(pend).strip()})
            pend = []; continue
        if t and not t.startswith('['):
            pend = []
    return out

CFG_CS   = src('AgentConfig.cs')
PROG_CS  = src('Program.cs')
DEPL_CS  = src('Deployer.cs')
REST_CS  = src('ProgramRestarter.cs')

CLASSES = {}
CLASSES.update(_extract_classes(CFG_CS))
CLASSES.update(_extract_classes(REST_CS))
CLASSES.update(_extract_classes(DEPL_CS))

def _set(text, name):
    m = re.search(name + r'\s*=\s*new(?:\([^)]*\))?\s*\{([^}]*)\}', text)
    return re.findall(r'"([\w-]+)"', m.group(1))

SOURCES   = _set(PROG_CS, 'KnownSources')
SECRETS   = set(_set(DEPL_CS, 'SecretAliases'))
KINDS     = ['Files'] + sorted(set(re.findall(r'Kind\.Equals\("(\w+)"', DEPL_CS)), key=lambda k: (len(k), k))
STARTINS  = re.search(r'is not \(([^)]*)\)', PROG_CS).group(1)
STARTINS  = [s for s in re.findall(r'"(\w+)"', STARTINS)]
CONSTS    = dict(re.findall(r'public const string (\w+) = @?"([^"]*)"', DEPL_CS))
SERVICE   = re.search(r'ServiceName = "(\w+)"', PROG_CS).group(1)
UNIT      = re.search(r'/etc/systemd/system/([\w-]+)\.service', PROG_CS).group(1)
FIREWALL  = re.search(r'rule name=\\"([^\\]+)\\"', PROG_CS).group(1)
BODYMB    = int(re.search(r'MaxRequestBodySize = (\d+) \* 1024', PROG_CS).group(1))

def _cli():
    block = re.search(r'Console\.WriteLine\("""(.*?)"""\);', PROG_CS, re.S).group(1)
    out = []
    for ln in block.split('\n'):
        m = re.match(r'\s{2,}(\w+)( \[\w+\])?\s{2,}(.+)$', ln)
        if m: out.append({'name': m.group(1), 'arg': (m.group(2) or '').strip(), 'doc': m.group(3).strip()})
    return out

def _endpoints():
    return [{'verb': m.group(1).upper(), 'path': m.group(2)}
            for m in re.finditer(r'app\.Map(Get|Post)\("([^"]+)"', PROG_CS)]

CLI       = _cli()
ENDPOINTS = _endpoints()
EX_WIN    = src(os.path.join('examples', 'agent.windows.json'))
EX_LINUX  = src(os.path.join('examples', 'agent.linux-haproxy.json'))
json.loads(EX_WIN); json.loads(EX_LINUX)          # the shipped examples must at least parse

MISSING, PROBLEMS = [], []
LANG = 'en'                     # set by the build loop; en -> docs/, es -> docs/es/

# ---------------------------------------------------------------- hand-kept words, checked against the sources
#  Descriptions for fields that carry no /// summary in the source.  A field
#  with neither is reported, so a new property cannot slip in undocumented.
FIELD_DOC = {
 'AgentConfig.Port':        'TCP port the agent listens on (HTTPS). Changing it needs a service restart, and the firewall rule opened by <code>install</code> only covers the port given at install time.',
 'AgentConfig.TlsPfx':      'File name of the agent\'s own TLS certificate, next to the executable. Created on first start.',
 'AgentConfig.TlsPassword': 'Password of <code>TlsPfx</code>. Written by the agent; leave it alone. If it does not open the PFX (for example after copying the example file over <code>agent.json</code>), the agent makes a new TLS certificate and the app asks you to trust the new fingerprint.',
 'AgentConfig.KeepBackups': 'How many earlier versions of each destination\'s files to keep in <code>backups/</code>. <code>0</code> turns backups off.',
 'AgentConfig.Slots':       'The slots this agent offers. An app deployment target names exactly one of them.',
 'Slot.Name':               'The name the app\'s deployment target uses. Matched without regard to case; must be unique in the file.',
 'Slot.Description':        'Free text shown in the app when you click <b>Connect &amp; list slots</b>.',
 'Slot.Destinations':       'Where this certificate goes on this server, processed in order.',
 'Destination.Name':        'Label used in the app\'s activity log, in <code>agent.log</code> and as the backup folder name. Falls back to <code>Folder</code> when empty.',
 'Destination.Folder':      'Target folder for <code>Files</code>. Created if it does not exist. Replaces <code>{folder}</code> in this destination\'s <code>Commands</code>.',
 'Destination.Files':       'The files to write, each a source and the file name to give it.',
 'FileSpec.FileName':       'Name of the file in <code>Folder</code>. An existing file is backed up, then replaced atomically.',
 'DeployRequest.Slot':        'Slot to deploy to.',
 'DeployRequest.Certificate': 'Name, domains, thumbprint and expiry, used for the log.',
 'DeployRequest.PfxPassword': 'The certificate\'s PFX password from the app. Sent only when the slot needs <code>pfx</code>, <code>p12</code> or <code>encrypted-key</code>.',
 'DeployRequest.Files':       'Source name &rarr; file contents in Base64. Exactly the sources the slot asked for.',
 'CertInfo.Name':        'Display name of the certificate in the app.',
 'CertInfo.Domains':     'Every domain on the certificate.',
 'CertInfo.Thumbprint':  'SHA-1 thumbprint of the issued certificate.',
 'CertInfo.NotAfter':    'Expiry date.',
}

#  A worked line of agent.json for every field.  The build prints any field without one.
USAGE = {
 'AgentConfig.Port':        '"Port": 9443',
 'AgentConfig.ApiKeyHash':  '"ApiKeyHash": "9F2C…64 hex characters…A1"      // written by install / newkey',
 'AgentConfig.AllowedIps':  '"AllowedIps": [ "192.168.1.50", "10.0.0.12" ]',
 'AgentConfig.TlsPfx':      '"TlsPfx": "agent-tls.pfx"',
 'AgentConfig.TlsPassword': '"TlsPassword": "(created automatically)"',
 'AgentConfig.KeepBackups': '"KeepBackups": 5',
 'AgentConfig.Slots':       '"Slots": [ { "Name": "example-com", "Destinations": [ … ] } ]',
 'Slot.Name':               '"Name": "reddin-wildcard"',
 'Slot.Description':        '"Description": "*.example.com for NetTalk and TSplus"',
 'Slot.Destinations':       '"Destinations": [ { "Name": "Web app", "Folder": "C:\\\\Apps\\\\Web\\\\certificates", "Files": [ … ] } ]',
 'Slot.Commands':           '"Commands": [ "systemctl reload nginx" ]',
 'Destination.Name':        '"Name": "NetTalk - Portal"',
 'Destination.Kind':        '"Kind": "TSplus"',
 'Destination.TsplusCertFolder': '"TsplusCertFolder": "D:\\\\TSplus\\\\UserDesktop\\\\files\\\\cert"',
 'Destination.TsplusFolder':     '"TsplusFolder": "D:\\\\TSplus"',
 'Destination.Folder':      '"Folder": "C:\\\\Apps\\\\Portal\\\\certificates"',
 'Destination.Files':       '"Files": [ { "Source": "fullchain", "FileName": "portal.crt" }, { "Source": "key", "FileName": "portal.key" } ]',
 'Destination.RestartServices': '"RestartServices": [ "PortalWeb" ]',
 'Destination.RestartPrograms': '"RestartPrograms": [ { "Path": "C:\\\\Apps\\\\Api\\\\ApiServer.exe" } ]',
 'Destination.Commands':    '"Commands": [ "haproxy -c -f /etc/haproxy/haproxy.cfg && systemctl reload haproxy" ]',
 'FileSpec.Source':         '{ "Source": "combined", "FileName": "example.com.pem" }',
 'FileSpec.FileName':       '{ "Source": "key", "FileName": "www.example.com.key" }',
 'ProgramSpec.Path':        '{ "Path": "C:\\\\Apps\\\\Api\\\\ApiServer.exe" }',
 'ProgramSpec.Arguments':   '{ "Path": "C:\\\\Apps\\\\Api\\\\ApiServer.exe", "Arguments": "/port=8443" }',
 'ProgramSpec.WorkingFolder':     '{ "Path": "C:\\\\Apps\\\\Api\\\\ApiServer.exe", "WorkingFolder": "C:\\\\Apps\\\\Api\\\\data" }',
 'ProgramSpec.StopTimeoutSeconds':'{ "Path": "C:\\\\Apps\\\\Api\\\\ApiServer.exe", "StopTimeoutSeconds": 45 }',
 'ProgramSpec.StartIn':     '{ "Path": "C:\\\\Apps\\\\Api\\\\ApiServer.exe", "StartIn": "Background" }',
 'DeployRequest.Slot':        '"slot": "reddin-wildcard"',
 'DeployRequest.Certificate': '"certificate": { "name": "reddin wildcard", "domains": [ "*.reddinassessments.com" ], … }',
 'DeployRequest.PfxPassword': '"pfxPassword": "only-when-a-pfx-is-needed"',
 'DeployRequest.Files':       '"files": { "fullchain": "LS0tLS1CRUdJTi…", "key": "LS0tLS1CRUdJTi…" }',
 'CertInfo.Name':        '"name": "reddin wildcard"',
 'CertInfo.Domains':     '"domains": [ "*.reddinassessments.com", "reddinassessments.com" ]',
 'CertInfo.Thumbprint':  '"thumbprint": "3A7F…"',
 'CertInfo.NotAfter':    '"notAfter": "2026-12-26T10:14:07Z"',
}

SOURCE_DOC = {
 'fullchain':         ('PEM', 'Certificate followed by the intermediates. What nginx, Apache 2.4.8+, NetTalk and most servers want as the certificate file.'),
 'key':               ('PEM, secret', 'Private key (PKCS#8 or traditional, as chosen in the app).'),
 'combined':          ('PEM, secret', 'Full chain and private key in one file. HAProxy, Cockpit <code>.cert</code>, Webmin, appliances.'),
 'combined-keyfirst': ('PEM, secret', 'Private key first, then the full chain. Postfix, lighttpd, Pound.'),
 'cer':               ('PEM', 'Certificate only.'),
 'crt':               ('PEM', 'Certificate only, the same bytes as <code>cer</code>.'),
 'der':               ('binary', 'Certificate only, DER encoded. Java and some Windows tools.'),
 'chain':             ('PEM', 'Intermediates only. Apache <code>SSLCertificateChainFile</code>.'),
 'fullchain-root':    ('PEM', 'Full chain plus the root, for devices that validate the whole chain.'),
 'encrypted-key':     ('PEM, secret', 'Private key encrypted with the certificate\'s PFX password.'),
 'pfx':               ('PKCS#12, secret', 'Certificate, chain and key, protected with the certificate\'s PFX password. IIS, Exchange, Windows.'),
 'p12':               ('PKCS#12, secret', 'The same as <code>pfx</code> with a <code>.p12</code> extension. Java/Tomcat, macOS.'),
 'p7b':               ('PKCS#7', 'Certificate and chain, no key.'),
 'k8s':               ('YAML, secret', 'Kubernetes <code>kubernetes.io/tls</code> Secret.'),
 'jks':               ('Java KeyStore, secret', 'Key and full chain; password from the app\'s Settings (default <code>secret</code>, which TSplus requires).'),
}

KIND_DOC = {
 'Files':     'Writes <code>Files</code> into <code>Folder</code>. The default when <code>Kind</code> is left out.',
 'TSplus':    'TSplus 15 or later. Imports the PFX with <code>CertificateManager.exe /add</code>. <code>Folder</code> and <code>Files</code> are ignored.',
 'TSplusJks': 'TSplus versions that use <code>cert.jks</code>. Replaces <code>Clients\\webserver\\cert.jks</code> and runs <code>AdminTool.exe /webrestart</code>.',
}

STARTIN_DOC = {
 'samesession': ('SameSession', 'Default. Each copy comes back on the desktop it was running on; if it was not running, on the console desktop.'),
 'console':     ('Console', 'Always on the console user\'s desktop.'),
 'background':  ('Background', 'Session 0, as the agent\'s account (LocalSystem), with no visible window.'),
}

CLI_USE = {
 'install':   ('.\\CertificateGet.Agent.exe install 9443', 'Creates <code>agent.json</code> (keeping an existing one), a new API key when there is no valid one, the TLS certificate, registers and starts the service and, on Windows, opens the firewall. Safe to run again, for example after moving the executable.'),
 'uninstall': ('sudo ./CertificateGet.Agent uninstall', 'Stops and removes the service and the firewall rule. <code>agent.json</code>, the TLS certificate and the backups stay.'),
 'newkey':    ('.\\CertificateGet.Agent.exe newkey', 'Replaces the API key. Enter the new key in every app target that uses this agent. No restart needed.'),
 'info':      ('sudo ./CertificateGet.Agent info', 'Prints the URL, the TLS fingerprint to compare with the app, the allowed IPs and, per slot, the sources it needs.'),
 'check':     ('.\\CertificateGet.Agent.exe check', 'Validates <code>agent.json</code>: JSON syntax, API key, IPs, duplicate slots, folders, sources, services, programs, TSplus paths. Exit code 0 when clean.'),
 'run':       ('sudo ./CertificateGet.Agent run', 'Runs in the console with the log on screen. Stop the service first, since both want the same port.'),
}

ENDPOINT_DOC = {
 '/api/slots':        ('Lists the slots. Used by <b>Connect &amp; list slots</b>.',
                       '{ "host": "WEB01", "version": "1.0.0.0",\n  "slots": [ { "name": "reddin-wildcard", "description": "…", "destinations": 3 } ] }'),
 '/api/slots/{name}': ('The sources a slot needs, so the app builds and sends exactly those. 404 when the slot does not exist.',
                       '{ "name": "reddin-wildcard", "files": [ "fullchain", "key", "pfx" ] }'),
 '/api/deploy':       ('Runs a deployment. The body is a <code>DeployRequest</code>; the answer lists every step.',
                       '{ "success": true, "error": null,\n  "steps": [ { "ok": true, "message": "NetTalk - Portal: wrote portal.crt, portal.key to D:\\\\Portal\\\\certs" },\n             { "ok": true, "message": "Restarted service PortalWeb" } ] }'),
}

# ---------------------------------------------------------------- helpers
def esc(s): return html.escape(s or '', quote=False)
def slug(s): return re.sub(r'[^a-z0-9]+', '-', s.lower()).strip('-')

def _hl_json(txt):
    out, pos = [], 0
    for m in re.finditer(r'"(?:\\.|[^"\\])*"(\s*:)?|//[^\n]*|\b(?:true|false|null)\b|-?\b\d+\b', txt):
        out.append(esc(txt[pos:m.start()]))
        t = m.group(0)
        if t.startswith('//'): cls = 'c'
        elif t.startswith('"'): cls = 'k' if m.group(1) else 's'
        else: cls = 'n'
        if cls == 'k':
            key = t[:len(t) - len(m.group(1))]
            out.append('<span class="t-k">%s</span>%s' % (esc(key), esc(m.group(1))))
        else:
            out.append('<span class="t-%s">%s</span>' % (cls, esc(t)))
        pos = m.end()
    out.append(esc(txt[pos:]))
    return ''.join(out)

def _hl_shell(txt):
    lines = []
    for ln in txt.split('\n'):
        m = re.match(r'^(.*?)(\s+#\s.*|^#.*)$', ln)
        if m: lines.append(esc(m.group(1)) + '<span class="t-c">%s</span>' % esc(m.group(2)))
        else: lines.append(esc(ln))
    return '\n'.join(lines)

def code(txt, lang='json'):
    body = txt.strip('\n')
    inner = _hl_json(body) if lang == 'json' else _hl_shell(body) if lang in ('bash', 'powershell', 'haproxy') else esc(body)
    return '<pre class="code" data-lang="%s"><code>%s</code></pre>' % (lang, inner)

def usecode(txt):
    return '<pre class="code code--use" data-lang="use"><code>%s</code></pre>' % _hl_json(txt)

def note(kind, title, body):
    return ('<aside class="note note--%s"><p class="note__t">%s</p><div class="note__b">%s</div></aside>'
            % (kind, esc(title), body))

def table(head, rows, cls=''):
    th = ''.join('<th>%s</th>' % h for h in head)
    tr = ''.join('<tr>%s</tr>' % ''.join('<td>%s</td>' % c for c in r) for r in rows)
    return ('<div class="tw"><table class="%s"><thead><tr>%s</tr></thead><tbody>%s</tbody></table></div>'
            % (cls, th, tr))

def steps(items):
    return '<ol class="steps">%s</ol>' % ''.join('<li>%s</li>' % i for i in items)

def flow(rows):
    out = ['<div class="stack">']
    for i, (lbl, txt, cls) in enumerate(rows):
        if i: out.append('<div class="arrow">&darr;</div>')
        out.append('<div class="layer %s"><b>%s</b><p>%s</p></div>' % (cls, lbl, txt))
    out.append('</div>')
    return ''.join(out)

# ---------------------------------------------------------------- volumes
VOLUMES = {'en': [
 ('getting-started.html',   'Getting Started',    'Install the app, first certificate, install the agent'),
 ('programmers-guide.html', "Programmer's Guide", 'Validation, authorities, deployment; how the agent works'),
 ('template-guide.html',    'Template Guide',     'Recipes in the app; agent.json for NetTalk, HAProxy, Cockpit, TSplus'),
 ('reference.html',         'Reference',          'Formats, settings; every agent field, command and endpoint'),
], 'es': [
 ('getting-started.html',   'Primeros pasos',       'Instalar el agente y conectar la aplicación'),
 ('programmers-guide.html', 'Guía del programador', 'Cómo se ejecuta una implementación, seguridad, notas de plataforma'),
 ('template-guide.html',    'Guía de plantillas',   'agent.json para NetTalk, HAProxy, Cockpit, TSplus'),
 ('reference.html',         'Referencia',           'Cada campo, origen, comando y endpoint'),
]}

#  Words of the page frame itself, per language.
CHROME = {
 'en': {'filter': 'Filter', 'filter_ph': 'Folder, combined, check&hellip;', 'lang': 'Language', 'brand': 'Manual',
        'footer': 'CertificateGet manual &mdash; four volumes, the app and the agent. The reference is generated from '
                  'the app\'s <code>CertificateStore.cs</code>, <code>Models.cs</code> and <code>DeployService.cs</code> and the '
                  'agent\'s <code>AgentConfig.cs</code>, <code>ProgramRestarter.cs</code>, <code>Program.cs</code> and '
                  '<code>Deployer.cs</code>, and the combined examples are the files in <code>CertificateGet.Agent/examples</code>, '
                  'so formats, settings, fields, defaults and commands are the ones in the build.'},
 'es': {'filter': 'Filtrar', 'filter_ph': 'Folder, combined, check&hellip;', 'lang': 'Idioma', 'brand': 'Manual',
        'footer': 'Manual de CertificateGet &mdash; cuatro volúmenes, la aplicación y el agente. La referencia se genera a partir de '
                  '<code>CertificateStore.cs</code>, <code>Models.cs</code> y <code>DeployService.cs</code> de la aplicación y '
                  '<code>AgentConfig.cs</code>, <code>ProgramRestarter.cs</code>, <code>Program.cs</code> y '
                  '<code>Deployer.cs</code> del agente, y los ejemplos completos son los archivos de <code>CertificateGet.Agent/examples</code>, '
                  'así que los formatos, ajustes, campos, valores predeterminados y comandos son los de la compilación.'},
}
LANGS = [('en', 'English'), ('es', 'Español')]

#  Published, each volume is its own page on its own address, so a relative
#  filename does not reach the next one.  Cross-volume links are these absolute
#  addresses; the local copies in docs/ therefore point at the published set.
PUBLISHED = {'en': {
 'getting-started.html':   'https://claude.ai/artifact/6NUtSWhxsJHAgFg1bsgEjH',
 'programmers-guide.html': 'https://claude.ai/artifact/Xk9wRJUG5rPquoFTwbCrFe',
 'template-guide.html':    'https://claude.ai/artifact/4NaQZxJRzNB1EgG6FPq4i7',
 'reference.html':         'https://claude.ai/artifact/WfYNLoBapj4dsyLWJMPU8s',
}, 'es': {
 'getting-started.html':   'https://claude.ai/artifact/5G22xWR3cTTfuWabzd1den',
 'programmers-guide.html': 'https://claude.ai/artifact/1Md1dk2STxaDExvMtmxifP',
 'template-guide.html':    'https://claude.ai/artifact/P99MLSHwS4nKgr7gTRCAKU',
 'reference.html':         'https://claude.ai/artifact/LV7XNKYtxXtAna9Xkw2CAJ',
}}

def pub(target, lang=None):
    return PUBLISHED[lang or LANG].get(target, target)

def href(target, current):
    return '#' if target == current else pub(target)

def xref(target, anchor, text):
    """A link into another volume."""
    return '<a href="%s#%s">%s</a>' % (pub(target), anchor, text)

CSS = """
:root{
  --paper:#fbfcfd; --surface:#f1f5f9; --sunken:#e9eff5; --rule:#d6e0ea;
  --ink:#0f1720; --soft:#4e5f70; --faint:#728396;
  --accent:#23629a; --accent-bg:#e4eef6; --accent-rule:#b9d3e6;
  --teal:#127068; --teal-bg:#e0f1ee;
  --warn:#8a5a12; --warn-bg:#f7eeda;
  --t-key:#1f5c91; --t-str:#11695f; --t-num:#8a5a12; --t-com:#7d8ea0;
}
@media (prefers-color-scheme:dark){
  :root:not([data-theme="light"]){
    color-scheme:dark;
    --paper:#0d131a; --surface:#151d26; --sunken:#111922; --rule:#25313d;
    --ink:#e2eaf2; --soft:#9aabbc; --faint:#7a8b9d;
    --accent:#6fadde; --accent-bg:#152738; --accent-rule:#28455f;
    --teal:#4fb5ab; --teal-bg:#102a29;
    --warn:#d8a545; --warn-bg:#2b2413;
    --t-key:#7fb6e3; --t-str:#6cc3b8; --t-num:#d8a545; --t-com:#6f8092;
  }
}
:root[data-theme="dark"]{
  color-scheme:dark;
  --paper:#0d131a; --surface:#151d26; --sunken:#111922; --rule:#25313d;
  --ink:#e2eaf2; --soft:#9aabbc; --faint:#7a8b9d;
  --accent:#6fadde; --accent-bg:#152738; --accent-rule:#28455f;
  --teal:#4fb5ab; --teal-bg:#102a29;
  --warn:#d8a545; --warn-bg:#2b2413;
  --t-key:#7fb6e3; --t-str:#6cc3b8; --t-num:#d8a545; --t-com:#6f8092;
}
*{box-sizing:border-box}
body{margin:0; background:var(--paper); color:var(--ink);
  font-family:"IBM Plex Serif",Georgia,serif; font-size:16px; line-height:1.62;
  -webkit-font-smoothing:antialiased}
h1,h2,h3,h4,.ui{font-family:"IBM Plex Sans",system-ui,-apple-system,Segoe UI,sans-serif}
code,pre,.mono{font-family:"IBM Plex Mono",ui-monospace,Consolas,monospace}
a{color:var(--accent)}
a:focus-visible{outline:2px solid var(--accent); outline-offset:2px; border-radius:3px}
.wrap{display:grid; grid-template-columns:280px minmax(0,1fr); align-items:start}
.side{position:sticky; top:env(safe-area-inset-top,0px); height:100vh; overflow-y:auto; padding:24px 20px 60px;
  border-right:1px solid var(--rule); background:var(--surface)}
.brand{font-family:"IBM Plex Sans",sans-serif; font-weight:600; font-size:15px; margin:0 0 14px}
.brand b{color:var(--accent)}
.langs{display:flex; gap:4px; margin:0 0 14px; padding:3px; border:1px solid var(--rule); border-radius:7px;
  background:var(--paper)}
.langs a{flex:1; text-align:center; padding:4px 8px; border-radius:5px; text-decoration:none;
  font:500 12px/1.3 "IBM Plex Sans",sans-serif; color:var(--soft)}
.langs a:hover{background:var(--sunken); color:var(--ink)}
.langs a.here{background:var(--accent-bg); color:var(--accent)}
.vols{list-style:none; margin:0 0 18px; padding:0 0 16px; display:flex; flex-direction:column; gap:3px;
  border-bottom:1px solid var(--rule)}
.vols a{display:block; padding:7px 10px; border-radius:6px; text-decoration:none;
  font:500 13px/1.35 "IBM Plex Sans",sans-serif; color:var(--soft); border:1px solid transparent}
.vols a:hover{background:var(--sunken); color:var(--ink)}
.vols a.here{background:var(--accent-bg); border-color:var(--accent-rule); color:var(--accent)}
.vols small{display:block; font:400 11px/1.35 "IBM Plex Sans",sans-serif; color:var(--faint); margin-top:2px}
.vols a.here small{color:var(--accent)}
.filter{width:100%; margin:0 0 16px; padding:7px 10px; font:13px/1.4 "IBM Plex Sans",sans-serif;
  color:var(--ink); background:var(--paper); border:1px solid var(--rule); border-radius:6px}
.filter:focus{outline:2px solid var(--accent); outline-offset:1px}
.nav__g{font:600 10.5px/1 "IBM Plex Sans",sans-serif; letter-spacing:.11em; text-transform:uppercase;
  color:var(--faint); margin:18px 0 8px}
.nav__l{list-style:none; margin:0; padding:0; display:flex; flex-direction:column; gap:1px}
.nav__l a{display:block; padding:4px 8px; border-radius:5px; text-decoration:none; color:var(--soft);
  font:400 13.5px/1.45 "IBM Plex Sans",sans-serif}
.nav__l a:hover{background:var(--sunken); color:var(--ink)}
.nav__l a.on{background:var(--accent-bg); color:var(--accent); font-weight:500}
.main{padding:0 0 120px; min-width:0}
.inner{max-width:980px; padding:0 40px}
.hero{padding:52px 40px 30px; border-bottom:1px solid var(--rule);
  background:linear-gradient(180deg,var(--accent-bg),transparent)}
.hero .inner{padding:0}
.eyebrow{font:600 11px/1 "IBM Plex Sans",sans-serif; letter-spacing:.14em; text-transform:uppercase;
  color:var(--accent); margin:0 0 12px}
h1{font-size:37px; line-height:1.1; margin:0 0 10px; letter-spacing:-.015em; text-wrap:balance}
.sub{font-size:17px; color:var(--soft); margin:0 0 18px; max-width:62ch}
.chips{display:flex; flex-wrap:wrap; gap:8px}
.chip{font:500 11.5px/1 "IBM Plex Sans",sans-serif; padding:6px 10px; border-radius:99px;
  border:1px solid var(--rule); background:var(--paper); color:var(--soft)}
.chip b{color:var(--ink); font-weight:600}
h2{font-size:26px; margin:60px 0 6px; letter-spacing:-.01em; scroll-margin-top:18px; text-wrap:balance}
h2 .k{font:600 10.5px/1 "IBM Plex Sans",sans-serif; letter-spacing:.12em; text-transform:uppercase;
  color:var(--accent); display:block; margin-bottom:9px}
h3{font-size:17.5px; margin:36px 0 10px; scroll-margin-top:18px; text-wrap:balance}
h4{font-size:14.5px; margin:24px 0 6px; color:var(--soft); font-weight:600}
p{margin:0 0 14px; max-width:70ch}
ul.b,ol.b{max-width:70ch; padding-left:20px; margin:0 0 16px}
ul.b li,ol.b li{margin:0 0 7px}
ol.steps{list-style:none; counter-reset:s; padding:0; margin:0 0 20px; max-width:74ch;
  display:flex; flex-direction:column; gap:12px}
ol.steps>li{counter-increment:s; display:grid; grid-template-columns:30px minmax(0,1fr); gap:12px}
ol.steps>li::before{content:counter(s); width:26px; height:26px; border-radius:50%;
  background:var(--accent-bg); border:1px solid var(--accent-rule); color:var(--accent);
  font:600 12.5px/24px "IBM Plex Sans",sans-serif; text-align:center; font-variant-numeric:tabular-nums}
ol.steps>li>div{min-width:0}
ol.steps .code{margin:8px 0 4px}
.code{background:var(--sunken); border:1px solid var(--rule); border-left:3px solid var(--accent-rule);
  border-radius:0 7px 7px 0; padding:14px 16px; overflow-x:auto; margin:0 0 18px;
  font-size:12.9px; line-height:1.62; position:relative}
.code code{white-space:pre; color:var(--ink)}
.code::after{content:attr(data-lang); position:absolute; top:0; right:0; padding:3px 9px;
  font:500 9.5px/1 "IBM Plex Sans",sans-serif; letter-spacing:.1em; text-transform:uppercase;
  color:var(--faint); background:var(--surface); border-left:1px solid var(--rule);
  border-bottom:1px solid var(--rule); border-radius:0 0 0 6px}
.code--use{margin:7px 0 2px; border-left-color:var(--teal); background:var(--paper);
  font-size:12.4px; padding:9px 12px}
.code--use::after{content:'agent.json'; color:var(--teal)}
.code--sh::after{content:'shell'} .code--resp::after{content:'response'}
.t-k{color:var(--t-key)} .t-s{color:var(--t-str)} .t-n{color:var(--t-num)} .t-c{color:var(--t-com); font-style:italic}
p code,li code,td code{background:var(--sunken); border:1px solid var(--rule); border-radius:4px;
  padding:.06em .34em; font-size:.86em}
.tw{overflow-x:auto; margin:0 0 20px; border:1px solid var(--rule); border-radius:8px; background:var(--paper)}
table{border-collapse:collapse; width:100%; font-size:13.5px; font-family:"IBM Plex Sans",sans-serif}
th{text-align:left; font-weight:600; font-size:11px; letter-spacing:.08em; text-transform:uppercase;
  color:var(--faint); padding:9px 14px; border-bottom:1px solid var(--rule); background:var(--surface)}
td{padding:10px 14px; border-bottom:1px solid var(--rule); vertical-align:top}
tr:last-child td{border-bottom:0}
.fns .fn__n{width:200px; white-space:nowrap}
.fns .fn__n code{font-size:12.8px; color:var(--accent); font-weight:500; background:none; border:0; padding:0}
.fns .fn__s code{font-size:12.2px; color:var(--soft); background:none; border:0; padding:0; white-space:pre-wrap}
.fn__d{margin:5px 0 0; font-family:"IBM Plex Serif",serif; font-size:13.8px; color:var(--ink); max-width:74ch}
.fn__d code{font-size:.84em}
.dflt{margin-left:8px; font:500 10.5px/1 "IBM Plex Mono",monospace; color:var(--faint);
  border:1px solid var(--rule); border-radius:4px; padding:2px 5px; vertical-align:1px}
.tag{display:inline-block; font:600 10px/1 "IBM Plex Sans",sans-serif; letter-spacing:.06em; text-transform:uppercase;
  padding:3px 6px; border-radius:4px; background:var(--teal-bg); color:var(--teal); margin-left:6px; vertical-align:1px}
.tag--warn{background:var(--warn-bg); color:var(--warn)}
.note{border:1px solid var(--rule); border-left:3px solid var(--accent); background:var(--surface);
  border-radius:0 7px 7px 0; padding:13px 16px; margin:0 0 18px; max-width:74ch}
.note--warn{border-left-color:var(--warn); background:var(--warn-bg)}
.note--ok{border-left-color:var(--teal); background:var(--teal-bg)}
.note__t{font:600 12px/1.3 "IBM Plex Sans",sans-serif; letter-spacing:.03em; margin:0 0 5px;
  text-transform:uppercase; color:var(--soft)}
.note--warn .note__t{color:var(--warn)} .note--ok .note__t{color:var(--teal)}
.note__b p{margin:0 0 8px; font-size:14.5px} .note__b p:last-child{margin:0}
.stack{display:flex; flex-direction:column; gap:9px; margin:0 0 22px; max-width:680px}
.layer{border:1px solid var(--rule); border-radius:8px; padding:12px 15px; background:var(--surface);
  display:grid; grid-template-columns:130px minmax(0,1fr); gap:14px; align-items:baseline}
.layer b{font:600 11px/1.4 "IBM Plex Sans",sans-serif; letter-spacing:.06em; text-transform:uppercase; color:var(--accent)}
.layer.srv b{color:var(--teal)}
.layer p{margin:0; font-size:14px; color:var(--soft); max-width:none}
.arrow{text-align:center; color:var(--faint); font-size:12px; margin:-4px 0}
.next{display:grid; grid-template-columns:repeat(auto-fit,minmax(210px,1fr)); gap:12px; margin:26px 0 0}
.next a{display:block; padding:14px 16px; border:1px solid var(--rule); border-radius:9px;
  background:var(--surface); text-decoration:none}
.next a:hover{border-color:var(--accent-rule); background:var(--accent-bg)}
.next b{display:block; font:600 14px/1.3 "IBM Plex Sans",sans-serif; color:var(--accent); margin-bottom:3px}
.next span{font:400 13px/1.45 "IBM Plex Serif",serif; color:var(--soft)}
.hide{display:none !important}
footer{margin-top:70px; padding:22px 0 0; border-top:1px solid var(--rule); color:var(--faint);
  font:400 13px/1.6 "IBM Plex Sans",sans-serif}
@media (max-width:900px){
  .wrap{grid-template-columns:1fr}
  .side{position:static; height:auto; border-right:0; border-bottom:1px solid var(--rule)}
  .inner,.hero{padding-left:18px; padding-right:18px}
  .layer{grid-template-columns:1fr; gap:4px}
  .fns .fn__n{white-space:normal; width:auto}
  h1{font-size:30px}
}
@media (prefers-reduced-motion:reduce){*{animation:none !important; transition:none !important}}
"""

JS = """
const q = document.getElementById('filter');
if (q) {
  const rows = [...document.querySelectorAll('tr.fn')];
  q.addEventListener('input', () => {
    const t = q.value.trim().toLowerCase();
    rows.forEach(r => r.classList.toggle('hide', t && !r.dataset.k.includes(t)));
    document.querySelectorAll('.tw').forEach(w => {
      const body = w.querySelector('tbody');
      if (!body || !body.querySelector('tr.fn')) return;
      const any = [...body.querySelectorAll('tr')].some(r => !r.classList.contains('hide'));
      w.classList.toggle('hide', !any);
    });
  });
}
/*  Which sidebar entry is lit: by position, not IntersectionObserver.  One click
    crosses several headings at once, they all arrive in a single callback, and the
    last one processed wins - which lights the wrong chapter.  */
const links = [...document.querySelectorAll('.nav__l a')];
if (links.length) {
  const byId  = new Map(links.map(a => [a.getAttribute('href').slice(1), a]));
  const marks = [...document.querySelectorAll('h2[id],h3[id]')].filter(h => byId.has(h.id));
  const light = a => links.forEach(l => l.classList.toggle('on', l === a));
  let held = null, holdUntil = 0, queued = false;
  function spy() {
    queued = false;
    if (held) { light(held); return; }
    let cur = marks[0];
    for (const h of marks) {
      if (h.getBoundingClientRect().top <= 120) cur = h; else break;
    }
    if (innerHeight + scrollY >= document.documentElement.scrollHeight - 2)
      cur = marks[marks.length - 1];
    if (cur) light(byId.get(cur.id));
  }
  links.forEach(a => a.addEventListener('click', () => {
    held = a; holdUntil = performance.now() + 700; light(a);
  }));
  const later = () => { if (!queued) { queued = true; requestAnimationFrame(spy); } };
  addEventListener('scroll', () => {
    if (held && performance.now() > holdUntil) held = null;
    later();
  }, {passive: true});
  addEventListener('resize', later);
  if (marks.length) spy();
}
"""

def volnav(current):
    out = ['<ul class="vols">']
    for i, (hrefname, name, blurb) in enumerate(VOLUMES[LANG]):
        here = ' class="here"' if hrefname == current else ''
        out.append('<li><a href="%s"%s>%s. %s<small>%s</small></a></li>'
                   % (href(hrefname, current), here, i + 1, esc(name), esc(blurb)))
    out.append('</ul>')
    return ''.join(out)

def headings(body):
    """Every anchored id in the body, with the words actually printed above it."""
    out = {}
    for m in re.finditer(r'<h([23]) id="([^"]+)"[^>]*>(.*?)</h\1>', body, re.S):
        txt = re.sub(r'<span class="k">.*?</span>', '', m.group(3), flags=re.S)
        out[m.group(2)] = html.unescape(re.sub(r"\s+", ' ', re.sub(r'<[^>]+>', '', txt)).strip())
    return out

def secnav(groups, titles):
    #  The sidebar prints the heading itself, never a second wording of it.
    out = []
    for group, ids in groups:
        if group: out.append('<p class="nav__g">%s</p>' % esc(group))
        out.append('<ul class="nav__l">')
        for aid in ids:
            out.append('<li><a href="#%s">%s</a></li>' % (aid, esc(titles.get(aid, '??' + aid))))
        out.append('</ul>')
    return ''.join(out)

def nextcards(names):
    cards = []
    for h in names:
        for hrefname, name, blurb in VOLUMES[LANG]:
            if hrefname == h:
                cards.append('<a href="%s"><b>%s &rarr;</b><span>%s</span></a>'
                             % (pub(hrefname), esc(name), esc(blurb)))
    return '<div class="next">%s</div>' % ''.join(cards)

def langnav(current):
    #  The same volume in the other language.
    out = ['<nav class="langs" aria-label="%s">' % CHROME[LANG]['lang']]
    for lc, name in LANGS:
        here = ' class="here" aria-current="true"' if lc == LANG else ''
        out.append('<a href="%s" lang="%s"%s>%s</a>' % ('#' if lc == LANG else pub(current, lc), lc, here, name))
    out.append('</nav>')
    return ''.join(out)

HEADINGS = {}                   # (lang, file) -> heading ids, compared across languages after the build

def page(filename, title, eyebrow, heading, sub, chips, groups, body, showfilter=False):
    titles = headings(body)
    HEADINGS[(LANG, filename)] = list(titles)
    where = filename if LANG == 'en' else LANG + '/' + filename
    linked = [aid for _, ids in groups for aid in ids]
    for aid in linked:
        if aid not in titles:
            PROBLEMS.append('%s: the nav points at #%s, which is not a heading' % (where, aid))
    for aid in titles:
        if aid not in linked:
            PROBLEMS.append('%s: heading #%s (%s) is in no nav' % (where, aid, titles[aid]))
    for m in re.finditer(r'href="#([^"]+)"', body):
        if m.group(1) not in titles:
            PROBLEMS.append('%s: in-page link #%s lands on no heading' % (where, m.group(1)))
    C = CHROME[LANG]
    nav = langnav(filename) + volnav(filename) + \
          ('<label class="ui" style="font-size:11px;color:var(--faint);letter-spacing:.08em;'
           'text-transform:uppercase" for="filter">%s</label>'
           '<input id="filter" class="filter" type="search" placeholder="%s" '
           'autocomplete="off">' % (C['filter'], C['filter_ph']) if showfilter else '') + secnav(groups, titles)
    chiphtml = ''.join('<span class="chip">%s</span>' % c for c in chips)
    doc = ('<html lang="%s">\n<title>%s</title>\n'
           '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n'
           '<link rel="preconnect" href="https://fonts.googleapis.com">\n'
           '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>\n'
           '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
           'family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans:wght@400;500;600&'
           'family=IBM+Plex+Serif:wght@400;600&display=swap">\n'
           '<style>%s</style>\n'
           '<div class="wrap">\n<nav class="side">\n'
           '  <p class="brand"><b>CertificateGet</b> %s</p>\n%s\n</nav>\n'
           '<main class="main">\n'
           '  <header class="hero"><div class="inner">\n'
           '    <p class="eyebrow">%s</p>\n    <h1>%s</h1>\n    <p class="sub">%s</p>\n'
           '    <div class="chips">%s</div>\n'
           '  </div></header>\n  <div class="inner">%s\n'
           '    <footer>%s</footer>\n'
           '  </div>\n</main>\n</div>\n<script>%s</script>\n'
           % (LANG, esc(title), CSS, C['brand'], nav, esc(eyebrow), esc(heading), sub, chiphtml, body, C['footer'], JS))
    sub_ = [] if LANG == 'en' else [LANG]
    os.makedirs(os.path.join(ROOT, 'docs', *sub_), exist_ok=True)
    io.open(os.path.join(ROOT, 'docs', *sub_, filename), 'w', encoding='utf-8', newline='\n').write(doc)
    #  The copy built into the app (Help, F1) opens from disk, so there the
    #  volumes link to each other by file name instead of by published address.
    #  English sits in help/, Spanish in help/es/.
    local = doc
    for lang, urls in PUBLISHED.items():
        for name, url in urls.items():
            if lang == LANG: rel = name
            elif lang == 'en': rel = '../' + name
            else: rel = lang + '/' + name
            local = local.replace(url, rel)
    os.makedirs(os.path.join(ROOT, 'docs', 'help', *sub_), exist_ok=True)
    io.open(os.path.join(ROOT, 'docs', 'help', *sub_, filename), 'w', encoding='utf-8', newline='\n').write(
        '<!doctype html>\n<meta charset="utf-8">\n' + local)
    return len(doc)

GS, PG, TG, RF = 'getting-started.html', 'programmers-guide.html', 'template-guide.html', 'reference.html'

# =====================================================================
#  1  GETTING STARTED
# =====================================================================
def build_getting_started():
    B = []; add = B.append
    app_getting_started(add)

    add('<h2 id="what"><span class="k">The agent</span>What the agent does</h2>')
    add('''<p>The agent is a small service you install on each server that uses a certificate. When the
CertificateGet app issues or renews a certificate, it sends the files to the agent over HTTPS. The agent
writes them where each program expects them, restarts what needs restarting, and reports every step back
to the app's activity log.</p>''')
    add(flow([
        ('CertificateGet', 'The desktop app on your PC issues or renews the certificate from Let\'s Encrypt or ZeroSSL.', ''),
        ('HTTPS :9443', 'The app asks the agent which files the slot needs, then sends exactly those. It checks the agent\'s pinned TLS fingerprint and sends the API key.', ''),
        ('Agent', 'A Windows service or systemd unit on the server. It reads <code>agent.json</code>, finds the slot and backs up the current files.', 'srv'),
        ('Destinations', 'Files written into each folder with the names each program expects, or imported into TSplus.', 'srv'),
        ('Restarts', 'Services, desktop programs and commands (for example <code>systemctl reload haproxy</code>), each once.', 'srv'),
    ]))
    add('''<p>Use the agent on servers that need more than a file copy: several NetTalk instances with their own file
names, TSplus, desktop programs that must be restarted on a user's desktop. For a single Linux box where an
upload and one command are enough, the app's SFTP target works without installing anything.</p>''')

    add('<h2 id="build"><span class="k">Step 1</span>Get the executables</h2>')
    add('''<p>Build both agents with the publish script in the repository root. They are self-contained single
files, so the servers need no .NET runtime.</p>''')
    add(code(r'''.\publish.ps1
# run\agent-windows\CertificateGet.Agent.exe   Windows servers (NetTalk, TSplus, IIS)
# run\agent-linux\CertificateGet.Agent         Linux servers (HAProxy, Cockpit, nginx), x64''', 'powershell'))
    add('''<p>Each output folder also gets <code>README.md</code> and <code>agent.example.json</code>. The example is
for reading. Do not copy it over <code>agent.json</code>: it holds placeholders instead of the real key hash and
TLS password.</p>''')

    add('<h2 id="win"><span class="k">Step 2a</span>Install on Windows</h2>')
    add('<p>Do this on the server that runs NetTalk, TSplus or the other programs using the certificate.</p>')
    add(steps([
        '<div><b>Copy the executable</b> to a folder of its own. The agent keeps <code>agent.json</code>, its TLS '
        'certificate, the log and the backups next to the executable.'
        + code(r'''New-Item -ItemType Directory "C:\Program Files\CertificateGet Agent" -Force
Copy-Item .\CertificateGet.Agent.exe "C:\Program Files\CertificateGet Agent\"''', 'powershell') + '</div>',
        '<div><b>Open PowerShell as administrator</b> and run <code>install</code>. Add a port number if 9443 is taken.'
        + code(r'''cd "C:\Program Files\CertificateGet Agent"
.\CertificateGet.Agent.exe install          # or: .\CertificateGet.Agent.exe install 9543''', 'powershell') + '</div>',
        '<div><b>Copy the three values it prints.</b> The API key is shown only this once.'
        + code('''Installed. Enter these in CertificateGet → certificate → Deployment → Add target → Agent:
  URL          https://WEB01:9443
  API key      q3ZtY0n8…Kd4      (shown once — store it now)
  Fingerprint  5E1B7C…90AF   (the app asks you to confirm this on first connect)''', 'text') + '</div>',
        '<div><b>Check the service.</b> <code>info</code> prints the URL and fingerprint again at any time.'
        + code(r'''Get-Service %s
.\CertificateGet.Agent.exe info''' % SERVICE, 'powershell') + '</div>',
    ]))
    add('<h3 id="win-what">What install changes on Windows</h3>')
    add(table(['Item', 'Detail'], [
        ['Service', '<code>%s</code> (display name <i>CertificateGet Agent</i>), delayed automatic start, runs as LocalSystem, restarted by Windows 60 seconds after a failure.' % SERVICE],
        ['Firewall', 'Inbound rule <i>%s</i> for the TCP port.' % FIREWALL],
        ['<code>agent.json</code>', 'Created with a sample slot, unless one already exists. The API key hash is stored, never the key.'],
        ['<code>agent-tls.pfx</code>', 'Self-signed TLS certificate for the agent, valid 20 years. The app pins its SHA-256 fingerprint.'],
    ]))

    add('<h2 id="linux"><span class="k">Step 2b</span>Install on Linux</h2>')
    add('<p>For HAProxy, Cockpit, nginx and other Linux services. The agent runs as root because it writes under <code>/etc</code> and restarts services.</p>')
    add(steps([
        '<div><b>Copy the binary to the server.</b>'
        + code('scp run/agent-linux/CertificateGet.Agent admin@lb01:/tmp/', 'bash') + '</div>',
        '<div><b>Put it in its own folder.</b> Use <code>cp</code>, not <code>mv</code>: on SELinux systems a file '
        'moved out of <code>/tmp</code> keeps a label that systemd refuses to run.'
        + code('''sudo mkdir -p /opt/certificateget-agent
sudo cp /tmp/CertificateGet.Agent /opt/certificateget-agent/
sudo chmod +x /opt/certificateget-agent/CertificateGet.Agent''', 'bash') + '</div>',
        '<div><b>Install.</b> Copy the URL, API key and fingerprint it prints.'
        + code('''cd /opt/certificateget-agent
sudo ./CertificateGet.Agent install         # or: sudo ./CertificateGet.Agent install 9543''', 'bash') + '</div>',
        '<div><b>Open the port.</b> Only one of these applies to your distribution.'
        + code('''sudo ufw allow 9443/tcp                                                          # Ubuntu / Debian
sudo firewall-cmd --permanent --add-port=9443/tcp && sudo firewall-cmd --reload  # RHEL / Rocky / Alma / Fedora''', 'bash') + '</div>',
        '<div><b>Check it is running.</b>'
        + code('''systemctl status %s
sudo ./CertificateGet.Agent info
journalctl -u %s -f          # live log; the same lines go to agent.log''' % (UNIT, UNIT), 'bash') + '</div>',
    ]))
    add('<h3 id="linux-what">What install changes on Linux</h3>')
    add(table(['Item', 'Detail'], [
        ['systemd unit', '<code>/etc/systemd/system/%s.service</code>, <code>Type=notify</code>, <code>Restart=on-failure</code>, enabled and started.' % UNIT],
        ['SELinux', 'When SELinux is enabled, the executable is labelled <code>bin_t</code> (<code>semanage fcontext</code> + <code>restorecon</code>, or <code>chcon</code> as a fallback).'],
        ['Firewall', 'Not touched. Open the port yourself (step 4).'],
        ['Files', '<code>agent.json</code> and <code>agent-tls.pfx</code> are created with mode 600.'],
    ]))

    add('<h2 id="slot"><span class="k">Step 3</span>Describe where the certificate goes</h2>')
    add('''<p>Open <code>agent.json</code> next to the executable and replace the sample slot. A <b>slot</b> is what a
certificate in the app points to; each slot lists <b>destinations</b>, and each destination says which files to
write where and what to restart. This one writes a certificate and key for one NetTalk service:</p>''')
    add(code(r'''{
  "Port": 9443,
  "ApiKeyHash": "…left as install wrote it…",
  "AllowedIps": [],
  "TlsPfx": "agent-tls.pfx",
  "TlsPassword": "…left as install wrote it…",
  "KeepBackups": 5,
  "Slots": [
    {
      "Name": "example-com",
      "Description": "www.example.com for the NetTalk web server",
      "Destinations": [
        {
          "Name": "NetTalk web",
          "Folder": "C:\\Apps\\Web\\certificates",
          "Files": [
            { "Source": "fullchain", "FileName": "www.example.com.crt" },
            { "Source": "key",       "FileName": "www.example.com.key" }
          ],
          "RestartServices": [ "WebServer" ]
        }
      ]
    }
  ]
}'''))
    add('<p>Then validate it. The agent reads the file again on every request, so no restart is needed (except after changing <code>Port</code>).</p>')
    add(code(r'''.\CertificateGet.Agent.exe check''', 'powershell'))
    add(code('''Slot "example-com"
  • NetTalk web
OK — configuration looks good.''', 'text'))
    add('<p>Ready-made files for NetTalk, HAProxy, Cockpit and TSplus are in the %s; every field is in the %s.</p>'
        % (xref(TG, 'nettalk', 'Template Guide'), xref(RF, 'root', 'Reference')))
    add(note('warn', 'Windows paths in JSON',
        '<p>Every backslash is written twice: <code>"C:\\\\Apps\\\\Web"</code>. A single backslash is a JSON syntax '
        'error, and <code>check</code> shows the line.</p>'))

    add('<h2 id="app"><span class="k">Step 4</span>Connect the app</h2>')
    add('<p>In CertificateGet, on the PC that issues the certificates:</p>')
    add(steps([
        '<div>Open <b>Certificates</b>, select the certificate, and in the <b>Deployment</b> section click <b>Add target</b>.</div>',
        '<div>Choose <b>CertificateGet Agent</b>. Enter a <b>Name</b> (for example <i>WEB01 NetTalk</i>), the <b>Agent URL</b> '
        '(<code>https://WEB01:9443</code>) and the <b>API key</b>.</div>',
        '<div>Click <b>Connect &amp; list slots</b>. The first time, a dialog shows the agent\'s TLS fingerprint. Compare it with '
        'the <code>info</code> output on the server and click <b>Trust</b>. The app then refuses any other certificate at that URL.</div>',
        '<div>Click the slot chip (for example <code>example-com</code>) to fill in <b>Slot on the agent</b>.</div>',
        '<div>Leave <b>Enabled</b> and <b>Deploy automatically after every issuance / renewal</b> ticked, then <b>Save target</b>.</div>',
        '<div>Click <b>Deploy now</b> to send the current certificate straight away.</div>',
    ]))
    add('<p>These four volumes are also built into the app: click <b>Help</b> at the bottom of the sidebar, or press <b>F1</b>.</p>')

    add('<h2 id="verify"><span class="k">Step 5</span>Check the first deployment</h2>')
    add('<p>The app\'s <b>Activity log</b> shows one line per agent step. A successful run looks like this:</p>')
    add(code('''NetTalk web: wrote www.example.com.crt, www.example.com.key to C:\\Apps\\Web\\certificates
Restarted service WebServer''', 'text'))
    add('<p>On the server, the same lines are in <code>agent.log</code> next to the executable, and the previous files are in <code>backups\\example-com\\NetTalk_web\\&lt;date-time&gt;\\</code>. Check what the server now presents:</p>')
    add(code('''openssl s_client -connect www.example.com:443 -servername www.example.com </dev/null 2>/dev/null \\
  | openssl x509 -noout -subject -issuer -enddate''', 'bash'))

    add('<h2 id="update"><span class="k">Maintenance</span>Update, move or remove the agent</h2>')
    add('<h3 id="update-win">Update on Windows</h3>')
    add(code(r'''sc.exe stop %s
Copy-Item .\CertificateGet.Agent.exe "C:\Program Files\CertificateGet Agent\" -Force
sc.exe start %s''' % (SERVICE, SERVICE), 'powershell'))
    add('<h3 id="update-linux">Update on Linux</h3>')
    add(code('''sudo systemctl stop %s
sudo cp /tmp/CertificateGet.Agent /opt/certificateget-agent/CertificateGet.Agent
sudo restorecon -v /opt/certificateget-agent/CertificateGet.Agent    # SELinux systems only
sudo systemctl start %s''' % (UNIT, UNIT), 'bash'))
    add('<h3 id="remove">Remove or reinstall</h3>')
    add('''<p><code>uninstall</code> removes the service and (on Windows) the firewall rule, and keeps
<code>agent.json</code>, the TLS certificate and the backups. Running <code>install</code> again keeps the existing
configuration and API key. If the TLS certificate is new (a fresh folder or another server), open the target in the
app, click <b>Forget fingerprint</b>, and trust the new one on the next connect.</p>''')

    add('<h2 id="next1"><span class="k">Getting Started</span>Where to go next</h2>')
    add(nextcards([TG, PG, RF]))

    nav = APP_NAV_GS + [
           ('Agent: install', ['what', 'build', 'win', 'win-what', 'linux', 'linux-what']),
           ('Agent: configure', ['slot', 'app', 'verify']),
           ('Agent: maintenance', ['update', 'update-win', 'update-linux', 'remove']),
           ('', ['next1'])]
    return page(GS, 'CertificateGet Getting Started', 'Volume 1', 'Getting Started',
                'Install the app and request your first certificate, then install the agent on your Windows or Linux '
                'servers, describe where the certificate goes, and connect the app to it.',
                ['<b>First</b> certificate', '<b>Windows</b> service', '<b>Linux</b> systemd', '<b>HTTPS</b> port 9443'],
                nav, ''.join(B))

# =====================================================================
#  2  PROGRAMMER'S GUIDE
# =====================================================================
def build_programmers_guide():
    B = []; add = B.append
    app_programmers_guide(add)

    add('<h2 id="model"><span class="k">Agent concepts</span>Slots, destinations and files</h2>')
    add('''<p>Three levels describe where a certificate goes on a server.</p>''')
    add(table(['Level', 'What it is', 'Typical count'], [
        ['<b>Slot</b>', 'One certificate as the server sees it. A deployment target in the app names one slot.', 'One per certificate, for example <code>reddin-wildcard</code>.'],
        ['<b>Destination</b>', 'One program that uses the certificate: a folder and file names, or a TSplus import, plus what to restart.', 'One per program instance.'],
        ['<b>File</b>', 'A <i>source</i> (the format, such as <code>fullchain</code>) and the file name to give it.', 'One to three per destination.'],
    ]))
    add('''<p>The app never decides folders or file names for an agent. It asks the agent which sources the slot
needs and sends those. When you add a NetTalk instance or change a file name, you change
<code>agent.json</code> on the server and nothing in the app.</p>''')
    add('''<p>One agent can hold many slots. Two certificates deployed to the same server are two slots in the same
file, and two targets in the app (one on each certificate) pointing at the same agent URL.</p>''')

    add('<h2 id="flow"><span class="k">Concepts</span>How a deployment runs</h2>')
    add('<p>The order matters when you write commands, so here it is exactly as the agent runs it.</p>')
    add(flow([
        ('1 · Ask', 'The app calls <code>GET /api/slots/{slot}</code> and gets the list of sources the slot needs.', ''),
        ('2 · Send', 'The app builds those files from the issued certificate and posts them in Base64 to <code>/api/deploy</code>, with the PFX password only when a <code>pfx</code>, <code>p12</code> or <code>encrypted-key</code> is needed.', ''),
        ('3 · Admit', 'The agent re-reads <code>agent.json</code>, checks <code>AllowedIps</code> and the API key. Deployments run one at a time.', 'srv'),
        ('4 · Write', 'For each destination in order: back up the current file, write the new one atomically. TSplus destinations import instead.', 'srv'),
        ('5 · Services', 'Every <code>RestartServices</code> entry of the successful destinations, each service once, in the order first listed.', 'srv'),
        ('6 · Programs', 'Every <code>RestartPrograms</code> entry (Windows), each executable once.', 'srv'),
        ('7 · Commands', 'Each destination\'s <code>Commands</code> in order, with <code>{folder}</code> replaced, then the slot\'s own <code>Commands</code>.', 'srv'),
        ('8 · Report', 'Every step, good or bad, goes back to the app\'s activity log and into <code>agent.log</code>.', ''),
    ]))
    add(note('warn', 'Commands run after all restarts',
        '<p>A destination\'s <code>Commands</code> do not run right after its own files are written; they run after '
        'every destination has been written and every service restarted. If a command must happen before a restart, '
        'leave <code>RestartServices</code> empty and do both in one command, for example '
        '<code>icacls … &amp;&amp; net stop X &amp;&amp; net start X</code>.</p>'))
    add('<h3 id="failures">When a step fails</h3>')
    add('''<p>A failed step does not stop the run. If writing a destination fails, that destination's restarts and
commands are skipped, and the other destinations carry on. A failed restart or a command with a non-zero exit code
is reported, and the next step still runs. The deployment is then marked failed in the app, with every step
listed, so you can see which part worked.</p>''')

    add('<h2 id="hot"><span class="k">Concepts</span>Editing agent.json on a live server</h2>')
    add('''<p>The agent reads <code>agent.json</code> again on every request. A new slot, destination or file name
applies to the next deployment without a restart. Only <code>Port</code> is read at start-up; after changing it,
restart the service and open the new port in the firewall.</p>''')
    add('''<p>A syntax error does not stop the service. Requests are refused with a clear message, which the app shows,
until the file is fixed. <code>check</code> prints the line:</p>''')
    add(code('''ERROR agent.json has a syntax error on line 14 (at $.Slots[0].Destinations[0].Folder):
        "Folder": "C:\\Apps\\Web\\certificates",
Common causes: text not inside "quotes", curly quotes instead of straight ", a missing comma between items,
or a comma after the last item. Backslashes in Windows paths must be doubled (C:\\\\Apps).''', 'text'))

    add('<h2 id="writes"><span class="k">Files</span>How files are written</h2>')
    add(ul_list([
        '<b>Atomically.</b> Each file is written to <code>&lt;name&gt;.cg-tmp</code> and then renamed over the old one, so a program never reads half a file.',
        '<b>Folders are created</b> when they do not exist.',
        '<b>Linux, new files:</b> key material (%s) gets mode 600; other files get 644.' % ', '.join('<code>%s</code>' % s for s in sorted(SECRETS)),
        '<b>Linux, replaced files</b> keep the owner, group, mode and SELinux label of the file they replace (<code>chown --reference</code>, <code>chcon --reference</code>). A Cockpit certificate set to <code>root:cockpit-ws 640</code> stays that way.',
        '<b>Windows</b> files inherit the folder\'s permissions.',
    ]))

    add('<h2 id="backups"><span class="k">Files</span>Backups</h2>')
    add('''<p>Before a file is replaced, the current one is copied into the agent's own folder, never next to the
live files. HAProxy loads every file in its certificate folder, so a <code>.bak</code> there would be served too.</p>''')
    add(code('''<agent folder>/backups/<slot>/<destination>/<yyyyMMdd-HHmmss>/<file>
C:\\Program Files\\CertificateGet Agent\\backups\\reddin-wildcard\\NetTalk_-_Portal\\20260927-031200\\portal.crt''', 'text'))
    add('''<p><code>KeepBackups</code> (default 5) is how many of those dated folders are kept per destination; 0 turns
backups off. To roll back, copy the files from a dated folder back into place and restart the program, or run the
destination's command by hand.</p>''')

    add('<h2 id="restarts"><span class="k">Restarts</span>Services, programs and commands</h2>')
    add('<h3 id="services">RestartServices</h3>')
    add('''<p>On Windows, the service is stopped (waiting up to 90 seconds), then started (waiting up to 90 seconds for
<i>Running</i>). On Linux, the agent runs <code>systemctl restart &lt;unit&gt;</code>. Each name is restarted once
per deployment, even when several destinations list it.</p>''')
    add(note('info', 'Reload rather than restart where you can',
        '<p>A restart drops open connections. For HAProxy and nginx, leave <code>RestartServices</code> empty and use a '
        '<code>Commands</code> entry that checks the configuration and reloads, for example '
        '<code>haproxy -c -f /etc/haproxy/haproxy.cfg &amp;&amp; systemctl reload haproxy</code>.</p>'))
    add('<h3 id="programs">RestartPrograms</h3>')
    add('''<p>For programs that run on a desktop instead of as a service, such as a NetTalk server started from the
Startup folder. For every running copy of the executable, the agent:</p>''')
    add('<ol class="b">%s</ol>' % ''.join('<li>%s</li>' % s for s in [
        'notes its full command line and the Windows session (desktop) it runs in,',
        'asks it to close normally, as if its close button were clicked,',
        'ends it if it is still running after <code>StopTimeoutSeconds</code> (default 20),',
        'starts it again in the same session, as the same user, with the same command line,',
        'checks that it is still running three seconds later.']))
    add('''<p>Several copies with different parameters each come back with their own parameters. A user must be
logged on, at the console or in a disconnected RDP session, for a program to come back on a desktop. On
unattended servers use automatic logon, or <code>"StartIn": "Background"</code> when no window is needed.</p>''')
    add('<h3 id="commands">Commands</h3>')
    add(table(['', 'Windows', 'Linux'], [
        ['Shell', '<code>cmd.exe /c &lt;command&gt;</code>', '<code>/bin/sh -c "&lt;command&gt;"</code>'],
        ['Runs as', 'LocalSystem', 'root'],
        ['Time limit', '2 minutes, then the process tree is ended', '2 minutes'],
        ['Fails when', 'exit code is not 0', 'exit code is not 0'],
        ['Output', 'first 500 characters go to the log', 'first 500 characters go to the log'],
    ]))
    add('''<p><code>{folder}</code> is replaced with the destination's <code>Folder</code>. In the slot's own
<code>Commands</code> there is no folder, so <code>{folder}</code> becomes empty there. Use <code>&amp;&amp;</code>
to stop at the first failure, and <code>;</code> to carry on regardless; the step's result is the exit code of the
last command that ran.</p>''')

    add('<h2 id="security"><span class="k">Security</span>Security model</h2>')
    add(table(['Protection', 'How it works'], [
        ['TLS only', 'The agent listens on HTTPS with its own certificate. The app pins its SHA-256 fingerprint on first connect, after you compare it with <code>info</code>, and refuses a different one later.'],
        ['API key', 'Sent in the <code>X-Api-Key</code> header. The agent stores only its SHA-256 hash and compares it in constant time. A wrong key is logged and answered after a 1.5 second delay.'],
        ['AllowedIps', 'When the list is not empty, only those addresses may connect (loopback is always allowed). Put the IP of the PC that runs CertificateGet here.'],
        ['Request size', 'Bodies over %d MB are refused.' % BODYMB],
        ['One at a time', 'Deployments are queued, so two renewals never write the same files at once.'],
    ]))
    add(note('warn', 'Protect the agent folder',
        '<p>It holds <code>agent.json</code>, the agent\'s TLS key and the backups, which contain private keys. Keep it '
        'readable by administrators or root only. Anyone who can edit <code>agent.json</code> can make the agent run a '
        'command as LocalSystem or root.</p>'))
    add('<p>To rotate the API key, run <code>newkey</code> on the server and paste the new key into every app target that uses the agent.</p>')

    add('<h2 id="trouble"><span class="k">Operations</span>Troubleshooting</h2>')
    add(table(['What you see', 'Cause', 'Fix'], [
        ['<i>Invalid API key.</i>', 'The key in the app does not match, or <code>ApiKeyHash</code> is a placeholder.', 'Run <code>newkey</code> and paste the key into the target.'],
        ['<i>… is not allowed to use this agent.</i>', 'The app PC\'s IP is not in <code>AllowedIps</code>.', 'Add it. Behind NAT, use the address the agent sees (it is in <code>agent.log</code>).'],
        ['<i>The agent\'s TLS certificate does not match …</i>', 'The agent was reinstalled or its <code>agent-tls.pfx</code> replaced.', 'Compare with <code>info</code>, then <b>Forget fingerprint</b> in the target and connect again.'],
        ['<i>Slot "x" is not configured on …</i>', 'Name typed differently in the app and in <code>agent.json</code>.', 'Use <b>Connect &amp; list slots</b> and click the chip.'],
        ['<i>cannot read its configuration</i>', 'JSON syntax error in <code>agent.json</code>.', 'Run <code>check</code>; it names the line.'],
        ['Connection times out', 'Firewall, or the service is not running.', '<code>Test-NetConnection host -Port 9443</code> from the app PC; check the service.'],
        ['<code>status=203/EXEC</code> in systemd', 'SELinux label on a binary moved from <code>/tmp</code>.', '<code>sudo restorecon -v /opt/certificateget-agent/CertificateGet.Agent</code>, or run <code>install</code> again.'],
        ['<i>Windows service "x" not found</i> from <code>check</code>', 'The service <i>name</i> differs from its display name.', '<code>Get-Service | Where DisplayName -like "*Web*"</code> and use the <code>Name</code> column.'],
        ['<i>Command failed (exit 1)</i>', 'The command itself failed; its output follows in the message.', 'Run the same command by hand as root / an elevated prompt.'],
        ['Cockpit falls back to its self-signed certificate', 'The key is not readable by <code>cockpit-ws</code>, or another file sorts later.', 'See the %s.' % xref(TG, 'cockpit', 'Cockpit template')],
    ]))

    add('<h2 id="notes"><span class="k">Notes</span>Platform notes</h2>')
    add('<p>Behaviour that belongs to the operating system or to the program receiving the certificate, found while running the agent on real servers.</p>')
    add('<h3 id="n-selinux">SELinux and moved binaries</h3>')
    add('''<p>A file moved with <code>mv</code> keeps the SELinux label of the place it came from. A binary moved out of
<code>/tmp</code> is labelled <code>user_tmp_t</code>, and systemd fails to start it with <code>status=203/EXEC</code>
and no other message. <code>install</code> labels the agent <code>bin_t</code>; after an update copy with
<code>cp</code> or run <code>restorecon</code>. The same applies to certificate files you move into place by hand, which
is why the Cockpit command runs <code>restorecon -F</code>.</p>''')
    add('<h3 id="n-example">Copying the example file over agent.json</h3>')
    add('''<p>The shipped examples contain placeholders for <code>ApiKeyHash</code> and <code>TlsPassword</code>. The agent
survives it: when the TLS password does not open <code>agent-tls.pfx</code>, it renames the old file to
<code>agent-tls.pfx.old-&lt;time&gt;</code> and creates a new certificate (the app will ask you to trust the new
fingerprint), and <code>check</code> reports the missing key. Copy only the <code>Slots</code> section from an example.</p>''')
    add('<h3 id="n-haproxy">HAProxy loads every file in the crt folder</h3>')
    add('''<p>With <code>crt /etc/haproxy/certs/</code>, HAProxy loads each file in that folder and picks one by SNI. A
stray backup or a half-written file there is loaded too, or stops HAProxy from starting. That is why the agent writes
atomically and keeps backups elsewhere, and why the reload command runs <code>haproxy -c</code> first.</p>''')
    add('<h3 id="n-cockpit">Cockpit reads the certificate as cockpit-ws</h3>')
    add('''<p><code>cockpit-tls</code> runs as the <code>cockpit-ws</code> user, so the key (or the combined
<code>.cert</code>) must be <code>root:cockpit-ws</code> with mode 640. Cockpit uses the <i>last</i>
<code>.cert</code>/<code>.crt</code> file in alphabetical order in <code>/etc/cockpit/ws-certs.d</code>. After several
failed starts systemd stops trying, which is why the command runs <code>systemctl reset-failed</code> before restarting
the socket.</p>''')
    add('<h3 id="n-nettalk">NetTalk reads its certificate at start-up</h3>')
    add('''<p>A NetTalk web server loads the certificate and key when it starts listening, so new files have no effect
until the service or program restarts. Every NetTalk destination therefore needs a <code>RestartServices</code> or
<code>RestartPrograms</code> entry.</p>''')
    add('<h3 id="n-tsplus">TSplus needs a password on the PFX</h3>')
    add('''<p><code>CertificateManager.exe</code> reads the password from <code>certpassword.txt</code> in its own
folder and will not import a PFX without one. If the certificate has no PFX password in the app, the agent wraps it
with a random password for the import. Both temporary files are deleted afterwards. Versions that use
<code>cert.jks</code> expect the keystore password <code>secret</code>, the app's default.</p>''')
    add('<h3 id="n-session">Desktop programs and session 0</h3>')
    add('''<p>The agent runs as a service in session 0, which has no desktop. To bring a program back where the user can
see it, it starts it in the user's session with that user's token. With nobody logged on there is no such session, and
the program can only be started with <code>"StartIn": "Background"</code>.</p>''')

    add('<h2 id="next2"><span class="k">Programmer\'s Guide</span>Where to go next</h2>')
    add(nextcards([TG, RF, GS]))

    nav = APP_NAV_PG + [
           ('Agent: concepts', ['model', 'flow', 'failures', 'hot']),
           ('Agent: files', ['writes', 'backups']),
           ('Agent: restarts', ['restarts', 'services', 'programs', 'commands']),
           ('Agent: operations', ['security', 'trouble']),
           ('Agent: platform notes', ['notes', 'n-selinux', 'n-example', 'n-haproxy', 'n-cockpit', 'n-nettalk', 'n-tsplus', 'n-session']),
           ('', ['next2'])]
    return page(PG, "CertificateGet Programmer's Guide", 'Volume 2', "Programmer's Guide",
                'How the app validates, which certificate authority to use, how certificates, renewals, deployment and '
                'secrets work; then how an agent deployment runs to the last restart, its security model, '
                'troubleshooting and the platform behaviour behind each rule.',
                ['<b>%d</b> validation methods' % len(APP_METHODS), '<b>2</b> authorities', '<b>atomic</b> writes', '<b>7</b> platform notes'],
                nav, ''.join(B))

def ul_list(items):
    return '<ul class="b">%s</ul>' % ''.join('<li>%s</li>' % i for i in items)

# =====================================================================
#  3  TEMPLATE GUIDE
# =====================================================================
T_NETTALK_ONE = r'''{
  "Name": "www-example-com",
  "Description": "www.example.com for the NetTalk web server",
  "Destinations": [
    {
      "Name": "NetTalk web",
      "Folder": "C:\\Apps\\Web\\certificates",
      "Files": [
        { "Source": "fullchain", "FileName": "www.example.com.crt" },
        { "Source": "key",       "FileName": "www.example.com.key" }
      ],
      "RestartServices": [ "WebServer" ]
    }
  ]
}'''

T_NETTALK_MANY = r'''{
  "Name": "reddin-wildcard",
  "Description": "*.reddinassessments.com for three NetTalk servers",
  "Destinations": [
    {
      "Name": "NetTalk - Assessments",
      "Folder": "C:\\Apps\\Assessments\\certificates",
      "Files": [
        { "Source": "fullchain", "FileName": "reddinassessments.com.crt" },
        { "Source": "key",       "FileName": "reddinassessments.com.key" }
      ],
      "RestartServices": [ "AssessmentsWeb" ]
    },
    {
      "Name": "NetTalk - Portal",
      "Folder": "D:\\Portal\\certs",
      "Files": [
        { "Source": "fullchain", "FileName": "portal.crt" },
        { "Source": "key",       "FileName": "portal.key" }
      ],
      "RestartServices": [ "PortalWeb" ]
    },
    {
      "Name": "NetTalk - Reports",
      "Folder": "D:\\Reports\\certificates",
      "Files": [
        { "Source": "fullchain", "FileName": "reports.reddinassessments.com.crt" },
        { "Source": "key",       "FileName": "reports.reddinassessments.com.key" }
      ],
      "RestartServices": [ "ReportsWeb" ]
    }
  ]
}'''

T_NETTALK_DESKTOP = r'''{
  "Name": "api-example-com",
  "Description": "api.example.com, NetTalk server started on the desktop",
  "Destinations": [
    {
      "Name": "NetTalk - API (desktop program)",
      "Folder": "C:\\Apps\\Api\\certificates",
      "Files": [
        { "Source": "fullchain", "FileName": "api.crt" },
        { "Source": "key",       "FileName": "api.key" }
      ],
      "RestartPrograms": [
        { "Path": "C:\\Apps\\Api\\ApiServer.exe", "StopTimeoutSeconds": 30 }
      ]
    }
  ]
}'''

T_HAPROXY = r'''{
  "Name": "example-com",
  "Description": "HAProxy on lb01",
  "Destinations": [
    {
      "Name": "HAProxy",
      "Folder": "/etc/haproxy/certs",
      "Files": [
        { "Source": "combined", "FileName": "example.com.pem" }
      ],
      "Commands": [ "haproxy -c -f /etc/haproxy/haproxy.cfg && systemctl reload haproxy" ]
    }
  ]
}'''

HAPROXY_CFG = '''frontend https-in
    bind :443 ssl crt /etc/haproxy/certs/ alpn h2,http/1.1
    bind :80
    http-request redirect scheme https unless { ssl_fc }
    default_backend web'''

T_HAPROXY_TWO = r'''"Slots": [
  {
    "Name": "example-com",
    "Destinations": [
      { "Name": "HAProxy", "Folder": "/etc/haproxy/certs",
        "Files": [ { "Source": "combined", "FileName": "example.com.pem" } ],
        "Commands": [ "haproxy -c -f /etc/haproxy/haproxy.cfg && systemctl reload haproxy" ] }
    ]
  },
  {
    "Name": "example-org",
    "Destinations": [
      { "Name": "HAProxy", "Folder": "/etc/haproxy/certs",
        "Files": [ { "Source": "combined", "FileName": "example.org.pem" } ],
        "Commands": [ "haproxy -c -f /etc/haproxy/haproxy.cfg && systemctl reload haproxy" ] }
    ]
  }
]'''

COCKPIT_CMD_A = ('chgrp cockpit-ws {folder}/reddinassessments.cert && chmod 640 {folder}/reddinassessments.cert && '
                 'restorecon -F {folder}/reddinassessments.cert ; systemctl reset-failed cockpit.socket cockpit ; '
                 'systemctl restart cockpit.socket ; systemctl try-restart cockpit')

T_COCKPIT_A = r'''{
  "Name": "Cockpit",
  "Folder": "/etc/cockpit/ws-certs.d",
  "Files": [
    { "Source": "combined", "FileName": "reddinassessments.cert" }
  ],
  "Commands": [ "%s" ]
}''' % COCKPIT_CMD_A

T_COCKPIT_B = r'''{
  "Name": "Cockpit",
  "Folder": "/etc/cockpit/ws-certs.d",
  "Files": [
    { "Source": "fullchain", "FileName": "90-letsencrypt.crt" },
    { "Source": "key",       "FileName": "90-letsencrypt.key" }
  ],
  "Commands": [ "chgrp cockpit-ws {folder}/90-letsencrypt.key && chmod 640 {folder}/90-letsencrypt.key && restorecon -F {folder}/90-letsencrypt.* ; systemctl reset-failed cockpit.socket cockpit ; systemctl restart cockpit.socket ; systemctl try-restart cockpit" ]
}'''

T_TSPLUS15 = r'''{
  "Name": "tsplus-example-com",
  "Description": "remote.example.com for the TSplus web portal",
  "Destinations": [
    { "Name": "TSplus web portal", "Kind": "TSplus" }
  ]
}'''

T_TSPLUSJKS = r'''{
  "Name": "tsplus-example-com",
  "Description": "remote.example.com for an older TSplus (cert.jks)",
  "Destinations": [
    { "Name": "TSplus web portal", "Kind": "TSplusJks" }
  ]
}'''

T_TSPLUS_PATHS = r'''{ "Name": "TSplus (15+) on D:",   "Kind": "TSplus",    "TsplusCertFolder": "D:\\TSplus\\UserDesktop\\files\\cert" }
{ "Name": "TSplus (jks) on D:",   "Kind": "TSplusJks", "TsplusFolder": "D:\\TSplus" }'''

def build_template_guide():
    B = []; add = B.append
    app_template_guide(add)

    add('<h2 id="anatomy"><span class="k">agent.json</span>The shape of the file</h2>')
    add('''<p><code>agent.json</code> sits next to the executable. The top of the file is written by
<code>install</code> and rarely changes; your work is in <code>Slots</code>. Each template below is one slot (or one
destination) to paste into that list.</p>''')
    add(code(r'''{
  "Port": 9443,                         // HTTPS port; restart the service after changing it
  "ApiKeyHash": "…",                    // written by install / newkey — do not edit
  "AllowedIps": [ "192.168.1.50" ],     // the PC running CertificateGet; [] = any
  "TlsPfx": "agent-tls.pfx",            // the agent's own TLS certificate
  "TlsPassword": "…",                   // written by the agent — do not edit
  "KeepBackups": 5,                     // dated backup folders kept per destination
  "Slots": [
    {
      "Name": "example-com",            // what the app's deployment target names
      "Description": "…",               // shown in the app
      "Destinations": [
        {
          "Name": "…",                  // shown in the log; also the backup folder name
          "Kind": "Files",              // Files (default), TSplus or TSplusJks
          "Folder": "…",                // created when missing
          "Files": [ { "Source": "fullchain", "FileName": "…" } ],
          "RestartServices": [],        // Windows service names / systemd units
          "RestartPrograms": [],        // Windows desktop programs
          "Commands": []                // cmd.exe on Windows, /bin/sh on Linux; {folder} = Folder
        }
      ],
      "Commands": []                    // run once, after all destinations
    }
  ]
}'''))
    add(note('warn', 'Comments are for reading only',
        '<p>JSON has no comments. The <code>//</code> notes above explain the fields; the real file must not contain them. '
        'The templates below are plain JSON and can be pasted as they are.</p>'))
    add('<p>After every change run <code>check</code>. Every field is described in the %s.</p>' % xref(RF, 'root', 'Reference'))

    # ---- NetTalk
    add('<h2 id="nettalk"><span class="k">Windows</span>NetTalk</h2>')
    add('''<p>A NetTalk web server reads a certificate file and a private key file, named in the WebServer
procedure's SSL settings. Each instance commonly uses its own <code>certificates</code> folder with
<code>&lt;domain&gt;.crt</code> and <code>&lt;domain&gt;.key</code>. Write <code>fullchain</code> as the certificate
so browsers get the intermediate, write <code>key</code> as the key, and restart the instance, because NetTalk
loads the files at start-up.</p>''')
    add(table(['NetTalk setting', 'Source', 'Example file'], [
        ['Certificate file', '<code>fullchain</code>', '<code>certificates\\www.example.com.crt</code>'],
        ['Private key file', '<code>key</code>', '<code>certificates\\www.example.com.key</code>'],
        ['Restart', '<code>RestartServices</code> or <code>RestartPrograms</code>', 'Service name, or the path of the <code>.exe</code>'],
    ]))
    add('<h3 id="nettalk-one">One NetTalk service</h3>')
    add('<p>The service name is the <i>Name</i> column of <code>Get-Service</code>, not the display name.</p>')
    add(code(T_NETTALK_ONE))
    add('<h3 id="nettalk-many">Several NetTalk instances, one certificate</h3>')
    add('''<p>A wildcard certificate used by several NetTalk servers is one slot with one destination per instance, each
with the file names that instance is configured with. The certificate is sent once and written three times.</p>''')
    add(code(T_NETTALK_MANY))
    add('<h3 id="nettalk-desktop">NetTalk running as a desktop program</h3>')
    add('''<p>When the server is started from a logged-on desktop instead of as a service, restart it with
<code>RestartPrograms</code>. Every running copy is closed and started again with its own command line, on the same
desktop.</p>''')
    add(code(T_NETTALK_DESKTOP))
    add('<p>If the program must stay hidden and no one is logged on, add <code>"StartIn": "Background"</code>. Details are in %s.</p>'
        % xref(PG, 'programs', 'RestartPrograms'))

    # ---- HAProxy
    add('<h2 id="haproxy"><span class="k">Linux</span>HAProxy</h2>')
    add('''<p>HAProxy wants the certificate, the chain and the key in one PEM file, which is the <code>combined</code>
source. Point the <code>bind</code> line at the folder, and HAProxy loads every file in it and picks one by SNI.</p>''')
    add(code(HAPROXY_CFG, 'haproxy'))
    add('<h3 id="haproxy-one">One certificate</h3>')
    add(code(T_HAPROXY))
    add('''<p>The command checks the whole configuration with the new file before reloading. If the check fails, the
reload does not run, HAProxy keeps serving with the certificate it already has, and the app shows the error.
<code>reload</code> keeps open connections; avoid <code>RestartServices: ["haproxy"]</code>.</p>''')
    add('<h3 id="haproxy-many">Several certificates in the same folder</h3>')
    add('<p>Each certificate is its own slot with its own file name. Each certificate in the app gets a target pointing at its slot.</p>')
    add(code(T_HAPROXY_TWO))

    # ---- Cockpit
    add('<h2 id="cockpit"><span class="k">Linux</span>Cockpit</h2>')
    add('''<p>Cockpit takes its certificate from <code>/etc/cockpit/ws-certs.d</code>. It uses the <b>last</b>
<code>.cert</code> or <code>.crt</code> file in alphabetical order; a <code>.cert</code> file holds the chain and key
together, and a <code>.crt</code> file needs a <code>.key</code> file of the same name. The key must be readable by
the <code>cockpit-ws</code> group.</p>''')
    add('<h3 id="cockpit-replace">Replace the certificate Cockpit already uses</h3>')
    add('''<p>If a certificate is already there, for example <code>reddinassessments.cert</code>, overwrite it with
<code>combined</code>. The agent keeps its owner, group, mode and SELinux label, and the command sets them again
for a first deployment.</p>''')
    add(code(T_COCKPIT_A))
    add('<h3 id="cockpit-new">Add a certificate next to the self-signed one</h3>')
    add('''<p>Otherwise write <code>fullchain</code> and <code>key</code> as <code>90-letsencrypt.crt</code> and
<code>90-letsencrypt.key</code>. They sort after Cockpit's <code>0-self-signed.cert</code>, so Cockpit picks them.</p>''')
    add(code(T_COCKPIT_B))
    add('<h3 id="cockpit-cmd">What the command does</h3>')
    add(table(['Part', 'Why'], [
        ['<code>chgrp cockpit-ws … &amp;&amp; chmod 640 …</code>', 'The key must be readable by <code>cockpit-tls</code>, which runs as <code>cockpit-ws</code>.'],
        ['<code>restorecon -F …</code>', 'Resets the SELinux label. Not installed on Debian/Ubuntu; the <code>;</code> after it lets the rest run anyway.'],
        ['<code>systemctl reset-failed cockpit.socket cockpit</code>', 'Clears systemd\'s start limit after earlier failures, so the restart is not refused.'],
        ['<code>systemctl restart cockpit.socket</code>', 'Cockpit is socket-activated; restarting the socket makes the next connection load the new certificate.'],
        ['<code>systemctl try-restart cockpit</code>', 'Restarts the running service only if it is running, and ends with exit code 0 so the step succeeds.'],
    ]))
    add('<p>To see which certificate Cockpit will use:</p>')
    add(code('''sudo /usr/lib/cockpit/cockpit-certificate-ensure --check      # Debian / Ubuntu
sudo /usr/libexec/cockpit-certificate-ensure --check         # RHEL / Rocky / Alma / Fedora''', 'bash'))

    # ---- TSplus
    add('<h2 id="tsplus"><span class="k">Windows</span>TSplus</h2>')
    add('''<p>TSplus has two ways of taking a certificate, depending on the version. Both are a destination with a
<code>Kind</code> and no files; the agent knows the paths.</p>''')
    add(table(['Kind', 'TSplus version', 'What the agent does'], [
        ['<code>TSplus</code>', '15 and later', 'Writes a temporary PFX and <code>certpassword.txt</code>, runs <code>%s\\CertificateManager.exe /add &lt;pfx&gt;</code>, deletes both.' % esc(CONSTS['DefaultTsplusCertFolder'])],
        ['<code>TSplusJks</code>', 'Versions that use <code>cert.jks</code>', 'Backs up and replaces <code>%s\\Clients\\webserver\\cert.jks</code> (password <code>secret</code>, full chain), then runs <code>UserDesktop\\files\\AdminTool.exe /webrestart</code>.' % esc(CONSTS['DefaultTsplusFolder'])],
    ]))
    add('<h3 id="tsplus-15">TSplus 15 and later</h3>')
    add(code(T_TSPLUS15))
    add('<h3 id="tsplus-jks">Older TSplus with cert.jks</h3>')
    add(code(T_TSPLUSJKS))
    add('<p>The JKS password comes from the app\'s <b>Settings</b> (<i>JKS keystore password</i>). Leave it at <code>secret</code>; TSplus expects that value.</p>')
    add('<h3 id="tsplus-paths">TSplus installed somewhere else</h3>')
    add(code(T_TSPLUS_PATHS))
    add('<p><code>check</code> confirms that <code>CertificateManager.exe</code> or the <code>Clients\\webserver</code> folder is where the destination says.</p>')

    # ---- together
    add('<h2 id="together"><span class="k">Complete files</span>Several programs on one server</h2>')
    add('''<p>These are the example files shipped with the agent (<code>agent.example.json</code> in each build folder),
printed here from the repository. The first puts one wildcard certificate into three NetTalk instances, one of them a
desktop program, and into TSplus. The second serves HAProxy and Cockpit on one Linux machine.</p>''')
    add('<h3 id="together-win">Windows: NetTalk and TSplus</h3>')
    add(code(EX_WIN.strip()))
    add('<p>A server has only one TSplus, so in practice keep just one of the two TSplus destinations: <code>TSplus</code> for version 15 and later, <code>TSplusJks</code> for older versions.</p>')
    add('<h3 id="together-linux">Linux: HAProxy and Cockpit</h3>')
    add(code(EX_LINUX.strip()))

    add('<h2 id="next3"><span class="k">Template Guide</span>Where to go next</h2>')
    add(nextcards([RF, PG, GS]))

    nav = APP_NAV_TG + [('agent.json', ['anatomy']),
           ('NetTalk', ['nettalk', 'nettalk-one', 'nettalk-many', 'nettalk-desktop']),
           ('HAProxy', ['haproxy', 'haproxy-one', 'haproxy-many']),
           ('Cockpit', ['cockpit', 'cockpit-replace', 'cockpit-new', 'cockpit-cmd']),
           ('TSplus', ['tsplus', 'tsplus-15', 'tsplus-jks', 'tsplus-paths']),
           ('Complete files', ['together', 'together-win', 'together-linux']),
           ('', ['next3'])]
    return page(TG, 'CertificateGet Template Guide', 'Volume 3', 'Template Guide',
                'Recipes for common setups in the app, then ready-made <code>agent.json</code> slots for NetTalk, HAProxy, '
                'Cockpit and TSplus, with the reasoning behind each file name and command.',
                ['<b>IIS</b> &amp; SFTP recipes', '<b>NetTalk</b> service &amp; desktop', '<b>HAProxy</b> combined PEM', '<b>Cockpit</b> ws-certs.d', '<b>TSplus</b> 15+ &amp; jks'],
                nav, ''.join(B))

# =====================================================================
#  4  REFERENCE
# =====================================================================
JSON_TYPE = {'int': 'number', 'string': 'string', 'string?': 'string, optional', 'bool': 'true / false',
             'DateTime': 'date-time string'}

JSON_TYPE_ES = {'int': 'número', 'string': 'cadena', 'string?': 'cadena, opcional', 'bool': 'true / false',
                'DateTime': 'cadena de fecha y hora'}

def jtype(t):
    es = LANG == 'es'
    if t in JSON_TYPE: return (JSON_TYPE_ES if es else JSON_TYPE)[t]
    m = re.match(r'List<(\w+)>', t)
    if m:
        if es: return 'arreglo de %s' % ('cadenas' if m.group(1) == 'string' else m.group(1))
        return 'array of %s' % ('strings' if m.group(1) == 'string' else m.group(1))
    m = re.match(r'Dictionary<string, string>', t)
    if m: return 'objeto (nombre &rarr; cadena)' if es else 'object (name &rarr; string)'
    return t.rstrip('?') + ((', opcional' if es else ', optional') if t.endswith('?') else '')

def jdefault(d, t):
    if not d: return ('ninguno' if LANG == 'es' else 'none') if t.endswith('?') else ''
    if d.startswith('new'): return '[]' if t.startswith('List') else '{}'
    if d.startswith('Guid') or '(' in d: return ''
    return d

def field_rows(clsname, prefix_json=True):
    cls = CLASSES[clsname]
    rows = []
    for pr in cls['props']:
        key = '%s.%s' % (clsname, pr['name'])
        if LANG == 'es':
            #  Spanish has no source summaries to fall back on: every field needs an entry.
            doc = FIELD_DOC_ES.get(key, '')
            if not doc: PROBLEMS.append('es/reference: %s has no FIELD_DOC_ES entry' % key)
        else:
            doc = FIELD_DOC.get(key) or esc(pr['doc'])
            if not doc: PROBLEMS.append('reference: %s has no /// summary and no FIELD_DOC entry' % key)
        use = USAGE.get(key)
        if use is None: MISSING.append(key)
        d = jdefault(pr['default'], pr['type'])
        name = pr['name'] if prefix_json else pr['name'][0].lower() + pr['name'][1:]
        rows.append('<tr class="fn" data-k="%s"><td class="fn__n"><code>%s</code></td>'
                    '<td class="fn__s"><code>%s</code>%s<p class="fn__d">%s</p>%s</td></tr>'
                    % (esc((clsname + ' ' + pr['name'] + ' ' + re.sub('<[^>]+>', '', doc)).lower()), esc(name),
                       jtype(pr['type']), '<span class="dflt">%s %s</span>' % ('predeterminado' if LANG == 'es' else 'default', esc(d)) if d else '',
                       doc, usecode(use) if use else ''))
    return '<div class="tw"><table class="fns"><tbody>%s</tbody></table></div>' % ''.join(rows)

def build_reference():
    B = []; add = B.append
    app_reference(add)

    add('<h2 id="root"><span class="k">agent.json</span>Top level</h2>')
    add('<p>%s Names are matched without regard to case.</p>' % esc(CLASSES['AgentConfig']['doc']))
    add(field_rows('AgentConfig'))
    add('<h3 id="slot">Slot</h3>')
    add('<p>%s An entry of <code>Slots</code>.</p>' % esc(CLASSES['Slot']['doc']))
    add(field_rows('Slot'))
    add('<h3 id="dest">Destination</h3>')
    add('<p>One program that uses the certificate. An entry of a slot\'s <code>Destinations</code>.</p>')
    add(field_rows('Destination'))
    add('<h3 id="file">File entry</h3>')
    add('<p>An entry of a destination\'s <code>Files</code>.</p>')
    add(field_rows('FileSpec'))
    add('<h3 id="prog">Program entry</h3>')
    add('<p>%s An entry of a destination\'s <code>RestartPrograms</code>. Windows only.</p>' % esc(CLASSES['ProgramSpec']['doc']))
    add(field_rows('ProgramSpec'))

    add('<h2 id="values"><span class="k">Values</span>Allowed values</h2>')
    add('<h3 id="sources">File sources</h3>')
    add('<p>The values <code>check</code> accepts for <code>Source</code>. Sources marked <i>secret</i> contain the private key and are written with mode 600 on Linux when new.</p>')
    rows = []
    for s in SOURCES:
        if s not in SOURCE_DOC:
            PROBLEMS.append('reference: source "%s" has no SOURCE_DOC entry' % s); continue
        fmt, d = SOURCE_DOC[s]
        tag = '<span class="tag tag--warn">secret</span>' if s in SECRETS else ''
        rows.append(['<code>%s</code>%s' % (s, tag), esc(fmt.replace(', secret', '')), d])
    for s in SOURCE_DOC:
        if s not in SOURCES: PROBLEMS.append('reference: SOURCE_DOC lists "%s", which the agent does not accept' % s)
    add(table(['Source', 'Format', 'Contents'], rows))
    add('<h3 id="kinds">Destination kinds</h3>')
    rows = []
    for k in KINDS:
        if k not in KIND_DOC: PROBLEMS.append('reference: Kind "%s" has no KIND_DOC entry' % k); continue
        rows.append(['<code>%s</code>' % k, KIND_DOC[k]])
    add(table(['Kind', 'Behaviour'], rows))
    add(table(['Default path', 'Value'], [['<code>TsplusCertFolder</code>', '<code>%s</code>' % esc(CONSTS['DefaultTsplusCertFolder'])],
                                          ['<code>TsplusFolder</code>', '<code>%s</code>' % esc(CONSTS['DefaultTsplusFolder'])]]))
    add('<h3 id="startin">StartIn values</h3>')
    rows = []
    for s in STARTINS:
        if s not in STARTIN_DOC: PROBLEMS.append('reference: StartIn "%s" has no STARTIN_DOC entry' % s); continue
        rows.append(['<code>%s</code>' % STARTIN_DOC[s][0], STARTIN_DOC[s][1]])
    add(table(['StartIn', 'Where the program comes back'], rows))
    add('<h3 id="placeholders">Placeholders</h3>')
    add(table(['Placeholder', 'Where', 'Replaced with'], [
        ['<code>{folder}</code>', 'A destination\'s <code>Commands</code>', 'That destination\'s <code>Folder</code>'],
        ['<code>{folder}</code>', 'A slot\'s <code>Commands</code>', 'Nothing (empty)'],
    ]))

    add('<h2 id="cli"><span class="k">Command line</span>Commands</h2>')
    add('<p>Run from the agent\'s folder, as administrator on Windows or with <code>sudo</code> on Linux. Without a command the agent runs in the console, as <code>run</code> does.</p>')
    rows = []
    for c in CLI:
        if c['name'] not in CLI_USE:
            PROBLEMS.append('reference: command "%s" has no CLI_USE entry' % c['name']); MISSING.append('cli ' + c['name']); continue
        use, more = CLI_USE[c['name']]
        rows.append('<tr class="fn" data-k="%s"><td class="fn__n"><code>%s%s</code></td><td class="fn__s">'
                    '<p class="fn__d" style="margin:0">%s</p><p class="fn__d">%s</p>'
                    '<pre class="code code--use code--sh" data-lang="use"><code>%s</code></pre></td></tr>'
                    % (esc(c['name'] + ' ' + c['doc']).lower(), esc(c['name']), (' ' + esc(c['arg'])) if c['arg'] else '',
                       esc(c['doc'][0].upper() + c['doc'][1:]) + '.', more, esc(use)))
    for k in CLI_USE:
        if k not in [c['name'] for c in CLI]: PROBLEMS.append('reference: CLI_USE lists "%s", which the agent does not have' % k)
    add('<div class="tw"><table class="fns"><tbody>%s</tbody></table></div>' % ''.join(rows))
    add(table(['Name', 'Value'], [['Windows service', '<code>%s</code>' % SERVICE], ['systemd unit', '<code>%s</code>' % UNIT],
                                  ['Firewall rule (Windows)', '<i>%s</i>' % FIREWALL]]))

    add('<h2 id="api"><span class="k">Protocol</span>HTTP API</h2>')
    add('''<p>What the app sends. Every request needs the <code>X-Api-Key</code> header and must come from an allowed IP.
Errors come back as <code>{ "error": "…" }</code> with status 401 (key), 403 (IP), 404 (slot) or 500
(<code>agent.json</code> unreadable).</p>''')
    rows = []
    for e in ENDPOINTS:
        if e['path'] not in ENDPOINT_DOC:
            PROBLEMS.append('reference: endpoint %s has no ENDPOINT_DOC entry' % e['path']); continue
        d, ex = ENDPOINT_DOC[e['path']]
        rows.append('<tr class="fn" data-k="%s"><td class="fn__n"><code>%s</code></td><td class="fn__s"><code>%s</code>'
                    '<p class="fn__d">%s</p><pre class="code code--use code--resp" data-lang="use"><code>%s</code></pre></td></tr>'
                    % (esc(e['path']).lower(), e['verb'], esc(e['path']), d, _hl_json(ex)))
    add('<div class="tw"><table class="fns"><tbody>%s</tbody></table></div>' % ''.join(rows))
    add('<h3 id="req">Deploy request</h3>')
    add('<p>The body of <code>POST /api/deploy</code>. Property names are camel-case on the wire.</p>')
    add(field_rows('DeployRequest', prefix_json=False))
    add('<h3 id="certinfo">Certificate info</h3>')
    add('<p>The <code>certificate</code> object inside the request.</p>')
    add(field_rows('CertInfo', prefix_json=False))

    add('<h2 id="disk"><span class="k">On disk</span>Files the agent keeps</h2>')
    add(table(['Path (next to the executable)', 'Contents'], [
        ['<code>agent.json</code>', 'The configuration. Mode 600 on Linux.'],
        ['<code>agent-tls.pfx</code>', 'The agent\'s TLS certificate and key. Mode 600 on Linux.'],
        ['<code>agent.log</code>', 'Every request, step and error, one line each. On Linux also in <code>journalctl -u %s</code>.' % UNIT],
        ['<code>backups/&lt;slot&gt;/&lt;destination&gt;/&lt;yyyyMMdd-HHmmss&gt;/</code>', 'Previous versions of replaced files; <code>KeepBackups</code> folders per destination.'],
        ['<code>work/</code>', 'Temporary PFX for a TSplus import, deleted afterwards.'],
    ]))

    add('<h2 id="next4"><span class="k">Reference</span>Where to go next</h2>')
    add(nextcards([GS, PG, TG]))

    nav = APP_NAV_RF + [
           ('Agent: agent.json', ['root', 'slot', 'dest', 'file', 'prog']),
           ('Agent: values', ['values', 'sources', 'kinds', 'startin', 'placeholders']),
           ('Agent: operation', ['cli', 'api', 'req', 'certinfo', 'disk']),
           ('', ['next4'])]
    nfields = sum(len(CLASSES[c]['props']) for c in ('AgentConfig', 'Slot', 'Destination', 'FileSpec', 'ProgramSpec'))
    return page(RF, 'CertificateGet Reference', 'Volume 4', 'Reference',
                'The app\'s file formats, validation methods and settings, then every <code>agent.json</code> field, '
                'allowed value, command and endpoint, all generated from the sources.',
                ['<b>%d</b> formats' % len(APP_FORMATS), '<b>%d</b> settings' % len(APP_SETTINGS), '<b>%d</b> agent fields' % nfields, '<b>%d</b> sources' % len(SOURCES), '<b>%d</b> commands' % len(CLI),
                 '<b>%d</b> endpoints' % len(ENDPOINTS)],
                nav, ''.join(B), showfilter=True)

# =====================================================================
#  The app's chapters, then the Spanish volumes: same helpers, same checks.
exec(compile(io.open(os.path.join(ROOT, 'docs', 'build-docs-app.py'), encoding='utf-8').read(),
             'build-docs-app.py', 'exec'))
exec(compile(io.open(os.path.join(ROOT, 'docs', 'build-docs-app-es.py'), encoding='utf-8').read(),
             'build-docs-app-es.py', 'exec'))
exec(compile(io.open(os.path.join(ROOT, 'docs', 'build-docs-es.py'), encoding='utf-8').read(),
             'build-docs-es.py', 'exec'))

if __name__ == '__main__':
    total = 0
    for LANG, builds, folder in (
            ('en', ((GS, build_getting_started), (PG, build_programmers_guide),
                    (TG, build_template_guide), (RF, build_reference)), 'docs/'),
            ('es', ((GS, build_getting_started_es), (PG, build_programmers_guide_es),
                    (TG, build_template_guide_es), (RF, build_reference_es)), 'docs/es/')):
        for fn, build in builds:
            kb = build()
            print('  %-29s %6.1f KB' % (folder + fn, kb / 1024.0))
            total += kb
    print('  %-29s %6.1f KB' % ('eight volumes (en + es)', total / 1024.0))
    #  A Spanish volume carries exactly the English volume's headings, in the same order.
    for fn in (GS, PG, TG, RF):
        en, es = HEADINGS.get(('en', fn), []), HEADINGS.get(('es', fn), [])
        if en != es:
            PROBLEMS.append('es/%s: headings differ from the English volume (missing %s, extra %s)'
                            % (fn, sorted(set(en) - set(es)) or '-', sorted(set(es) - set(en)) or '-'))
    for name, en_, es_ in (('FIELD_DOC', FIELD_DOC, FIELD_DOC_ES), ('SOURCE_DOC', SOURCE_DOC, SOURCE_DOC_ES),
                           ('KIND_DOC', KIND_DOC, KIND_DOC_ES), ('STARTIN_DOC', STARTIN_DOC, STARTIN_DOC_ES),
                           ('CLI_USE', CLI_USE, CLI_USE_ES), ('ENDPOINT_DOC', ENDPOINT_DOC, ENDPOINT_DOC_ES)):
        for k in en_:
            if k not in es_: PROBLEMS.append('es: %s has "%s" with no %s_ES entry' % (name, k, name))
    if MISSING: print('  !! no worked example for: ' + ', '.join(sorted(set(MISSING))))
    else: print('  every field has a worked example')
    if PROBLEMS:
        for line in PROBLEMS: print('  !! ' + line)
    else: print('  every nav entry names the heading it lands on')
    if any('PENDING' in u for urls in PUBLISHED.values() for u in urls.values()): print('  !! PUBLISHED still has placeholder addresses')
    sys.exit(1 if (MISSING or PROBLEMS) else 0)
