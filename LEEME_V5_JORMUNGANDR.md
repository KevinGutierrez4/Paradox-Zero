# One More Time: V5 (animación, IA, Jörmungandr y portales)

Escena que se abre y se juega: `Assets/OneMoreTime/Generated_Hacha_Final 2/Scenes/OneMoreTime_Demo.unity`. Es la escena registrada en Build Settings.

En esta versión no se ha regenerado nada. No se ha creado ninguna carpeta `Generated*` nueva. Las carpetas antiguas `Generated`, `Generated 1-3`, `Generated_Hacha_Final` y `Generated_Hacha_Final 1` no se han tocado.

> **Validación real en Unity 6000.5.5f1:** compila sin errores y el recorrido automático pasa **523/523 comprobaciones** en Play Mode sin excepciones (`Verification/play-results.txt`). Además, el recorrido se filmó fotograma a fotograma (`Tools > One More Time > 8. Capturas V5`) para revisar animaciones, portal y jefe (ver sección 7).

---

## 1. Diagnóstico inicial

| # | Gravedad | Problema confirmado | Causa |
|---|---|---|---|
| 1 | Alta | Arqueros y magos mostraban el disparo sin generar proyectil desde la fase 3 | `EnemyAI` disparaba con un reloj propio, pero `ProjectileWeapon.TryFire` aplicaba otro cooldown (`attackInterval` = 1,2 s) mayor que el intervalo escalado por dificultad (≈1,0–1,1 s). Ese cooldown rechazaba el disparo en silencio. |
| 2 | Media | Dos sistemas escribían las mismas articulaciones | En enemigos y jefe, el Animator estaba activo con clips que fijan la posición y la rotación de todas las articulaciones. `ArticulatedRig` las reescribía en LateUpdate, con relojes distintos (0,8 s fijo frente al reloj real). |
| 3 | Media | Los pies se deslizaban al caminar (arquero, mago y lobos) | La cadencia del ciclo era `velocidad × 1,7`. Con la zancada de 0,24, el pie apoyado retrocedía a ~¼ de la velocidad del cuerpo. |
| 4 | Media | La muerte de los lobos era incorrecta | El rig giraba −85° sobre los pies y el lobo quedaba "de pie" sobre el hocico (se ve en `Verification/animals-poses.png`). |
| 5 | Media | La muerte se cortaba | `deathDuration` era 0,5 s y la caída dura 0,5–0,6 s: el enemigo se destruía al tocar el suelo. |
| 6 | Media | Los lobos eran idénticos | Los dos prefabs tenían exactamente los mismos valores. El ataque solo giraba el cuello y la mandíbula, sin anticipación ni embestida. |
| 7 | Media | El ataque a distancia no tenía etapas | Era una curva seno de 0,8 s sin levantar, tensar, apuntar ni soltar. El muzzle del arquero estaba 0,18 u por encima de la empuñadura. |
| 8 | Alta (diseño) | El jefe no tenía presentación | Atacaba 1,2 s después de aparecer, tenía 2 patrones y una sola furia (50 %), y su escala (~3 u) era la de un enemigo normal. |
| 9 | Media (diseño) | No había portales | La fase terminaba sola al llegar a una barra (`PhaseExit`). |

**Riesgos que no se pudieron confirmar estáticamente**
- La última ejecución automática (`Verification/play-results.txt`, 16:16) falló en *"Phase 1 clears into paused upgrade choice"*. Parece un timeout del test a timeScale 10. No hay evidencia de un fallo del juego.
- No había ningún AudioSource ni AudioClip en el proyecto. Sigue sin haberlos.
- **Fenrir y Loki:** no existe ningún sprite, prefab ni script con esos nombres. No están implementados.

## 2. Archivos modificados y creados

