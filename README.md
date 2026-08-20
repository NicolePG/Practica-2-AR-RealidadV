# Jenga AR

Juego de mesa de realidad aumentada inspirado en Jenga, para **tres jugadores
locales por turnos**. La torre aparece sobre un marcador impreso y los jugadores
retiran bloques por turnos hasta que alguien la derriba.

- **Motor:** Unity **6000.5.6f1** (URP)
- **AR:** Vuforia Engine 11.4.4, Image Target
- **Plataforma:** Android (IL2CPP, ARM64, min SDK 33 = Android 13)

---

## Antes de abrir el proyecto: falta el paquete de Vuforia

El paquete `com.ptc.vuforia.engine-11.4.4.tgz` pesa **138 MB** y supera el
limite de 100 MB por archivo de GitHub, asi que **no esta en el repositorio**.
Sin el, Unity abre el proyecto con errores de compilacion porque no encuentra
las clases de Vuforia.

Para dejarlo funcionando:

1. Conseguir el archivo `com.ptc.vuforia.engine-11.4.4.tgz`, ya sea copiandolo
   de otra maquina que tenga el proyecto o descargando **Vuforia Engine 11.4.4**
   desde <https://developer.vuforia.com/downloads/sdk>.
2. Copiarlo dentro de la carpeta `Packages/` del proyecto, con ese nombre exacto.
3. Recien ahi abrir el proyecto con Unity.

`Packages/manifest.json` ya lo referencia asi:

```json
"com.ptc.vuforia.engine": "file:com.ptc.vuforia.engine-11.4.4.tgz"
```

La licencia de Vuforia ya viene configurada en
`Assets/Resources/VuforiaConfiguration.asset`, no hay que tocar nada mas.

---

## El marcador

La imagen a detectar es `jenga_target`
(`Assets/Editor/Vuforia/ImageTargetTextures/JengaAR/jenga_target_scaled.jpg`).

**Imprimila y apoyala plana sobre una mesa.** Si la mostras en la pantalla de un
monitor tambien la detecta, pero al ser una superficie vertical no vas a poder
caminar alrededor de la torre, que es la mitad de la gracia.

---

## Como jugar

1. **INICIAR JUEGO** en el menu.
2. Escribir los nombres de los 3 jugadores y **CONTINUAR**.
3. Apuntar la camara al marcador. La torre aparece cuando el seguimiento se
   estabiliza (el cartel de abajo a la izquierda avisa el estado).
4. Por turnos:
   - **Arrastrar sobre un bloque** para sacarlo. Hay que sacarlo casi entero;
     si lo soltas antes, vuelve a su lugar y **el turno no cambia**.
   - Cuando sale, el bloque vuela solo hasta la cima.
   - **Arrastrar sobre una zona vacia** gira la torre, para sacar de otra cara
     sin tener que caminar alrededor del marcador.
5. Pierde el jugador que derriba la torre.

### Reglas implementadas

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

## Generar el APK

1. `File > Build Profiles`
2. Elegir el perfil **Android** y, si no esta activo, **Switch Platform**
   (la primera vez tarda: reimporta todas las texturas al formato de Android).
3. Verificar que `Assets/Scenes/SampleScene.unity` este tildada en la lista de
   escenas.
4. **Build**.

Se firma con la clave de depuracion, asi que Android va a pedir permiso para
instalar de origenes desconocidos. Para publicar en Play Store haria falta un
keystore propio.

> El **min SDK es 33 (Android 13)**: en telefonos con una version anterior el
> APK no instala. Se baja en `Player > Other Settings > Minimum API Level`.

---

## Estructura

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

Los valores de juego (sensibilidad del giro, dureza de la torre, umbral de
derrumbe) estan expuestos en el Inspector de `JengaTower` y del `GameManager`.
