# ATLAS GT --- MASTER BUILD SPECIFICATION --- DEFINITIVA PARA KIMI

## Gateway & Translator para máquinas, equipos e infraestructura legacy

**Documento maestro / producto / arquitectura / laboratorio /
comercialización**\
**Estado:** especificación ejecutable definitiva --- construir sobre el
repositorio existente\
**Principio:** **si habla, lo escuchamos; si habla otro idioma, lo
traducimos; si está mudo, lo instrumentamos.**

------------------------------------------------------------------------

# 0A. ORDEN DE EJECUCIÓN --- LEER ANTES DE TOCAR CÓDIGO

Kimi: **NO diseñes Atlas GT desde cero, NO te quedes pensando la
arquitectura durante horas y NO regreses solamente una auditoría.**

La arquitectura, el alcance y las decisiones importantes están en este
documento. Tu trabajo es ejecutar.

Primero inspecciona el repositorio actual y clasifica cada pieza como:

`FUNCIONA / PARCIAL / ROTA / FALTA / BASURA`

Después trabaja continuamente:

`READ → VERIFY → FIX/IMPLEMENT → BUILD → TEST → CORRECT → RETEST → NEXT`

Reglas de ejecución:

1.  Conservar todo lo que ya sea correcto.
2.  Corregir lo parcial o roto.
3.  Implementar lo faltante.
4.  Eliminar sólo basura demostrable, nunca código útil por comodidad.
5.  No reconstruir de cero para evitar entender el código existente.
6.  No crear otra arquitectura paralela.
7.  No detenerse después del diagnóstico.
8.  No pedir decisiones que este documento ya tomó.
9.  No declarar terminado por compilar.
10. No avanzar con un gate roto.
11. No hacer refactor cosmético mientras falte funcionalidad.
12. No hacer commit/push hasta que el bloque esté verificado; al cierre
    reportar SHA y branch.
13. Si algo depende de hardware externo no disponible, llegar hasta el
    límite verificable, marcar `EXTERNAL / NOT PROVEN` y continuar con
    el resto.
14. Simulación válida se etiqueta `SIMULATED`; jamás se vende como
    hardware real probado.

**Prohibido reducir Atlas GT a demo, minimal, MVP, skeleton, placeholder
o showcase.** El objetivo es un producto completo y extensible dentro de
lo verificable por software.

------------------------------------------------------------------------

# 0B. EXPERIENCIA DE USUARIO --- TAN IMPORTANTE COMO EL CORE

Atlas GT puede ser técnicamente grande por dentro, pero **NO debe
sentirse complicado para el usuario normal**.

Debe existir separación clara entre operación cotidiana y configuración
técnica.

## Usuario / Operador --- simple

El operador NO debe necesitar entender Modbus, registros, baud rate,
endian, OPC UA NodeIds, topics MQTT ni topología interna para usar una
integración ya configurada.

Flujo normal:

`Entrar → elegir planta/área/máquina → ver estado → producción/datos/alarmas → historial/reporte`

La interfaz del operador debe priorizar:

-   nombre humano de máquina;
-   estado actual;
-   conexión/health;
-   producción/ciclos cuando aplique;
-   señales importantes con unidad;
-   alarmas accionables;
-   eventos recientes;
-   histórico;
-   reportes/exportación;
-   mensajes entendibles.

Nunca mostrar un stack trace o error crudo como explicación principal.

## Administrador / Ingeniero --- configuración poderosa

Las configuraciones grandes, delicadas o poco frecuentes viven en área
administrativa/técnica separada.

Debe permitir administrar:

-   Tenant/Site/Area/Line/Asset;
-   usuarios, roles y permisos;
-   conectores;
-   interfaces/endpoints;
-   Device Profiles;
-   mappings;
-   unidades/scaling/endian;
-   polling/rate limits/timeouts/retry;
-   discovery autorizado;
-   reglas derivadas;
-   alarmas;
-   historian/retención;
-   backups/restores;
-   exportadores;
-   seguridad;
-   auditoría;
-   Edge/gateways;
-   import/export/versionado de configuración.

Para configuraciones grandes:

-   búsqueda;
-   filtros;
-   paginación o virtualización cuando corresponda;
-   edición por grupos;
-   import/export;
-   validación antes de guardar;
-   resumen de cambios;
-   confirmación para cambios peligrosos;
-   rollback/versionado donde aplique;
-   defaults seguros;
-   ayuda contextual;
-   nombres humanos además de identificadores técnicos.

## Técnico / Device Lab

Debe existir un espacio especializado para quien sí necesita ver las
tripas:

`raw → frames/registers/nodes → mapping → correlación → evidencia → confidence → profile`

Así evitamos llenar la pantalla del operador con ingeniería.

## Regla UX

**Simple por defecto; profundidad bajo demanda.**

No esconder información técnica: colocarla en el nivel correcto.

------------------------------------------------------------------------

# 0C. FLUJO DE ALTA DE UNA MÁQUINA

El sistema debe guiar, no obligar al usuario a construir una integración
desde cero.

Flujo:

`Nueva máquina` → ubicación y nombre → detectar/seleccionar interfaz →
intentar identificación segura → elegir ruta
`Native / Legacy-Unknown / Instrumented` → configurar o aplicar Device
Profile → validar conexión → observar → mapear/confirmar señales →
ejecutar pruebas → guardar perfil/version → activar monitorización →
dashboard/historian

Si existe un Device Profile compatible:

`identificar → proponer perfil → mostrar evidencia/compatibilidad → validar → aplicar`

No aplicar silenciosamente un perfil sólo porque coincide la marca.

------------------------------------------------------------------------