### Scripts modificados
| Archivo | Cambio |
|---|---|
| `Scripts/Combat/AnimationDriver.cs` | Desactiva el Animator cuando hay `ArticulatedRig`, que pasa a ser el único que escribe las articulaciones. Añade `FadeOut` (desvanecido tras la muerte, en tiempo no escalado), `SetFade` (portal) y `CacheRenderers`. Las piezas `RT_*` conservan el alfa que les da el rig. |
| `Scripts/Combat/ArticulatedRig.cs` | Lee el reloj compartido `IAttackClock`. Ciclo de pasos sin deslizamiento (ω = π·v / 2·zancada, con la fase de apoyo lineal), invertido al caminar hacia atrás. Lobos con IK de 4 patas: agacharse, embestir o saltar, mordida, reacción al daño y muerte tumbada. Arquero (levantar, tensar, apuntar, soltar, recuperar, flecha en la cuerda). Mago (levantar, canalizar con temblor y brillo, lanzar, recuperar). |
| `Scripts/Combat/MeleeWeapon.cs` | `BeginExternalSwing` y `StrikeAt`: golpe gobernado por la IA en un punto móvil (la mandíbula), una vez por objetivo y bloqueado por paredes. El hacha del jugador no cambia. |
| `Scripts/Combat/ProjectileWeapon.cs` | `FireNow`: dispara en el frame de suelta, sin un cooldown que pueda tragarse el disparo. |
| `Scripts/Combat/ProjectilePool.cs` | Getters `ArrowSprite`, `MagicSprite` y `ActiveCount`. |
| `Scripts/Combat/Health.cs` | `Invulnerable` (presentación del jefe). Se reinicia en `ResetHealth`. |
| `Scripts/Enemies/EnemyActor.cs` | El cadáver permanece al menos 1,25 s y se desvanece 0,4 s. Ganchos `FadesCorpse` y `DestroysCorpse`. |
| `Scripts/Enemies/EnemyAI.cs` | Reescrito como máquina de estados por arquetipo (ver sección 4). Implementa `IAttackClock`. |
| `Scripts/Enemies/BossAI.cs` | Reescrito: presentación, rugido silencioso, 5 ataques y 3 etapas (ver sección 5). |
| `Scripts/Core/GameManager.cs` | Integra los portales. La fase normal ya no termina sola: se abre el portal y entrar en él llama a la misma `FinishPhase()`, que lleva a las mejoras. Añade bloqueo de control, `Winter`, encuadre de cámara y limpieza de efectos. |
| `Scripts/Player/PlayerController.cs` | `SetControlLocked`, `ControlLocked`, `InteractPressed` (W / ↑ / E) y `Grounded`. |
| `Scripts/World/CameraFollow2D.cs` | Enfoque cinemático, zoom suave, sacudida (en tiempo de juego, se detiene con la pausa) y límites que siguen respetando el escenario. |
| `Scripts/World/BackgroundDirector.cs` | Tinte suave por invierno (configurable; blanco = arte original). |
| `Scripts/UI/UIManager.cs` | Textos de invierno, del portal y del jefe. La barra del jefe aparece al terminar la presentación. Añade la tecla del portal a los controles. |
| `Editor/DemoPlayValidation.cs` | El test automático usa el portal y la presentación del jefe. Añade comprobaciones nuevas: portal cerrado antes de los objetivos, sin transición doble, vida y estadísticas intactas, llegada sobre el terreno, jefe invulnerable y sin ataques durante la intro, ninguna AudioSource, sin proyectiles ni peligros al terminar. |
| `Editor/DemoSceneBuilder.cs` | Si alguien regenera, el prefab del jefe recibe sus sprites nuevos. |

### Scripts nuevos (con su `.meta` y un GUID nuevo)
`Scripts/Combat/AttackClock.cs`, `Scripts/Combat/ArticulatedRig.Serpent.cs`, `Scripts/Enemies/BossHazard.cs`, `Scripts/World/PixelFx.cs`, `Scripts/World/PixelParticle.cs`, `Scripts/World/DimensionalPortal2D.cs`, `Scripts/World/PortalVisualController.cs`, `Scripts/World/PortalTransitionController.cs`.

