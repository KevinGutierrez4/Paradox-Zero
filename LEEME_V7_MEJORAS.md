# One More Time · V7: mejoras de Jörmungandr y del combate

Unity 6000.5.5f1. Escena activa: `Assets/OneMoreTime/Generated_Hacha_Final 2/Scenes/OneMoreTime_Demo.unity`. No se modificó la escena.

Esta versión parte del proyecto V6 y no reconstruye nada desde cero:
- No hay escenas ni carpetas nuevas duplicadas.
- Se conservan todos los `.meta` y GUID existentes.
- Solo se crearon 5 archivos nuevos: `PlayerLifeSteal.cs`, `JP1/2/3_NeckTile.png` y el prefab `SerpentMovingPlatform.prefab`, cada uno con su `.meta`.
- El rugido sigue siendo silencioso: no hay ningún `AudioSource` ni `AudioClip`. Una prueba lo comprueba durante las dos presentaciones.

## Resultado en Unity (ejecutado en la laptop)

| Prueba | Resultado |
|---|---|
| Compilación (Assets > Refresh) | 0 errores |
| `Tools > One More Time > 9` (prefabs de escenario) | 17 prefabs generados, entre ellos el nuevo `SerpentMovingPlatform` |
| `Tools > One More Time > 5` (Play Mode completo) | **OMT_PLAY_PASS: 678 comprobaciones, 0 fallos, 0 excepciones** (`Verification/play-results.txt`) |
| `Tools > One More Time > 10` (capturas) | Terminó en Victoria con 0 errores, sin proyectiles ni peligros al final (`Verification/V6Play/*.png`, `log.txt`) |

En V6 había 650 comprobaciones; las 28 nuevas cubren lo de esta versión.

## 1. Corrección visual del jefe (cuello continuo)

**Problema:**
- El cuello eran segmentos sueltos, así que se veían anillos y uniones.
- El punto de anclaje estaba en `y = -1.6`, y durante la aparición la cabeza "se hundía". Por eso parte del cuello quedaba bajo el suelo y la silueta se cortaba.

**Solución en `JormungandrBody.cs`:**
- **Cuello de malla continua.** Es un "tubo" con 40 muestras que sigue una curva de Bézier desde el montículo de nieve hasta la articulación de la cabeza.
- **Textura.** Usa una textura repetida en U (`JP{fase}_NeckTile.png`), extraída de las piezas cilíndricas originales de cada atlas. El dorso y el vientre quedan siempre en su lado.
- **Grosor.** El ancho baja de forma progresiva, de 0,8 a un 16 % menos, sin estiramientos bruscos.
- **Aletas.** Se distribuyen a lo largo de la curva.
- **Base oculta.** El cuello nace detrás del montículo (`p0 = (0, -0.3)`), así la base nunca se ve flotando ni cortada. La cabeza sale del montículo en lugar de hundirse.
- **Transformaciones.** Dos mallas (A/B) hacen el cambio de fase desde la base hacia la cabeza con alfa por vértice. El orden se mantiene: cuerpo, cuello y por último la cabeza.
- **Datos para QA:** `NeckStartWorld`, `NeckEndWorld`, `NeckMaxGap`, `HeadBounds` y `VisualBounds`.
- **Prefab.** `Boss.prefab` solo recibe el campo `neckTile` en cada fase. El GUID no cambia.

**Pruebas:**
- La cabeza entera queda dentro de la cámara durante el rugido.
- El cuello es una sola tira: hueco máximo < 0,35, extremo unido a la cabeza (< 0,05) y base a la altura del suelo.
- La raíz del jefe no se mueve en ningún ataque.

## 2. Nueva presentación en 4 etapas (`BossAI.Intro`)

1. **Despertar.**
   - Se bloquea el combate.
   - La cámara recorre el escenario mientras una onda de brillo pasa por las escamas del terreno.
   - Hay un temblor suave y partículas de hielo y nieve.
2. **Aparición.**
   - La cámara sube hacia la cabeza.
   - El cuello se eleva poco a poco con un vaivén orgánico.
   - Los ojos se encienden y la mandíbula se abre hasta la pose amenazante (gruñido, mandíbula al 75 % e inclinación de -8°).
   - El cuerpo sigue anclado al escenario: no vuela ni persigue al jugador.
