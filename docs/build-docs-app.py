# The app's chapters of the manual, in English.  Loaded by build-docs.py into its own namespace,
# like build-docs-es.py, and called from the four volume builders so the app comes first in each
# volume and the agent after it.  The app part of the reference is READ OUT OF THE SOURCES:
# CertificateStore.cs (file formats), Models.cs (settings, validation methods) and DeployService.cs
# (SFTP file types).  A format, setting, method or file type without a description fails the build.

APP = os.path.join(ROOT, 'CertificateGet')

def app_src(name):
    return io.open(os.path.join(APP, name), encoding='utf-8-sig').read().replace('\r\n', '\n')

STORE_CS  = app_src(os.path.join('Services', 'CertificateStore.cs'))
MODELS_CS = app_src(os.path.join('Models', 'Models.cs'))
DEPLOY_CS = app_src(os.path.join('Services', 'DeployService.cs'))

def _unq(s): return s.replace('\\"', '"').replace('\\\\', '\\')

def _formats():
    consts = dict(re.findall(r'public const string (\w+) = "([^"]*)";', STORE_CS))
    block = re.search(r'CertFormat\[\] All =\s*\{(.*?)\n    \};', STORE_CS, re.S).group(1)
    out = []
    for m in re.finditer(r'F\((\w+), "([^"]*)", "((?:[^"\\]|\\.)*)"([^)]*)\)'
                         r'|new\((\w+), "([^"]*)", "((?:[^"\\]|\\.)*)",\s*_ => new\[\] \{ ([^}]*) \}\)', block):
        if m.group(1):
            flags = m.group(4)
            out.append({'id': consts[m.group(1)], 'label': m.group(2), 'doc': _unq(m.group(3)),
                        'files': ['name' + consts[m.group(1)]],
                        'required': 'required: true' in flags, 'on': 'on: true' in flags, 'pwd': 'pwd: true' in flags})
        else:
            out.append({'id': consts[m.group(5)], 'label': m.group(6), 'doc': _unq(m.group(7)),
                        'files': re.findall(r'"([^"]+)"', m.group(8)), 'required': False, 'on': False, 'pwd': False})
    return out

def _enum(name):
    return re.findall(r'(\w+)', re.search(r'public enum ' + name + r'\s*\{([^}]*)\}', MODELS_CS).group(1))

def _class_props(name):
    body = re.search(r'public class ' + name + r'\s*\{(.*?)\n\}', MODELS_CS, re.S).group(1)
    return re.findall(r'public [\w<>?, ]+ (\w+) \{ get; set; \}', body)

APP_FORMATS  = _formats()
APP_METHODS  = _enum('ChallengeMethod')
METHOD_NAME  = dict(re.findall(r'ChallengeMethod\.(\w+) => "([^"]*)"', MODELS_CS))
APP_KEYTYPES = _enum('CertKeyType')
APP_SETTINGS = _class_props('AppSettings')
APP_SOURCES  = re.findall(r'\("([\w-]+)", CertFileKind\.(\w+), "([^"]*)"\)', DEPLOY_CS)
APP_SECRETS  = set(re.findall(r'"([\w-]+)"', re.search(r'SecretAliases = new\(\) \{([^}]*)\}', DEPLOY_CS).group(1)))
PLACEHOLDERS = sorted(set(re.findall(r'Replace\("(\{\w+\})"', DEPLOY_CS)))
assert len(APP_FORMATS) >= 10 and APP_METHODS and APP_SETTINGS and APP_SOURCES, 'app source extraction failed'

# ---------------------------------------------------------------- hand-kept words, checked against the sources
#  Validation methods: when to use each, and what it needs.
METHOD_DOC = {
 'HttpSelfHosted': ('Run the app on the server the domain points to. It answers on port 80 for a few seconds, without IIS.',
                    'Port 80 free on this PC and reachable from the internet. No administrator rights needed.'),
 'HttpWebRoot':    ('A web server (IIS, Apache, nginx) already serves the site. The app writes the challenge file into its folder.',
                    'Write access to the site\'s root folder, local or UNC path. Adds a <code>web.config</code> for IIS.'),
 'DnsManual':      ('Works anywhere, and for wildcards. You add the TXT records yourself; the app checks them before validating.',
                    'Access to the DNS control panel for every request and renewal.'),
 'DnsCloudflare':  ('Creates and removes the TXT records through the Cloudflare API.', 'An API token with Zone Read and DNS Edit.'),
 'DnsHostinger':   ('Creates and removes the TXT records through the Hostinger API.', 'An hPanel API token; the domain on Hostinger\'s name servers.'),
 'DnsConstellix':  ('Creates and removes the TXT records through the Constellix API (v4).', 'An API key and secret key; a correct PC clock.'),
 'DnsAcmeDns':     ('Works with any DNS host. One CNAME per domain, then no DNS changes and no DNS credentials on this PC.',
                    'An acme-dns server (public or your own) and one CNAME for <code>_acme-challenge</code>.'),
 'DnsMadeEasy':    ('Creates and removes the TXT records through the DNS Made Easy API (v2.0).', 'An API key and secret key; a correct PC clock.'),
 'DnsNamecheap':   ('Creates and removes the TXT records through the Namecheap API; every other record is written back unchanged.',
                    'API access turned on and this PC\'s public IPv4 whitelisted.'),
}