### Assets
- `Generated_Hacha_Final 2/Prefabs/Boss.prefab`: nuevos campos de `BossAI` (presencia, tiempos, etapas, ataques y 6 referencias a sprites). No cambian el GUID ni la jerarquía.
- `Generated_Hacha_Final 2/Prefabs/Archer.prefab`: `Muzzle` pasa de (0.12, 0.18) a (0.10, 0) en el espacio del arco, a la altura de la empuñadura y la cuerda.
- `Art/JormungandrV5/`: 6 PNG nuevos recortados de `Art/Originales/06_serpiente_azul` (cabeza de ataque, cabeza de rugido, nube de aliento, segmento de cuerpo, aleta y colmillos de hielo), sin fragmentos de celdas vecinas. Los originales no se modifican. Las cabezas tienen el pivote en el ojo para encajar con la cabeza neutra.
- `Verification/V5/*.png`: vistas previas de poses (ver sección 7).

La escena, el RunConfig y los demás prefabs no se han modificado.

## 3. Animaciones

- **Un solo sistema por articulación:** `ArticulatedRig` (LateUpdate). Los Animator Controllers y clips siguen en el proyecto como vistas previas. Sus estados no tienen contradicciones (AnyState→Die, Attack/Special por trigger y salida por exitTime), pero ya no se ejecutan en juego.
- **Idle:** respiración. **Caminar:** pies plantados sin deslizar y elevados al volver; los lobos trotan en pares diagonales.
- **Ataque:** Anticipación → Activo → Recuperación, con el reloj de la IA (`IAttackClock`).
- **Daño:** retroceso de torso y cabeza (humanos), sacudida y retroceso de cabeza (lobos), destello.
- **Muerte:** humanos caen de frente. Los lobos se desploman tumbados con las patas plegadas. El cuerpo permanece y se desvanece. La muerte cancela el ataque, la flecha y el brillo.
- **Escalas:** nunca se escalan piezas de personajes. La IK solo rota. El jefe se escala de forma uniforme en la raíz (×1,8, configurable) y conserva sus proporciones.

## 4. Enemigos

Estados: **Idle → Patrol → Chase → Anticipation → Attack → Recovery → Chase/Idle**, con **Hurt** (interrumpe todo salvo la ventana activa) y **Dead**. La detección tiene histéresis (adquiere a 16 u y suelta a 20 u). Al patrullar hay pausas en los extremos del territorio, y el enemigo solo gira si hay más de 0,3 u de diferencia horizontal.

| Enemigo | Comportamiento | Anticipación / Activo / Recuperación |
|---|---|---|
| **Lobo blanco** | Persigue y se acerca despacio al final. Se agacha con la mandíbula entreabierta, embiste (6,5 u/s en la primera mitad, se detiene al contacto) y muerde: el daño se aplica solo cuando la mandíbula se cierra (40–85 % del activo), en la caja de la cabeza. | 0,42 / 0,22 / 0,48 s |
| **Lobo azul** | ×1,3 de velocidad. Retrocede brevemente, salta (7,5 × 5,2), muerde con una ventana corta (55–75 %) y aterriza con una recuperación larga que permite contraatacar. | 0,30 / 0,30 / 0,62 s |
| **Arquero** | Mantiene una distancia de tiro (retrocede si estás a menos de 2,6 u). Levanta el arco, tensa con la flecha en la cuerda, apunta (deja de seguirte en el último 20 %) y suelta: la flecha sale del arco en el frame de suelta. Comprueba línea de tiro y colisiones. | 0,90 / 0,10 / 0,45 s |
| **Mago** | Levanta el bastón, canaliza con chispas que convergen y un brillo creciente en la punta, lanza desde la cabeza del bastón y se recupera. | 1,00 / 0,16 / 0,60 s |

Todos respetan los límites de la fase y el borde del suelo. La anticipación casi no se acelera con la dificultad (sigue siendo legible); lo que se acorta es el intervalo entre ataques.

## 5. Jörmungandr

