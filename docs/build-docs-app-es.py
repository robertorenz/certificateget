# Los capítulos de la aplicación, en español.  build-docs.py lo carga después de build-docs-app.py y
# usa sus datos leídos de las fuentes (APP_FORMATS, APP_METHODS, APP_SETTINGS, APP_SOURCES...).  Cada
# formato, método, tipo de clave, ajuste y tipo de archivo necesita aquí su descripción en español;
# la compilación falla si falta alguna.  La interfaz de la aplicación está en inglés, así que los
# nombres de botones y campos se citan tal cual.

METHOD_DOC_ES = {
 'HttpSelfHosted': ('Ejecute la aplicación en el servidor al que apunta el dominio. Responde en el puerto 80 unos segundos, sin IIS.',
                    'El puerto 80 libre en este PC y accesible desde internet. No requiere permisos de administrador.'),
 'HttpWebRoot':    ('Un servidor web (IIS, Apache, nginx) ya sirve el sitio. La aplicación escribe el archivo del desafío en su carpeta.',
                    'Permiso de escritura en la carpeta raíz del sitio, local o ruta UNC. Añade un <code>web.config</code> para IIS.'),
 'DnsManual':      ('Funciona en cualquier caso, también con comodines. Usted crea los registros TXT; la aplicación los comprueba antes de validar.',
                    'Acceso al panel DNS en cada solicitud y renovación.'),
 'DnsCloudflare':  ('Crea y elimina los registros TXT mediante la API de Cloudflare.', 'Un token de API con Zone Read y DNS Edit.'),
 'DnsHostinger':   ('Crea y elimina los registros TXT mediante la API de Hostinger.', 'Un token de API de hPanel; el dominio en los servidores DNS de Hostinger.'),
 'DnsConstellix':  ('Crea y elimina los registros TXT mediante la API de Constellix (v4).', 'Una clave de API y una clave secreta; el reloj del PC en hora.'),
 'DnsAcmeDns':     ('Funciona con cualquier proveedor DNS. Un CNAME por dominio y después ningún cambio de DNS ni credenciales DNS en este PC.',
                    'Un servidor acme-dns (público o propio) y un CNAME para <code>_acme-challenge</code>.'),
 'DnsMadeEasy':    ('Crea y elimina los registros TXT mediante la API de DNS Made Easy (v2.0).', 'Una clave de API y una clave secreta; el reloj del PC en hora.'),
 'DnsNamecheap':   ('Crea y elimina los registros TXT mediante la API de Namecheap; todos los demás registros se vuelven a escribir sin cambios.',
                    'Acceso a la API activado y la IPv4 pública de este PC en la lista blanca.'),
}

KEYTYPE_DOC_ES = {
 'Rsa2048':   ('RSA 2048', 'La más compatible. La predeterminada.'),
 'Rsa3072':   ('RSA 3072', 'RSA más fuerte, conexiones algo más lentas.'),
 'Rsa4096':   ('RSA 4096', 'La RSA más fuerte; conexiones más lentas y archivos más grandes.'),
 'EcdsaP256': ('ECDSA P-256', 'Rápida y moderna; algunos dispositivos antiguos no la aceptan.'),
 'EcdsaP384': ('ECDSA P-384', 'ECDSA más fuerte.'),
}

SETTINGS_DOC_ES = {
 'StorePath':            ('Storage &rarr; Store folder', 'Carpeta con todos los certificados, claves privadas, las claves de las cuentas ACME y el registro de actividad. Cambiarla no mueve los archivos existentes.'),
 'DefaultEmail':         ('Defaults &rarr; Contact e-mail', 'Correo de contacto para certificados y cuentas ACME nuevos. En ZeroSSL también sirve para vincular la cuenta cuando no hay clave de API.'),
 'DefaultEnvironment':   ('Defaults &rarr; Let\'s Encrypt environment', 'Staging o Production para certificados nuevos de Let\'s Encrypt.'),
 'DefaultAuthority':     ('Defaults &rarr; Certificate authority', 'Let\'s Encrypt o ZeroSSL para certificados nuevos.'),
 'ProtectedZeroSslApiKey': ('ACME accounts &rarr; ZeroSSL API key', 'Opcional. Vincula la cuenta ACME de ZeroSSL a su cuenta de ZeroSSL. Solo se lee al crear esa cuenta. Cifrada (DPAPI).'),
 'DefaultKeyType':       ('Defaults &rarr; Key type', 'Tipo de clave para certificados nuevos.'),
 'ProtectedCloudflareToken':  ('DNS &rarr; Cloudflare API token', 'Para <b>DNS — Cloudflare</b>. Cifrado (DPAPI).'),
 'ProtectedHostingerToken':   ('DNS &rarr; Hostinger API token', 'Para <b>DNS — Hostinger</b>. Cifrado (DPAPI).'),
 'ProtectedConstellixApiKey': ('DNS &rarr; Constellix API key', 'Para <b>DNS — Constellix</b>. Cifrada (DPAPI).'),
 'ProtectedConstellixSecretKey': ('DNS &rarr; Constellix secret key', 'Para <b>DNS — Constellix</b>. Cifrada (DPAPI).'),
 'ProtectedDnsMadeEasyApiKey':   ('DNS &rarr; DNS Made Easy API key', 'Para <b>DNS — DNS Made Easy</b>. Cifrada (DPAPI).'),
 'ProtectedDnsMadeEasySecretKey': ('DNS &rarr; DNS Made Easy secret key', 'Para <b>DNS — DNS Made Easy</b>. Cifrada (DPAPI).'),
 'NamecheapApiUser':     ('DNS &rarr; Namecheap API user', 'Su nombre de usuario de Namecheap.'),
 'ProtectedNamecheapApiKey': ('DNS &rarr; Namecheap API key', 'Para <b>DNS — Namecheap</b>. Cifrada (DPAPI).'),
 'NamecheapClientIp':    ('DNS &rarr; Client IP', 'La IPv4 pública en la lista blanca de Namecheap. Vacía = se detecta en cada solicitud.'),
 'AcmeDnsServer':        ('DNS &rarr; acme-dns server', 'Servidor usado para los registros nuevos de acme-dns. Predeterminado: el servidor público.'),
 'AcmeDnsAccounts':      ('DNS &rarr; acme-dns registrations', 'Un registro por dominio base, creado en su primera solicitud, con el destino del CNAME que hay que crear. <b>Copy target</b> y <b>Remove</b>.'),
 'KeyFormatPkcs8':       ('Output formats &rarr; Private key format', 'PKCS#8 (<code>BEGIN PRIVATE KEY</code>, el predeterminado) o tradicional (<code>BEGIN RSA/EC PRIVATE KEY</code>) para los archivos <code>.key</code>.'),
 'PfxLegacyEncryption':  ('Output formats &rarr; PFX encryption', '3DES/SHA-1 (el predeterminado, se importa en todas partes) o AES-256/SHA-256 (Windows Server 2019+, OpenSSL moderno).'),
 'JksPassword':          ('Output formats &rarr; JKS keystore password', 'Contraseña de <code>cert.jks</code> y de su clave. TSplus solo acepta <code>secret</code>, la predeterminada.'),
 'DnsResolvers':         ('DNS &rarr; Public DNS resolvers', 'Resolvedores usados para comprobar que los registros TXT y CNAME son visibles antes de validar. Predeterminados <code>1.1.1.1, 8.8.8.8</code>.'),
 'DnsPropagationTimeoutSeconds': ('DNS &rarr; Maximum wait for DNS propagation', 'Cuánto esperan los métodos con proveedor DNS a que aparezcan los registros, de 30 a 3600 segundos. Predeterminado 600.'),
 'RenewWarningDays':     ('Defaults &rarr; Warn when a certificate expires within', 'Días antes del vencimiento a partir de los cuales un certificado aparece como próximo a vencer, de 1 a 90. Predeterminado 30.'),
 'IssueFormats':         ('Files written for new certificates', 'Formatos opcionales que se escriben en cada emisión, además de los obligatorios. Cualquier formato se puede generar después con <b>Export</b>.'),
}