# 0D. ESTADOS VISIBLES Y HONESTOS

La UI y los reportes deben distinguir como mínimo:

`ONLINE / OFFLINE / DEGRADED / UNKNOWN`

y, para procedencia/validación:

`REAL / SIMULATED`
`DOCUMENTED / VERIFIED / EXPERIMENTAL_HIGH / EXPERIMENTAL / INFERRED / UNKNOWN`

El usuario debe poder saber **qué sabemos, por qué lo sabemos y qué tan
confiable es** sin leer logs internos.

------------------------------------------------------------------------

# 0. Con manzanas 🍎

Una fábrica tiene una máquina vieja.

### Caso A --- La máquina sí habla

Tiene RS-485 y Modbus.

``` text
MÁQUINA → RS-485/Modbus → Atlas GT → datos entendibles
```

Atlas descubre o configura registros:

``` text
40001 = 73
40002 = 1
40003 = 18452
```

y los convierte en:

``` text
Temperatura = 73 °C
Motor = ENCENDIDO
Piezas = 18,452
```

Luego los entrega a dashboard, historial, MQTT, OPC UA, REST, MES,
SCADA, etc.

### Caso B --- Habla, pero nadie entiende el idioma

Tiene serial/CAN/TCP propietario.

Atlas GT primero trabaja **pasivamente**:

``` text
capturar → timestamp → comparar → correlacionar → formular hipótesis
```

El operador prende el motor:

``` text
0x18A cambia 00 → 01
```

Lo apaga:

``` text
0x18A cambia 01 → 00
```

Atlas propone:

> "Esta señal parece corresponder a MOTOR_RUNNING. Confianza
> experimental: alta. ¿Confirmar?"

**No escribe comandos a ciegas.**

### Caso C --- La máquina no habla

Prensa vieja, torno, bomba, compresor, horno, etc.

``` text
Corriente ─┐
Vibración ─┤
Temperatura├→ Atlas Edge → Atlas GT
Fotocelda ─┤
Inductivo ─┤
Entrada 24V┘
```

Entonces Atlas puede inferir/medir:

``` text
Produciendo
Detenida
Ciclo
Piezas
Tiempo activo
Temperatura
Consumo
Alarma
Tendencia de vibración
```

**No necesitamos reemplazar el cerebro de la máquina para empezar a
obtener datos.**

------------------------------------------------------------------------

# 1. Problema que resolvemos

Las empresas acumulan generaciones distintas de equipos:

-   PLC/PAC modernos;
-   PLC antiguos;
-   CNC;
-   VFD;
-   compresores;
-   chillers;
-   bombas;
-   hornos;
-   prensas;
-   inyectoras;
-   máquinas de empaque;
-   bordadoras;
-   básculas;
-   robots/cobots;
-   UPS/PDU;
-   medidores;
-   HVAC/BMS;
-   sensores;
-   equipos seriales;
-   equipos CAN;
-   equipos Ethernet propietarios;
-   máquinas completamente mudas.

El problema no es solamente "conectarlos". Es convertir señales y
protocolos heterogéneos en **información confiable, contextualizada y
reutilizable** sin obligar a reemplazar activos funcionales.

------------------------------------------------------------------------

# 2. Qué ES Atlas GT

Atlas GT será una plataforma **offline-first, edge-first y modular**
para:

1.  descubrir interfaces;
2.  conectarse de manera segura;
3.  capturar señales;
4.  decodificar protocolos conocidos;
5.  mapear datos a significado;
6.  instrumentar equipos mudos;
7.  normalizar telemetría;
8.  historizar;
9.  detectar eventos/anomalías;
10. exportar a sistemas externos;
11. diagnosticar fallas de integración;
12. mantener Device Profiles reutilizables;
13. documentar automáticamente el levantamiento;
14. operar aunque Internet falle.

No será un simple "convertidor Modbus".

------------------------------------------------------------------------

# 3. Qué NO ES

-   No es un PLC de seguridad.
-   No sustituye E-stop, interlocks, relés de seguridad ni SIS.
-   No escribe registros desconocidos "para ver qué pasa".
-   No controla equipos sin autorización/documentación/pruebas.
-   No promete entender automáticamente cualquier protocolo propietario.
-   No depende de nube.
-   No requiere reemplazar la máquina.
-   No mezcla lectura/monitorización con control sin barreras
    explícitas.

------------------------------------------------------------------------

# 4. Regla de oro de seguridad

Atlas GT tendrá estados de capacidad separados:

``` text
DISCOVER
   ↓
OBSERVE
   ↓
DECODE
   ↓
NORMALIZE
   ↓
MONITOR
   ↓
WRITE-TEST
   ↓
CONTROL
```

Avanzar de un nivel a otro requiere evidencia y permisos.

Por defecto un dispositivo nuevo entra en **READ-ONLY / PASSIVE-FIRST**.

Una integración jamás obtiene capacidad de escritura porque "parece
Modbus".

------------------------------------------------------------------------

# 5. Arquitectura