**Presentación (≈6,8 s; tiempos en el inspector de `BossAI`), una vez por encuentro:**
- **A. El despertar (1,6 s):** la cámara deja de seguir al jugador, vibración creciente, hielo cayendo, vapor en el suelo, el cuerpo ondula y la cabeza asoma.
- **B. Aparición (2,0 s):** la cámara sube hacia la cabeza, el cuello se eleva, los ojos se encienden y la mandíbula se abre mostrando los colmillos (cabeza de ataque).
- **C. Rugido silencioso (2,3 s):** cabeza hacia atrás, mandíbula al máximo (cabeza de rugido), cuello tenso, vibración de la cabeza, ondulación, sacudida de cámara en el clímax, hielo, niebla y destello. Después la mandíbula se cierra poco a poco. **No hay ningún sonido:** no se crea ni existe ninguna AudioSource ni AudioClip (el test automático lo comprueba).
- **D. Comienza la batalla (0,9 s):** encuadre de combate (ortho 5,8), aparece la barra de vida, el jefe deja de ser invulnerable, el jugador recupera el control y empieza el primer patrón.

Durante la presentación el jefe es invulnerable, no ataca y el jugador no tiene control. No se usa `timeScale = 0`. Reiniciar crea un jefe nuevo y restablece cámara, control y sombreado.

**Ataques:**
1. **Mordida colosal:** retrocede la cabeza con la boca entreabierta y marca en rojo el punto de impacto. La cabeza se lanza y la hitbox sigue a la cabeza real; el daño solo cuenta mientras la cabeza llega y en un instante de mantenimiento. Se esquiva saliendo de la marca. Solo la usa si estás a su alcance.
2. **Aliento venenoso:** preparación con partículas verdes en la boca. El cono sale de la boca con un alcance configurable (6,5 u) y una duración controlada (1,4 s). La franja verde del suelo marca dónde el cono pasa a altura de un jugador de pie.
3. **Golpe de cuerpo:** el cuerpo ondula y se marca la zona de impacto y el recorrido de la onda. Impacto, sacudida y una onda que viaja sobre `SerpentSurface` (se salta). En la etapa 3 hay una segunda onda.
4. **Colmillos de hielo:** marcas en el suelo durante 0,9 s y después los colmillos brotan desde el suelo, duran 1,2 s y se retraen. Siempre queda un hueco libre de 3 u a pocos pasos del jugador. En la etapa 3 hay dos oleadas.
5. **Ráfaga:** preparación propia (brillo naranja en la boca, sin retroceso). Tres patrones: abanico dirigido, filas alternas con huecos que se pueden atravesar y barrido de arriba abajo.

**Etapas** (umbrales configurables): >70 % mordida y ráfaga, ritmo lento · 70–35 % se enfurece (rugido corto, ×1,2 de ritmo, aliento, golpe y combos golpe+ráfaga) · <35 % furia del Ragnarök (ojos al máximo, escena más oscura, colmillos de hielo, combos colmillos+ráfaga y mordida+golpe, pausas dramáticas antes de los ataques peligrosos, ×1,38). Las transiciones ocurren entre ataques, nunca a mitad de uno.

**Al morir:** se detienen todas sus corrutinas, se eliminan los peligros, la escena termina limpia y la serpiente se hunde con la cabeza caída. Los proyectiles se limpian en `FinishPhase` y se alcanza la victoria.

## 6. Portales

`DimensionalPortal2D` (estados **Inactive → Opening → Active → Entering → Transition**, más Closing), `PortalVisualController` (anillo de piedra ovalado, 8 runas del futhark antiguo, núcleo en espiral animado y motas discretas) y `PortalTransitionController` (flujo).

