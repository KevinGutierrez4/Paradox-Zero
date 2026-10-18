# One More Time · V6: auditoría y mejora integral

Unity 6000.5.5f1. Escena activa: `Assets/OneMoreTime/Generated_Hacha_Final 2/Scenes/OneMoreTime_Demo.unity`.

No se reconstruyó nada desde cero. Se conservan todos los sprites, `.meta` y GUID existentes. No se editaron las carpetas Generated antiguas, salvo el `Boss.prefab` activo (solo cambian sus campos) y `ProjectSettings/TagManager.asset` (nueva capa `OMT_Prop`). Tampoco se ejecutaron los generadores antiguos.

**Resultado en Unity (laptop):** `Tools > One More Time > 5` → **OMT_PLAY_PASS, 650 comprobaciones, 0 errores** (`Verification/play-results.txt`). La herramienta de capturas `10` terminó con 0 errores y en Victoria, sin proyectiles ni peligros activos al final (`Verification/V6Play/*.png` y `log.txt`).

## 1. Diagnóstico inicial

| # | Problema encontrado | Causa |
|---|---|---|
| P1 | El jugador se paraba encima de los lobos y era empujado o arrastrado | `CollisionRules2D` activaba la física entre jugador y enemigo |
| P2 | El jugador no tenía retroceso | Solo los enemigos reaccionaban a `DamageInfo.Knockback`; las flechas y el jefe no enviaban ninguna dirección |
| P3 | El jefe era una criatura con torso, espiral y cola que se movía; no usaba los atlas por fase | Rig V5 genérico |
| P4 | El jefe disparaba balas (*Burst*) y lanzaba una onda‑proyectil (*Slam*) | Solo había 2 cambios de etapa, sin transformación, aturdimiento ni QTE |
| P5 | Solo 2 de 6 cartas tenían coste | — |
| P6 | No había tutorial ni esquiva | — |
| P7 | No había plataformas, coberturas ni trampas | Los atlas nuevos no estaban importados |
| P8 | Los arqueros disparaban siempre desde el suelo | — |

## 2. Cambios por prioridad

### P1: colisiones
- `CollisionRules2D` ignora la física entre `OMT_Player` y `OMT_Enemy`. Los enemigos siguen chocando con el terreno.
- El daño no depende de esa física: usa `OverlapBox`, `StrikeAt`, `CircleCast` y bounds.
- El jefe actúa como pared explícita mediante `PlayerController.SetExtraMaxX`.
- Nueva capa `OMT_Prop` para coberturas. Es sólida para el jugador y los proyectiles; los enemigos la atraviesan, así no se atascan.

### P2: retroceso en ambos sentidos
**Jugador** (`PlayerController`). Al recibir daño:
- recibe un impulso sobre todo horizontal, alejándose de la fuente;
- pierde el control 0,2 s y lo recupera de forma progresiva;
- todo es configurable: escala, mínimo y máximo horizontal, elevación máxima, y la reducción con guardia (×0,45);
- los muros lo detienen (cuerpo dinámico contra el suelo);
- la invulnerabilidad breve la aporta `Health` y evita los golpes múltiples;
- aparecen partículas de impacto.

**Enemigos:** conservan `knockbackResistance` (0–1). El jefe tiene resistencia 1: es cinemático y nunca se desplaza.

**Flechas:** empujan en la dirección del vuelo. El jefe envía dirección en todos sus golpes.

### P3: Jörmungandr reconstruido con los 3 atlas (`JormungandrBody.cs`)
- Hay 48 recortes en `Art/JormungandrPhases/` (PNG y `.meta` con pivotes medidos sobre el ojo y la articulación del cuello, filtro Point).
- **Cabeza:**
  - cráneo con **mandíbula independiente**, que rota sobre su bisagra y deja ver un interior de boca;
  - tres poses dibujadas que se mezclan: abierta del todo, rugido y preparando mordida;
  - el ojo brilla.
- **Cuello:** cadena de segmentos cilíndricos sobre una curva Bézier. La cantidad de anillos se adapta a la longitud, así nunca quedan huecos. Las aletas dorsales tapan las uniones. La base queda oculta tras un montículo de nieve.
- **El escenario es su cuerpo:** el jefe no camina, no vuela, no persigue y no flota (la raíz es cinemática y fija; hay una prueba que lo comprueba). Lo único que se mueve es la cabeza, la mandíbula, el cuello y los lomos de escamas incrustados en el suelo. Esos lomos se mueven en las olas y contracciones, y en el resto del tiempo solo respiran levemente.
- **Morfología por regiones:** lomos → cuello (desde la base) → cabeza **al final**.

### P4: tres fases sin ningún proyectil del jefe (`BossAI.cs`, `BossHazard.cs`)
Configurable en el prefab:
- umbrales `phase2Threshold` 0,70 y `phase3Threshold` 0,35;
- duraciones `bodyMorphDuration`, `neckMorphDuration` y `headMorphDuration`;
- `stunWindow` y enfriamientos;
- duración del aliento, del viento, de las garras y de las nubes.