#  Formato (id de CertificateStore.cs) -> (nombre, contenido y uso).
FORMAT_DOC_ES = {
 '.pfx':                  ('PFX / PKCS#12', 'Certificado, cadena y clave privada en un archivo protegido con contraseña (IIS, Azure, Exchange, Windows).'),
 '.cer':                  ('CER (PEM)', 'Solo el certificado, texto PEM en Base64.'),
 '.key':                  ('KEY (PEM)', 'La clave privada, texto PEM. Manténgala en secreto.'),
 '-fullchain.pem':        ('Cadena completa PEM', 'El certificado seguido de la cadena intermedia (ssl_certificate de nginx, Apache 2.4.8+).'),
 '-combined.pem':         ('PEM combinado', 'Cadena completa y clave privada en un solo archivo (HAProxy, Webmin, muchos dispositivos).'),
 '-chain.cer':            ('Cadena (PEM)', 'Solo los certificados intermedios (SSLCertificateChainFile de Apache).'),
 '-der.cer':              ('CER (DER binario)', 'Solo el certificado, codificación DER binaria (Java, algunas herramientas de Windows).'),
 '.p7b':                  ('P7B / PKCS#7', 'Certificado y cadena, sin clave privada (intermedios de Windows, keytool de Java, Tomcat, F5, Citrix, Palo Alto).'),
 'certbot':               ('Nombres estilo Certbot', 'cert.pem, privkey.pem, chain.pem y fullchain.pem: los nombres que esperan las guías de Linux, Synology, Home Assistant, Proxmox y las imágenes Docker.'),
 '-encrypted.key':        ('KEY (cifrada)', 'Clave privada protegida con la contraseña PFX, BEGIN ENCRYPTED PRIVATE KEY (FortiGate, Sophos, Cisco, Apache con frase de paso).'),
 '-fullchain-root.pem':   ('Cadena completa + raíz', 'Certificado, intermedios y la raíz, para dispositivos que validan toda la cadena al importarla.'),
 '-combined-keyfirst.pem': ('PEM combinado (clave primero)', 'Clave privada seguida de la cadena completa (smtpd_tls_chain_files de Postfix, lighttpd, Pound).'),
 '.p12':                  ('P12', 'El mismo contenido que el PFX con extensión .p12 (almacenes de Java/Tomcat, llavero de macOS, Android).'),
 '.crt':                  ('CRT (PEM)', 'Solo el certificado, PEM, con la extensión .crt que usan Linux y Apache.'),
 '-k8s-secret.yaml':      ('Secret TLS de Kubernetes', 'Manifiesto YAML con tls.crt (cadena completa) y tls.key, listo para kubectl apply.'),
 'cert.jks':              ('JKS (cert.jks de TSplus)', 'Java KeyStore con la clave y la cadena completa; contraseña del almacén y de la clave tomada de Settings (predeterminada "secret", que TSplus exige). Para TSplus cópielo a TSplus\\Clients\\webserver y ejecute AdminTool.exe /webrestart.'),
}

SOURCE_LABEL_ES = {
 'combined': 'PEM combinado (cadena completa + clave)', 'combined-keyfirst': 'PEM combinado (clave primero)',
 'fullchain': 'Cadena completa PEM', 'fullchain-root': 'Cadena completa + raíz PEM', 'cer': 'Certificado (PEM)',
 'crt': 'Certificado (PEM, .crt)', 'der': 'Certificado (DER)', 'chain': 'Cadena (PEM)', 'key': 'Clave privada (PEM)',
 'encrypted-key': 'Clave privada (cifrada)', 'pfx': 'PFX', 'p12': 'P12', 'p7b': 'P7B', 'k8s': 'Secret de Kubernetes',
 'jks': 'JKS (almacén de Java)',
}