``` text
                   ┌─────────────────────────┐
                   │       ATLAS GT UI       │
                   └────────────┬────────────┘
                                │
 ┌──────────────────────────────┼─────────────────────────────┐
 │                              │                             │
 ▼                              ▼                             ▼
DISCOVERY                  DEVICE LAB                    OPERATIONS
 │                              │                             │
 ▼                              ▼                             ▼
INTERFACE                 SIGNAL MAPPER                 DASHBOARDS
INVENTORY                 CORRELATOR                    ALARMS
 │                              │                       HISTORIAN
 ▼                              ▼                             │
CONNECTORS ─────────────→ NORMALIZED MODEL ←─────────────────┘
 │                              │
 ├─ Modbus RTU/TCP              ├─ Assets
 ├─ OPC UA                      ├─ Signals
 ├─ MQTT                        ├─ States
 ├─ BACnet                      ├─ Events
 ├─ CAN                         ├─ Counters
 ├─ SNMP                        ├─ Measurements
 ├─ Serial                      └─ Commands
 ├─ TCP/UDP
 ├─ HTTP/REST
 ├─ ODBC/files
 └─ Custom
                                │
              ┌─────────────────┼──────────────────┐
              ▼                 ▼                  ▼
            MQTT              OPC UA              REST
              │                 │                  │
              └──────── SCADA / MES / ERP / BI ───┘
```

------------------------------------------------------------------------

# 6. Las tres rutas de integración

## Ruta 1 --- Native

La máquina expone protocolo conocido.

Ejemplos:

-   Modbus RTU/TCP;
-   OPC UA;
-   BACnet;
-   CAN/CANopen/J1939 cuando corresponda;
-   SNMP;
-   MQTT;
-   HTTP/REST;
-   TCP/UDP documentado.

Resultado: configurar adaptador, validar significado y crear Device
Profile.

## Ruta 2 --- Legacy / Unknown

Existe comunicación, pero documentación insuficiente.

Procedimiento:

1.  identificar interfaz eléctrica/lógica;
2.  no conectar hasta conocer niveles/aislamiento;
3.  capturar de manera pasiva cuando sea posible;
4.  timestamp;
5.  identificar framing/patrones;
6.  correlacionar con acciones observables;
7.  construir hipótesis;
8.  repetir pruebas;
9.  confirmar con manuales/evidencia;
10. crear decoder experimental;
11. validar;
12. sólo después evaluar escritura.

## Ruta 3 --- Instrumented

No existe interfaz aprovechable.

Sensores externos posibles:

-   corriente;
-   voltaje;
-   energía;
-   vibración;
-   temperatura;
-   presión;
-   flujo;
-   proximidad inductiva;
-   fotocelda;
-   encoder;
-   contacto seco;
-   señal 24 V aislada;
-   torre luminosa;
-   pulsos;
-   RPM;
-   apertura/posición.

La primera versión prioriza **sensado no invasivo o eléctricamente
aislado**.

------------------------------------------------------------------------

# 7. Atlas Edge

No diseñar hardware propio hasta demostrar que hace falta.

## Edge Lite

Para una o pocas máquinas:

-   MCU/SBC según necesidad;
-   entradas aisladas;
-   RS-485;
-   Ethernet/Wi-Fi cuando sea apropiado;
-   almacenamiento temporal;
-   watchdog;
-   configuración local.

## Edge Industrial

-   alimentación industrial;
-   aislamiento;
-   Ethernet;
-   RS-232/422/485;
-   CAN;
-   DI/DO aislados;
-   entradas analógicas cuando proceda;
-   almacenamiento local;
-   RTC;
-   watchdog;
-   montaje DIN;
-   rango industrial cuando el caso lo exija.

## Edge Pro

-   múltiples máquinas;
-   historian local;
-   reglas;
-   buffering;
-   redundancia;
-   múltiples protocolos;
-   gestión remota autorizada.

### Regla Build-vs-Buy

Antes de fabricar PCB/caja:

``` text
¿Existe gateway comercial económico que cumpla?
       ├─ sí → integrarlo
       └─ no → evaluar hardware Atlas
```

El valor principal debe estar en **software + conocimiento +
integración + perfiles**, no en reinventar un convertidor RS-485.

------------------------------------------------------------------------

# 8. Protocolos y tecnologías prioritarias

## Tier 1

-   Modbus RTU/TCP
-   MQTT
-   OPC UA
-   RS-232/422/485
-   HTTP/REST
-   TCP/UDP
-   digital inputs / pulse counters

## Tier 2

-   CAN / CANopen / J1939 según industria
-   BACnet/IP y MS/TP
-   SNMP
-   ODBC/SQL
-   CSV/files/FTP/SFTP

## Tier 3

-   PROFINET
-   EtherNet/IP
-   vendor SDKs/APIs
-   protocolos propietarios documentados/licenciados
-   conectores específicos por fabricante

**No confundir transporte con protocolo.** RS-485 no significa Modbus;
Ethernet no significa OPC UA.

------------------------------------------------------------------------

# 9. Evidencia tecnológica

ThingsBoard IoT Gateway demuestra una arquitectura real de conectores
desacoplados para MQTT, Modbus, OPC-UA, BACnet, REST, BLE, CAN, FTP,
ODBC, SNMP y sockets, además de extensiones personalizadas. Esto valida
el patrón de gateway multiprotocolo que Atlas GT adoptará, sin
obligarnos a copiar su producto.

La documentación actual también confirma: - Modbus sobre
TCP/UDP/serial; - OPC-UA con lectura/escritura, nodos y seguridad; -
BACnet para automatización de edificios; - CAN como bus integrable; -
almacenamiento/buffering local en gateway.

Atlas GT debe poder aprovechar software abierto existente cuando
convenga en vez de reimplementar todo.

------------------------------------------------------------------------

# 10. Modelo normalizado

Un protocolo no debe contaminar el resto del sistema.

``` text
Asset
 ├─ Identity
 ├─ Interfaces[]
 ├─ Capabilities[]
 ├─ Signals[]
 ├─ States[]
 ├─ Events[]
 ├─ Counters[]
 ├─ Measurements[]
 ├─ Commands[]
 └─ Evidence[]
```

Ejemplo:

``` text
Asset: PRENSA-04

Signal:
  key: motor.current
  value: 8.7
  unit: A
  source: modbus.holding.40037
  quality: GOOD
  timestamp: ...
  confidence: VERIFIED

State:
  key: machine.run_state
  value: RUNNING
  derived_from:
    motor.current > threshold
    cycle_sensor active
```

------------------------------------------------------------------------

# 11. Device Profile

Cada integración terminada genera un paquete reutilizable:

``` text
Manufacturer
Model
Controller
Firmware
Interfaces
Electrical notes
Protocol
Connection parameters
Register/node map
Scaling
Units
Enums
Polling limits
Commands
Read/write permissions
Known quirks
Safety notes
Evidence
Tests
Profile version
```

No identificar solamente por marca.

`Siemens XYZ + controller ABC + firmware 2.4` puede comportarse distinto
de otra revisión.

------------------------------------------------------------------------

# 12. Discovery

## Red

-   hosts autorizados;
-   puertos esperados;
-   servicios;
-   OPC UA endpoints;
-   SNMP cuando esté configurado;
-   gateways;
-   identificación no destructiva.

## Serial

-   inventario de puertos;
-   baud/parity/data/stop configurables;
-   captura;
-   framing;
-   Modbus RTU probe sólo bajo perfil seguro.

## CAN

-   interfaz;
-   bitrate configurado;
-   captura pasiva;
-   IDs;
-   frecuencia;
-   cambios;
-   decoders.

## Sensores

-   autoidentificación cuando exista;
-   canal;
-   calibración;
-   unidades;
-   health/status.

Discovery no significa bombardear una red OT con escaneos agresivos.

------------------------------------------------------------------------

# 13. Signal Correlator --- la parte divertida 😈

Objetivo: ayudar a descubrir significado sin inventarlo.

Ejemplo:

``` text
ANTES:
R17 = 0
R23 = 481
R41 = 0

OPERADOR ENCIENDE MOTOR

DESPUÉS:
R17 = 1
R23 = 481
R41 = 0

OPERADOR APAGA MOTOR

R17 = 0
```

Atlas:

``` text
Hipótesis:
R17 → motor_running
evidencia: 8/8 transiciones correlacionadas
confianza: EXPERIMENTAL-ALTA
```

Otro:

``` text
R23:
22 → 28 → 35 → 47 → 62
durante calentamiento

Hipótesis:
temperatura o variable térmica
```

**No llamar "temperatura" hasta confirmar unidad/escalado/evidencia.**

------------------------------------------------------------------------

# 14. Confidence / Provenance

Toda interpretación:

-   `DOCUMENTED`
-   `VERIFIED`
-   `EXPERIMENTAL_HIGH`
-   `EXPERIMENTAL`
-   `INFERRED`
-   `UNKNOWN`

Y fuente:

-   manual fabricante;
-   EDS/GSD/DBC/register map;
-   SDK;
-   observación;
-   prueba controlada;
-   técnico/cliente;
-   community report.

Nunca mezclar hecho documentado con inferencia.

------------------------------------------------------------------------

# 15. Read vs Write

Cada señal/comando declara:

``` text
READ_ONLY
SAFE_WRITE_TEST
CONTROL
SAFETY_RELATED
UNKNOWN
```

`SAFETY_RELATED` jamás se automatiza como control normal.

Para habilitar escritura:

1.  documentación o evidencia fuerte;
2.  rango;
3.  tipo;
4.  estado permitido;
5.  rollback;
6.  autorización;
7.  prueba offline/simulador;
8.  prueba controlada;
9.  audit log.

------------------------------------------------------------------------

# 16. Store & Forward

La planta no puede depender de Internet.

``` text
DEVICE → EDGE BUFFER → Atlas GT
                    ↘
                     Internet caído
                     guarda local
                     reenvía después
```

Cada muestra/evento debe tener timestamp, calidad y origen.

Evitar duplicados al reintentar mediante IDs/idempotencia donde sea
posible.

------------------------------------------------------------------------

# 17. Historian

Guardar:

-   telemetría;
-   estados;
-   eventos;
-   alarmas;
-   counters;
-   downtime;
-   quality;
-   conexión/desconexión;
-   cambios de configuración.

Políticas de retención configurables:

``` text
raw high-frequency → corto plazo
aggregates         → largo plazo
events/alarms      → histórico
```

No llenar discos eternamente con ruido.

------------------------------------------------------------------------

# 18. Automatizaciones vendibles

## Atlas Count

Para máquinas mudas: - piezas; - ciclos; - run/stop; - tiempos; - turno.

## Atlas OEE

-   availability;
-   performance;
-   quality si existe señal/dato;
-   downtime reasons;
-   producción por turno.

No inventar Quality si no existe conteo bueno/malo.

## Atlas Energy

-   consumo;
-   demanda;
-   energía por máquina/turno/producto cuando sea correlacionable;
-   anomalías.

## Atlas Health

-   vibración;
-   temperatura;
-   corriente;
-   horas;
-   tendencias;
-   mantenimiento basado en condición.

No llamar "predictive maintenance" a simples thresholds.

## Atlas Connect

-   legacy → protocolo moderno.

## Atlas Building

-   BACnet/Modbus/HVAC/medidores/UPS.

## Atlas Vision

Futuro módulo separado para inspección visual; no requisito de GT V1.

## Atlas Full

Composición de módulos, no monolito distinto.

------------------------------------------------------------------------

# 19. Atlas Doctor

Diagnóstico de integración:

``` text
"No hay datos"
 ↓
¿Edge vivo?
¿interfaz arriba?
¿cableado?
¿serial settings?
¿unit id?
¿timeout?
¿CRC?
¿register map?
¿permisos?
¿gateway?
```