Todo ataque tiene Anticipación, Activo y Recuperación (expuestos en `IAttackClock`), aviso en el suelo, enfriamiento y una regla de selección: fase, alcance, sin repetir el anterior y enfriamiento especial para J.

**Tabla por ataque** ("probado" = pasa la prueba automática de Unity: recorre las 3 etapas, crea un aviso o peligro ligado al ataque y no genera proyectiles):

| Fase | Ataque | Anticipación → Activo → Recuperación | Contrajuego | Probado en Unity |
|---|---|---|---|---|
| 1 | A · Mordida de Midgard | Cabeza atrás y marca roja → embestida con mordida al llegar → cabeza apoyada en el suelo | Salir de la marca; golpear la cabeza en la recuperación | Sí |
| 1 | B · Ventisca rasante | Inhala y franja escarchada → viento continuo bajo (1,8 u) → vuelve | Cobertura de piedra o plataforma alta | Sí (daña al descubierto; no daña tras cobertura ni en plataforma) |
| 1 | C · Ola de escamas | Contrae y tiembla → un lomo de su espalda recorre el suelo → vuelve | Saltar la cresta | Sí |
| 2 | D · Erupción de colmillos helados | Llamada → 4 sectores escalonados con aviso de 0,8 s → vuelve | Pasarse al sector que acaba de estallar | Sí |
| 2 | E · Respiración de veneno ancestral | Se alza → cono continuo verde → nube persistente 4 s | Cobertura o plataforma alta; salir de la nube | Sí |
| 2 | F · Contracción de dos tiempos | Tensión → franjas impares y después pares (2 pulsos) | Quedarse en la franja segura de cada pulso | Sí |
| 3 | G · Espinas carmesí en cadena | Gruñido → cadena de espinas hacia el jugador (aviso de 0,35 s por columna) | Saltar o esquivar a un lado | Sí |
| 3 | H · Aliento del Ragnarök | Se alza → cono rojo continuo | Pegarse a una cobertura | Sí (la cobertura lo bloquea) |
| 3 | I · Fractura del mundo | 3 fisuras brillantes → estallido y ondas a ambos lados | Saltar las ondas | Sí |
| 3 | J · Juicio de la serpiente | Mordida rápida → cadena carmesí → aliento corto (enfriamiento 14 s) | Combinación de los anteriores | Sí |
| T1→2 | Quebranto glacial | 3 sectores progresivos y zona segura iluminada | Ir a la zona segura | Sí (una sola vez) |
| T2→3 | Eclipse carmesí | Tinte rojo, ola roja en las escamas, franjas de espinas, cuello contraído | Franjas alternas | Sí (una sola vez, tras liberar al jugador) |

**Transformación al cruzar un umbral.** Los estados se registran en `TransitionTrace` y la prueba verificó este orden exacto:

| Paso | Estado | Qué ocurre |
|---|---|---|
| 1 | Cancel | Cancela el ataque, borra peligros, jefe invulnerable |
| 2 | MorphBody | Lomos del cuerpo, uno a uno |
| 3 | MorphNeck | Cuello desde la base |
| 4 | MorphHead | Cabeza, al final |
| 5 | Roar | Rugido **silencioso**: no existe `AudioSource` ni `AudioClip` |
| 6 | Stun | QTE |
| 7 | Recover | Recuperación |
| 8 | Warning | Aviso del especial |
| 9 | Special | Especial una sola vez y ventana de castigo con la cabeza baja |

Otros detalles de las transiciones:
- Si un golpe cruza los dos umbrales, las transiciones **se encolan** (probado).
- Una sola barra de vida, que nunca se reinicia.
- Dentro de cada fase, el daño cambia gradualmente los lomos del cuerpo (fases 1–2) y aumenta el tinte rojo (fase 3).

**QTE de aturdimiento** (`BossRoarStunController.cs`):
- Dos teclas de {A, W, D} por evento: A+D en la fase 2 y W+D en la fase 3.
- Cuatro ciclos completos (8 pulsaciones), editable.
- Teclas grandes y contador `0/4`. Solo cuenta la tecla pulsada (key-down): las teclas incorrectas o fuera de orden se ignoran.
- Bloquea el movimiento, el salto y el ataque.
- Tiempo límite de 6 s: libera con una penalización leve que nunca mata.
- Pausa congela el temporizador; muerte, victoria o menú lo cierran.
- Estados: Idle, Stunned, Escaping, Recovered, FailedTimeout.
- El jefe no ataca mientras dura el aturdimiento.

**Cámara de combate:** encuadra a la vez al héroe y la cabeza del jefe, con zoom limitado a 7,4.

### P5: mejoras con BUFF y DEBUFF reales
Todas las cartas muestran nombre, descripción, `[+] BUFF` en verde y `[-] DEBUFF` en rojo, con los valores antes → después.