KEYTYPE_DOC = {
 'Rsa2048':   ('RSA 2048', 'The most compatible. The default.'),
 'Rsa3072':   ('RSA 3072', 'Stronger RSA, slightly slower handshakes.'),
 'Rsa4096':   ('RSA 4096', 'Strongest RSA; slower handshakes and larger files.'),
 'EcdsaP256': ('ECDSA P-256', 'Fast and modern; not accepted by some older appliances.'),
 'EcdsaP384': ('ECDSA P-384', 'Stronger ECDSA.'),
}

#  Every setting in settings.json: where it is in the app, and what it does.
SETTINGS_DOC = {
 'StorePath':            ('Storage &rarr; Store folder', 'Folder holding every certificate, private key, the ACME account keys and the activity log. Changing it does not move existing files.'),
 'DefaultEmail':         ('Defaults &rarr; Contact e-mail', 'Contact e-mail for new certificates and ACME accounts. For ZeroSSL, also used to link the account when there is no API key.'),
 'DefaultEnvironment':   ('Defaults &rarr; Let\'s Encrypt environment', 'Staging or Production for new Let\'s Encrypt certificates.'),
 'DefaultAuthority':     ('Defaults &rarr; Certificate authority', 'Let\'s Encrypt or ZeroSSL for new certificates.'),
 'ProtectedZeroSslApiKey': ('ACME accounts &rarr; ZeroSSL API key', 'Optional. Links the ZeroSSL ACME account to your ZeroSSL account. Read only when that account is created. Encrypted (DPAPI).'),
 'DefaultKeyType':       ('Defaults &rarr; Key type', 'Key type for new certificates.'),
 'ProtectedCloudflareToken':  ('DNS &rarr; Cloudflare API token', 'For <b>DNS — Cloudflare</b>. Encrypted (DPAPI).'),
 'ProtectedHostingerToken':   ('DNS &rarr; Hostinger API token', 'For <b>DNS — Hostinger</b>. Encrypted (DPAPI).'),
 'ProtectedConstellixApiKey': ('DNS &rarr; Constellix API key', 'For <b>DNS — Constellix</b>. Encrypted (DPAPI).'),
 'ProtectedConstellixSecretKey': ('DNS &rarr; Constellix secret key', 'For <b>DNS — Constellix</b>. Encrypted (DPAPI).'),
 'ProtectedDnsMadeEasyApiKey':   ('DNS &rarr; DNS Made Easy API key', 'For <b>DNS — DNS Made Easy</b>. Encrypted (DPAPI).'),
 'ProtectedDnsMadeEasySecretKey': ('DNS &rarr; DNS Made Easy secret key', 'For <b>DNS — DNS Made Easy</b>. Encrypted (DPAPI).'),
 'NamecheapApiUser':     ('DNS &rarr; Namecheap API user', 'Your Namecheap user name.'),
 'ProtectedNamecheapApiKey': ('DNS &rarr; Namecheap API key', 'For <b>DNS — Namecheap</b>. Encrypted (DPAPI).'),
 'NamecheapClientIp':    ('DNS &rarr; Client IP', 'The public IPv4 whitelisted at Namecheap. Empty = detect it on each request.'),
 'AcmeDnsServer':        ('DNS &rarr; acme-dns server', 'Server used for new acme-dns registrations. Default: the public server.'),
 'AcmeDnsAccounts':      ('DNS &rarr; acme-dns registrations', 'One registration per base domain, created on its first request, with the CNAME target to create. <b>Copy target</b> and <b>Remove</b>.'),
 'KeyFormatPkcs8':       ('Output formats &rarr; Private key format', 'PKCS#8 (<code>BEGIN PRIVATE KEY</code>, the default) or traditional (<code>BEGIN RSA/EC PRIVATE KEY</code>) for <code>.key</code> files.'),
 'PfxLegacyEncryption':  ('Output formats &rarr; PFX encryption', '3DES/SHA-1 (the default, imports everywhere) or AES-256/SHA-256 (Windows Server 2019+, modern OpenSSL).'),
 'JksPassword':          ('Output formats &rarr; JKS keystore password', 'Password of <code>cert.jks</code> and its key. TSplus only accepts <code>secret</code>, the default.'),
 'DnsResolvers':         ('DNS &rarr; Public DNS resolvers', 'Resolvers used to check that TXT and CNAME records are visible before validating. Default <code>1.1.1.1, 8.8.8.8</code>.'),
 'DnsPropagationTimeoutSeconds': ('DNS &rarr; Maximum wait for DNS propagation', 'How long the DNS provider methods wait for records to appear, 30 to 3600 seconds. Default 600.'),
 'RenewWarningDays':     ('Defaults &rarr; Warn when a certificate expires within', 'Days before expiry at which a certificate shows as expiring soon, 1 to 90. Default 30.'),
 'IssueFormats':         ('Files written for new certificates', 'Optional formats written with every issuance, besides the required ones. Any format can still be made later with <b>Export</b>.'),
}

