# Jenga AR

Juego de mesa de realidad aumentada inspirado en Jenga, para **tres jugadores
locales por turnos**. La torre aparece sobre un marcador impreso y los jugadores
retiran bloques por turnos hasta que alguien la derriba.

- **Motor:** Unity **6000.5.6f1** (URP)
- **AR:** Vuforia Engine 11.4.4, Image Target
- **Plataforma:** Android (IL2CPP, ARM64, min SDK 33 = Android 13)

---

# Instalacion

El proyecto necesita **dos descargas**: el repositorio y el paquete de Vuforia.

El paquete pesa 132 MB y GitHub no acepta archivos de mas de 100 MB dentro de
un repositorio, asi que va aparte, en la pestana **Releases**. Todo lo demas
(licencia, base de datos del marcador, escena, scripts) ya esta en el repo y no
hay que configurar nada.

## Paso 1 - Instalar Unity

Desde Unity Hub, instalar la version **exacta**:

```
6000.5.6f1
```

Con estos modulos tildados:

- **Android Build Support**
  - Android SDK & NDK Tools
  - OpenJDK

> Si Unity Hub ofrece "actualizar" el proyecto a otra version, decir que **no**
> e instalar la 6000.5.6f1.

## Paso 2 - Descargar el repositorio

```bash
git clone https://github.com/NicolePG/Practica-2-AR-RealidadV.git
```

## Paso 3 - Descargar el paquete de Vuforia

En el repo de GitHub, ir a la pestana **Releases** y descargar
`com.ptc.vuforia.engine-11.4.4.tgz`.

## Paso 4 - Poner el paquete en su lugar

Mover el `.tgz` dentro de la carpeta `Packages` del proyecto, sin renombrarlo:

```
Practica-2-AR-RealidadV/
├── Assets/
├── Packages/
│   ├── manifest.json
│   ├── packages-lock.json
│   └── com.ptc.vuforia.engine-11.4.4.tgz   <-- ACA
└── ProjectSettings/
```

`manifest.json` ya lo referencia asi, no hay que editarlo:

```json
"com.ptc.vuforia.engine": "file:com.ptc.vuforia.engine-11.4.4.tgz"
```

## Paso 5 - Abrir el proyecto

Unity Hub → **Add** → **Add project from disk** → elegir la carpeta
`Practica-2-AR-RealidadV` → clic para abrir.

La primera apertura tarda entre 10 y 20 minutos: importa todos los assets y
compila Vuforia desde cero.

---

> ## El paso 4 va ANTES del paso 5
>
> Si se abre Unity sin el `.tgz` en su lugar, los scripts de Vuforia no existen.
> Sus componentes quedan como *"Missing (Mono Script)"* y en cuanto Unity guarde
> algo **se pierden**: el ImageTarget de la escena, el VuforiaBehaviour de la
> camara y la licencia. No se recupera: hay que borrar la carpeta y volver a
> clonar.

## Verificar que quedo bien

1. La consola no muestra errores rojos.
2. En `SampleScene` estan `ARCamera`, `ImageTarget > JengaTower`, `Canvas`,
   `UIManager`, `GameManager` y `EventSystem`.
3. Ningun componente dice *"Missing (Mono Script)"*.
4. `Window > Vuforia Configuration`: el campo de licencia tiene texto.

Si algo de eso falla, el `.tgz` no estaba en su lugar al abrir. Borrar la
carpeta y repetir desde el paso 2.

---

## Como publicar el paquete en Releases (una sola vez)

Solo hace falta si el Release todavia no existe:

1. En el repo → **Releases** → **Create a new release**
2. *Choose a tag* → escribir `v1.0` → **Create new tag: v1.0 on publish**
3. *Release title* → `Paquete de Vuforia`
4. Arrastrar `com.ptc.vuforia.engine-11.4.4.tgz` al area de
   **Attach binaries**
5. **Publish release**

El archivo original esta en `Packages/` de cualquier copia del proyecto que ya
funcione. Tambien se puede bajar Vuforia Engine 11.4.4 desde
<https://developer.vuforia.com/downloads/sdk> (requiere cuenta).