Ejemplos de diagnósticos:

-   `CRC errors ↑` → revisar ruido/cableado/baud/paridad/topología.
-   timeouts en un slave → revisar ID, conexión o carga.
-   todos los devices caen → upstream/gateway.
-   valor imposible → byte order/word order/scaling/tipo.
-   counter retrocede → reset/overflow/reboot.
-   timestamps saltan → reloj/NTP/RTC.
-   dato congelado pero conexión viva → stale-data alarm.
-   lectura funciona, escritura no → permisos/rango/estado/protocolo.

Doctor explica **qué sabe, qué sospecha y qué prueba sigue**.

------------------------------------------------------------------------

# 20. Alarm Engine

Alarmas no serán simples popups.

Cada alarma:

-   source;
-   condition;
-   severity;
-   debounce;
-   hysteresis;
-   delay;
-   acknowledgement;
-   clear condition;
-   maintenance suppression;
-   owner;
-   audit.

Evitar alarm storm.

------------------------------------------------------------------------

# 21. Reglas derivadas

Ejemplo:

``` text
RUNNING =
 current > 3.2A
 AND cycle pulse seen within 15s
```

Pero conservar señales originales.

Las reglas son versionadas y testeables.

------------------------------------------------------------------------

# 22. Dashboard

Vista operador:

``` text
PRENSA 04
🟢 PRODUCIENDO

Piezas turno: 1,284
Ritmo: 18/min
Paro acumulado: 00:47
Corriente: 8.7 A
Temperatura: 61 °C

Último evento:
00:31 — paro 4m 12s
```

Vista técnico:

-   raw signals;
-   protocol;
-   quality;
-   latency;
-   polling;
-   errors;
-   packets/frames permitidos;
-   mapping;
-   evidence;
-   logs.

Vista ingeniería:

-   correlación;
-   charts;
-   register/node explorer;
-   decoder;
-   profile editor;
-   tests.

------------------------------------------------------------------------

# 23. Auditoría

Registrar:

``` text
quién
cuándo
qué dispositivo
qué cambió
antes/después
qué comando
resultado
correlation id
```

Especialmente toda escritura/control.

------------------------------------------------------------------------

# 24. Reliability

## Backups

-   DB;
-   Device Profiles;
-   mappings;
-   dashboards;
-   rules;
-   alarms;
-   historian metadata;
-   configuration;
-   secrets mediante almacén seguro;
-   custom adapters.

## Recovery

-   backup automático;
-   checksums;
-   restore drills;
-   export/import de Device Profile;
-   segundo servidor opcional.

## Edge

Si server cae, Edge puede seguir bufferizando según capacidad.

------------------------------------------------------------------------

# 25. Seguridad OT

Principios:

-   segmentación IT/OT;
-   least privilege;
-   read-only por defecto;
-   allowlist;
-   credenciales fuera de código;
-   TLS/certificados donde protocolo lo soporte;
-   no abrir OT a Internet;
-   logs;
-   backups;
-   actualización controlada;
-   inventory;
-   rollback;
-   rate limits/polling limits.

**Atlas GT no sustituye arquitectura de seguridad industrial ni
controles funcionales/safety.**

------------------------------------------------------------------------

# 26. Hardware sin casarnos con una marca

Antes de comprar:

### Gateway

Evaluar: - CPU/RAM/storage; - temperatura; - alimentación; - DIN; -
RS-485; - CAN; - Ethernet; - aislamiento; - watchdog; - Linux/Windows; -
disponibilidad México; - costo; - reemplazabilidad.

### Sensores

Evaluar: - rango; - precisión; - salida; - aislamiento; -
alimentación; - montaje; - calibración; - ambiente; - repetibilidad; -
costo.

Crear `Hardware Compatibility Profiles`; no hardcodear "sensor Atlas".

------------------------------------------------------------------------

# 27. Simuladores --- desarrollo a \$0

Antes de tocar una fábrica:

``` text
PC
├─ Modbus simulator
├─ OPC UA demo server
├─ MQTT broker
├─ virtual serial
├─ virtual CAN
├─ BACnet simulator
└─ fake machine generator
```

Crear una `Atlas Fake Factory`:

``` text
Prensa
Compresor
Horno
Bomba
Conveyor
CNC fake
UPS fake
```

Cada equipo: - estados; - telemetría; - fallas; - disconnects; - bad
values; - counter rollover; - latency; - noise; - malformed packets.

Esto permite pruebas E2E sin hardware.

------------------------------------------------------------------------

# 28. Pruebas maliciosamente realistas

1.  desconectar cable;
2.  reboot device;
3.  cambiar IP;
4.  duplicar Unit ID;
5.  baud incorrecto;
6.  paridad incorrecta;
7.  byte order incorrecto;
8.  word order incorrecto;
9.  scaling x10/x100;
10. counter overflow;
11. timestamp malo;
12. sensor congelado;
13. ruido/intermitencia;
14. Internet caído;
15. server caído;
16. disco casi lleno;
17. Edge sin corriente;
18. 100 dispositivos reconectando;
19. registro desaparece tras firmware;
20. dato cambia de tipo;
21. escritura rechazada;
22. timeout;
23. packet malformed;
24. protocolo propietario parcialmente conocido;
25. sensor externo desconectado;
26. señal invertida;
27. máquina encendida pero sin producir;
28. producción sin corriente esperada;
29. alarma que oscila;
30. tormenta de eventos.

------------------------------------------------------------------------

# 29. Prueba reina