- Colores por invierno: escarcha cian (fases 1–2), violeta aurora (3–4) y brasa roja (5–jefe).
- **Funcionamiento:**
  1. El portal se abre al final del tramo solo cuando se cumplen los objetivos (todos los enemigos del presupuesto derrotados).
  2. Te acercas y pulsas W / ↑ / E.
  3. El jugador es atraído al centro y se desvanece en 0,8 s.
  4. `GameManager.CompletePhaseThroughPortal()` vuelve a validar los objetivos y abre la selección de mejoras existente.
  5. Tras elegir, la siguiente fase coloca al jugador sobre el terreno, el portal de llegada se abre y se cierra, el de salida queda reiniciado y la cámara y los límites se actualizan.
- No toca vida, estadísticas ni el contador de fases.
- Bloquea entradas dobles (`Busy`).
- Se reinicia al volver al menú o al empezar una partida.
- El antiguo `PhaseExit` se conserva en la escena, oculto.

**Prefabs de portal:** no se creó un prefab. El portal se construye por código al iniciar la escena, con texturas pixel art generadas en runtime, para no editar la escena YAML a mano sin Unity. Si lo quieres como prefab, basta con arrastrar `ExitPortal` desde la jerarquía en Play Mode.

## 7. Resultados de pruebas

**Ejecutado en Unity 6000.5.5f1 (copia de prueba en tu equipo):**
- **Compilación:** 0 errores. El primer intento detectó un error (CS0102: nombre `Fade` duplicado en `AnimationDriver`), ya corregido.
- **Recorrido automático (`5. Probar combate…`):** `OMT_PLAY_PASS`, 523 comprobaciones, sin excepciones. Incluye las comprobaciones originales más las nuevas:
  - portal cerrado antes de los objetivos, una sola entrada, vida y estadísticas intactas, llegada sobre el terreno en las fases 2–6;
  - jefe invulnerable y sin ataques durante la presentación, control bloqueado y devuelto al terminar, ninguna AudioSource (rugido silencioso);
  - ráfagas tras el aviso, paso a la etapa 2, victoria, y ningún proyectil ni peligro activo al final.
- **Filmación (`8. Capturas V5`):**
  - Lobo blanco, lobo azul, arquero y mago completan Persecución → Anticipación → Ataque → Recuperación.
  - Las muertes caen y se desvanecen.
  - El portal se abre, se atraviesa y lleva a las mejoras.
  - La presentación completa del jefe y sus etapas 1, 2 y 3 funcionan con ráfaga, golpe con onda, aliento y colmillos de hielo.
  - La derrota termina en victoria con 0 errores.
  - Hojas de contacto en `Verification/V5Play/`.
- **Mordida colosal:** en la filmación el jugador estaba fuera de su alcance, así que no aparece. Se vio en la vista previa de poses, pero no se ha probado en juego.

**Ajustes hechos tras ver las capturas:**
- Aviso de ataque más sutil (el tinte naranja era excesivo).
- Los lobos retroceden un paso tras morder.
- La cámara ya no muestra franjas vacías arriba ni abajo al alejarse en la arena del jefe: el zoom se limita a la altura del dibujo del escenario.

**Pendiente (solo lo puede valorar una persona jugando):** sensación con teclado real y balance de dificultad de las etapas del jefe. El test usa invulnerabilidad y elimina enemigos por código.

## 8. Cómo abrir y ejecutar

1. Abre la carpeta del proyecto con Unity **6000.5.5f1**.
2. Espera a que compile. Si aparece un error, la consola indica el archivo y la línea.
3. Abre `Assets/OneMoreTime/Generated_Hacha_Final 2/Scenes/OneMoreTime_Demo.unity`, pulsa Play y luego JUGAR.
4. Controles nuevos: **W / ↑ / E** para atravesar el portal.
5. Prueba automática: `Tools > One More Time > 5. Probar combate y recorrido automático`. Resultados en `Verification/play-results.txt`.
6. Ajustes: inspector de `Boss.prefab` (presencia, tiempos de la intro, umbrales y ataques), de `Archer.prefab`/`Mage.prefab`/lobos (`EnemyAI`: el arquetipo se deduce solo; se puede forzar) y de `BackgroundDirector` (tintes por invierno).