3. **Rugido silencioso.**
   - Mandíbula muy abierta, cabeza inclinada y sacudida del cuello.
   - Sacudida de cámara controlada (0,3) y partículas de hielo.
   - Después vuelve a la pose estable. No suena nada.
4. **Inicio de la batalla.**
   - La cámara pasa al encuadre de combate.
   - Aparece la barra de vida, que estuvo oculta toda la presentación.
   - Se devuelve el control, el estado pasa a `Attack` y empieza el primer ataque.

**Repetible:** una prueba reinicia la partida, vuelve a la fase 6 y comprueba que la presentación empieza de nuevo (control bloqueado, contador a 0). También comprueba que termina en silencio y devuelve el control con la barra visible.

## 3. Vida por golpe válido (`PlayerLifeSteal.cs`, nuevo)

**Cuánto cura:**
- 1 % de la vida máxima contra enemigos normales y 0,5 % contra Jörmungandr.
- Se configura en el Inspector del componente y en `RunConfig.asset` (`lifeOnHitEnemy`, `lifeOnHitBoss`).
- Usa `Health.Heal`, así que nunca supera la vida máxima, que ya incluye las mejoras.

**Cuándo cura:**
- Una sola vez por ataque. `MeleeWeapon` numera cada golpe (`SwingSerial`) y emite `HitConfirmed` solo cuando `TakeDamage` confirma daño real. Así no duplica con varias hitboxes, varios enemigos o las dos hitboxes del jefe.
- No cura con golpes a enemigos muertos, objetos decorativos o no enemigos, objetivos invulnerables, golpes bloqueados ni fallos.
- Funciona mientras el jefe está agotado y es compatible con los buffs.
- Indicador: unas pocas motas verdes en el pecho del jugador.

**Pruebas pasadas:**
- Un golpe a dos enemigos cura +1,00 una sola vez.
- Cada golpe nuevo vuelve a curar.
- Un fallo no cura.
- Un objeto decorativo no cura.
- Un enemigo muerto no cura.
- Con la vida llena se mantiene en el máximo.
- Golpe al jefe agotado: cura +0,50 una vez.

## 4. Agotamiento del jefe tras 5 ataques

**Estados y contador:**
- Nueva máquina de estados `BossCombatState`: `Intro`, `Attack`, `Recovery`, `Exhausted`, `Recovering`, `Transition` y `Defeated`.
- `AttacksSinceRest` solo sube cuando un ataque llega a terminar. Al 5.º entra en `Exhaustion()`.
- Configurable: `attacksBeforeExhaustion = 5` y `exhaustionDuration = 2.5 s`.

**Durante el agotamiento:**
- Se borran los peligros activos y no ataca.
- La cabeza baja al suelo con respiración agotada: mandíbula entreabierta, ojos atenuados al 12 %, contracciones de respiración, escamas relajadas y un tinte que pulsa en el cuerpo como señal de vulnerabilidad.
- La cámara se mantiene estable.
- Recibe daño normal y el robo de vida funciona.

**Al terminar:** pasa a `Recovering` y luego a `Recovery`. El contador vuelve a 0.

**Reinicios y prioridad:**
- El contador vuelve a 0 en cada transición de fase, que tiene prioridad y entra en el estado `Transition`.
- También vuelve a 0 al morir el jefe (`Defeated`) y al reiniciar.
- No se cuenta durante la presentación ni durante las transformaciones.
- Es independiente del aturdimiento del rugido sobre el jugador.

**Medido en Unity:**
- El agotamiento empieza justo después del 5.º ataque.
- Dura 2,505 s (tolerancia ±0,1).
- El jefe es vulnerable y no tiene ataque activo.
- Después reanuda sus ataques.

## 5. Debuffs reducidos alrededor de un 12,5 % (valores efectivos)

Cambiado en `UpgradeDefinition.cs`. Los buffs, nombres, interfaz BUFF/DEBUFF y límites no cambian.

| Carta | Debuff | Antes | Ahora |
|---|---|---|---|
| Furia del Berserker | Daño recibido | +12 % | **+10,5 %** |
| Resistencia de Midgard | Velocidad | -8 % | **-7 %** |
| Golpe Devastador | Cadencia | -15 % | **-13 %** |
| Paso del Guerrero | Vida máxima | -8 % | **-7 %** |
| Técnica del hacha | Daño | -8 % | **-7 %** |
| Piel de piedra | Velocidad | -10 % | **-8,75 %** |
| Pacto vampírico | Vida máxima | -10 % | **-8,75 %** |