> El registro oficial `registry.packages.developer.vuforia.com` solo publica
> hasta la version 9.6.3, asi que la 11.4.4 no se puede resolver desde ahi.

---

# El marcador

La imagen a detectar es `jenga_target`:

```
Assets/Editor/Vuforia/ImageTargetTextures/JengaAR/jenga_target_scaled.jpg
```

**Imprimirla y apoyarla plana sobre una mesa.** Mostrada en la pantalla de un
monitor tambien se detecta, pero al ser una superficie vertical no se puede
caminar alrededor de la torre.

---

# Como jugar

1. **INICIAR JUEGO** en el menu.
2. Escribir los nombres de los 3 jugadores y **CONTINUAR**.
3. Apuntar la camara al marcador. La torre aparece cuando el seguimiento se
   estabiliza; el cartel de abajo a la izquierda avisa el estado.
4. Por turnos:
   - **Arrastrar sobre un bloque** para sacarlo. Hay que sacarlo casi entero;
     si se suelta antes, vuelve a su lugar y **el turno no cambia**.
   - Cuando sale, el bloque vuela solo hasta la cima.
   - **Arrastrar sobre una zona vacia** gira la torre, para sacar de otra cara
     sin caminar alrededor del marcador.
5. Pierde el jugador que derriba la torre.

## Reglas implementadas

| # | Regla |
|---|-------|
| 1 | La partida comienza con el Jugador 1 |
| 2 | Cada jugador mueve un unico bloque por turno |
| 3 | No se pueden retirar bloques del nivel superior |
| 4 | El bloque retirado debe colocarse sobre la torre |
| 5 | Un movimiento invalido no cambia el turno |
| 6 | El jugador que derriba la torre pierde |
| 7 | Despues del Jugador 3, el turno regresa al Jugador 1 |
| 8 | La manipulacion solo se habilita con el seguimiento AR estable |

---

# Generar el APK

1. `File > Build Profiles`
2. Elegir el perfil **Android** y, si no esta activo, **Switch Platform**
   (la primera vez tarda: reimporta todas las texturas al formato de Android).
3. Verificar que `Assets/Scenes/SampleScene.unity` este tildada en la lista de
   escenas.
4. **Build**.

Se firma con la clave de depuracion, asi que Android pide permiso para instalar
de origenes desconocidos. Para publicar en Play Store haria falta un keystore
propio.

> El **min SDK es 33 (Android 13)**: en telefonos con una version anterior el
> APK no instala. Se cambia en `Player > Other Settings > Minimum API Level`.

---

# Ajustes de juego

Todo se toca desde el Inspector, sin recompilar.

**`JengaTower > JengaBuilder`**

| Campo | Que hace |
|-------|----------|
| `Block Size` | Tamano de la torre. Es la unica perilla: los munequitos se reubican y reescalan solos |
| `Carried Radius Factor` | Distancia de los munequitos al eje de la torre |
| `Carried Height Factor` | Alto de los munequitos respecto de la torre |
| `Extraction Friction` | Cuanto "agarra" el bloque mientras se lo saca |

**`JengaTower > JengaInteraction`**

| Campo | Que hace |
|-------|----------|
| `Rotation Sensitivity` | Grados de giro por cada 100 px de arrastre |
| `Hover Height` | Altura del vuelo del bloque. Es absoluta: si se cambia mucho `Block Size`, moverla en la misma proporcion |
| `Tilted Blocks To Lose` | Cuantos bloques torcidos hacen perder. Menos = mas dificil |
| `Fallen Blocks To Lose` | Cuantos bloques caidos hacen perder |

---

# Estructura

```
Assets/
  Scripts/
    GameManager.cs               Turnos y reglas de la partida
    JengaBuilder.cs              Construye la torre y su fisica
    JengaInteraction.cs          Sacar, colocar y girar con el dedo
    UI/UIManager.cs              Paneles, textos y audio
    UI/TurnHud.cs                Fila de jugadores del HUD
    Indicadores de Turno/        Munequitos 3D alrededor de la torre
  Prefabs/
    Canvas.prefab                Toda la interfaz
    Players/                     Marcadores de jugador
  Scenes/SampleScene.unity       Escena unica
```
