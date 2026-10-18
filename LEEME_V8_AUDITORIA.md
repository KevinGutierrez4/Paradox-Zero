# One More Time · V8: plataforma destructible, Robo de Vida por mejora y auditoría

Proyecto auditado en la laptop (`C:\Users\joela\Downloads\OMT_V5_test`), con Unity abierto y controlado desde esta sesión.

**Resumen:**
- Se parte de la V7. No se reconstruyó nada.
- No se creó ninguna escena ni carpeta nueva. La escena no se modificó.
- Se conservan todos los `.meta` y GUID. El prefab de la antigua plataforma móvil se **renombró** (con su `.meta`) a `SerpentCrumblingPlatform.prefab`, así que conserva su GUID `61f9ef43…`.
- No se tocó la entrada de Jörmungandr, su rugido, sus tres fases ni el agotamiento.

## 0. Análisis inicial

| Elemento | Resultado |
|---|---|
| Unity | 6000.5.5f1, URP 2D 17.6.0, uGUI 2.5.0 |
| Entrada | `activeInputHandler: 2` (Ambos): el juego usa el Input Manager clásico; el paquete Input System 1.19.0 también está instalado |
| Escena principal | `Assets/OneMoreTime/Generated_Hacha_Final 2/Scenes/OneMoreTime_Demo.unity`, la única en Build Settings |
| Copias obsoletas | `Generated_Hacha_Final`, `Generated_Hacha_Final 1`, `Generated 3`, `Scenes/SampleScene`: no se usan y no se tocaron |
| Referencias | Se revisaron todos los GUID de la escena activa, los prefabs activos, `Prefabs/Environment` y los ScriptableObjects. Los únicos GUID que no están en `Assets` son de uGUI (Text, Image, Button, Slider, CanvasScaler, GraphicRaycaster, EventSystem, StandaloneInputModule), es decir, de paquetes. **No hay Missing Scripts.** |
| Eventos | Todas las suscripciones (`StateChanged`, `Died`, `HitReceived`, `HitConfirmed`, `Changed`) tienen su baja en `OnDestroy` o se re-suscriben con `-=` antes de `+=`. No hay fugas. |
| Rendimiento | No hay búsquedas `Find*` dentro de `Update`/`FixedUpdate`. Solo se hacen en `Start`/`Initialize`. No se encontraron corrutinas duplicadas. |
| Cómo estaban las dos mecánicas | La arena final tenía `SerpentMovingPlatform` (`MovingPlatform`, de ida y vuelta). El robo de vida estaba **activo desde el inicio** (1 % contra enemigos y 0,5 % contra el jefe, leído de `RunConfig.lifeOnHit*`), sin carta ni niveles. |

## 1. Errores confirmados y corregidos

| Problema | Evidencia / reproducción | Archivo | Severidad | Corrección aplicada | Prueba y resultado |
|---|---|---|---|---|---|
| El robo de vida se aplicaba desde el inicio de la partida | Golpear con 70/100 de vida subía a 71 sin haber elegido ninguna carta (prueba V7 «heals +1») | `PlayerLifeSteal.cs`, `RunConfig.cs`, `GameManager.cs` | Alta (diseño incorrecto) | Ya no hay valor base: el componente lee el porcentaje de las estadísticas del jugador (nivel 0 = desactivado). Se quitaron `lifeOnHitEnemy`/`lifeOnHitBoss` | PASS: «a new run starts without life steal», «without the card, hitting enemies/Jörmungandr does not recover health» |
| Plataforma móvil en la arena final (mecánica ya no deseada) | `StageBuilder.Layout(boss)` creaba `SerpentMovingPlatform` | `StageBuilder.cs`, `ArenaPropFactory.cs` | Alta (requisito) | Sustituida por una única `SerpentCrumblingPlatform` fija | PASS: «exactly ONE extra destructible platform and no moving platform» |
| 70 advertencias CS0618 de la API de Unity 6.5 | Consola: `FindFirstObjectByType` y `FindObjectsSortMode` están obsoletos | 8 scripts (4 de juego y 4 de editor) | Baja | `FindAnyObjectByType` y las sobrecargas de `FindObjectsByType` sin orden | Compilación: de 70 advertencias a 0 |
| `JormungandrBody.pose` (struct no serializable) marcado para serializar | Advertencia UAC1001 | `JormungandrBody.cs` | Baja | `[NonSerialized]`: es un estado que se recalcula en cada frame | Compilación: 0 advertencias |
| Campo `EnemyAI.preparing` asignado pero nunca leído | Advertencia CS0414 | `EnemyAI.cs` | Baja | Se expone como `Preparing` (diagnóstico y QA). Lo usa la prueba de spawn | Compilación: 0 advertencias |
| Comprobación intermitente «Enemy spawns on surface phase 2» | Falló 1 de 3 ejecuciones y no se reprodujo en las otras dos | `DemoPlayValidation.cs` (prueba) | Baja (prueba, no juego) | Causa probable: la prueba corre a `timeScale = 10` y un lobo azul puede estar ya en el aire en su salto de ataque (`leapLift 5.2`) cuando se le ve por primera vez. Ahora la prueba solo exige estar sobre la superficie a los enemigos que no están atacando ni en el aire. El código de spawn ya colocaba en `GroundHeight + 0,04` | Ver ejecución final abajo. La causa no se confirmó con datos: se añadió diagnóstico (nombre, x, y, suelo, velocidad vertical) por si vuelve a ocurrir |

