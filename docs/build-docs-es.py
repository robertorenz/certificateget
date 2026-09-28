# Los cuatro volúmenes en español.  No se ejecuta solo: build-docs.py lo carga en su
# propio espacio de nombres, así que usa los mismos ayudantes (code, note, table, steps,
# flow, page, field_rows...), los mismos datos leídos de las fuentes (CLASSES, SOURCES,
# KINDS, CLI, ENDPOINTS...) y las mismas comprobaciones.  Cada volumen debe tener
# exactamente los encabezados (ids) del volumen inglés, en el mismo orden; la compilación
# falla si no.  La interfaz de la aplicación y la salida del agente están en inglés, así
# que los nombres de botones, campos de agent.json y mensajes se citan tal cual.

# ---------------------------------------------------------------- textos de la referencia
FIELD_DOC_ES = {
 'AgentConfig.Port':        'Puerto TCP en el que escucha el agente (HTTPS). Cambiarlo requiere reiniciar el servicio, y la regla de firewall que abre <code>install</code> solo cubre el puerto indicado al instalar.',
 'AgentConfig.ApiKeyHash':  'SHA-256 (hex) de la clave de API. La clave en sí solo se muestra una vez, con <code>install</code> o <code>newkey</code>.',
 'AgentConfig.AllowedIps':  'Lista opcional de IP de clientes que pueden conectarse. Vacía = cualquiera.',
 'AgentConfig.TlsPfx':      'Nombre del archivo del certificado TLS propio del agente, junto al ejecutable. Se crea en el primer arranque.',
 'AgentConfig.TlsPassword': 'Contraseña de <code>TlsPfx</code>. La escribe el agente; no la modifique. Si no abre el PFX (por ejemplo, después de copiar el archivo de ejemplo sobre <code>agent.json</code>), el agente crea un certificado TLS nuevo y la aplicación le pide confiar en la nueva huella digital.',
 'AgentConfig.KeepBackups': 'Cuántas versiones anteriores de los archivos de cada destino se conservan en <code>backups/</code>. <code>0</code> desactiva las copias de seguridad.',
 'AgentConfig.Slots':       'Los slots que ofrece este agente. Un objetivo de despliegue de la aplicación nombra exactamente uno de ellos.',
 'Slot.Name':               'El nombre que usa el objetivo de despliegue de la aplicación. Se compara sin distinguir mayúsculas; debe ser único en el archivo.',
 'Slot.Description':        'Texto libre que se muestra en la aplicación al hacer clic en <b>Connect &amp; list slots</b>.',
 'Slot.Destinations':       'Adónde va este certificado en este servidor, procesado en orden.',
 'Slot.Commands':           'Comandos que se ejecutan una vez, después de escribir todos los destinos y reiniciar sus servicios.',
 'Destination.Name':        'Etiqueta usada en el registro de actividad de la aplicación, en <code>agent.log</code> y como nombre de la carpeta de copias. Si está vacía se usa <code>Folder</code>.',
 'Destination.Kind':        '<code>Files</code> (predeterminado): escribe los archivos de <code>Files</code> en <code>Folder</code>. <code>TSplus</code>: importa un PFX con el CertificateManager de TSplus (TSplus 15+). <code>TSplusJks</code>: escribe <code>cert.jks</code> en <code>TSplus\\Clients\\webserver</code> y reinicia el servidor web de TSplus (versiones que usan <code>cert.jks</code>).',
 'Destination.TsplusCertFolder': 'Solo TSplus: su carpeta <code>cert</code> (predeterminada <code>C:\\Program Files (x86)\\TSplus\\UserDesktop\\files\\cert</code>).',
 'Destination.TsplusFolder':     'Solo TSplusJks: carpeta de instalación de TSplus (predeterminada <code>C:\\Program Files (x86)\\TSplus</code>).',
 'Destination.Folder':      'Carpeta de destino para <code>Files</code>. Se crea si no existe. Sustituye a <code>{folder}</code> en los <code>Commands</code> de este destino.',
 'Destination.Files':       'Los archivos que se escriben, cada uno con un origen y el nombre de archivo que recibe.',
 'Destination.RestartServices': 'Nombres de servicios de Windows (o unidades systemd en Linux) que se reinician después de escribir los archivos.',
 'Destination.RestartPrograms': 'Solo Windows: programas de escritorio (.exe) que se cierran y se vuelven a iniciar en el escritorio del mismo usuario.',
 'Destination.Commands':    'Comandos que se ejecutan después de escribir los archivos (<code>cmd.exe</code> en Windows, <code>/bin/sh</code> en Linux).',
 'FileSpec.Source':         'El formato del archivo: uno de los <a href="#sources">orígenes de archivo</a>.',
 'FileSpec.FileName':       'Nombre del archivo en <code>Folder</code>. Un archivo existente se copia como respaldo y luego se reemplaza de forma atómica.',
 'ProgramSpec.Path':        'Ruta completa del .exe.',
 'ProgramSpec.Arguments':   'Argumentos usados solo cuando el programa no estaba en ejecución (si no, cada instancia conserva su propia línea de comandos).',
 'ProgramSpec.WorkingFolder':      'Carpeta de trabajo del nuevo proceso; de forma predeterminada, la carpeta del exe.',
 'ProgramSpec.StopTimeoutSeconds': 'Segundos de espera a un cierre normal antes de terminar el proceso a la fuerza.',
 'ProgramSpec.StartIn':     'Dónde vuelve el programa: uno de los <a href="#startin">valores de StartIn</a>.',
 'DeployRequest.Slot':        'Slot en el que se despliega.',
 'DeployRequest.Certificate': 'Nombre, dominios, huella y vencimiento, usados para el registro.',
 'DeployRequest.PfxPassword': 'La contraseña PFX del certificado, desde la aplicación. Solo se envía cuando el slot necesita <code>pfx</code>, <code>p12</code> o <code>encrypted-key</code>.',
 'DeployRequest.Files':       'Nombre del origen &rarr; contenido del archivo en Base64. Exactamente los orígenes que pidió el slot.',
 'CertInfo.Name':        'Nombre del certificado en la aplicación.',
 'CertInfo.Domains':     'Todos los dominios del certificado.',
 'CertInfo.Thumbprint':  'Huella SHA-1 del certificado emitido.',
 'CertInfo.NotAfter':    'Fecha de vencimiento.',
}

CLASS_DOC_ES = {
 'AgentConfig': 'agent.json: está junto al ejecutable y se vuelve a leer en cada solicitud, así que los cambios se aplican de inmediato.',
 'Slot':        'A qué corresponde en este servidor un certificado de la aplicación: uno o más destinos.',
 'ProgramSpec': 'Un programa de escritorio (no un servicio) que se reinicia después de escribir los archivos del certificado.',
}

SOURCE_DOC_ES = {
 'fullchain':         ('PEM', 'El certificado seguido de los intermedios. Lo que nginx, Apache 2.4.8+, NetTalk y la mayoría de los servidores esperan como archivo de certificado.'),
 'key':               ('PEM', 'Clave privada (PKCS#8 o tradicional, según se elija en la aplicación).'),
 'combined':          ('PEM', 'Cadena completa y clave privada en un solo archivo. HAProxy, <code>.cert</code> de Cockpit, Webmin, dispositivos.'),
 'combined-keyfirst': ('PEM', 'Primero la clave privada, luego la cadena completa. Postfix, lighttpd, Pound.'),
 'cer':               ('PEM', 'Solo el certificado.'),
 'crt':               ('PEM', 'Solo el certificado, los mismos bytes que <code>cer</code>.'),
 'der':               ('binario', 'Solo el certificado, codificado en DER. Java y algunas herramientas de Windows.'),
 'chain':             ('PEM', 'Solo los intermedios. <code>SSLCertificateChainFile</code> de Apache.'),
 'fullchain-root':    ('PEM', 'Cadena completa más la raíz, para dispositivos que validan toda la cadena.'),
 'encrypted-key':     ('PEM', 'Clave privada cifrada con la contraseña PFX del certificado.'),
 'pfx':               ('PKCS#12', 'Certificado, cadena y clave, protegidos con la contraseña PFX del certificado. IIS, Exchange, Windows.'),
 'p12':               ('PKCS#12', 'Lo mismo que <code>pfx</code> con extensión <code>.p12</code>. Java/Tomcat, macOS.'),
 'p7b':               ('PKCS#7', 'Certificado y cadena, sin clave.'),
 'k8s':               ('YAML', 'Secret de Kubernetes <code>kubernetes.io/tls</code>.'),
 'jks':               ('Java KeyStore', 'Clave y cadena completa; contraseña tomada de <b>Settings</b> de la aplicación (predeterminada <code>secret</code>, que TSplus exige).'),
}