Una prensa simulada de 1987 **sin protocolo**:

``` text
CT current
inductive cycle sensor
temperature
alarm contact
```

Atlas debe:

1.  detectar Edge;
2.  adquirir señales;
3.  calibrar;
4.  crear perfil;
5.  determinar RUN/STOP;
6.  contar ciclos;
7.  historizar;
8.  generar dashboard;
9.  detectar desconexión;
10. bufferizar durante caída;
11. recuperar;
12. exportar MQTT/OPC UA/REST;
13. reconstruir auditoría completa.

Si esto funciona, tenemos el corazón comercial.

------------------------------------------------------------------------

# 30. Producto comercial

No vender "MQTT".

Vender resultado:

### Oferta 1

**"Quiero saber cuánto produce esta máquina."** → Atlas Count.

### Oferta 2

**"Quiero saber por qué se para."** → Atlas OEE + downtime.

### Oferta 3

**"Mi máquina vieja no se conecta al MES."** → Atlas Connect.

### Oferta 4

**"Quiero vigilar condición/consumo."** → Atlas Health/Energy.

### Oferta 5

**"Tengo 40 máquinas de marcas distintas."** → Atlas GT Fleet.

Modelo comercial posible: - levantamiento; - hardware; - instalación; -
configuración; - Device Profile; - dashboard; - capacitación; -
soporte; - módulos opcionales.

No fijar precios hasta conocer hardware, mercado y costo de instalación.

------------------------------------------------------------------------

# 31. Moat / valor acumulativo

El activo importante puede terminar siendo la biblioteca:

``` text
Atlas Device Library
Manufacturer
Model
Controller
Firmware
Protocol
Map
Quirks
Tests
Hardware recipe
```

La primera máquina desconocida cuesta investigación.

La número 50 del mismo controlador puede convertirse en:

``` text
Conectar → identificar → aplicar perfil → validar
```

Ese conocimiento reusable es parte central del producto.

------------------------------------------------------------------------

# 32. DECISIONES CERRADAS --- NO VOLVER A PREGUNTAR

Estas decisiones YA ESTÁN TOMADAS. No gastar tiempo reabriéndolas:

### D1 --- Control

¿V1 será **100% monitorización/read-only**, dejando escritura/control
para una fase posterior?

**DECISIÓN:** sí. Reduce riesgo y permite entregar
Count/OEE/Energy/Health antes.

### D2 --- Edge

¿Queremos que el servidor Atlas GT pueda vivir en una PC normal y Edge
sea opcional, o exigimos una caja por máquina?

**DECISIÓN:** PC/server central + Edge sólo cuando
distancia/interfaz/aislamiento/offline buffering lo justifique.

### D3 --- Multiempresa

¿Producto pensado desde inicio para múltiples plantas/clientes
separados?

**DECISIÓN:** sí en modelo de datos (`Tenant/Site/Area/Line/Asset`),
aunque V1 sólo use una planta.

### D4 --- Device Profiles

¿Los perfiles creados en cliente son privados por defecto y el cliente
decide si comparte uno anonimizado?

**DECISIÓN:** sí.

### D5 --- Hardware

¿Vendemos hardware propio desde V1?

**DECISIÓN:** no. Primero integrar gateways/sensores existentes.
Hardware Atlas sólo cuando haya una ventaja concreta.

### D6 --- Cloud

¿Habrá portal remoto opcional futuro?

**DECISIÓN:** arquitectura preparada, pero cero dependencia operativa de
nube.

------------------------------------------------------------------------

# 33. Modelo jerárquico

``` text
Tenant
└── Site
    └── Area
        └── Line
            └── Asset
                ├── Interfaces
                ├── Signals
                ├── States
                ├── Events
                ├── Commands
                ├── Profiles
                └── History
```

Así no reconstruimos todo cuando pase de un taller a una planta con 300
equipos.

------------------------------------------------------------------------

# 34. Stack: decidir por requisitos, no por moda

Dado un entorno .NET, una opción razonable a evaluar:

-   ASP.NET Core;
-   background workers;
-   SignalR;
-   DB relacional para configuración;
-   time-series strategy separada;
-   MQTT broker externo;
-   adapters como plugins/processes;
-   Docker opcional;
-   Linux/Windows Edge según hardware.

Pero **no congelar stack de historian/protocol libraries** hasta hacer
spikes y medir.

Usar software existente cuando aporte: - ThingsBoard Gateway como
referencia/componente potencial; - Mosquitto/EMQX para MQTT; - Node-RED
para laboratorio/prototipos; - librerías maduras de protocolo; -
simuladores.

No reescribir protocolos estándar por orgullo.

------------------------------------------------------------------------

# 35. BLOQUES QUIRÚRGICOS DE CONSTRUCCIÓN PARA KIMI

## F0 --- Research & ADRs

Sin UI bonita. - protocolos; - librerías; - licencias; - modelo
normalizado; - seguridad; - simulator matrix.

**Salida:** decisiones arquitectónicas documentadas.

## F1 --- Fake Factory

Simulador + MQTT + Modbus.

**Salida:** máquinas falsas generando estados/fallas reproducibles.

## F2 --- Core

Asset model, signals, quality, timestamps, profiles, audit.

## F3 --- Modbus

RTU/TCP, polling, mapping, endian/scaling, reconnect.

## F4 --- MQTT

publish/subscribe, buffering/idempotencia.

## F5 --- Dashboard

raw + normalized + states.

## F6 --- Device Profiles

export/import/version/evidence/tests.

## F7 --- Correlator

comparar señales con eventos marcados por operador.

## F8 --- External sensors

Edge simulator primero; hardware después.