## 2. Detectados y pendientes (no corregidos a propósito)

| Problema | Motivo |
|---|---|
| En la escena guardada, `Player_Calvo` tiene `PlayerGuard.damageMultiplier = 1` (el escudo no reduce el daño) y `dodgeDuration = 0.5` | Son cambios del Inspector anteriores a estas tareas y pueden ser intencionales. Revertirlos cambiaría el diseño. Si fue accidental, basta con poner 0,35 en el componente. |
| Carpetas Generated antiguas en el proyecto | No se usan. Borrarlas es destructivo y no hace falta para que funcione. |

## 3. Plataforma destructible única

**Componente nuevo `Scripts/World/CrumblingPlatform.cs`.** Es una máquina de estados con un solo temporizador:

| Estado | Qué pasa |
|---|---|
| **Idle** | Superficie sólida. Al aterrizar el jugador (su collider de suelo es esta plataforma) pasa a Cracking. |
| **Cracking** | Cuenta el tiempo de permanencia en el paso de física. Las grietas crecen en 3 etapas. En los **últimos 0,6 s**: temblor, parpadeo azulado y esquirlas de hielo cayendo. Si el jugador se baja, vuelve a Idle y el tiempo se reinicia. |
| **Breaking** | A los **2 s** el collider se desactiva en el acto (no queda suelo invisible) y la losa se parte en sus 5 fragmentos, que caen girando y se desvanecen (0,45 s). El jugador cae por gravedad. |
| **Destroyed** | Invisible y sin colisión. |
| **Respawning** | Los fragmentos vuelven a su sitio para que la losa esté entera **exactamente 3 s después de romperse**. Si el jugador ocupa su espacio, queda como un fantasma sin colisión hasta que el espacio esté libre: nunca lo empuja ni lo atrapa. |

**Configuración y prefab:**
- Configurable en el Inspector: `timeToBreak` 2, `warningTime` 0,6, `respawnTime` 3, duraciones de las animaciones, `startActive` y `animatedDestruction`.
- La raíz y el collider no se mueven nunca (no hay movimiento horizontal ni vertical).
- Prefab `Prefabs/Environment/SerpentCrumblingPlatform.prefab`, con el mismo GUID que tenía el prefab de la plataforma móvil.
- Generado por `Tools > One More Time > 9`.
- Plataforma de un solo sentido (sólida desde arriba, S+Espacio para bajar), en la capa de suelo.

**Arte:** sale del sprite existente `SerpentScalePlatform.png` (escamas de Jörmungandr con nieve y carámbanos):
- `SerpentScalePlatform_Crack1/2/3.png`: grietas superpuestas en 3 etapas, recortadas a la silueta, con brillo de hielo.
- `SerpentScaleShard0…4.png`: los 5 fragmentos reales de la losa, cada uno con su pivote en el centro.
- Todos están registrados en `OMT_ArenaPropLibrary`.

**Animación:** igual que el resto de trampas y plataformas del proyecto (`ArenaTrap`), las animaciones de grietas, advertencia, rotura y reaparición se hacen por código en el propio componente. No se creó un Animator Controller.

**En la arena** (`StageBuilder`): se mantienen las 3 plataformas fijas y las 2 coberturas de la V6. La móvil se sustituye por la destructible (`t = 0,86`, altura 2,3, alcanzable con un salto). Fondo, jefe y suelo no cambian.

## 4. Robo de Vida como mejora seleccionable