| Carta | BUFF | DEBUFF |
|---|---|---|
| Furia del Berserker | +25 % daño | +12 % daño recibido |
| Resistencia de Midgard | +25 % vida máxima | −8 % velocidad |
| Golpe Devastador | +40 % daño | −15 % ataques/s |
| Paso del Guerrero | +15 % velocidad | −8 % vida máxima |
| Técnica del hacha | +18 % ataques/s | −8 % daño |
| Piel de piedra | −15 % daño recibido | −10 % velocidad |
| Pacto vampírico | +2 vida por baja | −10 % vida máxima |

`UpgradeDefinition.Playable` impide cualquier combinación injugable. La prueba verifica que el BUFF y el DEBUFF se aplican de verdad.

### P6: tutorial y esquiva
- **Esquiva:** **L o Shift izquierdo**, un dash de 0,2 s con invulnerabilidad de 0,24 s y enfriamiento de 0,65 s. Cancela el retroceso pendiente. Probado.
- **Tutorial** (`TutorialController.cs`) con teclas grandes en la parte inferior, lejos de la barra de vida. Pasos:
  1. Moverse (A/D).
  2. Saltar la caja (Espacio).
  3. Atacar 2 veces (J).
  4. Defenderse (K).
  5. Esquivar sobre las púas (L/Shift).
- Tab lo salta. Se guarda en `PlayerPrefs` (`OMT.TutorialDone.v1`) y solo aparece en la fase 1. La aparición de enemigos espera mientras está activo.

### P7: escenario funcional desde los atlas
- 51 recortes en `Art/Environment/`.
- 16 prefabs en `Prefabs/Environment/`, generados con **Tools > One More Time > 9** usando el mismo código de `ArenaPropFactory`: IcePlatform, SerpentScalePlatform, StoneCover, WoodenBarricade, BreakableCover, ArcherPost, IceSpikes, BoneSpikes, VenomVent, JawTrap, IceGeyser, DangerousIce, Crack, UnstablePlatform, MovingPlatform e IcicleTrap.
- **Plataformas de un sentido** (`PlatformEffector2D`). S + Espacio baja de ellas.
- **Coberturas:**
  - la de piedra bloquea proyectiles y aliento;
  - la barricada de madera bloquea solo proyectiles;
  - la caja rompible absorbe 30 de daño y se rompe;
  - las decorativas no tienen collider.
- **Trampas** con ciclo Idle → Warning → Active → Cooldown. Solo dañan en Active (probado). Cambian de sprite según el estado: géiser inactivo o en erupción, mandíbula abierta o cerrada, púas retraídas o fuera.
- **Distribución por invierno** (`StageBuilder.cs`) con dificultad creciente:
  - invierno 1: huesos y piedra;
  - invierno 2: hielo, géiseres, plataformas inestables y móviles;
  - invierno 3: veneno, carámbanos, fisuras y más arqueros.
- La arena del jefe solo tiene 2 coberturas útiles y 3 plataformas altas.

### P8: puestos de arquero
Los arqueros ocupan puestos elevados libres por delante del jugador:
- `AnchorTo` limita su movimiento a los bordes del puesto;
- el ángulo de puntería permite tiros descendentes (−62°);
- respetan las coberturas: no disparan si una cobertura sólida tapa la línea de tiro.

Probado: el puesto está a 2,4 u (alcanzable de un salto) y el arquero no cae ni sale del puesto.

## 3. Lo que NO se ha probado con teclado real
- El QTE se probó llamando a `Press(KeyCode)`. La lectura real con `Input.GetKeyDown` está implementada pero no la ha pulsado una persona.
- Los pasos del tutorial se forzaron en las pruebas y capturas. Su detección con controles reales está implementada pero no se ha jugado a mano.
- La bajada de plataformas, el transporte en la plataforma móvil, la rotura de la caja con flechas y el daño individual de cada trampa están implementados. No tienen prueba automática propia: la prueba solo verifica que cada trampa daña únicamente en su estado activo.

## 4. Scripts nuevos
- `Enemies/`: `JormungandrBody`, `BossRoarStunController`.
- `World/`: `ArenaCover`, `OneWayPlatform`, `ArcherPost`, `MovingPlatform`, `ArenaTrap`, `ArenaPropLibrary`, `ArenaPropFactory`, `StageBuilder`.
- `UI/`: `TutorialController`.
- `Editor/`: `ArenaPrefabGenerator` (menú 9), `DemoV6VisualCheck` (menú 10).

## 5. Scripts modificados
- Combate y jugador: `BossAI` (reescrito), `BossHazard`, `PlayerController`, `Health`, `Projectile`, `ProjectilePool`.
- Enemigos: `EnemyActor`, `EnemyAI`, `EnemySpawner`.
- Sistema: `GameManager`, `UIManager`, `UpgradeDefinition`, `UpgradeCardUI`, `CollisionRules2D`.
- Pruebas: `DemoPlayValidation`, `DemoValidation`.