APP_PLACEHOLDER_DOC_ES = {
 '{domain}': 'El primer dominio, con <code>*</code> escrito como <code>wildcard</code>: <code>wildcard.example.com</code>',
 '{name}':   'El nombre del certificado, adaptado para usarse como nombre de archivo',
}

# =====================================================================
#  1  PRIMEROS PASOS — la aplicación
# =====================================================================
def app_getting_started_es(add):
    add('<h2 id="a-overview"><span class="k">Introducción</span>Qué hace CertificateGet</h2>')
    add('''<p>CertificateGet es una aplicación de escritorio para Windows que obtiene certificados TLS gratuitos y de
confianza de Let\'s Encrypt o ZeroSSL. Demuestra que usted controla cada dominio, guarda cada certificado con todo su
historial y en todos los formatos de archivo habituales, y puede enviar los certificados nuevos a sus servidores tras
cada emisión. El <b>CertificateGet Agent</b> opcional, un pequeño servicio en cada servidor, coloca los archivos y
reinicia lo que los usa.</p>''')
    add(flow([
        ('Solicitar', 'Elija los dominios, cómo demostrar que los controla y la autoridad de certificación.', ''),
        ('Validar', 'La aplicación responde al desafío HTTP o DNS: su propio servidor web, un archivo en la raíz de su sitio o registros TXT (a mano o mediante la API de un proveedor DNS).', ''),
        ('Guardar', 'El certificado y la clave se guardan en todos los formatos elegidos, en una carpeta con fecha, con los ajustes para renovarlo.', ''),
        ('Usar', 'Abra la carpeta, exporte, instale en Windows, copie en Base64 o despliegue automáticamente en los servidores.', 'srv'),
    ]))

    add('<h2 id="a-install"><span class="k">La aplicación</span>Instalar la aplicación</h2>')
    add(steps([
        '<div><b>Descargue</b> <code>CertificateGet.exe</code> de la página de versiones de GitHub. Es un único archivo autocontenido: '
        'sin instalador y sin runtime de .NET. Windows 10/11 o Windows Server 2016 y posteriores.</div>',
        '<div><b>Colóquelo donde quiera</b> y ejecútelo. Sus certificados no se guardan junto al exe sino en la carpeta del almacén, '
        '<code>%LOCALAPPDATA%\\CertificateGet\\Store</code> de forma predeterminada (se cambia en <b>Settings</b>).</div>',
        '<div><b>Ejecútela como administrador</b> solo para instalar certificados en el almacén <i>Local Machine</i> o escribir en '
        'la raíz de un sitio que solo pueden modificar los administradores. El servidor web integrado en el puerto 80 no lo necesita.</div>',
        '<div><b>Fije sus valores predeterminados</b> en <b>Settings</b>: correo de contacto, autoridad de certificación, tipo de '
        'clave y, si los usa, los tokens de los proveedores DNS. Cada ajuste se describe en la %s.</div>' % xref(RF, 'a-settings', 'Referencia'),
    ]))
    add('<p>Para compilarla usted mismo: <code>.\\publish.ps1</code> en el repositorio genera <code>run\\CertificateGet.exe</code> y ambos agentes.</p>')

    add('<h2 id="a-first"><span class="k">La aplicación</span>Solicitar su primer certificado</h2>')
    add('<p>Empiece en <b>New certificate</b> en la barra lateral. Pruebe primero con <b>Staging</b> de Let\'s Encrypt: tiene límites amplios y emite certificados de prueba no confiables.</p>')
    add(steps([
        '<div><b>1 · Domains.</b> Elija <b>Standard</b> y escriba los nombres, uno por línea (hasta 100). El primero es el nombre '
        'principal y da nombre a los archivos.</div>',
        '<div><b>2 · Validation method.</b> Elija cómo demostrar que controla los nombres. Para un servidor al que apunta el '
        'dominio, <b>HTTP — built-in web server</b> es lo más sencillo; los demás se explican en la %s.</div>' % xref(PG, 'a-methods', 'Guía del programador'),
        '<div><b>3 · Options.</b> Deje <b>Let\'s Encrypt</b> y <b>Staging (test)</b>. Elija un tipo de clave (RSA 2048 es la más '
        'compatible) y, si quiere, una contraseña PFX. Se guarda cifrada, así que puede volver a exportar más tarde.</div>',
        '<div>Haga clic en <b>Request certificate</b> y siga <b>Progress</b> a la derecha. Con DNS manual, un cuadro de diálogo '
        'enumera los registros TXT que debe crear; haga clic en <b>Check DNS now</b> hasta que sean visibles y luego en <b>Continue validation</b>.</div>',
        '<div>Cuando funcione, abra el certificado en <b>Certificates</b>, haga clic en <b>Renew</b>, cambie a <b>Production (trusted)</b> '
        'y vuelva a solicitarlo. Los archivos de confianza se añaden al historial del mismo certificado.</div>',
    ]))
    add('<h3 id="a-wildcard">Un certificado comodín</h3>')
    add('''<p>Elija <b>Wildcard</b> y escriba el dominio base, por ejemplo <code>example.com</code>, para obtener
<code>*.example.com</code>. Deje marcada <b>Also cover the bare domain</b> para incluir también <code>example.com</code>.
Los comodines solo se pueden validar por DNS, así que elija uno de los métodos DNS. Con la API de un proveedor DNS o
acme-dns, las renovaciones no requieren cambios manuales de DNS.</p>''')

    add('<h2 id="a-use"><span class="k">La aplicación</span>Usar el certificado</h2>')
    add('''<p><b>Certificates</b> enumera todo lo que ha solicitado, con los días que le quedan. Los certificados que
vencen dentro del periodo de aviso (30 días de forma predeterminada) aparecen como próximos a vencer, y los vencidos o
nunca emitidos en rojo. Seleccione uno para ver sus archivos, su historial de emisiones, sus objetivos de despliegue y
sus acciones.</p>''')
    add(table(['Acción', 'Qué hace'], [
        ['<b>Open folder</b>', 'Abre en el Explorador la carpeta de la última emisión.'],
        ['<b>B64</b> (en un archivo)', 'Copia el archivo como una sola línea Base64, para Azure Key Vault, paneles web o secretos de CI.'],
        ['<b>Export…</b>', 'Escribe los formatos que elija en una carpeta, con la contraseña PFX guardada, una nueva o ninguna.'],
        ['<b>Install in Windows</b>', 'Añade el certificado y la clave al almacén <i>Personal</i> de Local Machine (IIS, RDP, SQL Server; requiere administrador) o de Current User.'],
        ['<b>Renew</b>', 'Carga los ajustes del certificado en <b>New certificate</b>; solicítelo para obtener una emisión nueva.'],
        ['<b>Delete</b>', 'Elimina el certificado y todos sus archivos guardados, claves privadas incluidas. Los certificados ya instalados en servidores siguen funcionando.'],
    ]))
    add('<h3 id="a-renew">Renovar antes del vencimiento</h3>')
    add('''<p>Los certificados de ambas autoridades son válidos 90 días como máximo. CertificateGet no renueva de forma
programada: cuando un certificado aparece como próximo a vencer, selecciónelo y haga clic en <b>Renew</b> y luego en
<b>Renew certificate</b>. Los archivos nuevos se añaden a su historial, y los objetivos con despliegue automático los
reciben de inmediato.</p>''')

    add('<h2 id="a-deploy"><span class="k">La aplicación</span>Enviarlo a sus servidores</h2>')
    add('''<p>Cada certificado puede tener <b>objetivos de despliegue</b>: selecciónelo y, en <b>Deployment</b>, haga clic en
<b>Add target</b>. Hay dos tipos:</p>''')
    add(ul_list([
        '<b>CertificateGet Agent</b>: un servicio en un servidor Windows o Linux que escribe los archivos donde cada programa los '
        'espera, reinicia servicios y programas, ejecuta comandos e importa en TSplus. Su instalación es el resto de este volumen.',
        '<b>SFTP / SSH</b>: sube los archivos que elija a una carpeta y ejecuta un comando, sin instalar nada en el servidor. '
        'Ajustes predefinidos para HAProxy y nginx. Vea %s.' % xref(PG, 'a-sftp', 'Objetivos SFTP / SSH'),
    ]))
    add('<p>Los objetivos marcados con <b>Deploy automatically after every issuance / renewal</b> reciben cada certificado nuevo; <b>Deploy now</b> envía el actual en cualquier momento.</p>')