SOURCE_LABEL = {}   # alias -> label, from DeployService.cs

def _app_checks(lang, methods, keytypes, settings):
    tag = '' if lang == 'en' else lang + '/'
    for m in APP_METHODS:
        if m not in methods: PROBLEMS.append('%sreference: validation method %s has no METHOD_DOC entry' % (tag, m))
    for k in APP_KEYTYPES:
        if k not in keytypes: PROBLEMS.append('%sreference: key type %s has no KEYTYPE_DOC entry' % (tag, k))
    for s in APP_SETTINGS:
        if s not in settings: PROBLEMS.append('%sreference: setting %s has no SETTINGS_DOC entry' % (tag, s))
    for s in settings:
        if s not in APP_SETTINGS: PROBLEMS.append('%sreference: SETTINGS_DOC lists %s, which AppSettings does not have' % (tag, s))

# =====================================================================
#  1  GETTING STARTED — the app
# =====================================================================
def app_getting_started(add):
    add('<h2 id="a-overview"><span class="k">Overview</span>What CertificateGet does</h2>')
    add('''<p>CertificateGet is a Windows desktop app that gets free, trusted TLS certificates from Let\'s Encrypt or
ZeroSSL. It proves you control each domain, keeps every certificate with its full history and in every common file
format, and can push new certificates to your servers after each issuance. The optional <b>CertificateGet Agent</b>, a
small service on each server, places the files and restarts what uses them.</p>''')
    add(flow([
        ('Request', 'Choose the domains, how to prove you control them, and the certificate authority.', ''),
        ('Validate', 'The app answers the HTTP or DNS challenge: its own web server, a file in your web root, or TXT records (by hand or through a DNS provider\'s API).', ''),
        ('Store', 'The certificate and key are saved in every format you chose, in a dated folder, with the settings to renew it.', ''),
        ('Use', 'Open the folder, export, install into Windows, copy as Base64, or deploy to servers automatically.', 'srv'),
    ]))

    add('<h2 id="a-install"><span class="k">The app</span>Install the app</h2>')
    add(steps([
        '<div><b>Download</b> <code>CertificateGet.exe</code> from the GitHub releases page. It is a single self-contained file: '
        'no installer and no .NET runtime needed. Windows 10/11 or Windows Server 2016 and later.</div>',
        '<div><b>Put it anywhere</b> and run it. Your certificates are not kept next to the exe but in the store folder, '
        '<code>%LOCALAPPDATA%\\CertificateGet\\Store</code> by default (change it in <b>Settings</b>).</div>',
        '<div><b>Run as administrator</b> only when you install certificates into the <i>Local Machine</i> store, or write '
        'into a web root that only administrators can change. The built-in web server on port 80 does not need it.</div>',
        '<div><b>Set your defaults</b> in <b>Settings</b>: contact e-mail, certificate authority, key type and, if you use them, '
        'the DNS provider tokens. Every setting is described in the %s.</div>' % xref(RF, 'a-settings', 'Reference'),
    ]))
    add('<p>To build it yourself instead: <code>.\\publish.ps1</code> in the repository writes <code>run\\CertificateGet.exe</code> and both agents.</p>')

    add('<h2 id="a-first"><span class="k">The app</span>Request your first certificate</h2>')
    add('<p>Start on <b>New certificate</b> in the sidebar. Test on Let\'s Encrypt <b>Staging</b> first: it has generous limits and issues untrusted test certificates.</p>')
    add(steps([
        '<div><b>1 · Domains.</b> Choose <b>Standard</b> and enter the names, one per line (up to 100). The first is the main '
        'name and names the files.</div>',
        '<div><b>2 · Validation method.</b> Choose how to prove you control the names. For a server that the domain points to, '
        '<b>HTTP — built-in web server</b> is the simplest; the others are explained in the %s.</div>' % xref(PG, 'a-methods', 'Programmer\'s Guide'),
        '<div><b>3 · Options.</b> Keep <b>Let\'s Encrypt</b> and <b>Staging (test)</b>. Choose a key type (RSA 2048 is the most '
        'compatible) and, if you want one, a PFX password. It is stored encrypted, so you can re-export later.</div>',
        '<div>Click <b>Request certificate</b> and follow <b>Progress</b> on the right. With manual DNS, a dialog lists the TXT '
        'records to create; click <b>Check DNS now</b> until they are visible, then <b>Continue validation</b>.</div>',
        '<div>When it works, open the certificate on <b>Certificates</b>, click <b>Renew</b>, switch to <b>Production (trusted)</b> '
        'and request again. The trusted files are added to the same certificate\'s history.</div>',
    ]))
    add('<h3 id="a-wildcard">A wildcard certificate</h3>')
    add('''<p>Choose <b>Wildcard</b> and enter the base domain, for example <code>example.com</code>, to get
<code>*.example.com</code>. Keep <b>Also cover the bare domain</b> ticked to include <code>example.com</code> itself.
Wildcards can only be validated through DNS, so pick one of the DNS methods. With a DNS provider API or acme-dns,
renewals need no manual DNS changes.</p>''')

    add('<h2 id="a-use"><span class="k">The app</span>Use the certificate</h2>')
    add('''<p><b>Certificates</b> lists everything you have requested, with how many days are left. Certificates that
expire within the warning period (30 days by default) show as expiring soon, and expired or never-issued ones in red.
Select one to see its files, issuance history, deployment targets and actions.</p>''')
    add(table(['Action', 'What it does'], [
        ['<b>Open folder</b>', 'Opens the latest issuance\'s folder in Explorer.'],
        ['<b>B64</b> (on a file)', 'Copies the file as a single Base64 line, for Azure Key Vault, web panels or CI secrets.'],
        ['<b>Export…</b>', 'Writes any formats to a folder of your choice, with the stored PFX password, a new one, or none.'],
        ['<b>Install in Windows</b>', 'Adds the certificate and key to the <i>Personal</i> store of Local Machine (IIS, RDP, SQL Server; needs administrator) or Current User.'],
        ['<b>Renew</b>', 'Loads the certificate\'s settings into <b>New certificate</b>; request to get a new issuance.'],
        ['<b>Delete</b>', 'Removes the certificate and all its stored files, private keys included. Certificates already on servers keep working.'],
    ]))
    add('<h3 id="a-renew">Renewing before it expires</h3>')
    add('''<p>Certificates from both authorities are valid for 90 days at most. CertificateGet does not renew on a
schedule: when a certificate shows as expiring soon, select it and click <b>Renew</b>, then <b>Renew certificate</b>.
The new files are added to its history, and targets set to deploy automatically receive them straight away.</p>''')

    add('<h2 id="a-deploy"><span class="k">The app</span>Send it to your servers</h2>')
    add('''<p>Each certificate can have <b>deployment targets</b>: select it, and under <b>Deployment</b> click <b>Add
target</b>. There are two kinds:</p>''')
    add(ul_list([
        '<b>CertificateGet Agent</b>: a service on a Windows or Linux server that writes the files where each program expects '
        'them, restarts services and programs, runs commands and imports into TSplus. Installing it is the rest of this volume.',
        '<b>SFTP / SSH</b>: uploads the files you choose to a folder and runs a command, with nothing to install on the server. '
        'Presets for HAProxy and nginx. See %s.' % xref(PG, 'a-sftp', 'SFTP / SSH targets'),
    ]))
    add('<p>Targets marked <b>Deploy automatically after every issuance / renewal</b> receive every new certificate; <b>Deploy now</b> sends the current one at any time.</p>')