**Datos:**
- `StatBlock` tiene `lifeStealLevel` y `lifeSteal`; los dos empiezan en 0.
- `UpgradeDefinition` incluye la carta `robovida`, que es la 8.ª del catálogo y aparece entre las opciones normales.

| Nivel | Bonificación | Acumulado | Debuff (defensa) | Daño recibido |
|---|---|---|---|---|
| Sin adquirir | 0 % | 0 % | — | 100 % |
| 1 | +0,2 % | 0,2 % | -1 % | 101 % |
| 2 | +0,3 % | 0,5 % | -2 % | 102 % |
| 3 | +0,4 % | 0,9 % | -3 % | 103 % |
| 4 | +0,5 % | 1,4 % | -4 % | 104 % |
| 5 | +0,6 % | 2,0 % | -5 % | 105 % |

**Configuración:** en `RunConfig.asset` (ScriptableObject):
- `lifeStealBonusPerLevel` = [0,002; 0,003; 0,004; 0,005; 0,006]. El número de entradas es el nivel máximo.
- `lifeStealDefensePenaltyPerLevel` = 0,01.

Se aplican con `LifeStealProgression.Configure` al empezar cada partida. Como todas las demás cartas tienen debuff, esta también lo tiene: pequeño, visible y configurable. Las penalizaciones de las otras cartas no se tocaron.

**Tarjeta** (texto real comprobado en la prueba):
```
ROBO DE VIDA — NIVEL 2
Nivel actual: 1 → siguiente: 2 (máx. 5)
Cada golpe que hiere de verdad te devuelve parte de tu vida máxima.
[+] BUFF · +0,3 % adicional de recuperación por golpe
TOTAL DESPUÉS DE ELEGIR: 0,5 % por golpe (5 de vida con 1000 máx.)
[-] DEBUFF · -1 % de defensa: daño recibido 101% → 102%
```
En el nivel máximo la carta deja de ofrecerse. El HUD muestra «Robo X %» cuando está activo, y el aviso de mejora muestra el nivel alcanzado (con coma decimal). La prueba enlaza la tarjeta real de la interfaz en los niveles 1, 2 y 5 y comprueba que el título y el texto caben sin recortarse. Capturas: `Verification/upgrade-card-robovida-level1/2/5.png`.

**Aplicación:** está centralizada en `PlayerLifeSteal`; ningún enemigo cura por su cuenta.
- Solo se ejecuta cuando `MeleeWeapon.HitConfirmed` confirma daño real. `Health.TakeDamage` devuelve falso si el objetivo está muerto o es invulnerable.
- Solo cura contra objetivos `EnemyActor` del equipo enemigo.
- Una vez por ataque (`SwingSerial`): dos enemigos, varios colliders o las dos hurtboxes del jefe dan una sola curación.
- Se calcula sobre la vida máxima actual y nunca la supera.

## 5. Pruebas realizadas en Unity (Play Mode, laptop)

`Tools > One More Time > 5`: **OMT_PLAY_PASS, 721 comprobaciones, 0 fallos y 0 excepciones** en la ejecución final (`Verification/play-results.txt`). Las 53 comprobaciones nuevas (V8) pasan. El historial de ejecuciones está en la sección 7.

**Robo de Vida (38 comprobaciones V8, todas PASS; incluyen 3 de la tarjeta real en la interfaz):**
- La carta existe con 5 niveles.
- La partida empieza a 0 %.
- Sin la carta no cura ni contra enemigos ni contra Jörmungandr.
- Niveles 1 a 5:
  - El texto de cada tarjeta coincide con los valores efectivos.
  - Cada nivel acumula correctamente: 0,2 / 0,5 / 0,9 / 1,4 / 2,0 %.
  - Medido con 1000 de vida máxima: +2 / +5 / +9 / +14 / +20 por golpe, una sola vez aunque haya dos enemigos dentro del golpe.
- Al máximo no vuelve a ofrecerse en 300 sorteos; sin adquirir, sí aparece.
- Se calcula sobre la vida máxima actual: 2 % de 1250 = 25.
- No cura con objetivo invulnerable, fallo, objeto decorativo, pared ni enemigo muerto.
- Nunca supera la vida máxima.
- Con nivel 2 contra el jefe agotado: +0,50 una vez.
- Al reiniciar las estadísticas desaparecen el robo y su debuff.