APP_NAV_GS_ES = [('La aplicación', ['a-overview', 'a-install', 'a-first', 'a-wildcard', 'a-use', 'a-renew', 'a-deploy'])]

# =====================================================================
#  2  GUÍA DEL PROGRAMADOR — la aplicación
# =====================================================================
def app_programmers_guide_es(add):
    add('<h2 id="a-methods"><span class="k">Validación</span>Elegir un método de validación</h2>')
    add('''<p>Antes de emitir, la autoridad de certificación comprueba que usted controla cada nombre, con una solicitud
HTTP al dominio o un registro TXT en su DNS. El método se elige para cada certificado en <b>New certificate</b>.</p>''')
    rows = []
    for m in APP_METHODS:
        if m in METHOD_DOC_ES:
            rows.append(['<b>%s</b>' % esc(METHOD_NAME.get(m, m)), METHOD_DOC_ES[m][0], METHOD_DOC_ES[m][1]])
    add(table(['Método', 'Cuándo usarlo', 'Necesita'], rows))
    add(note('info', 'Los comodines necesitan DNS', '<p>Un nombre comodín solo se puede demostrar por DNS. Los métodos HTTP se rechazan para certificados comodín.</p>'))
    add('<h3 id="a-http">Validación HTTP</h3>')
    add('''<p>La autoridad de certificación solicita <code>http://&lt;dominio&gt;/.well-known/acme-challenge/&lt;token&gt;</code>
en el puerto 80, siempre el 80, desde internet. Con <b>HTTP — built-in web server</b> la aplicación escucha en el puerto
80 de este PC durante los segundos que dura la comprobación; el puerto debe estar libre (detenga IIS o use el método de
carpeta del sitio) y redirigido a este PC. Otro puerto solo sirve detrás de una redirección desde el 80. Con
<b>HTTP — existing web site folder</b> la aplicación escribe el archivo del token en la carpeta del sitio (ruta local o
recurso UNC), añade un <code>web.config</code> para que IIS sirva archivos sin extensión y lo elimina todo al terminar.</p>''')
    add('<h3 id="a-dns">Validación DNS</h3>')
    add('''<p>La autoridad de certificación consulta un registro TXT en <code>_acme-challenge.&lt;dominio&gt;</code>. Antes
de pedirle que lo compruebe, la aplicación consulta los resolvedores públicos de Settings hasta que todos los registros
sean visibles, como máximo durante la espera de propagación (600 segundos de forma predeterminada). Con <b>DNS — manual
TXT record</b> usted crea los registros en su proveedor DNS desde el cuadro de diálogo, que tiene botones para copiar y
un botón <b>Check DNS now</b>. Borre los registros antiguos después de la solicitud; cada solicitud usa valores nuevos.</p>''')
    add('<h3 id="a-dns-api">APIs de proveedores DNS</h3>')
    add('<p>Los métodos con proveedor crean los registros TXT, esperan a que aparezcan y después eliminan solo sus propios valores. Escriba las credenciales en <b>Settings &rarr; DNS</b> y use el botón <b>Test</b> que hay junto a ellas.</p>')
    add(table(['Proveedor', 'Dónde obtener las credenciales', 'Notas'], [
        ['Cloudflare', 'My Profile &rarr; API Tokens &rarr; Create Token, con Zone &rarr; Zone &rarr; Read y Zone &rarr; DNS &rarr; Edit.', '<b>Test token</b> enumera las zonas que puede ver.'],
        ['Hostinger', 'hPanel &rarr; Account (icono del perfil) &rarr; API.', 'El dominio debe usar los servidores DNS de Hostinger. Los demás registros no se tocan.'],
        ['Constellix', 'Edit My Account &rarr; API Keys. La clave secreta solo se muestra una vez, al crear la clave.', 'Las solicitudes se firman con la hora actual; el reloj del PC debe estar en hora.'],
        ['DNS Made Easy', 'Config &rarr; Account Information muestra la clave de API y la clave secreta.', 'Firmadas con la hora actual, igual que Constellix.'],
        ['Namecheap', 'Profile &rarr; Tools &rarr; API Access: actívelo y añada la IPv4 pública de este PC a la lista blanca.', 'Namecheap solo permite el acceso a la API a cuentas que cumplen sus requisitos, y solo puede reemplazar la lista completa de registros de un dominio, así que la aplicación vuelve a escribir todos los demás registros sin cambios. Deje <b>Client IP</b> vacío para detectarla.'],
    ]))
    add('<h3 id="a-acmedns">acme-dns</h3>')
    add('''<p><a href="https://github.com/joohoi/acme-dns">acme-dns</a> es un pequeño servidor DNS que solo responde
consultas TXT de <code>_acme-challenge</code>. Úselo cuando su proveedor DNS no tenga API o no quiera credenciales DNS en
este PC. En la primera solicitud de un dominio la aplicación lo registra y muestra un CNAME que debe crear, por ejemplo
<code>_acme-challenge.example.com CNAME 8e5700ea-….auth.acme-dns.io</code>. Ese único CNAME cubre el dominio y su
comodín. Después, las renovaciones no requieren cambios de DNS. Los registros aparecen en <b>Settings</b>, con
<b>Copy target</b> y <b>Remove</b>.</p>''')
    add(note('warn', 'Use su propio servidor acme-dns en producción',
        '<p>El servidor público <code>auth.acme-dns.io</code> sirve para pruebas. Quien tenga las credenciales de un registro puede '
        'obtener certificados para ese dominio, así que en producción ejecute el suyo y escríbalo en <b>Settings &rarr; acme-dns server</b>.</p>'))

    # ---- autoridades
    add('<h2 id="ca"><span class="k">Autoridades de certificación</span>Let\'s Encrypt y ZeroSSL</h2>')
    add('''<p>La aplicación obtiene certificados de cualquiera de dos autoridades de certificación ACME, elegida para cada
certificado en <b>Certificate authority</b> de la página <b>New certificate</b>. El valor predeterminado para
certificados nuevos se fija en <b>Settings</b>. Ambas emiten certificados gratuitos y de confianza, válidos 90 días,
para un nombre, varios nombres o comodines.</p>''')
    add(table(['', 'Let\'s Encrypt', 'ZeroSSL'], [
        ['Servidor de pruebas', 'Sí: <b>Staging</b> emite certificados no confiables con límites amplios', 'No: cada solicitud es un certificado de confianza'],
        ['Cuenta', 'Se crea automáticamente; el correo de contacto es opcional', 'Debe estar vinculada a una cuenta de ZeroSSL (External Account Binding); vea más abajo'],
        ['Límites de uso', 'Sí, por ejemplo 5 validaciones fallidas por hora por cuenta y nombre, 50 certificados por dominio registrado por semana', 'Sin límites de uso ACME'],
        ['Tiempo de emisión', 'Segundos tras la validación', 'Normalmente segundos, a veces unos minutos; la aplicación espera hasta 10'],
        ['Cadena', 'Raíces ISRG', 'Raíces de Sectigo (USERTrust)'],
        ['Cuenta guardada', '<code>accounts\\staging.json</code>, <code>accounts\\production.json</code>', '<code>accounts\\zerossl.json</code>'],
    ]))
    add('''<p>Elija Let\'s Encrypt cuando quiera probar primero una configuración nueva en Staging. Elija ZeroSSL cuando haya
alcanzado un límite de Let\'s Encrypt, quiera ver los certificados en un panel de ZeroSSL o quiera una segunda autoridad
de respaldo.</p>''')
    add('<h3 id="ca-zerossl">Configurar ZeroSSL</h3>')
    add('''<p>ZeroSSL solo acepta cuentas ACME vinculadas a una cuenta de ZeroSSL. La aplicación obtiene de ZeroSSL las
credenciales de vinculación una sola vez, al crear la cuenta ACME de ZeroSSL, de una de estas dos formas:</p>''')
    add(ul_list([
        '<b>Con una clave de API de ZeroSSL</b> (recomendado). Inicie sesión en zerossl.com, abra <b>Developer</b>, copie la '
        '<b>API Access Key</b> y péguela en <b>Settings &rarr; ACME accounts &rarr; ZeroSSL API key</b>. La cuenta ACME queda '
        'vinculada a su cuenta de ZeroSSL, y los certificados que emite aparecen en su panel de ZeroSSL.',
        '<b>Solo con el correo de contacto.</b> Deje vacía la clave de API y escriba el correo de contacto en el certificado (o el '
        'predeterminado en Settings). ZeroSSL crea una cuenta para esa dirección si no tiene una, o reutiliza la existente.',
    ]))
    add('''<p>Después solo se usa la clave de la cuenta ACME guardada en <code>accounts\\zerossl.json</code>; la clave de API
no se vuelve a leer. Para pasar a otra cuenta de ZeroSSL, cambie la clave o el correo y haga clic en <b>Reset
accounts</b> en Settings. Eso también restablece las cuentas de Let\'s Encrypt; se vuelven a crear automáticamente en
la siguiente solicitud, y los certificados emitidos no se ven afectados.</p>''')
    add(note('warn', 'ZeroSSL no tiene servidor de pruebas',
        '<p>Cada solicitud a ZeroSSL emite un certificado real y de confianza. Para probar un método de validación o una '
        'configuración DNS nuevos, ejecútelo una vez con Let\'s Encrypt Staging y luego cambie el certificado a ZeroSSL.</p>'))
    add('<h3 id="ca-switch">Cambiar un certificado a la otra autoridad</h3>')
    add('''<p>Abra el certificado, haga clic en <b>Renew</b>, cambie <b>Certificate authority</b> y solicítelo. Los archivos
nuevos se añaden al historial del mismo certificado, los objetivos de despliegue no cambian, y las renovaciones
siguientes usan la última autoridad elegida. Al agente no le importa qué autoridad emitió el certificado: recibe los
mismos orígenes en ambos casos. Solo cambia <code>fullchain-root</code>, porque termina en la raíz de esa autoridad.</p>''')

    # ---- certificados
    add('<h2 id="a-certs"><span class="k">Certificados</span>Certificados, emisiones y archivos</h2>')
    add('''<p>Un <b>certificado</b> en la aplicación es una definición: sus nombres, método de validación, autoridad, tipo
de clave, contraseña PFX y objetivos de despliegue. Cada solicitud correcta añade una <b>emisión</b>, una carpeta con
fecha que contiene los archivos, a su historial. <b>Renew</b> reutiliza la definición; la última emisión es la que se
exporta, instala y despliega.</p>''')
    add(ul_list([
        '<b>Una clave privada nueva en cada emisión.</b> Cada solicitud genera una clave nueva del tipo elegido; nunca se reutiliza una clave.',
        '<b>Archivos escritos:</b> los formatos obligatorios (certificado, clave y cadena en PEM), a partir de los cuales se generan las exportaciones y renovaciones, más los formatos marcados en <b>Settings &rarr; Files written for new certificates</b>. Cualquier otro formato se puede generar después con <b>Export</b>. La lista completa está en la %s.' % xref(RF, 'a-formats', 'Referencia'),
        '<b>Los nombres de archivo</b> salen del primer dominio, con <code>*</code> escrito como <code>wildcard</code>: <code>wildcard.example.com.pfx</code>.',
        '<b>Las opciones de salida</b> de Settings se aplican a las emisiones y exportaciones nuevas: <code>.key</code> PKCS#8 o tradicional, PFX 3DES o AES-256 y la contraseña JKS.',
    ]))
    add('<h3 id="a-keytypes">Tipos de clave</h3>')
    add(table(['Tipo de clave', 'Notas'], [[KEYTYPE_DOC_ES[k][0], KEYTYPE_DOC_ES[k][1]] for k in APP_KEYTYPES if k in KEYTYPE_DOC_ES]))
    add('<h3 id="a-expiry">Vencimiento y renovación</h3>')
    add('''<p>La lista y los totales de la parte superior de <b>Certificates</b> cuentan los certificados como válidos,
próximos a vencer (dentro de <b>Settings &rarr; Warn when a certificate expires within</b>, 30 días de forma
predeterminada) o vencidos / no emitidos. La renovación es manual: <b>Renew</b> y luego <b>Renew certificate</b>. La
aplicación no se ejecuta en segundo plano ni de forma programada, así que revísela con regularidad, o renueve todos los
certificados próximos a vencer cuando la abra. Let\'s Encrypt ya no envía avisos de vencimiento por correo.</p>''')

    # ---- despliegue
    add('<h2 id="a-targets"><span class="k">Despliegue</span>Objetivos de despliegue</h2>')
    add('''<p>Un certificado puede tener cualquier número de objetivos. Tras cada emisión o renovación, cada objetivo
habilitado con <b>Deploy automatically</b> recibe los archivos nuevos; el resultado aparece en el objetivo y en el
registro de actividad. Un objetivo que falla no afecta a los demás ni a la emisión; corríjalo y haga clic en <b>Deploy now</b>.</p>''')
    add(table(['', 'CertificateGet Agent', 'SFTP / SSH'], [
        ['En el servidor', 'El servicio del agente (Windows o Linux)', 'Nada; un inicio de sesión SSH'],
        ['Adónde van los archivos', 'Se decide en el servidor en <code>agent.json</code>: muchas carpetas, nombres por programa', 'Una carpeta y los archivos que enumere en el objetivo'],
        ['Después', 'Reinicia servicios y programas de escritorio; ejecuta comandos; importa en TSplus; copias de seguridad', 'Un comando por SSH'],
        ['Seguridad', 'HTTPS con huella fijada, clave de API, lista de IP permitidas', 'SSH con clave de host fijada; contraseña o clave privada'],
    ]))
    add('<h3 id="a-sftp">Objetivos SFTP / SSH</h3>')
    add(steps([
        '<div>Escriba el <b>Host</b>, el <b>Port</b> (22) y el <b>User</b>, y elija <b>Password</b> o <b>Private key file</b> '
        '(con su frase de paso, si la tiene). Ambos se guardan cifrados.</div>',
        '<div>Escriba la <b>Remote folder</b> y los <b>Files to upload</b>: un tipo de archivo y el nombre remoto de cada uno. '
        '<code>{domain}</code> en un nombre se convierte en el primer dominio y <code>{name}</code> en el nombre del certificado. Los '
        'ajustes predefinidos <b>HAProxy</b> y <b>nginx</b> rellenan una carpeta, los archivos y el comando.</div>',
        '<div>Opcionalmente, un <b>Command to run afterwards</b>, que se ejecuta por SSH con el mismo usuario. El despliegue falla '
        'si termina con un código distinto de cero.</div>',
        '<div>Haga clic en <b>Test connection</b>. La primera vez, la aplicación muestra la huella de la clave de host SSH del '
        'servidor; confíe en ella y la aplicación rechazará cualquier otra clave. <b>Forget host key</b> la borra tras reconstruir un servidor.</div>',
    ]))
    add('''<p>Cada archivo se sube como <code>&lt;nombre&gt;.cg-upload</code> y luego se renombra sobre el anterior, así que
el servidor nunca lee medio archivo. Los archivos que contienen la clave privada reciben modo 600 y los demás 644. Los
tipos de archivo están en la %s.</p>''' % xref(RF, 'a-sftp-files', 'Referencia'))

    # ---- almacén
    add('<h2 id="a-store"><span class="k">Almacenamiento</span>Carpeta del almacén, secretos y registro de actividad</h2>')
    add('''<p>Todo lo que guarda la aplicación está en la carpeta del almacén (<b>Settings &rarr; Store folder</b>,
predeterminada <code>%%LOCALAPPDATA%%\\CertificateGet\\Store</code>): los certificados y su historial, las claves de las
cuentas ACME y el registro de actividad. Los ajustes están en <code>%%LOCALAPPDATA%%\\CertificateGet\\settings.json</code>.
La estructura está en la %s.</p>''' % xref(RF, 'a-layout', 'Referencia'))
    add(ul_list([
        '<b>Cifrado con DPAPI de Windows</b> para su usuario de Windows: contraseñas PFX, claves de cuentas ACME, tokens y claves DNS, '
        'contraseñas de acme-dns, contraseñas SFTP y frases de paso de claves, claves de API de agentes. Otro usuario de Windows, u '
        'otro PC, no puede descifrarlos; la aplicación crea entonces una cuenta ACME nueva y vuelve a pedir los demás secretos.',
        '<b>Sin cifrar:</b> las claves privadas de los archivos <code>.key</code> y <code>.pem</code>, como en cualquier servidor web. '
        'Mantenga el almacén fuera de carpetas sincronizadas en la nube y legible solo por usted.',
        '<b>Registro de actividad:</b> cada solicitud, validación, exportación, instalación, despliegue y error, en '
        '<code>activity.jsonl</code>. La página <b>Activity log</b> lo busca y filtra, y exporta a CSV.',
    ]))