## F9 --- OPC UA

client y después server/export si se justifica.

## F10 --- Doctor

diagnóstico de conectividad/configuración/calidad.

## F11 --- OEE/Count

producto comercial completo sobre el Core.

## F12 --- Energy/Health

módulos.

## F13 --- BACnet/CAN/SNMP

según clientes/casos reales.

## F14 --- Fleet/Reliability

multi-site, backup, emergency.

------------------------------------------------------------------------

# 36. Definition of Done por adaptador

Un conector NO está terminado porque "leyó un dato".

Debe probar:

-   connect;
-   reconnect;
-   timeout;
-   invalid config;
-   malformed response;
-   disconnect;
-   restart;
-   mapping;
-   units;
-   quality;
-   timestamp;
-   logging;
-   metrics;
-   load;
-   graceful shutdown;
-   buffer behavior;
-   profile import/export;
-   security configuration;
-   tests.

Para escritura además: - allowlist; - range; - state constraints; -
authorization; - audit; - failure/rollback semantics.

------------------------------------------------------------------------

# 37. INSTRUCCIÓN MAESTRA PARA KIMI

> Construye y termina Atlas GT por bloques verificables sobre lo que YA
> existe. No intentes completar toda la plataforma en un solo pase.
> Antes de modificar código, inspecciona repositorio y ADRs. No inventes
> protocolos, registros, APIs, SDKs ni capacidades. Toda integración
> desconocida inicia read-only. Mantén Core, Connectors, Normalization,
> Historian, UI, Analytics y Commercial Modules desacoplados. Cada fase
> termina con build limpio, pruebas automatizadas, prueba E2E y
> documentación de evidencia. Si una prueba falla, diagnostica, corrige
> y vuelve a ejecutar antes de avanzar. No reemplaces una librería
> madura por implementación propia sin justificarlo. Conserva
> compatibilidad offline y logs estructurados. No declares soporte de un
> equipo hasta probar modelo/controlador/firmware o marcarlo
> explícitamente como experimental.

------------------------------------------------------------------------

# 38. PRIMER PRODUCTO COMPLETO SOBRE EL CORE

**Atlas GT Count**

Problema:

> "No sé cuánto trabaja realmente esta máquina."

Hardware mínimo según caso: - señal existente o sensor de ciclo; -
opcional corriente; - Edge si hace falta.

Software:

``` text
Machine
  ↓
Signal
  ↓
Atlas GT
  ↓
RUN / STOP / CYCLE
  ↓
Historian
  ↓
Dashboard
```

Entrega:

-   piezas;
-   tiempo produciendo;
-   tiempo parada;
-   eventos;
-   turnos;
-   histórico;
-   exportación;
-   reporte;
-   health del sensor/gateway.

Después se agregan OEE, Energy, Health y Connect.

------------------------------------------------------------------------

# 39. La oportunidad perdida

No competir inicialmente contra Siemens/Rockwell vendiendo
"automatización completa".

Buscar las máquinas que quedaron **entre mundos**:

``` text
demasiado buenas para tirarlas
demasiado viejas para Industria 4.0
demasiado raras para integración plug-and-play
demasiado importantes para ignorarlas
```

Ahí Atlas GT puede tener sentido.

La pregunta comercial no es:

> "¿Usan OPC UA?"

Es:

> **"¿Qué máquina o equipo quisiera usted poder medir/conectar pero
> nadie le ha podido sacar datos sin reemplazarlo?"**

Esa pregunta descubre trabajo.

------------------------------------------------------------------------

# 40. Fuentes técnicas iniciales

-   ThingsBoard IoT Gateway --- arquitectura y conectores:
    https://thingsboard.io/docs/iot-gateway/what-is-thingsboard-iot-gateway/
-   ThingsBoard connectors:
    https://thingsboard.io/docs/iot-gateway/connectors/
-   Modbus connector:
    https://thingsboard.io/docs/iot-gateway/config/modbus/
-   OPC-UA connector:
    https://thingsboard.io/docs/iot-gateway/config/opc-ua/
-   BACnet connector:
    https://thingsboard.io/docs/iot-gateway/config/bacnet/
-   CAN connector: https://thingsboard.io/docs/iot-gateway/config/can/
-   General gateway/buffering/config:
    https://thingsboard.io/docs/iot-gateway/config/general/
-   ThingsBoard Edge connectivity:
    https://thingsboard.io/docs/edge/reference/apis-and-sdks/overview/

Estas fuentes validan el patrón multiprotocolo y servirán para comparar
decisiones. Atlas GT debe ampliar investigación por protocolo/fabricante
conforme entren casos reales.

------------------------------------------------------------------------

# 41. Frase de producto

## **ATLAS GT**

### *La máquina no tiene que ser nueva para empezar a dar información.*

``` text
HABLA       → LA CONECTAMOS
OTRO IDIOMA → LA TRADUCIMOS
NO HABLA    → LA INSTRUMENTAMOS
```

Y todo termina convertido en información útil, trazable y recuperable.

------------------------------------------------------------------------

# 42. Norte técnico

El éxito de Atlas GT no se mide por cuántos protocolos aparecen en el
README.

Se mide por esto:

> **Llegamos a un equipo autorizado que hoy es una caja negra; sin
> comprometer su operación, logramos obtener señales confiables,
> explicar qué significan, conservarlas, entregarlas en un formato
> moderno y repetir la integración de forma documentada.**

Eso es Atlas GT.

------------------------------------------------------------------------

# 43. GATES DE CIERRE --- NO NEGOCIABLES

Cada bloque termina sólo si cumple:

`IMPLEMENTADO → BUILD RELEASE → TESTS → E2E APLICABLE → EVIDENCIA → DOCUMENTACIÓN`

Cierre global de software exige:

-   `dotnet restore` correcto;
-   `dotnet build -c Release` con 0 errores;
-   warnings corregidos o justificados técnicamente;
-   UnitTests reales;
-   IntegrationTests reales;
-   ProtocolTests reales;
-   SimulatorTests reales;
-   EndToEndTests reales;
-   ninguna suite vacía ni `UnitTest1` de plantilla;
-   migraciones desde base limpia verificadas cuando exista DB;
-   backup/restore verificado;
-   reinicio/recovery verificado;
-   store-and-forward sin duplicación silenciosa;
-   seguridad passive/read-only por defecto;
-   secretos fuera de Git;
-   repo sin `.vs`, `bin`, `obj`, cookies, responses, backups ni
    temporales;
-   documentación alineada con lo realmente probado;
-   prueba reina completada en simulación;
-   working tree limpio.

Hardware/PLC/planta real no disponible:

`EXTERNAL / NOT PROVEN`

Eso NO autoriza a inventar éxito y tampoco debe bloquear el cierre del
software verificable.

------------------------------------------------------------------------

# 44. ORDEN QUIRÚRGICO A → K

## A --- Baseline e higiene

Inspeccionar todo el repo; clasificar
FUNCIONA/PARCIAL/ROTA/FALTA/BASURA; limpiar artefactos; revisar
secretos; verificar referencias, solución, migraciones y tests; build
Release.

**Gate A:** baseline reproducible y repo técnicamente entendible.

## B --- Core universal

Cerrar Domain, jerarquía, identities, capabilities, quality, provenance,
observations, evidence, profiles y contratos de conectores.

**Gate B:** Core no conoce protocolos concretos y tiene tests reales.

## C --- Persistencia / Historian / Audit

Cerrar persistencia, migraciones, historian, retención, auditoría,
backup/restore y recuperación.

**Gate C:** integración real de persistencia pasa.

## D --- Fake Factory

Cerrar simuladores reproducibles, faults, malformed data, disconnects,
rollover, latency y reconexión.

**Gate D:** SimulatorTests completos; todo marcado SIMULATED.

## E --- Conectividad prioritaria

Cerrar transportes y conectores Tier 1 realmente implementados;
reconnect, timeout, cancellation, mapping, quality y cleanup.

**Gate E:** ProtocolTests pasan; no declarar protocolos que no existan.

## F --- Discovery / Observation

Cerrar discovery autorizado, inventario, sesiones, captura, health y
passive-first.

**Gate F:** ninguna escritura accidental durante discovery.

## G --- Normalization / Device Profiles

Cerrar mapping, scaling, units, endian, profile
import/export/version/evidence.

**Gate G:** varios conectores alimentan el mismo Core sin contaminarlo.

## H --- Correlator / Inference / Doctor

Cerrar correlación, confidence, provenance y diagnóstico
`KNOWN/SUSPECTED/NEXT TEST`.

**Gate H:** ninguna inferencia aparece como VERIFIED sin
evidencia/promoción.

## I --- UX completa

Cerrar Operador, Admin, Técnico/Device Lab, configuración grande,
dashboards, búsquedas, validaciones y mensajes humanos.

**Gate I:** un operador usa una máquina configurada sin conocer el
protocolo; un admin puede configurar a fondo sin tocar código.

## J --- Atlas Count + resiliencia

Cerrar Count sobre Core, historian, alarmas, store-forward,
idempotencia, restart, disk pressure y concurrencia.

**Gate J:** Count no bypassa arquitectura y sobreviven fallos simulados.

## K --- Cierre malicioso

Ejecutar matriz de pruebas adversas, prueba reina, restore, build
Release, todas las suites, revisión de secretos/basura/documentación.

**Gate K:** reporte factual + SHA/branch; sólo entonces declarar cierre
de software.

------------------------------------------------------------------------

# 45. REPORTE FINAL DE KIMI

Responder al final con datos, no marketing:

``` text
COMMIT:
BRANCH:

BUILD RELEASE:
Errors:
Warnings:

TESTS:
Unit:
Integration:
Protocol:
Simulator:
E2E:
Total:
Failed:
Skipped:

DATABASE/MIGRATIONS:
BACKUP/RESTORE:
FAKE FACTORY:
PRUEBA REINA:
PASSIVE-FIRST:
SECRETS:
GIT CLEANLINESS:

IMPLEMENTADO:
...

EXTERNAL / NOT PROVEN:
...

RIESGOS RESIDUALES:
...

ARCHIVOS PRINCIPALES CAMBIADOS:
...
```

No usar `100%`, `production-ready`, `industrial-grade` ni "soporta X"
sin evidencia.

------------------------------------------------------------------------

# 46. ORDEN FINAL --- EMPIEZA, NO REDISEÑES

**Kimi: empieza inmediatamente por A sobre el repositorio EXISTENTE.**
No regreses un plan para que nosotros lo ejecutemos. No te detengas al
terminar la auditoría inicial. No conviertas el producto en
minimal/demo/MVP/skeleton. No borres funcionalidad correcta para
simplificar. No inventes hardware ni protocolos. No gastes tiempo
reabriendo decisiones cerradas.

Trabaja:

`READ → VERIFY → FIX/IMPLEMENT → BUILD → TEST → FIX → RETEST → NEXT`

hasta llegar a K o hasta encontrar un bloqueo externo real que no pueda
resolverse en software. En ese caso marca únicamente esa capacidad
`EXTERNAL / NOT PROVEN` y continúa con todo lo demás.