KIND_DOC_ES = {
 'Files':     'Escribe <code>Files</code> en <code>Folder</code>. Es el valor predeterminado cuando se omite <code>Kind</code>.',
 'TSplus':    'TSplus 15 o posterior. Importa el PFX con <code>CertificateManager.exe /add</code>. Se ignoran <code>Folder</code> y <code>Files</code>.',
 'TSplusJks': 'Versiones de TSplus que usan <code>cert.jks</code>. Reemplaza <code>Clients\\webserver\\cert.jks</code> y ejecuta <code>AdminTool.exe /webrestart</code>.',
}

STARTIN_DOC_ES = {
 'samesession': ('SameSession', 'Predeterminado. Cada copia vuelve al escritorio en el que se ejecutaba; si no se estaba ejecutando, al escritorio de la consola.'),
 'console':     ('Console', 'Siempre en el escritorio del usuario de la consola.'),
 'background':  ('Background', 'Sesión 0, con la cuenta del agente (LocalSystem), sin ventana visible.'),
}

#  Descripción breve (la de la ayuda del agente, traducida), ejemplo y explicación.
CLI_DOC_ES = {
 'install':   'crea agent.json, la clave de API y el certificado TLS, registra e inicia el servicio',
 'uninstall': 'detiene y elimina el servicio (conserva agent.json)',
 'newkey':    'genera una nueva clave de API (la anterior deja de funcionar)',
 'info':      'muestra la URL, la huella TLS y los slots configurados',
 'check':     'valida agent.json (carpetas, servicios, tipos de archivo)',
 'run':       'se ejecuta en esta consola (predeterminado)',
}

CLI_USE_ES = {
 'install':   ('.\\CertificateGet.Agent.exe install 9443', 'Crea <code>agent.json</code> (conservando uno existente), una nueva clave de API cuando no hay una válida y el certificado TLS; registra e inicia el servicio y, en Windows, abre el firewall. Se puede volver a ejecutar sin riesgo, por ejemplo tras mover el ejecutable.'),
 'uninstall': ('sudo ./CertificateGet.Agent uninstall', 'Detiene y elimina el servicio y la regla de firewall. <code>agent.json</code>, el certificado TLS y las copias de seguridad se conservan.'),
 'newkey':    ('.\\CertificateGet.Agent.exe newkey', 'Reemplaza la clave de API. Escriba la nueva clave en cada objetivo de la aplicación que use este agente. No hace falta reiniciar.'),
 'info':      ('sudo ./CertificateGet.Agent info', 'Muestra la URL, la huella TLS para comparar con la aplicación, las IP permitidas y, por slot, los orígenes que necesita.'),
 'check':     ('.\\CertificateGet.Agent.exe check', 'Valida <code>agent.json</code>: sintaxis JSON, clave de API, IP, slots duplicados, carpetas, orígenes, servicios, programas, rutas de TSplus. Código de salida 0 si todo está bien.'),
 'run':       ('sudo ./CertificateGet.Agent run', 'Se ejecuta en la consola con el registro en pantalla. Detenga antes el servicio, porque ambos usan el mismo puerto.'),
}

ENDPOINT_DOC_ES = {
 '/api/slots':        ('Lista los slots. Lo usa <b>Connect &amp; list slots</b>.', ENDPOINT_DOC['/api/slots'][1]),
 '/api/slots/{name}': ('Los orígenes que necesita un slot, para que la aplicación genere y envíe exactamente esos. 404 si el slot no existe.', ENDPOINT_DOC['/api/slots/{name}'][1]),
 '/api/deploy':       ('Ejecuta un despliegue. El cuerpo es un <code>DeployRequest</code>; la respuesta enumera cada paso.', ENDPOINT_DOC['/api/deploy'][1]),
}