APP_NAV_PG_ES = [('Validación', ['a-methods', 'a-http', 'a-dns', 'a-dns-api', 'a-acmedns']),
                 ('Autoridades de certificación', ['ca', 'ca-zerossl', 'ca-switch']),
                 ('Certificados', ['a-certs', 'a-keytypes', 'a-expiry']),
                 ('Despliegue', ['a-targets', 'a-sftp']),
                 ('Almacenamiento', ['a-store'])]

# =====================================================================
#  3  GUÍA DE PLANTILLAS — recetas en la aplicación
# =====================================================================
def app_template_guide_es(add):
    add('<h2 id="a-iis"><span class="k">Receta</span>Sitio IIS en el mismo servidor</h2>')
    add('<p>Ejecute CertificateGet como administrador en el servidor IIS.</p>')
    add(table(['Ajuste', 'Valor'], [
        ['Validation method', '<b>HTTP — existing web site folder</b>, carpeta del sitio <code>C:\\inetpub\\wwwroot</code> (la ruta física del sitio)'],
        ['Key type', 'RSA 2048'],
        ['Tras emitir', '<b>Install in Windows</b> &rarr; <i>Local Machine</i>, y en el Administrador de IIS edite el enlace <b>https</b> del sitio y elija el certificado nuevo'],
    ]))
    add('<p>Tras cada renovación instale el certificado nuevo y vuelva a elegirlo en el enlace. Para automatizarlo, use un agente con un comando que actualice el enlace.</p>')

    add('<h2 id="a-wild-cf"><span class="k">Receta</span>Comodín con Cloudflare</h2>')
    add(table(['Ajuste', 'Valor'], [
        ['Settings', 'Token de API de Cloudflare con Zone Read y DNS Edit; <b>Test token</b>'],
        ['Domains', '<b>Wildcard</b>, dominio base <code>example.com</code>, <b>Also cover the bare domain</b> marcado'],
        ['Validation method', '<b>DNS — Cloudflare (automatic)</b>'],
        ['Resultado', 'Un certificado para <code>*.example.com</code> y <code>example.com</code>; las renovaciones no requieren pasos manuales'],
    ]))
    add('<p>Lo mismo funciona con Hostinger, Constellix, DNS Made Easy, Namecheap o acme-dns: solo cambian el método y sus credenciales.</p>')

    add('<h2 id="a-sftp-haproxy"><span class="k">Receta</span>HAProxy por SFTP</h2>')
    add('<p>Para un servidor HAProxy donde basta con subir el archivo y recargar. Añada un objetivo de tipo <b>SFTP / SSH</b> y haga clic en el ajuste predefinido <b>HAProxy</b>:</p>')
    add(table(['Campo', 'Valor predefinido'], [
        ['Remote folder', '<code>/etc/haproxy/certs</code>'],
        ['Files', '<code>combined</code> &rarr; <code>{domain}.pem</code>'],
        ['Command', '<code>haproxy -c -f /etc/haproxy/haproxy.cfg &amp;&amp; systemctl reload haproxy</code>'],
    ]))
    add('<p>El usuario debe poder escribir en la carpeta y recargar HAProxy (root, o permisos sudo para el comando). Para varios programas en un servidor, o Cockpit junto a HAProxy, use el agente (%s).</p>' % xref(TG, 'haproxy', 'plantilla de HAProxy'))

    add('<h2 id="a-sftp-nginx"><span class="k">Receta</span>nginx por SFTP</h2>')
    add(table(['Campo', 'Valor predefinido'], [
        ['Remote folder', '<code>/etc/nginx/ssl</code>'],
        ['Files', '<code>fullchain</code> &rarr; <code>{domain}.crt</code>, <code>key</code> &rarr; <code>{domain}.key</code>'],
        ['Command', '<code>nginx -t &amp;&amp; systemctl reload nginx</code>'],
    ]))
    add(code('''ssl_certificate     /etc/nginx/ssl/www.example.com.crt;
ssl_certificate_key /etc/nginx/ssl/www.example.com.key;''', 'text'))