APP_NAV_GS = [('The app', ['a-overview', 'a-install', 'a-first', 'a-wildcard', 'a-use', 'a-renew', 'a-deploy'])]

# =====================================================================
#  2  PROGRAMMER'S GUIDE — the app
# =====================================================================
def app_programmers_guide(add):
    add('<h2 id="a-methods"><span class="k">Validation</span>Choosing a validation method</h2>')
    add('''<p>Before issuing, the certificate authority checks that you control every name, with an HTTP request to the
domain or a TXT record in its DNS. Pick the method per certificate on <b>New certificate</b>.</p>''')
    rows = []
    for m in APP_METHODS:
        if m in METHOD_DOC:
            rows.append(['<b>%s</b>' % esc(METHOD_NAME.get(m, m)), METHOD_DOC[m][0], METHOD_DOC[m][1]])
    add(table(['Method', 'When to use it', 'Needs'], rows))
    add(note('info', 'Wildcards need DNS', '<p>A wildcard name can only be proved through DNS. HTTP methods are refused for wildcard certificates.</p>'))
    add('<h3 id="a-http">HTTP validation</h3>')
    add('''<p>The certificate authority requests <code>http://&lt;domain&gt;/.well-known/acme-challenge/&lt;token&gt;</code>
on port 80, always port 80, from the internet. With <b>HTTP — built-in web server</b> the app listens on port 80 of this
PC for the few seconds the check takes; the port must be free (stop IIS or use the web root method) and forwarded to
this PC. Another port only helps behind a port-forward from 80. With <b>HTTP — existing web site folder</b> the app
writes the token file into the site\'s folder (a local path or a UNC share), adds a <code>web.config</code> so IIS serves
files without an extension, and removes everything afterwards.</p>''')
    add('<h3 id="a-dns">DNS validation</h3>')
    add('''<p>The certificate authority looks up a TXT record at <code>_acme-challenge.&lt;domain&gt;</code>. Before asking
it to check, the app queries the public resolvers from Settings until every record is visible, for up to the
propagation wait (600 seconds by default). With <b>DNS — manual TXT record</b> you create the records at your DNS host
from the dialog, which has copy buttons and a <b>Check DNS now</b> button. Delete the old records after the request;
each request uses new values.</p>''')
    add('<h3 id="a-dns-api">DNS provider APIs</h3>')
    add('<p>The provider methods create the TXT records, wait for them, and remove only their own values afterwards. Enter the credentials in <b>Settings &rarr; DNS</b> and use the <b>Test</b> button next to them.</p>')
    add(table(['Provider', 'Where to get the credentials', 'Notes'], [
        ['Cloudflare', 'My Profile &rarr; API Tokens &rarr; Create Token, with Zone &rarr; Zone &rarr; Read and Zone &rarr; DNS &rarr; Edit.', '<b>Test token</b> lists the zones it can see.'],
        ['Hostinger', 'hPanel &rarr; Account (profile icon) &rarr; API.', 'The domain must use Hostinger\'s name servers. Other records are not touched.'],
        ['Constellix', 'Edit My Account &rarr; API Keys. The secret key is shown once, when the key is created.', 'Requests are signed with the current time; the PC clock must be correct.'],
        ['DNS Made Easy', 'Config &rarr; Account Information shows the API key and secret key.', 'Signed with the current time, like Constellix.'],
        ['Namecheap', 'Profile &rarr; Tools &rarr; API Access: turn it on and whitelist this PC\'s public IPv4.', 'Namecheap only allows API access for qualifying accounts, and can only replace a domain\'s whole record list, so the app writes every other record back unchanged. Leave <b>Client IP</b> empty to detect it.'],
    ]))
    add('<h3 id="a-acmedns">acme-dns</h3>')
    add('''<p><a href="https://github.com/joohoi/acme-dns">acme-dns</a> is a small DNS server that only answers
<code>_acme-challenge</code> TXT queries. Use it when your DNS host has no API, or you do not want DNS credentials on this
PC. On the first request for a domain the app registers it and shows one CNAME to create, for example
<code>_acme-challenge.example.com CNAME 8e5700ea-….auth.acme-dns.io</code>. That one CNAME covers the domain and its
wildcard. After that, renewals need no DNS changes. Registrations are listed in <b>Settings</b>, with <b>Copy target</b>
and <b>Remove</b>.</p>''')
    add(note('warn', 'Use your own acme-dns server in production',
        '<p>The public server <code>auth.acme-dns.io</code> is fine for testing. Anyone holding a registration\'s credentials can '
        'get certificates for that domain, so for production run your own and enter it under <b>Settings &rarr; acme-dns server</b>.</p>'))

    # ---- certificate authorities
    add('<h2 id="ca"><span class="k">Certificate authorities</span>Let\'s Encrypt and ZeroSSL</h2>')
    add('''<p>The app gets certificates from either of two ACME certificate authorities, chosen per certificate under
<b>Certificate authority</b> on the <b>New certificate</b> page. The default for new certificates is set in
<b>Settings</b>. Both issue free, trusted certificates valid for 90 days, for single names, several names or wildcards.</p>''')
    add(table(['', 'Let\'s Encrypt', 'ZeroSSL'], [
        ['Test server', 'Yes: <b>Staging</b> issues untrusted certificates with generous limits', 'None: every request is a trusted certificate'],
        ['Account', 'Created automatically; the contact e-mail is optional', 'Must be linked to a ZeroSSL account (External Account Binding); see below'],
        ['Rate limits', 'Yes, for example 5 failed validations per hour per account and hostname, 50 certificates per registered domain per week', 'No ACME rate limits'],
        ['Time to issue', 'Seconds after validation', 'Usually seconds, sometimes a few minutes; the app waits up to 10'],
        ['Chain', 'ISRG roots', 'Sectigo (USERTrust) roots'],
        ['Stored account', '<code>accounts\\staging.json</code>, <code>accounts\\production.json</code>', '<code>accounts\\zerossl.json</code>'],
    ]))
    add('''<p>Pick Let\'s Encrypt when you want to test a new setup on Staging first. Pick ZeroSSL when you have hit a Let\'s
Encrypt rate limit, want the certificates listed in a ZeroSSL dashboard, or want a second authority to fall back on.</p>''')
    add('<h3 id="ca-zerossl">Setting up ZeroSSL</h3>')
    add('''<p>ZeroSSL only accepts ACME accounts that are bound to a ZeroSSL account. The app fetches the binding
credentials from ZeroSSL once, when the ZeroSSL ACME account is first created, in one of two ways:</p>''')
    add(ul_list([
        '<b>With a ZeroSSL API key</b> (recommended). Sign in at zerossl.com, open <b>Developer</b>, copy the <b>API Access Key</b> '
        'and paste it into <b>Settings &rarr; ACME accounts &rarr; ZeroSSL API key</b>. The ACME account is then linked to your '
        'ZeroSSL account, and the certificates it issues appear in your ZeroSSL dashboard.',
        '<b>With the contact e-mail only.</b> Leave the API key empty and fill in the contact e-mail on the certificate (or the '
        'default one in Settings). ZeroSSL creates an account for that address if it has none, or reuses the existing one.',
    ]))
    add('''<p>After that, only the ACME account key in <code>accounts\\zerossl.json</code> is used; the API key is not read
again. To move to a different ZeroSSL account, change the key or e-mail and click <b>Reset accounts</b> in Settings.
That resets the Let\'s Encrypt accounts too; they are created again automatically on the next request, and issued
certificates are not affected.</p>''')
    add(note('warn', 'No staging on ZeroSSL',
        '<p>Every ZeroSSL request issues a real, trusted certificate. To test a new validation method or DNS setup, run it '
        'once against Let\'s Encrypt Staging, then switch the certificate to ZeroSSL.</p>'))
    add('<h3 id="ca-switch">Switching a certificate to the other authority</h3>')
    add('''<p>Open the certificate, click <b>Renew</b>, change <b>Certificate authority</b> and request. The new files are
added to the same certificate\'s history, the deployment targets stay as they are, and later renewals use the authority
chosen last. The agent does not care which authority issued a certificate: it receives the same sources either way.
Only <code>fullchain-root</code> differs, because it ends in that authority\'s root.</p>''')

    # ---- certificates
    add('<h2 id="a-certs"><span class="k">Certificates</span>Certificates, issuances and files</h2>')
    add('''<p>A <b>certificate</b> in the app is a definition: its names, validation method, authority, key type, PFX
password and deployment targets. Each successful request adds an <b>issuance</b>, a dated folder with the files, to its
history. <b>Renew</b> reuses the definition; the latest issuance is the one that is exported, installed and deployed.</p>''')
    add(ul_list([
        '<b>A new private key for every issuance.</b> Each request generates a fresh key of the chosen type; a key is never reused.',
        '<b>Files written:</b> the required formats (certificate, key and chain in PEM), which exports and renewals are built from, plus the formats ticked under <b>Settings &rarr; Files written for new certificates</b>. Any other format can be made later with <b>Export</b>. The full list is in the %s.' % xref(RF, 'a-formats', 'Reference'),
        '<b>File names</b> come from the first domain, with <code>*</code> written as <code>wildcard</code>: <code>wildcard.example.com.pfx</code>.',
        '<b>Output options</b> in Settings apply to new issuances and exports: PKCS#8 or traditional <code>.key</code>, 3DES or AES-256 PFX, and the JKS password.',
    ]))
    add('<h3 id="a-keytypes">Key types</h3>')
    add(table(['Key type', 'Notes'], [[KEYTYPE_DOC[k][0], KEYTYPE_DOC[k][1]] for k in APP_KEYTYPES if k in KEYTYPE_DOC]))
    add('<h3 id="a-expiry">Expiry and renewal</h3>')
    add('''<p>The list and the totals at the top of <b>Certificates</b> count certificates as valid, expiring soon (within
<b>Settings &rarr; Warn when a certificate expires within</b>, 30 days by default) or expired / not issued. Renewal is
manual: <b>Renew</b> then <b>Renew certificate</b>. The app does not run in the background or on a schedule, so check
it regularly, or renew all certificates that are expiring soon when you open it. Let\'s Encrypt no longer sends expiry
e-mails.</p>''')

    # ---- deployment
    add('<h2 id="a-targets"><span class="k">Deployment</span>Deployment targets</h2>')
    add('''<p>A certificate can have any number of targets. After every issuance or renewal, each enabled target set to
<b>Deploy automatically</b> receives the new files; the result shows on the target and in the activity log. A failed
target does not affect the others or the issuance itself; fix it and click <b>Deploy now</b>.</p>''')
    add(table(['', 'CertificateGet Agent', 'SFTP / SSH'], [
        ['On the server', 'The agent service (Windows or Linux)', 'Nothing; an SSH login'],
        ['Where files go', 'Decided on the server in <code>agent.json</code>: many folders, file names per program', 'One folder and the files you list on the target'],
        ['Afterwards', 'Restarts services, desktop programs; runs commands; TSplus import; backups', 'One command over SSH'],
        ['Security', 'HTTPS with a pinned fingerprint, API key, IP allow-list', 'SSH with a pinned host key; password or private key'],
    ]))
    add('<h3 id="a-sftp">SFTP / SSH targets</h3>')
    add(steps([
        '<div>Enter the <b>Host</b>, <b>Port</b> (22) and <b>User</b>, and choose <b>Password</b> or <b>Private key file</b> '
        '(with its passphrase, if any). Both are stored encrypted.</div>',
        '<div>Enter the <b>Remote folder</b> and the <b>Files to upload</b>: a file type and the remote name for each. '
        '<code>{domain}</code> in a name becomes the first domain and <code>{name}</code> the certificate\'s name. The <b>HAProxy</b> '
        'and <b>nginx</b> presets fill in a folder, files and command.</div>',
        '<div>Optionally a <b>Command to run afterwards</b>, run over SSH as the same user. The deployment fails if it exits '
        'with a non-zero code.</div>',
        '<div>Click <b>Test connection</b>. The first time, the app shows the server\'s SSH host key fingerprint; trust it and '
        'the app refuses any other key later. <b>Forget host key</b> clears it after a server is rebuilt.</div>',
    ]))
    add('''<p>Each file is uploaded as <code>&lt;name&gt;.cg-upload</code> and then renamed over the old one, so the server
never reads half a file. Files holding the private key get mode 600, others 644. The file types are listed in the
%s.</p>''' % xref(RF, 'a-sftp-files', 'Reference'))

    # ---- storage and secrets
    add('<h2 id="a-store"><span class="k">Storage</span>Store folder, secrets and activity log</h2>')
    add('''<p>Everything the app keeps is in the store folder (<b>Settings &rarr; Store folder</b>, default
<code>%%LOCALAPPDATA%%\\CertificateGet\\Store</code>): certificates and their history, the ACME account keys and the
activity log. Settings themselves are in <code>%%LOCALAPPDATA%%\\CertificateGet\\settings.json</code>. The layout is in the
%s.</p>''' % xref(RF, 'a-layout', 'Reference'))
    add(ul_list([
        '<b>Encrypted with Windows DPAPI</b> for your Windows user: PFX passwords, ACME account keys, DNS tokens and keys, acme-dns '
        'passwords, SFTP passwords and key passphrases, agent API keys. Another Windows user, or another PC, cannot decrypt them; '
        'the app then creates a new ACME account and asks for the other secrets again.',
        '<b>Not encrypted:</b> the private keys in the <code>.key</code> and <code>.pem</code> files, as on any web server. Keep '
        'the store out of cloud-synced folders and readable only by you.',
        '<b>Activity log:</b> every request, validation, export, install, deployment and error, in '
        '<code>activity.jsonl</code>. The <b>Activity log</b> page searches and filters it and exports CSV.',
    ]))