# =====================================================================
#  1  PRIMEROS PASOS
# =====================================================================
def build_getting_started_es():
    B = []; add = B.append

    add('<h2 id="what"><span class="k">Introducción</span>Qué hace el agente</h2>')
    add('''<p>El agente es un pequeño servicio que se instala en cada servidor que usa un certificado. Cuando la
aplicación CertificateGet emite o renueva un certificado, envía los archivos al agente por HTTPS. El agente los
escribe donde cada programa los espera, reinicia lo que haga falta e informa de cada paso en el registro de
actividad de la aplicación.</p>''')
    add(flow([
        ('CertificateGet', 'La aplicación de escritorio de su PC emite o renueva el certificado con Let\'s Encrypt o ZeroSSL.', ''),
        ('HTTPS :9443', 'La aplicación pregunta al agente qué archivos necesita el slot y envía exactamente esos. Comprueba la huella TLS fijada del agente y envía la clave de API.', ''),
        ('Agente', 'Un servicio de Windows o una unidad systemd en el servidor. Lee <code>agent.json</code>, busca el slot y respalda los archivos actuales.', 'srv'),
        ('Destinos', 'Archivos escritos en cada carpeta con los nombres que espera cada programa, o importados en TSplus.', 'srv'),
        ('Reinicios', 'Servicios, programas de escritorio y comandos (por ejemplo <code>systemctl reload haproxy</code>), cada uno una vez.', 'srv'),
    ]))
    add('''<p>Use el agente en servidores que necesitan algo más que copiar un archivo: varias instancias de NetTalk con
sus propios nombres de archivo, TSplus, programas de escritorio que hay que reiniciar en el escritorio de un usuario.
Para un solo servidor Linux donde basta con subir el archivo y ejecutar un comando, el objetivo SFTP de la aplicación
funciona sin instalar nada.</p>''')

    add('<h2 id="build"><span class="k">Paso 1</span>Obtener los ejecutables</h2>')
    add('''<p>Compile ambos agentes con el script de publicación de la raíz del repositorio. Son archivos únicos
autocontenidos, así que los servidores no necesitan el runtime de .NET.</p>''')
    add(code(r'''.\publish.ps1
# run\agent-windows\CertificateGet.Agent.exe   servidores Windows (NetTalk, TSplus, IIS)
# run\agent-linux\CertificateGet.Agent         servidores Linux (HAProxy, Cockpit, nginx), x64''', 'powershell'))
    add('''<p>Cada carpeta de salida recibe además <code>README.md</code> y <code>agent.example.json</code>. El ejemplo es
para leerlo. No lo copie sobre <code>agent.json</code>: contiene marcadores en lugar del hash real de la clave y de la
contraseña TLS.</p>''')

    add('<h2 id="win"><span class="k">Paso 2a</span>Instalar en Windows</h2>')
    add('<p>Hágalo en el servidor que ejecuta NetTalk, TSplus u otros programas que usan el certificado.</p>')
    add(steps([
        '<div><b>Copie el ejecutable</b> a una carpeta propia. El agente guarda <code>agent.json</code>, su certificado '
        'TLS, el registro y las copias de seguridad junto al ejecutable.'
        + code(r'''New-Item -ItemType Directory "C:\Program Files\CertificateGet Agent" -Force
Copy-Item .\CertificateGet.Agent.exe "C:\Program Files\CertificateGet Agent\"''', 'powershell') + '</div>',
        '<div><b>Abra PowerShell como administrador</b> y ejecute <code>install</code>. Añada un número de puerto si el 9443 está ocupado.'
        + code(r'''cd "C:\Program Files\CertificateGet Agent"
.\CertificateGet.Agent.exe install          # o bien: .\CertificateGet.Agent.exe install 9543''', 'powershell') + '</div>',
        '<div><b>Copie los tres valores que muestra.</b> La clave de API solo se muestra esta vez. (El agente escribe en inglés.)'
        + code('''Installed. Enter these in CertificateGet → certificate → Deployment → Add target → Agent:
  URL          https://WEB01:9443
  API key      q3ZtY0n8…Kd4      (shown once — store it now)
  Fingerprint  5E1B7C…90AF   (the app asks you to confirm this on first connect)''', 'text') + '</div>',
        '<div><b>Compruebe el servicio.</b> <code>info</code> vuelve a mostrar la URL y la huella en cualquier momento.'
        + code(r'''Get-Service %s
.\CertificateGet.Agent.exe info''' % SERVICE, 'powershell') + '</div>',
    ]))
    add('<h3 id="win-what">Qué cambia install en Windows</h3>')
    add(table(['Elemento', 'Detalle'], [
        ['Servicio', '<code>%s</code> (nombre para mostrar <i>CertificateGet Agent</i>), inicio automático retrasado, se ejecuta como LocalSystem, Windows lo reinicia 60 segundos después de un fallo.' % SERVICE],
        ['Firewall', 'Regla de entrada <i>%s</i> para el puerto TCP.' % FIREWALL],
        ['<code>agent.json</code>', 'Se crea con un slot de ejemplo, salvo que ya exista. Se guarda el hash de la clave de API, nunca la clave.'],
        ['<code>agent-tls.pfx</code>', 'Certificado TLS autofirmado del agente, válido 20 años. La aplicación fija su huella SHA-256.'],
    ]))

    add('<h2 id="linux"><span class="k">Paso 2b</span>Instalar en Linux</h2>')
    add('<p>Para HAProxy, Cockpit, nginx y otros servicios de Linux. El agente se ejecuta como root porque escribe en <code>/etc</code> y reinicia servicios.</p>')
    add(steps([
        '<div><b>Copie el binario al servidor.</b>'
        + code('scp run/agent-linux/CertificateGet.Agent admin@lb01:/tmp/', 'bash') + '</div>',
        '<div><b>Póngalo en su propia carpeta.</b> Use <code>cp</code>, no <code>mv</code>: en sistemas con SELinux, un archivo '
        'movido desde <code>/tmp</code> conserva una etiqueta con la que systemd se niega a ejecutarlo.'
        + code('''sudo mkdir -p /opt/certificateget-agent
sudo cp /tmp/CertificateGet.Agent /opt/certificateget-agent/
sudo chmod +x /opt/certificateget-agent/CertificateGet.Agent''', 'bash') + '</div>',
        '<div><b>Instale.</b> Copie la URL, la clave de API y la huella que muestra.'
        + code('''cd /opt/certificateget-agent
sudo ./CertificateGet.Agent install         # o bien: sudo ./CertificateGet.Agent install 9543''', 'bash') + '</div>',
        '<div><b>Abra el puerto.</b> Solo una de estas líneas corresponde a su distribución.'
        + code('''sudo ufw allow 9443/tcp                                                          # Ubuntu / Debian
sudo firewall-cmd --permanent --add-port=9443/tcp && sudo firewall-cmd --reload  # RHEL / Rocky / Alma / Fedora''', 'bash') + '</div>',
        '<div><b>Compruebe que está en ejecución.</b>'
        + code('''systemctl status %s
sudo ./CertificateGet.Agent info
journalctl -u %s -f          # registro en vivo; las mismas líneas van a agent.log''' % (UNIT, UNIT), 'bash') + '</div>',
    ]))
    add('<h3 id="linux-what">Qué cambia install en Linux</h3>')
    add(table(['Elemento', 'Detalle'], [
        ['Unidad systemd', '<code>/etc/systemd/system/%s.service</code>, <code>Type=notify</code>, <code>Restart=on-failure</code>, habilitada e iniciada.' % UNIT],
        ['SELinux', 'Con SELinux habilitado, el ejecutable se etiqueta <code>bin_t</code> (<code>semanage fcontext</code> + <code>restorecon</code>, o <code>chcon</code> como alternativa).'],
        ['Firewall', 'No se toca. Abra el puerto usted mismo (paso 4).'],
        ['Archivos', '<code>agent.json</code> y <code>agent-tls.pfx</code> se crean con modo 600.'],
    ]))

    add('<h2 id="slot"><span class="k">Paso 3</span>Describir adónde va el certificado</h2>')
    add('''<p>Abra <code>agent.json</code> junto al ejecutable y reemplace el slot de ejemplo. Un <b>slot</b> es aquello a
lo que apunta un certificado en la aplicación; cada slot enumera <b>destinos</b>, y cada destino indica qué archivos
escribir, dónde, y qué reiniciar. Este escribe un certificado y una clave para un servicio NetTalk:</p>''')
    add(code(r'''{
  "Port": 9443,
  "ApiKeyHash": "…tal como lo escribió install…",
  "AllowedIps": [],
  "TlsPfx": "agent-tls.pfx",
  "TlsPassword": "…tal como lo escribió install…",
  "KeepBackups": 5,
  "Slots": [
    {
      "Name": "example-com",
      "Description": "www.example.com para el servidor web NetTalk",
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
    add('<p>Después, valídelo. El agente vuelve a leer el archivo en cada solicitud, así que no hace falta reiniciar (salvo tras cambiar <code>Port</code>).</p>')
    add(code(r'''.\CertificateGet.Agent.exe check''', 'powershell'))
    add(code('''Slot "example-com"
  • NetTalk web
OK — configuration looks good.''', 'text'))
    add('<p>Hay archivos listos para NetTalk, HAProxy, Cockpit y TSplus en la %s; cada campo está en la %s.</p>'
        % (xref(TG, 'nettalk', 'Guía de plantillas'), xref(RF, 'root', 'Referencia')))
    add(note('warn', 'Rutas de Windows en JSON',
        '<p>Cada barra invertida se escribe dos veces: <code>"C:\\\\Apps\\\\Web"</code>. Una sola barra invertida es un '
        'error de sintaxis JSON, y <code>check</code> muestra la línea.</p>'))

    add('<h2 id="app"><span class="k">Paso 4</span>Conectar la aplicación</h2>')
    add('<p>En CertificateGet, en el PC que emite los certificados (la interfaz está en inglés):</p>')
    add(steps([
        '<div>Abra <b>Certificates</b>, seleccione el certificado y, en la sección <b>Deployment</b>, haga clic en <b>Add target</b>.</div>',
        '<div>Elija <b>CertificateGet Agent</b>. Escriba un <b>Name</b> (por ejemplo <i>WEB01 NetTalk</i>), la <b>Agent URL</b> '
        '(<code>https://WEB01:9443</code>) y la <b>API key</b>.</div>',
        '<div>Haga clic en <b>Connect &amp; list slots</b>. La primera vez, un cuadro de diálogo muestra la huella TLS del agente. '
        'Compárela con la salida de <code>info</code> en el servidor y haga clic en <b>Trust</b>. A partir de entonces la '
        'aplicación rechaza cualquier otro certificado en esa URL.</div>',
        '<div>Haga clic en la etiqueta del slot (por ejemplo <code>example-com</code>) para rellenar <b>Slot on the agent</b>.</div>',
        '<div>Deje marcadas <b>Enabled</b> y <b>Deploy automatically after every issuance / renewal</b>, y luego <b>Save target</b>.</div>',
        '<div>Haga clic en <b>Deploy now</b> para enviar el certificado actual de inmediato.</div>',
    ]))
    add('<p>Estos cuatro volúmenes también están incluidos en la aplicación: haga clic en <b>Help</b> al pie de la barra lateral, o pulse <b>F1</b>, y elija <b>Español</b>.</p>')

    add('<h2 id="verify"><span class="k">Paso 5</span>Comprobar el primer despliegue</h2>')
    add('<p>El <b>Activity log</b> de la aplicación muestra una línea por cada paso del agente. Un despliegue correcto se ve así:</p>')
    add(code('''NetTalk web: wrote www.example.com.crt, www.example.com.key to C:\\Apps\\Web\\certificates
Restarted service WebServer''', 'text'))
    add('<p>En el servidor, las mismas líneas están en <code>agent.log</code> junto al ejecutable, y los archivos anteriores en <code>backups\\example-com\\NetTalk_web\\&lt;fecha-hora&gt;\\</code>. Compruebe qué presenta ahora el servidor:</p>')
    add(code('''openssl s_client -connect www.example.com:443 -servername www.example.com </dev/null 2>/dev/null \\
  | openssl x509 -noout -subject -issuer -enddate''', 'bash'))

    add('<h2 id="update"><span class="k">Mantenimiento</span>Actualizar, mover o quitar el agente</h2>')
    add('<h3 id="update-win">Actualizar en Windows</h3>')
    add(code(r'''sc.exe stop %s
Copy-Item .\CertificateGet.Agent.exe "C:\Program Files\CertificateGet Agent\" -Force
sc.exe start %s''' % (SERVICE, SERVICE), 'powershell'))
    add('<h3 id="update-linux">Actualizar en Linux</h3>')
    add(code('''sudo systemctl stop %s
sudo cp /tmp/CertificateGet.Agent /opt/certificateget-agent/CertificateGet.Agent
sudo restorecon -v /opt/certificateget-agent/CertificateGet.Agent    # solo sistemas con SELinux
sudo systemctl start %s''' % (UNIT, UNIT), 'bash'))
    add('<h3 id="remove">Quitar o reinstalar</h3>')
    add('''<p><code>uninstall</code> elimina el servicio y (en Windows) la regla de firewall, y conserva
<code>agent.json</code>, el certificado TLS y las copias de seguridad. Volver a ejecutar <code>install</code> conserva la
configuración y la clave de API existentes. Si el certificado TLS es nuevo (una carpeta nueva u otro servidor), abra el
objetivo en la aplicación, haga clic en <b>Forget fingerprint</b> y confíe en la nueva huella en la siguiente conexión.</p>''')

    add('<h2 id="next1"><span class="k">Primeros pasos</span>Adónde ir después</h2>')
    add(nextcards([TG, PG, RF]))

    nav = [('Instalar', ['what', 'build', 'win', 'win-what', 'linux', 'linux-what']),
           ('Configurar', ['slot', 'app', 'verify']),
           ('Mantenimiento', ['update', 'update-win', 'update-linux', 'remove']),
           ('', ['next1'])]
    return page(GS, 'Primeros pasos del Agente', 'Volumen 1', 'Primeros pasos',
                'Compile el agente, instálelo como servicio en Windows o Linux, describa adónde va el certificado '
                'y conecte la aplicación CertificateGet.',
                ['Servicio <b>Windows</b>', '<b>systemd</b> en Linux', '<b>HTTPS</b> puerto 9443', '<b>5</b> pasos'],
                nav, ''.join(B))

# =====================================================================
#  2  GUÍA DEL PROGRAMADOR
# =====================================================================
def build_programmers_guide_es():
    B = []; add = B.append

    add('<h2 id="model"><span class="k">Conceptos</span>Slots, destinos y archivos</h2>')
    add('<p>Tres niveles describen adónde va un certificado en un servidor.</p>')
    add(table(['Nivel', 'Qué es', 'Cantidad habitual'], [
        ['<b>Slot</b>', 'Un certificado tal como lo ve el servidor. Un objetivo de despliegue de la aplicación nombra un slot.', 'Uno por certificado, por ejemplo <code>reddin-wildcard</code>.'],
        ['<b>Destino</b>', 'Un programa que usa el certificado: una carpeta y nombres de archivo, o una importación en TSplus, más lo que hay que reiniciar.', 'Uno por instancia de programa.'],
        ['<b>Archivo</b>', 'Un <i>origen</i> (el formato, como <code>fullchain</code>) y el nombre de archivo que recibe.', 'De uno a tres por destino.'],
    ]))
    add('''<p>La aplicación nunca decide carpetas ni nombres de archivo por un agente. Pregunta al agente qué orígenes
necesita el slot y envía esos. Cuando añade una instancia de NetTalk o cambia un nombre de archivo, cambia
<code>agent.json</code> en el servidor y nada en la aplicación.</p>''')
    add('''<p>Un agente puede tener muchos slots. Dos certificados desplegados en el mismo servidor son dos slots en el mismo
archivo, y dos objetivos en la aplicación (uno en cada certificado) que apuntan a la misma URL del agente.</p>''')

    add('<h2 id="flow"><span class="k">Conceptos</span>Cómo se ejecuta un despliegue</h2>')
    add('<p>El orden importa al escribir comandos, así que aquí está exactamente como lo ejecuta el agente.</p>')
    add(flow([
        ('1 · Preguntar', 'La aplicación llama a <code>GET /api/slots/{slot}</code> y obtiene la lista de orígenes que necesita el slot.', ''),
        ('2 · Enviar', 'La aplicación genera esos archivos a partir del certificado emitido y los envía en Base64 a <code>/api/deploy</code>, con la contraseña PFX solo cuando se necesita <code>pfx</code>, <code>p12</code> o <code>encrypted-key</code>.', ''),
        ('3 · Admitir', 'El agente vuelve a leer <code>agent.json</code> y comprueba <code>AllowedIps</code> y la clave de API. Los despliegues se ejecutan de uno en uno.', 'srv'),
        ('4 · Escribir', 'Para cada destino, en orden: respalda el archivo actual y escribe el nuevo de forma atómica. Los destinos TSplus importan en su lugar.', 'srv'),
        ('5 · Servicios', 'Cada entrada <code>RestartServices</code> de los destinos correctos, cada servicio una vez, en el orden en que aparece por primera vez.', 'srv'),
        ('6 · Programas', 'Cada entrada <code>RestartPrograms</code> (Windows), cada ejecutable una vez.', 'srv'),
        ('7 · Comandos', 'Los <code>Commands</code> de cada destino en orden, con <code>{folder}</code> sustituido, y después los <code>Commands</code> del propio slot.', 'srv'),
        ('8 · Informar', 'Cada paso, bueno o malo, vuelve al registro de actividad de la aplicación y a <code>agent.log</code>.', ''),
    ]))
    add(note('warn', 'Los comandos se ejecutan después de todos los reinicios',
        '<p>Los <code>Commands</code> de un destino no se ejecutan justo después de escribir sus archivos; se ejecutan '
        'cuando todos los destinos se han escrito y todos los servicios se han reiniciado. Si un comando debe ir antes de '
        'un reinicio, deje <code>RestartServices</code> vacío y haga ambas cosas en un solo comando, por ejemplo '
        '<code>icacls … &amp;&amp; net stop X &amp;&amp; net start X</code>.</p>'))
    add('<h3 id="failures">Cuando un paso falla</h3>')
    add('''<p>Un paso fallido no detiene el despliegue. Si falla la escritura de un destino, se omiten los reinicios y
comandos de ese destino y los demás destinos continúan. Un reinicio fallido o un comando con código de salida distinto
de cero se notifica, y el paso siguiente se ejecuta igualmente. El despliegue queda marcado como fallido en la
aplicación, con todos los pasos listados, para que pueda ver qué parte funcionó.</p>''')

    add('<h2 id="hot"><span class="k">Conceptos</span>Editar agent.json en un servidor en producción</h2>')
    add('''<p>El agente vuelve a leer <code>agent.json</code> en cada solicitud. Un slot, destino o nombre de archivo
nuevo se aplica al siguiente despliegue sin reiniciar. Solo <code>Port</code> se lee al arrancar; después de cambiarlo,
reinicie el servicio y abra el nuevo puerto en el firewall.</p>''')
    add('''<p>Un error de sintaxis no detiene el servicio. Las solicitudes se rechazan con un mensaje claro, que la
aplicación muestra, hasta que se corrija el archivo. <code>check</code> muestra la línea:</p>''')
    add(code('''ERROR agent.json has a syntax error on line 14 (at $.Slots[0].Destinations[0].Folder):
        "Folder": "C:\\Apps\\Web\\certificates",
Common causes: text not inside "quotes", curly quotes instead of straight ", a missing comma between items,
or a comma after the last item. Backslashes in Windows paths must be doubled (C:\\\\Apps).''', 'text'))

    add('<h2 id="writes"><span class="k">Archivos</span>Cómo se escriben los archivos</h2>')
    add(ul_list([
        '<b>De forma atómica.</b> Cada archivo se escribe en <code>&lt;nombre&gt;.cg-tmp</code> y luego se renombra sobre el anterior, así que un programa nunca lee medio archivo.',
        '<b>Las carpetas se crean</b> cuando no existen.',
        '<b>Linux, archivos nuevos:</b> el material de clave (%s) recibe modo 600; los demás archivos, 644.' % ', '.join('<code>%s</code>' % s for s in sorted(SECRETS)),
        '<b>Linux, archivos reemplazados:</b> conservan el propietario, grupo, modo y etiqueta SELinux del archivo que reemplazan (<code>chown --reference</code>, <code>chcon --reference</code>). Un certificado de Cockpit configurado como <code>root:cockpit-ws 640</code> se queda así.',
        '<b>En Windows</b> los archivos heredan los permisos de la carpeta.',
    ]))

    add('<h2 id="backups"><span class="k">Archivos</span>Copias de seguridad</h2>')
    add('''<p>Antes de reemplazar un archivo, el actual se copia en la carpeta propia del agente, nunca junto a los
archivos en uso. HAProxy carga todos los archivos de su carpeta de certificados, así que un <code>.bak</code> allí
también se serviría.</p>''')
    add(code('''<carpeta del agente>/backups/<slot>/<destino>/<yyyyMMdd-HHmmss>/<archivo>
C:\\Program Files\\CertificateGet Agent\\backups\\reddin-wildcard\\NetTalk_-_Portal\\20260927-031200\\portal.crt''', 'text'))
    add('''<p><code>KeepBackups</code> (predeterminado 5) es cuántas de esas carpetas fechadas se conservan por destino;
0 desactiva las copias. Para volver atrás, copie los archivos de una carpeta fechada a su sitio y reinicie el programa,
o ejecute a mano el comando del destino.</p>''')

    add('<h2 id="restarts"><span class="k">Reinicios</span>Servicios, programas y comandos</h2>')
    add('<h3 id="services">RestartServices</h3>')
    add('''<p>En Windows, el servicio se detiene (esperando hasta 90 segundos) y luego se inicia (esperando hasta 90
segundos a que esté <i>Running</i>). En Linux, el agente ejecuta <code>systemctl restart &lt;unidad&gt;</code>. Cada
nombre se reinicia una sola vez por despliegue, aunque lo incluyan varios destinos.</p>''')
    add(note('info', 'Recargue en lugar de reiniciar cuando pueda',
        '<p>Un reinicio corta las conexiones abiertas. Para HAProxy y nginx, deje <code>RestartServices</code> vacío y '
        'use una entrada <code>Commands</code> que compruebe la configuración y recargue, por ejemplo '
        '<code>haproxy -c -f /etc/haproxy/haproxy.cfg &amp;&amp; systemctl reload haproxy</code>.</p>'))
    add('<h3 id="programs">RestartPrograms</h3>')
    add('''<p>Para programas que se ejecutan en un escritorio en lugar de como servicio, como un servidor NetTalk iniciado
desde la carpeta Inicio. Para cada copia en ejecución del ejecutable, el agente:</p>''')
    add('<ol class="b">%s</ol>' % ''.join('<li>%s</li>' % s for s in [
        'anota su línea de comandos completa y la sesión de Windows (escritorio) en la que se ejecuta,',
        'le pide que se cierre normalmente, como si se hiciera clic en su botón de cerrar,',
        'lo termina si sigue en ejecución tras <code>StopTimeoutSeconds</code> (predeterminado 20),',
        'lo vuelve a iniciar en la misma sesión, como el mismo usuario, con la misma línea de comandos,',
        'comprueba que sigue en ejecución tres segundos después.']))
    add('''<p>Varias copias con parámetros distintos vuelven cada una con sus propios parámetros. Para que un programa
vuelva a un escritorio, debe haber un usuario con sesión iniciada, en la consola o en una sesión RDP desconectada. En
servidores desatendidos use el inicio de sesión automático, o <code>"StartIn": "Background"</code> si no hace falta
ventana.</p>''')
    add('<h3 id="commands">Commands</h3>')
    add(table(['', 'Windows', 'Linux'], [
        ['Shell', '<code>cmd.exe /c &lt;comando&gt;</code>', '<code>/bin/sh -c "&lt;comando&gt;"</code>'],
        ['Se ejecuta como', 'LocalSystem', 'root'],
        ['Límite de tiempo', '2 minutos, después se termina el árbol de procesos', '2 minutos'],
        ['Falla cuando', 'el código de salida no es 0', 'el código de salida no es 0'],
        ['Salida', 'los primeros 500 caracteres van al registro', 'los primeros 500 caracteres van al registro'],
    ]))
    add('''<p><code>{folder}</code> se sustituye por el <code>Folder</code> del destino. En los <code>Commands</code>
del propio slot no hay carpeta, así que ahí <code>{folder}</code> queda vacío. Use <code>&amp;&amp;</code> para
detenerse en el primer fallo y <code>;</code> para continuar pase lo que pase; el resultado del paso es el código de
salida del último comando ejecutado.</p>''')

    add('<h2 id="security"><span class="k">Seguridad</span>Modelo de seguridad</h2>')
    add(table(['Protección', 'Cómo funciona'], [
        ['Solo TLS', 'El agente escucha en HTTPS con su propio certificado. La aplicación fija su huella SHA-256 en la primera conexión, después de que usted la compare con <code>info</code>, y rechaza una distinta más adelante.'],
        ['Clave de API', 'Se envía en el encabezado <code>X-Api-Key</code>. El agente guarda solo su hash SHA-256 y lo compara en tiempo constante. Una clave incorrecta se registra y se responde tras una espera de 1,5 segundos.'],
        ['AllowedIps', 'Si la lista no está vacía, solo esas direcciones pueden conectarse (loopback siempre está permitido). Ponga aquí la IP del PC que ejecuta CertificateGet.'],
        ['Tamaño de la solicitud', 'Se rechazan cuerpos de más de %d MB.' % BODYMB],
        ['De uno en uno', 'Los despliegues se ponen en cola, así que dos renovaciones nunca escriben los mismos archivos a la vez.'],
    ]))
    add(note('warn', 'Proteja la carpeta del agente',
        '<p>Contiene <code>agent.json</code>, la clave TLS del agente y las copias de seguridad, que incluyen claves '
        'privadas. Manténgala legible solo por administradores o root. Quien pueda editar <code>agent.json</code> puede '
        'hacer que el agente ejecute un comando como LocalSystem o root.</p>'))
    add('<p>Para rotar la clave de API, ejecute <code>newkey</code> en el servidor y pegue la nueva clave en cada objetivo de la aplicación que use el agente.</p>')

    add('<h2 id="trouble"><span class="k">Operación</span>Solución de problemas</h2>')
    add('<p>Los mensajes aparecen en inglés, tal como los muestran la aplicación y el agente.</p>')
    add(table(['Lo que ve', 'Causa', 'Solución'], [
        ['<i>Invalid API key.</i>', 'La clave de la aplicación no coincide, o <code>ApiKeyHash</code> es un marcador.', 'Ejecute <code>newkey</code> y pegue la clave en el objetivo.'],
        ['<i>… is not allowed to use this agent.</i>', 'La IP del PC de la aplicación no está en <code>AllowedIps</code>.', 'Añádala. Detrás de NAT, use la dirección que ve el agente (está en <code>agent.log</code>).'],
        ['<i>The agent\'s TLS certificate does not match …</i>', 'El agente se reinstaló o se reemplazó su <code>agent-tls.pfx</code>.', 'Compare con <code>info</code>, luego <b>Forget fingerprint</b> en el objetivo y conecte de nuevo.'],
        ['<i>Slot "x" is not configured on …</i>', 'El nombre está escrito de forma distinta en la aplicación y en <code>agent.json</code>.', 'Use <b>Connect &amp; list slots</b> y haga clic en la etiqueta.'],
        ['<i>cannot read its configuration</i>', 'Error de sintaxis JSON en <code>agent.json</code>.', 'Ejecute <code>check</code>; indica la línea.'],
        ['La conexión agota el tiempo', 'Firewall, o el servicio no está en ejecución.', '<code>Test-NetConnection host -Port 9443</code> desde el PC de la aplicación; compruebe el servicio.'],
        ['<code>status=203/EXEC</code> en systemd', 'Etiqueta SELinux en un binario movido desde <code>/tmp</code>.', '<code>sudo restorecon -v /opt/certificateget-agent/CertificateGet.Agent</code>, o vuelva a ejecutar <code>install</code>.'],
        ['<i>Windows service "x" not found</i> desde <code>check</code>', 'El <i>nombre</i> del servicio es distinto de su nombre para mostrar.', '<code>Get-Service | Where DisplayName -like "*Web*"</code> y use la columna <code>Name</code>.'],
        ['<i>Command failed (exit 1)</i>', 'El propio comando falló; su salida aparece a continuación en el mensaje.', 'Ejecute el mismo comando a mano como root / en un símbolo del sistema elevado.'],
        ['Cockpit vuelve a su certificado autofirmado', 'La clave no es legible por <code>cockpit-ws</code>, u otro archivo va después en orden alfabético.', 'Vea la %s.' % xref(TG, 'cockpit', 'plantilla de Cockpit')],
    ]))

    add('<h2 id="notes"><span class="k">Notas</span>Notas de plataforma</h2>')
    add('<p>Comportamiento propio del sistema operativo o del programa que recibe el certificado, descubierto al usar el agente en servidores reales.</p>')
    add('<h3 id="n-selinux">SELinux y binarios movidos</h3>')
    add('''<p>Un archivo movido con <code>mv</code> conserva la etiqueta SELinux del lugar de origen. Un binario movido
desde <code>/tmp</code> queda etiquetado <code>user_tmp_t</code>, y systemd no lo inicia, con <code>status=203/EXEC</code>
y ningún otro mensaje. <code>install</code> etiqueta el agente como <code>bin_t</code>; tras una actualización copie con
<code>cp</code> o ejecute <code>restorecon</code>. Lo mismo vale para archivos de certificado que coloque a mano, por eso
el comando de Cockpit ejecuta <code>restorecon -F</code>.</p>''')
    add('<h3 id="n-example">Copiar el archivo de ejemplo sobre agent.json</h3>')
    add('''<p>Los ejemplos incluidos contienen marcadores para <code>ApiKeyHash</code> y <code>TlsPassword</code>. El agente
lo soporta: cuando la contraseña TLS no abre <code>agent-tls.pfx</code>, renombra el archivo antiguo a
<code>agent-tls.pfx.old-&lt;hora&gt;</code> y crea un certificado nuevo (la aplicación le pedirá confiar en la nueva
huella), y <code>check</code> informa de la clave que falta. Copie de un ejemplo solo la sección <code>Slots</code>.</p>''')
    add('<h3 id="n-haproxy">HAProxy carga todos los archivos de la carpeta crt</h3>')
    add('''<p>Con <code>crt /etc/haproxy/certs/</code>, HAProxy carga cada archivo de esa carpeta y elige uno por SNI. Una
copia de seguridad olvidada o un archivo a medio escribir allí también se carga, o impide que HAProxy arranque. Por eso
el agente escribe de forma atómica y guarda las copias en otro lugar, y por eso el comando de recarga ejecuta primero
<code>haproxy -c</code>.</p>''')
    add('<h3 id="n-cockpit">Cockpit lee el certificado como cockpit-ws</h3>')
    add('''<p><code>cockpit-tls</code> se ejecuta como el usuario <code>cockpit-ws</code>, así que la clave (o el
<code>.cert</code> combinado) debe ser <code>root:cockpit-ws</code> con modo 640. Cockpit usa el <i>último</i> archivo
<code>.cert</code>/<code>.crt</code> en orden alfabético de <code>/etc/cockpit/ws-certs.d</code>. Tras varios arranques
fallidos systemd deja de intentarlo, por eso el comando ejecuta <code>systemctl reset-failed</code> antes de reiniciar
el socket.</p>''')
    add('<h3 id="n-nettalk">NetTalk lee su certificado al arrancar</h3>')
    add('''<p>Un servidor web NetTalk carga el certificado y la clave cuando empieza a escuchar, así que los archivos nuevos
no tienen efecto hasta que se reinicia el servicio o el programa. Por eso todo destino NetTalk necesita una entrada
<code>RestartServices</code> o <code>RestartPrograms</code>.</p>''')
    add('<h3 id="n-tsplus">TSplus necesita una contraseña en el PFX</h3>')
    add('''<p><code>CertificateManager.exe</code> lee la contraseña de <code>certpassword.txt</code> en su propia carpeta y
no importa un PFX sin ella. Si el certificado no tiene contraseña PFX en la aplicación, el agente lo envuelve con una
contraseña aleatoria para la importación. Ambos archivos temporales se eliminan después. Las versiones que usan
<code>cert.jks</code> esperan la contraseña de almacén <code>secret</code>, la predeterminada de la aplicación.</p>''')
    add('<h3 id="n-session">Programas de escritorio y la sesión 0</h3>')
    add('''<p>El agente se ejecuta como servicio en la sesión 0, que no tiene escritorio. Para devolver un programa donde el
usuario pueda verlo, lo inicia en la sesión del usuario con el token de ese usuario. Si nadie ha iniciado sesión no
existe tal sesión, y el programa solo puede iniciarse con <code>"StartIn": "Background"</code>.</p>''')

    add('<h2 id="next2"><span class="k">Guía del programador</span>Adónde ir después</h2>')
    add(nextcards([TG, RF, GS]))

    nav = [('Conceptos', ['model', 'flow', 'failures', 'hot']),
           ('Archivos', ['writes', 'backups']),
           ('Reinicios', ['restarts', 'services', 'programs', 'commands']),
           ('Operación', ['security', 'trouble']),
           ('Notas de plataforma', ['notes', 'n-selinux', 'n-example', 'n-haproxy', 'n-cockpit', 'n-nettalk', 'n-tsplus', 'n-session']),
           ('', ['next2'])]
    return page(PG, 'Guía del programador del Agente', 'Volumen 2', 'Guía del programador',
                'Cómo se ejecuta un despliegue desde la aplicación hasta el último reinicio, cómo se tratan los archivos '
                'y las copias de seguridad, el modelo de seguridad, la solución de problemas y el comportamiento de cada '
                'plataforma detrás de cada regla.',
                ['<b>8</b> etapas', 'escritura <b>atómica</b>', 'TLS <b>fijado</b>', '<b>7</b> notas de plataforma'],
                nav, ''.join(B))

# =====================================================================
#  3  GUÍA DE PLANTILLAS
# =====================================================================
#  Las plantillas son las inglesas con las descripciones traducidas: los campos, nombres
#  de archivo y comandos no cambian.
def _es_json(t):
    for en, es in (('www.example.com for the NetTalk web server', 'www.example.com para el servidor web NetTalk'),
                   ('*.reddinassessments.com for three NetTalk servers', '*.reddinassessments.com para tres servidores NetTalk'),
                   ('api.example.com, NetTalk server started on the desktop', 'api.example.com, servidor NetTalk iniciado en el escritorio'),
                   ('NetTalk - API (desktop program)', 'NetTalk - API (programa de escritorio)'),
                   ('HAProxy on lb01', 'HAProxy en lb01'),
                   ('remote.example.com for the TSplus web portal', 'remote.example.com para el portal web de TSplus'),
                   ('remote.example.com for an older TSplus (cert.jks)', 'remote.example.com para un TSplus antiguo (cert.jks)'),
                   ('"TSplus web portal"', '"Portal web de TSplus"'),
                   ('TSplus (15+) on D:', 'TSplus (15+) en D:'),
                   ('TSplus (jks) on D:', 'TSplus (jks) en D:')):
        t = t.replace(en, es)
    return t

def build_template_guide_es():
    B = []; add = B.append

    add('<h2 id="anatomy"><span class="k">agent.json</span>La forma del archivo</h2>')
    add('''<p><code>agent.json</code> está junto al ejecutable. La parte superior del archivo la escribe
<code>install</code> y casi nunca cambia; su trabajo está en <code>Slots</code>. Cada plantilla de abajo es un slot (o un
destino) para pegar en esa lista.</p>''')
    add(code(r'''{
  "Port": 9443,                         // puerto HTTPS; reinicie el servicio tras cambiarlo
  "ApiKeyHash": "…",                    // lo escriben install / newkey — no lo edite
  "AllowedIps": [ "192.168.1.50" ],     // el PC que ejecuta CertificateGet; [] = cualquiera
  "TlsPfx": "agent-tls.pfx",            // el certificado TLS propio del agente
  "TlsPassword": "…",                   // lo escribe el agente — no lo edite
  "KeepBackups": 5,                     // carpetas de copias fechadas por destino
  "Slots": [
    {
      "Name": "example-com",            // lo que nombra el objetivo de despliegue de la aplicación
      "Description": "…",               // se muestra en la aplicación
      "Destinations": [
        {
          "Name": "…",                  // se muestra en el registro; también nombre de la carpeta de copias
          "Kind": "Files",              // Files (predeterminado), TSplus o TSplusJks
          "Folder": "…",                // se crea si no existe
          "Files": [ { "Source": "fullchain", "FileName": "…" } ],
          "RestartServices": [],        // nombres de servicios de Windows / unidades systemd
          "RestartPrograms": [],        // programas de escritorio de Windows
          "Commands": []                // cmd.exe en Windows, /bin/sh en Linux; {folder} = Folder
        }
      ],
      "Commands": []                    // se ejecutan una vez, después de todos los destinos
    }
  ]
}'''))
    add(note('warn', 'Los comentarios son solo para leer',
        '<p>JSON no admite comentarios. Las notas <code>//</code> de arriba explican los campos; el archivo real no debe '
        'contenerlas. Las plantillas de abajo son JSON puro y se pueden pegar tal cual.</p>'))
    add('<p>Después de cada cambio ejecute <code>check</code>. Cada campo se describe en la %s.</p>' % xref(RF, 'root', 'Referencia'))

    # ---- NetTalk
    add('<h2 id="nettalk"><span class="k">Windows</span>NetTalk</h2>')
    add('''<p>Un servidor web NetTalk lee un archivo de certificado y un archivo de clave privada, indicados en la
configuración SSL del procedimiento WebServer. Cada instancia suele usar su propia carpeta <code>certificates</code>
con <code>&lt;dominio&gt;.crt</code> y <code>&lt;dominio&gt;.key</code>. Escriba <code>fullchain</code> como
certificado para que los navegadores reciban el intermedio, <code>key</code> como clave, y reinicie la instancia,
porque NetTalk carga los archivos al arrancar.</p>''')
    add(table(['Ajuste de NetTalk', 'Origen', 'Archivo de ejemplo'], [
        ['Archivo de certificado', '<code>fullchain</code>', '<code>certificates\\www.example.com.crt</code>'],
        ['Archivo de clave privada', '<code>key</code>', '<code>certificates\\www.example.com.key</code>'],
        ['Reinicio', '<code>RestartServices</code> o <code>RestartPrograms</code>', 'Nombre del servicio, o la ruta del <code>.exe</code>'],
    ]))
    add('<h3 id="nettalk-one">Un servicio NetTalk</h3>')
    add('<p>El nombre del servicio es la columna <i>Name</i> de <code>Get-Service</code>, no el nombre para mostrar.</p>')
    add(code(_es_json(T_NETTALK_ONE)))
    add('<h3 id="nettalk-many">Varias instancias de NetTalk, un certificado</h3>')
    add('''<p>Un certificado comodín usado por varios servidores NetTalk es un slot con un destino por instancia, cada uno
con los nombres de archivo que esa instancia tiene configurados. El certificado se envía una vez y se escribe tres
veces.</p>''')
    add(code(_es_json(T_NETTALK_MANY)))
    add('<h3 id="nettalk-desktop">NetTalk como programa de escritorio</h3>')
    add('''<p>Cuando el servidor se inicia desde un escritorio con sesión iniciada en lugar de como servicio, reinícielo
con <code>RestartPrograms</code>. Cada copia en ejecución se cierra y se vuelve a iniciar con su propia línea de
comandos, en el mismo escritorio.</p>''')
    add(code(_es_json(T_NETTALK_DESKTOP)))
    add('<p>Si el programa debe permanecer oculto y nadie ha iniciado sesión, añada <code>"StartIn": "Background"</code>. Los detalles están en %s.</p>'
        % xref(PG, 'programs', 'RestartPrograms'))

    # ---- HAProxy
    add('<h2 id="haproxy"><span class="k">Linux</span>HAProxy</h2>')
    add('''<p>HAProxy quiere el certificado, la cadena y la clave en un solo archivo PEM, que es el origen
<code>combined</code>. Apunte la línea <code>bind</code> a la carpeta, y HAProxy carga cada archivo de ella y elige uno
por SNI.</p>''')
    add(code(HAPROXY_CFG, 'haproxy'))
    add('<h3 id="haproxy-one">Un certificado</h3>')
    add(code(_es_json(T_HAPROXY)))
    add('''<p>El comando comprueba toda la configuración con el archivo nuevo antes de recargar. Si la comprobación falla,
la recarga no se ejecuta, HAProxy sigue sirviendo con el certificado que ya tenía y la aplicación muestra el error.
<code>reload</code> mantiene las conexiones abiertas; evite <code>RestartServices: ["haproxy"]</code>.</p>''')
    add('<h3 id="haproxy-many">Varios certificados en la misma carpeta</h3>')
    add('<p>Cada certificado es su propio slot con su propio nombre de archivo. Cada certificado de la aplicación recibe un objetivo que apunta a su slot.</p>')
    add(code(T_HAPROXY_TWO))

    # ---- Cockpit
    add('<h2 id="cockpit"><span class="k">Linux</span>Cockpit</h2>')
    add('''<p>Cockpit toma su certificado de <code>/etc/cockpit/ws-certs.d</code>. Usa el <b>último</b> archivo
<code>.cert</code> o <code>.crt</code> en orden alfabético; un archivo <code>.cert</code> contiene la cadena y la clave
juntas, y un archivo <code>.crt</code> necesita un archivo <code>.key</code> con el mismo nombre. La clave debe ser
legible por el grupo <code>cockpit-ws</code>.</p>''')
    add('<h3 id="cockpit-replace">Reemplazar el certificado que Cockpit ya usa</h3>')
    add('''<p>Si ya hay un certificado, por ejemplo <code>reddinassessments.cert</code>, sobrescríbalo con
<code>combined</code>. El agente conserva su propietario, grupo, modo y etiqueta SELinux, y el comando los vuelve a
establecer para un primer despliegue.</p>''')
    add(code(T_COCKPIT_A))
    add('<h3 id="cockpit-new">Añadir un certificado junto al autofirmado</h3>')
    add('''<p>Si no, escriba <code>fullchain</code> y <code>key</code> como <code>90-letsencrypt.crt</code> y
<code>90-letsencrypt.key</code>. Van después de <code>0-self-signed.cert</code> de Cockpit en orden alfabético, así que
Cockpit los elige.</p>''')
    add(code(T_COCKPIT_B))
    add('<h3 id="cockpit-cmd">Qué hace el comando</h3>')
    add(table(['Parte', 'Por qué'], [
        ['<code>chgrp cockpit-ws … &amp;&amp; chmod 640 …</code>', 'La clave debe ser legible por <code>cockpit-tls</code>, que se ejecuta como <code>cockpit-ws</code>.'],
        ['<code>restorecon -F …</code>', 'Restablece la etiqueta SELinux. No está instalado en Debian/Ubuntu; el <code>;</code> que le sigue permite que el resto se ejecute igualmente.'],
        ['<code>systemctl reset-failed cockpit.socket cockpit</code>', 'Borra el límite de arranques de systemd tras fallos anteriores, para que el reinicio no se rechace.'],
        ['<code>systemctl restart cockpit.socket</code>', 'Cockpit se activa por socket; reiniciar el socket hace que la siguiente conexión cargue el certificado nuevo.'],
        ['<code>systemctl try-restart cockpit</code>', 'Reinicia el servicio solo si está en ejecución, y termina con código de salida 0 para que el paso tenga éxito.'],
    ]))
    add('<p>Para ver qué certificado usará Cockpit:</p>')
    add(code('''sudo /usr/lib/cockpit/cockpit-certificate-ensure --check      # Debian / Ubuntu
sudo /usr/libexec/cockpit-certificate-ensure --check         # RHEL / Rocky / Alma / Fedora''', 'bash'))

    # ---- TSplus
    add('<h2 id="tsplus"><span class="k">Windows</span>TSplus</h2>')
    add('''<p>TSplus tiene dos formas de recibir un certificado, según la versión. Ambas son un destino con un
<code>Kind</code> y sin archivos; el agente conoce las rutas.</p>''')
    add(table(['Kind', 'Versión de TSplus', 'Qué hace el agente'], [
        ['<code>TSplus</code>', '15 y posteriores', 'Escribe un PFX temporal y <code>certpassword.txt</code>, ejecuta <code>%s\\CertificateManager.exe /add &lt;pfx&gt;</code> y elimina ambos.' % esc(CONSTS['DefaultTsplusCertFolder'])],
        ['<code>TSplusJks</code>', 'Versiones que usan <code>cert.jks</code>', 'Respalda y reemplaza <code>%s\\Clients\\webserver\\cert.jks</code> (contraseña <code>secret</code>, cadena completa) y luego ejecuta <code>UserDesktop\\files\\AdminTool.exe /webrestart</code>.' % esc(CONSTS['DefaultTsplusFolder'])],
    ]))
    add('<h3 id="tsplus-15">TSplus 15 y posteriores</h3>')
    add(code(_es_json(T_TSPLUS15)))
    add('<h3 id="tsplus-jks">TSplus antiguo con cert.jks</h3>')
    add(code(_es_json(T_TSPLUSJKS)))
    add('<p>La contraseña JKS viene de <b>Settings</b> de la aplicación (<i>JKS keystore password</i>). Déjela en <code>secret</code>; TSplus espera ese valor.</p>')
    add('<h3 id="tsplus-paths">TSplus instalado en otra ubicación</h3>')
    add(code(_es_json(T_TSPLUS_PATHS)))
    add('<p><code>check</code> confirma que <code>CertificateManager.exe</code> o la carpeta <code>Clients\\webserver</code> están donde indica el destino.</p>')

    # ---- juntos
    add('<h2 id="together"><span class="k">Archivos completos</span>Varios programas en un servidor</h2>')
    add('''<p>Estos son los archivos de ejemplo que se entregan con el agente (<code>agent.example.json</code> en cada
carpeta de compilación), impresos aquí desde el repositorio, por eso sus descripciones están en inglés. El primero
coloca un certificado comodín en tres instancias de NetTalk, una de ellas un programa de escritorio, y en TSplus. El
segundo sirve HAProxy y Cockpit en una misma máquina Linux.</p>''')
    add('<h3 id="together-win">Windows: NetTalk y TSplus</h3>')
    add(code(EX_WIN.strip()))
    add('<p>Un servidor tiene un solo TSplus, así que en la práctica conserve solo uno de los dos destinos TSplus: <code>TSplus</code> para la versión 15 y posteriores, <code>TSplusJks</code> para versiones anteriores.</p>')
    add('<h3 id="together-linux">Linux: HAProxy y Cockpit</h3>')
    add(code(EX_LINUX.strip()))

    add('<h2 id="next3"><span class="k">Guía de plantillas</span>Adónde ir después</h2>')
    add(nextcards([RF, PG, GS]))

    nav = [('agent.json', ['anatomy']),
           ('NetTalk', ['nettalk', 'nettalk-one', 'nettalk-many', 'nettalk-desktop']),
           ('HAProxy', ['haproxy', 'haproxy-one', 'haproxy-many']),
           ('Cockpit', ['cockpit', 'cockpit-replace', 'cockpit-new', 'cockpit-cmd']),
           ('TSplus', ['tsplus', 'tsplus-15', 'tsplus-jks', 'tsplus-paths']),
           ('Archivos completos', ['together', 'together-win', 'together-linux']),
           ('', ['next3'])]
    return page(TG, 'Guía de plantillas del Agente', 'Volumen 3', 'Guía de plantillas',
                'Slots de <code>agent.json</code> listos para NetTalk, HAProxy, Cockpit y TSplus, con el razonamiento '
                'detrás de cada nombre de archivo y comando.',
                ['<b>NetTalk</b> servicio y escritorio', '<b>HAProxy</b> PEM combinado', '<b>Cockpit</b> ws-certs.d', '<b>TSplus</b> 15+ y jks'],
                nav, ''.join(B))

# =====================================================================
#  4  REFERENCIA
# =====================================================================
def build_reference_es():
    B = []; add = B.append

    add('<h2 id="root"><span class="k">agent.json</span>Nivel superior</h2>')
    add('<p>%s Los nombres se comparan sin distinguir mayúsculas.</p>' % esc(CLASS_DOC_ES['AgentConfig']))
    add(field_rows('AgentConfig'))
    add('<h3 id="slot">Slot</h3>')
    add('<p>%s Una entrada de <code>Slots</code>.</p>' % esc(CLASS_DOC_ES['Slot']))
    add(field_rows('Slot'))
    add('<h3 id="dest">Destino</h3>')
    add('<p>Un programa que usa el certificado. Una entrada de <code>Destinations</code> de un slot.</p>')
    add(field_rows('Destination'))
    add('<h3 id="file">Entrada de archivo</h3>')
    add('<p>Una entrada de <code>Files</code> de un destino.</p>')
    add(field_rows('FileSpec'))
    add('<h3 id="prog">Entrada de programa</h3>')
    add('<p>%s Una entrada de <code>RestartPrograms</code> de un destino. Solo Windows.</p>' % esc(CLASS_DOC_ES['ProgramSpec']))
    add(field_rows('ProgramSpec'))

    add('<h2 id="values"><span class="k">Valores</span>Valores permitidos</h2>')
    add('<h3 id="sources">Orígenes de archivo</h3>')
    add('<p>Los valores que <code>check</code> acepta para <code>Source</code>. Los orígenes marcados como <i>secreto</i> contienen la clave privada y, cuando son nuevos, se escriben con modo 600 en Linux.</p>')
    rows = []
    for s in SOURCES:
        if s not in SOURCE_DOC_ES:
            PROBLEMS.append('es/reference: source "%s" has no SOURCE_DOC_ES entry' % s); continue
        fmt, d = SOURCE_DOC_ES[s]
        tag = '<span class="tag tag--warn">secreto</span>' if s in SECRETS else ''
        rows.append(['<code>%s</code>%s' % (s, tag), esc(fmt), d])
    add(table(['Origen', 'Formato', 'Contenido'], rows))
    add('<h3 id="kinds">Tipos de destino</h3>')
    rows = []
    for k in KINDS:
        if k not in KIND_DOC_ES: PROBLEMS.append('es/reference: Kind "%s" has no KIND_DOC_ES entry' % k); continue
        rows.append(['<code>%s</code>' % k, KIND_DOC_ES[k]])
    add(table(['Kind', 'Comportamiento'], rows))
    add(table(['Ruta predeterminada', 'Valor'], [['<code>TsplusCertFolder</code>', '<code>%s</code>' % esc(CONSTS['DefaultTsplusCertFolder'])],
                                                 ['<code>TsplusFolder</code>', '<code>%s</code>' % esc(CONSTS['DefaultTsplusFolder'])]]))
    add('<h3 id="startin">Valores de StartIn</h3>')
    rows = []
    for s in STARTINS:
        if s not in STARTIN_DOC_ES: PROBLEMS.append('es/reference: StartIn "%s" has no STARTIN_DOC_ES entry' % s); continue
        rows.append(['<code>%s</code>' % STARTIN_DOC_ES[s][0], STARTIN_DOC_ES[s][1]])
    add(table(['StartIn', 'Dónde vuelve el programa'], rows))
    add('<h3 id="placeholders">Marcadores</h3>')
    add(table(['Marcador', 'Dónde', 'Se sustituye por'], [
        ['<code>{folder}</code>', '<code>Commands</code> de un destino', 'El <code>Folder</code> de ese destino'],
        ['<code>{folder}</code>', '<code>Commands</code> de un slot', 'Nada (vacío)'],
    ]))

    add('<h2 id="cli"><span class="k">Línea de comandos</span>Comandos</h2>')
    add('<p>Se ejecutan desde la carpeta del agente, como administrador en Windows o con <code>sudo</code> en Linux. Sin comando, el agente se ejecuta en la consola, igual que <code>run</code>.</p>')
    rows = []
    for c in CLI:
        if c['name'] not in CLI_USE_ES or c['name'] not in CLI_DOC_ES:
            PROBLEMS.append('es/reference: command "%s" has no CLI_USE_ES / CLI_DOC_ES entry' % c['name']); continue
        use, more = CLI_USE_ES[c['name']]
        d = CLI_DOC_ES[c['name']]
        rows.append('<tr class="fn" data-k="%s"><td class="fn__n"><code>%s%s</code></td><td class="fn__s">'
                    '<p class="fn__d" style="margin:0">%s</p><p class="fn__d">%s</p>'
                    '<pre class="code code--use code--sh" data-lang="use"><code>%s</code></pre></td></tr>'
                    % (esc(c['name'] + ' ' + d).lower(), esc(c['name']), (' ' + esc(c['arg'])) if c['arg'] else '',
                       esc(d[0].upper() + d[1:]) + '.', more, esc(use)))
    add('<div class="tw"><table class="fns"><tbody>%s</tbody></table></div>' % ''.join(rows))
    add(table(['Nombre', 'Valor'], [['Servicio de Windows', '<code>%s</code>' % SERVICE], ['Unidad systemd', '<code>%s</code>' % UNIT],
                                    ['Regla de firewall (Windows)', '<i>%s</i>' % FIREWALL]]))

    add('<h2 id="api"><span class="k">Protocolo</span>API HTTP</h2>')
    add('''<p>Lo que envía la aplicación. Cada solicitud necesita el encabezado <code>X-Api-Key</code> y debe venir de una
IP permitida. Los errores vuelven como <code>{ "error": "…" }</code> con estado 401 (clave), 403 (IP), 404 (slot) o 500
(<code>agent.json</code> ilegible).</p>''')
    rows = []
    for e in ENDPOINTS:
        if e['path'] not in ENDPOINT_DOC_ES:
            PROBLEMS.append('es/reference: endpoint %s has no ENDPOINT_DOC_ES entry' % e['path']); continue
        d, ex = ENDPOINT_DOC_ES[e['path']]
        rows.append('<tr class="fn" data-k="%s"><td class="fn__n"><code>%s</code></td><td class="fn__s"><code>%s</code>'
                    '<p class="fn__d">%s</p><pre class="code code--use code--resp" data-lang="use"><code>%s</code></pre></td></tr>'
                    % (esc(e['path']).lower(), e['verb'], esc(e['path']), d, _hl_json(ex)))
    add('<div class="tw"><table class="fns"><tbody>%s</tbody></table></div>' % ''.join(rows))
    add('<h3 id="req">Solicitud de despliegue</h3>')
    add('<p>El cuerpo de <code>POST /api/deploy</code>. Los nombres de propiedad van en camelCase en la red.</p>')
    add(field_rows('DeployRequest', prefix_json=False))
    add('<h3 id="certinfo">Información del certificado</h3>')
    add('<p>El objeto <code>certificate</code> dentro de la solicitud.</p>')
    add(field_rows('CertInfo', prefix_json=False))

    add('<h2 id="disk"><span class="k">En disco</span>Archivos que guarda el agente</h2>')
    add(table(['Ruta (junto al ejecutable)', 'Contenido'], [
        ['<code>agent.json</code>', 'La configuración. Modo 600 en Linux.'],
        ['<code>agent-tls.pfx</code>', 'El certificado TLS y la clave del agente. Modo 600 en Linux.'],
        ['<code>agent.log</code>', 'Cada solicitud, paso y error, una línea cada uno. En Linux también en <code>journalctl -u %s</code>.' % UNIT],
        ['<code>backups/&lt;slot&gt;/&lt;destino&gt;/&lt;yyyyMMdd-HHmmss&gt;/</code>', 'Versiones anteriores de los archivos reemplazados; <code>KeepBackups</code> carpetas por destino.'],
        ['<code>work/</code>', 'PFX temporal para una importación de TSplus, eliminado después.'],
    ]))

    add('<h2 id="next4"><span class="k">Referencia</span>Adónde ir después</h2>')
    add(nextcards([GS, PG, TG]))

    nav = [('agent.json', ['root', 'slot', 'dest', 'file', 'prog']),
           ('Valores', ['values', 'sources', 'kinds', 'startin', 'placeholders']),
           ('Operación', ['cli', 'api', 'req', 'certinfo', 'disk']),
           ('', ['next4'])]
    nfields = sum(len(CLASSES[c]['props']) for c in ('AgentConfig', 'Slot', 'Destination', 'FileSpec', 'ProgramSpec'))
    return page(RF, 'Referencia del Agente', 'Volumen 4', 'Referencia',
                'Cada campo de <code>agent.json</code>, valor permitido, comando y endpoint, generados a partir de las '
                'fuentes del agente, con una línea de ejemplo en cada campo.',
                ['<b>%d</b> campos' % nfields, '<b>%d</b> orígenes' % len(SOURCES), '<b>%d</b> comandos' % len(CLI),
                 '<b>%d</b> endpoints' % len(ENDPOINTS)],
                nav, ''.join(B), showfilter=True)