Las pruebas comprueban el efecto real en el juego: Berserker recibe un 10,5 % más de daño, Midgard pierde un 7 % de velocidad, Piedra un 8,75 % y Vampiro queda en 91,25 de vida máxima.

## 6. Plataforma móvil en la arena final

**Prefab:** `Prefabs/Environment/SerpentMovingPlatform.prefab` (nuevo).
- Sprite: `SerpentScalePlatform`, escamas de la serpiente con hielo y nieve, el mismo estilo que el terreno.
- Plataforma de un solo sentido con cuerpo cinemático.

**Movimiento (`MovingPlatform.cs`, reescrito):**
- Configurable en el Inspector: `travel` (2,5, 0), `speed` 1,1 u/s y `endPause` 0,6 s.
- Va y vuelve en bucle con suavizado en los extremos.
- En la arena se usa un recorrido de 2,4 unidades a una altura de 2,3.

**Transporte del jugador:**
- Mueve al jugador cuando su collider de suelo es la superficie de la plataforma.
- Lo desplaza exactamente lo mismo que la plataforma en cada paso de física.
- No usa `transform.parent`. El jugador puede saltar y bajar con normalidad.

**Pruebas pasadas:**
- Recorrido de 2 a 3 unidades con pausas.
- Alcanzable con un salto normal (2,28 sobre el suelo).
- Su trayectoria no se solapa con otros colliders ni con la pared del jefe.
- El jugador se queda encima y viaja con ella (2,11 unidades).
- No mueve el cuerpo de la serpiente.

No se tocó el fondo panorámico, sus colores, montañas ni iluminación.

## 7. Lo que se conserva

- 3 fases del jefe.
- Transformación en orden: cuerpo, cuello, cabeza, rugido y aturdimiento.
- QTE del aturdimiento (4 ciclos; A+D y W+D).
- Especiales (Quebranto glacial y Eclipse carmesí).
- Sin ataques de proyectiles genéricos.
- La vida no se reinicia en las transiciones.

Todo esto sigue cubierto por las pruebas existentes, que pasan.

## 8. Nota sobre un valor de tu escena

La escena guardada en la laptop (00:57 del 18/10, antes de este encargo) tiene dos cambios hechos en el Inspector sobre el jugador:
- `PlayerGuard.damageMultiplier = 1`: con este valor **el escudo no reduce el daño**. El valor del prefab es 0,35, es decir, reduce un 65 %.
- `dodgeDuration = 0.5`.

No los revertí porque pueden ser intencionales. Las pruebas del escudo ahora leen el valor configurado en lugar de dar por hecho 0,35. Si fue un cambio accidental, basta con poner `Damage Multiplier` a 0,35 en el componente `PlayerGuard` de `Player_Calvo`.

## Archivos modificados

- **Código:**
  - `Scripts/Enemies/BossAI.cs`
  - `Scripts/Enemies/JormungandrBody.cs`
  - `Scripts/Combat/MeleeWeapon.cs`
  - `Scripts/Player/PlayerController.cs`
  - `Scripts/Player/PlayerLifeSteal.cs` (nuevo)
  - `Scripts/Core/RunConfig.cs`
  - `Scripts/Core/GameManager.cs`
  - `Scripts/Progression/UpgradeDefinition.cs`
  - `Scripts/World/MovingPlatform.cs`
  - `Scripts/World/ArenaPropFactory.cs`
  - `Scripts/World/StageBuilder.cs`
- **Arte:** `Art/JormungandrPhases/JP1_NeckTile.png`, `JP2_NeckTile.png` y `JP3_NeckTile.png` (nuevos).
- **Datos y prefabs:**
  - `Generated_Hacha_Final 2/Prefabs/Boss.prefab`
  - `Generated_Hacha_Final 2/RunConfig.asset`
  - `Resources/OMT_ArenaPropLibrary.asset`
  - `Prefabs/Environment/*` (regenerados, con el nuevo `SerpentMovingPlatform`)
- **Pruebas:** `Editor/DemoPlayValidation.cs` y `Editor/DemoV6VisualCheck.cs` (nuevas hojas `04b_exhaustion` y `04c_moving_platform`).