APP_NAV_PG = [('Validation', ['a-methods', 'a-http', 'a-dns', 'a-dns-api', 'a-acmedns']),
              ('Certificate authorities', ['ca', 'ca-zerossl', 'ca-switch']),
              ('Certificates', ['a-certs', 'a-keytypes', 'a-expiry']),
              ('Deployment', ['a-targets', 'a-sftp']),
              ('Storage', ['a-store'])]

# =====================================================================
#  3  TEMPLATE GUIDE — recipes in the app
# =====================================================================
def app_template_guide(add):
    add('<h2 id="a-iis"><span class="k">Recipe</span>IIS site on the same server</h2>')
    add('<p>Run CertificateGet as administrator on the IIS server.</p>')
    add(table(['Setting', 'Value'], [
        ['Validation method', '<b>HTTP — existing web site folder</b>, web root <code>C:\\inetpub\\wwwroot</code> (the site\'s physical path)'],
        ['Key type', 'RSA 2048'],
        ['After issuing', '<b>Install in Windows</b> &rarr; <i>Local Machine</i>, then in IIS Manager edit the site\'s <b>https</b> binding and pick the new certificate'],
    ]))
    add('<p>After each renewal install the new certificate and pick it in the binding again. To automate that, use an agent with a command that updates the binding.</p>')

    add('<h2 id="a-wild-cf"><span class="k">Recipe</span>Wildcard with Cloudflare</h2>')
    add(table(['Setting', 'Value'], [
        ['Settings', 'Cloudflare API token with Zone Read and DNS Edit; <b>Test token</b>'],
        ['Domains', '<b>Wildcard</b>, base domain <code>example.com</code>, <b>Also cover the bare domain</b> ticked'],
        ['Validation method', '<b>DNS — Cloudflare (automatic)</b>'],
        ['Result', 'One certificate for <code>*.example.com</code> and <code>example.com</code>; renewals need no manual step'],
    ]))
    add('<p>The same works with Hostinger, Constellix, DNS Made Easy, Namecheap or acme-dns: only the method and its credentials change.</p>')

    add('<h2 id="a-sftp-haproxy"><span class="k">Recipe</span>HAProxy over SFTP</h2>')
    add('<p>For one HAProxy server where an upload and a reload are enough. Add a target of type <b>SFTP / SSH</b> and click the <b>HAProxy</b> preset:</p>')
    add(table(['Field', 'Preset value'], [
        ['Remote folder', '<code>/etc/haproxy/certs</code>'],
        ['Files', '<code>combined</code> &rarr; <code>{domain}.pem</code>'],
        ['Command', '<code>haproxy -c -f /etc/haproxy/haproxy.cfg &amp;&amp; systemctl reload haproxy</code>'],
    ]))
    add('<p>The user must be able to write the folder and reload HAProxy (root, or sudo rights for the command). For several programs on one server, or Cockpit next to HAProxy, use the agent (%s).</p>' % xref(TG, 'haproxy', 'HAProxy template'))

    add('<h2 id="a-sftp-nginx"><span class="k">Recipe</span>nginx over SFTP</h2>')
    add(table(['Field', 'Preset value'], [
        ['Remote folder', '<code>/etc/nginx/ssl</code>'],
        ['Files', '<code>fullchain</code> &rarr; <code>{domain}.crt</code>, <code>key</code> &rarr; <code>{domain}.key</code>'],
        ['Command', '<code>nginx -t &amp;&amp; systemctl reload nginx</code>'],
    ]))
    add(code('''ssl_certificate     /etc/nginx/ssl/www.example.com.crt;
ssl_certificate_key /etc/nginx/ssl/www.example.com.key;''', 'text'))