**Plataforma destructible (15 comprobaciones V8, todas PASS):**
- Hay una sola plataforma adicional y ninguna móvil.
- Configuración 2 / 0,6 / 3.
- Altura 2,30, alcanzable con un salto. No se solapa con el escenario ni con la pared del jefe.
- Al aterrizar empieza a contar. Al bajarse se reinicia (Idle, 0,00 s).
- **Se rompe a los 2,00 s.** La advertencia dura los **últimos 0,60 s**.
- El collider se apaga al romperse y el jugador cae.
- Queda invisible y sin colisión.
- **Reaparece a los 3,00 s.**
- Con el jugador en su espacio espera, y aparece al quedar libre.
- Ciclo repetido: 2 roturas y 2 reapariciones.
- No se mueve; el jefe y el suelo principal siguen intactos.

**Regresión:**
- Todas las comprobaciones anteriores siguen pasando: animaciones, combate, knockback, escudo, esquiva, IA, arqueros, trampas, portales, cartas y debuffs, intro silenciosa, 3 fases con transformación, QTE del rugido, agotamiento tras 5 ataques, muerte/reinicio, victoria y repetición de la presentación.
- La prueba de «plataforma alta contra la ventisca» excluye la destructible, porque se rompería a los 2 s; usa una plataforma fija.

**Capturas** `Tools > One More Time > 10`: terminan en Victoria con 0 errores. Nueva hoja `Verification/V6Play/04c_crumbling_platform.png`, con la secuencia Cracking → WARNING → Breaking → Destroyed → Respawning (2,8 s) → Idle (3,1 s).

**No ejecutable en este entorno:** una prueba manual con mando o teclado jugada por una persona. Toda la verificación se hizo con el ejecutor automático en Play Mode y con las capturas.

## 6. Archivos

- **Nuevos:**
  - `Scripts/World/CrumblingPlatform.cs` (+meta)
  - `Art/Environment/SerpentScalePlatform_Crack1-3.png` y `SerpentScaleShard0-4.png` (+meta)
- **Renombrado** (mismo GUID): `Prefabs/Environment/SerpentMovingPlatform.prefab` → `SerpentCrumblingPlatform.prefab`, regenerado.
- **Modificados:**
  - `UpgradeDefinition.cs`: estadística LifeSteal, carta por niveles, tabla `LifeStealProgression`, textos.
  - `PlayerLifeSteal.cs`: solo con la carta; porcentaje desde las estadísticas.
  - `RunConfig.cs` y `RunConfig.asset`: tabla de niveles y debuff; se eliminan `lifeOnHit*`.
  - `GameManager.cs`: configura la tabla.
  - `UIManager.cs`: HUD y aviso.
  - `UpgradeCardUI.cs`: título con nivel.
  - `ArenaPropFactory.cs`: nuevo tipo; se elimina el tipo móvil de la arena.
  - `StageBuilder.cs`: una sola plataforma destructible.
  - `OMT_ArenaPropLibrary.asset`: 8 sprites nuevos y el prefab renombrado.
  - `ArenaTrap.cs`, `MovingPlatform.cs`, `BossRoarStunController.cs`: API no obsoleta.
  - `EnemyAI.cs`: `Preparing`.
  - `JormungandrBody.cs`: `[NonSerialized]`.
  - `Editor/DemoPlayValidation.cs`, `DemoV6VisualCheck.cs`, `DemoValidation.cs`, `DemoV5VisualCheck.cs`: pruebas nuevas y API.

## 7. Historial de ejecuciones de la prueba completa (todas en la laptop)

| # | Resultado | Qué pasó |
|---|---|---|
| 1 | FAIL en «Enemy spawns on surface phase 2» | Prueba intermitente con enemigos en el aire (ver tabla de errores); todas las V8 de Robo de Vida ya pasaban |
| 2 | FAIL en «stepping off … resets» | La prueba solo esperaba 2 pasos de física tras mover al jugador. El controlador y la plataforma se ejecutan en el mismo paso, así que la plataforma podía ver todavía el suelo anterior. La prueba ahora espera 0,25 s; el componente no cambió |
| 3 | **PASS, 719 comprobaciones** | — |
| 4 | **PASS, 718 comprobaciones** (tras ajustar la prueba de spawn) | El número varía en ±1 porque los enemigos que aparecen dependen de la semilla aleatoria |
| 5 | **PASS, 721 comprobaciones** (final) | Se añadieron las 3 comprobaciones de la tarjeta real y la coma decimal en el HUD |

Compilación final: **0 errores y 0 advertencias** en la consola de Unity (antes había 70 advertencias).