APP_NAV_TG_ES = [('Recetas en la aplicación', ['a-iis', 'a-wild-cf', 'a-sftp-haproxy', 'a-sftp-nginx'])]

# =====================================================================
#  4  REFERENCIA — la aplicación, generada desde sus fuentes
# =====================================================================
def app_reference_es(add):
    for f in APP_FORMATS:
        if f['id'] not in FORMAT_DOC_ES: PROBLEMS.append('es/reference: format %s has no FORMAT_DOC_ES entry' % f['id'])
    for a, _, _ in APP_SOURCES:
        if a not in SOURCE_LABEL_ES: PROBLEMS.append('es/reference: SFTP file type %s has no SOURCE_LABEL_ES entry' % a)
    for p in PLACEHOLDERS:
        if p not in APP_PLACEHOLDER_DOC_ES: PROBLEMS.append('es/reference: placeholder %s has no APP_PLACEHOLDER_DOC_ES entry' % p)

    add('<h2 id="a-formats"><span class="k">La aplicación</span>Formatos de archivo</h2>')
    add('''<p>Todos los formatos que la aplicación puede escribir, leídos de <code>CertificateStore.cs</code>. <code>name</code>
es el primer dominio. Los formatos <i>Siempre</i> se escriben en cada emisión; los <i>Predeterminado</i> están marcados en
Settings hasta que usted los cambie; los <i>Opcional</i> se escriben si se marcan en Settings, o con <b>Export</b>. Los
formatos que necesitan contraseña usan la contraseña PFX del certificado.</p>''')
    rows = []
    for f in APP_FORMATS:
        if f['id'] not in FORMAT_DOC_ES: continue
        state = 'Siempre' if f['required'] else 'Predeterminado' if f['on'] else 'Opcional'
        rows.append(['<code>%s</code>' % '</code> <code>'.join(esc(n) for n in f['files']),
                     esc(FORMAT_DOC_ES[f['id']][0]), esc(FORMAT_DOC_ES[f['id']][1]), state])
    add(table(['Archivos', 'Formato', 'Contenido y uso', 'Se escribe'], rows))

    add('<h2 id="a-methods-ref"><span class="k">La aplicación</span>Métodos de validación</h2>')
    add(table(['Método', 'Cuándo usarlo', 'Necesita'],
              [['<b>%s</b>' % esc(METHOD_NAME.get(m, m)), METHOD_DOC_ES[m][0], METHOD_DOC_ES[m][1]] for m in APP_METHODS if m in METHOD_DOC_ES]))

    add('<h2 id="a-settings"><span class="k">La aplicación</span>Ajustes</h2>')
    add('<p>Cada valor de <code>settings.json</code>, leído de <code>AppSettings</code> en <code>Models.cs</code>, con el lugar donde se cambia en la página <b>Settings</b>.</p>')
    add(table(['Ajuste', 'En la aplicación', 'Qué hace'],
              [['<code>%s</code>' % s, SETTINGS_DOC_ES[s][0], SETTINGS_DOC_ES[s][1]] for s in APP_SETTINGS if s in SETTINGS_DOC_ES]))

    add('<h2 id="a-sftp-files"><span class="k">La aplicación</span>Tipos de archivo y marcadores SFTP</h2>')
    add('<p>Los tipos de archivo que puede subir un objetivo SFTP, leídos de <code>DeployService.cs</code>. Son los mismos nombres que el agente usa como orígenes. Los marcados como <i>secreto</i> contienen la clave privada y se suben con modo 600.</p>')
    add(table(['Tipo', 'Archivo'], [['<code>%s</code>%s' % (a, '<span class="tag tag--warn">secreto</span>' if a in APP_SECRETS else ''), esc(SOURCE_LABEL_ES.get(a, lbl))]
                                    for a, _, lbl in APP_SOURCES]))
    add(table(['Marcador', 'Se sustituye por'], [['<code>%s</code>' % p, APP_PLACEHOLDER_DOC_ES.get(p, '??')] for p in PLACEHOLDERS]))

    add('<h2 id="a-layout"><span class="k">La aplicación</span>Estructura de la carpeta del almacén</h2>')
    add(code('''%LOCALAPPDATA%\\CertificateGet\\
  settings.json                          ajustes (secretos cifrados con DPAPI)
  help\\                                  este manual, escrito al abrir Help
  Store\\                                 la carpeta del almacén (Settings → Store folder)
    activity.jsonl                       registro de actividad, una línea JSON por entrada
    accounts\\
      staging.json, production.json      claves de las cuentas ACME de Let's Encrypt (DPAPI)
      zerossl.json                       clave de la cuenta ACME de ZeroSSL (DPAPI)
    certificates\\
      wildcard.example.com_<id>\\
        profile.json                     nombres, método, opciones, objetivos, historial
        2026-09-24_231200\\               una carpeta por emisión, con sus archivos''', 'text'))
    _app_checks('es', METHOD_DOC_ES, KEYTYPE_DOC_ES, SETTINGS_DOC_ES)

APP_NAV_RF_ES = [('La aplicación', ['a-formats', 'a-methods-ref', 'a-settings', 'a-sftp-files', 'a-layout'])]