APP_NAV_TG = [('Recipes in the app', ['a-iis', 'a-wild-cf', 'a-sftp-haproxy', 'a-sftp-nginx'])]

# =====================================================================
#  4  REFERENCE — the app, generated from its sources
# =====================================================================
def _format_rows(docs):
    rows = []
    for f in APP_FORMATS:
        state = 'Always' if f['required'] else 'Default' if f['on'] else 'Optional'
        rows.append(['<code>%s</code>' % '</code> <code>'.join(esc(n) for n in f['files']),
                     esc(docs[f['id']][0] if docs else f['label']),
                     esc(docs[f['id']][1]) if docs else esc(f['doc']), state])
    return rows

def app_reference(add):
    add('<h2 id="a-formats"><span class="k">The app</span>File formats</h2>')
    add('''<p>Every format the app can write, read from <code>CertificateStore.cs</code>. <code>name</code> is the first
domain. <i>Always</i> formats are written with every issuance; <i>Default</i> ones are ticked in Settings until you change
them; <i>Optional</i> ones are written when ticked in Settings, or by <b>Export</b>. Formats that need a password use the
certificate\'s PFX password.</p>''')
    add(table(['Files', 'Format', 'Contents and use', 'Written'], _format_rows(None)))

    add('<h2 id="a-methods-ref"><span class="k">The app</span>Validation methods</h2>')
    add(table(['Method', 'When to use it', 'Needs'],
              [['<b>%s</b>' % esc(METHOD_NAME.get(m, m)), METHOD_DOC[m][0], METHOD_DOC[m][1]] for m in APP_METHODS if m in METHOD_DOC]))

    add('<h2 id="a-settings"><span class="k">The app</span>Settings</h2>')
    add('<p>Every value in <code>settings.json</code>, read from <code>AppSettings</code> in <code>Models.cs</code>, with where to change it on the <b>Settings</b> page.</p>')
    add(table(['Setting', 'In the app', 'What it does'],
              [['<code>%s</code>' % s, SETTINGS_DOC[s][0], SETTINGS_DOC[s][1]] for s in APP_SETTINGS if s in SETTINGS_DOC]))

    add('<h2 id="a-sftp-files"><span class="k">The app</span>SFTP file types and placeholders</h2>')
    add('<p>The file types an SFTP target can upload, read from <code>DeployService.cs</code>. They are the same names the agent uses as sources. Files marked <i>secret</i> contain the private key and are uploaded with mode 600.</p>')
    add(table(['Type', 'File'], [['<code>%s</code>%s' % (a, '<span class="tag tag--warn">secret</span>' if a in APP_SECRETS else ''), esc(lbl)]
                                  for a, _, lbl in APP_SOURCES]))
    add(table(['Placeholder', 'Replaced with'], [['<code>%s</code>' % p, APP_PLACEHOLDER_DOC.get(p, '??')] for p in PLACEHOLDERS]))
    for p in PLACEHOLDERS:
        if p not in APP_PLACEHOLDER_DOC: PROBLEMS.append('reference: placeholder %s has no APP_PLACEHOLDER_DOC entry' % p)

    add('<h2 id="a-layout"><span class="k">The app</span>Store folder layout</h2>')
    add(code('''%LOCALAPPDATA%\\CertificateGet\\
  settings.json                          settings (secrets encrypted with DPAPI)
  help\\                                  this manual, written when Help is opened
  Store\\                                 the store folder (Settings → Store folder)
    activity.jsonl                       activity log, one JSON line per entry
    accounts\\
      staging.json, production.json      Let's Encrypt ACME account keys (DPAPI)
      zerossl.json                       ZeroSSL ACME account key (DPAPI)
    certificates\\
      wildcard.example.com_<id>\\
        profile.json                     names, method, options, targets, history
        2026-09-24_231200\\               one folder per issuance, with its files''', 'text'))
    _app_checks('en', METHOD_DOC, KEYTYPE_DOC, SETTINGS_DOC)

APP_PLACEHOLDER_DOC = {
 '{domain}': 'The first domain, with <code>*</code> written as <code>wildcard</code>: <code>wildcard.example.com</code>',
 '{name}':   'The certificate\'s display name, made safe for a file name',
}

APP_NAV_RF = [('The app', ['a-formats', 'a-methods-ref', 'a-settings', 'a-sftp-files', 'a-layout'])]
