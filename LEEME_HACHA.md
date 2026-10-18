# One More Time — hacha y animación articulada

Abre `Assets/OneMoreTime/Generated_Hacha_Final/Scenes/OneMoreTime_Demo.unity`, pulsa Play y luego JUGAR. La escena ya está generada y configurada como escena de inicio en Build Settings. Las escenas y carpetas Generated anteriores se conservan.

## Controles

- A/D o flechas: movimiento.
- Espacio: salto con buffer y tolerancia al abandonar el suelo.
- S o flecha abajo: agacharse; un techo impide levantarse.
- J o botón izquierdo, también mantenido: hacha cuerpo a cuerpo.
- Esc: pausa.

## Cambios

El protagonista usa el hacha original y no tiene ProjectileWeapon. MeleeWeapon sincroniza anticipación, ventana de impacto y recuperación con ArticulatedRig; la cadencia usa la estadística fireRate, inicialmente 1,6 ataques/s y con límite de 4. Cada barrido daña una vez a cada Health, acepta varios blancos y comprueba equipo y obstáculos. El giro se bloquea durante el golpe y se aplica entre golpes; hay un buffer de 140 ms. La pausa, la muerte y el cambio de estado cancelan golpes y entradas pendientes.

Los rigs humanos tienen pelvis, torso, cabeza, hombros, codos y rodillas. La locomoción usa articulación y cinemática inversa de las piernas; el agachado mantiene las escalas de las piezas. La muerte termina con tiempo no escalado. Health comunica el daño para activar reacción y destello.

Los clips de cada personaje se generan a partir del mismo evaluador de poses. El Animator conserva estados y parámetros de locomoción/ataque/muerte; ArticulatedRig ajusta la pose final en LateUpdate para seguir velocidad, salto, aterrizaje y el reloj real del arma sin interrumpir las piernas al atacar. La física permanece en la raíz y no hay root motion.

Los recortes derivados se guardan como assets de Sprite con copias de textura y conservan los PNG originales. Se excluyen fragmentos vecinos y líneas de recorte de la copia generada. La etiqueta Qebin del torso pertenece al dibujo original y se conserva.

El arquero dispara flechas y el mago magia después de preparar el ataque. Ambos lobos usan MeleeWeapon y carecen de ProjectileWeapon. La serpiente tiene poses propias y patrones avisados; sus ondas siguen la altura del terreno.

SerpentSurface mantiene imagen y EdgeCollider2D en el mismo transform. Sus seis perfiles, con imágenes configurables y puntos normalizados, siguen la espalda e ignoran las espinas decorativas. Entrada, enemigos, boss, salida y cámara consultan la altura. Selecciona SerpentBack_Surface para ver los puntos y líneas verdes; edita phaseProfiles y vuelve a entrar en Play para aplicarlos.

## Generar y verificar

No necesitas regenerar para jugar. `Tools > One More Time > 1. Crear demo 2D` crea una versión nueva sin sobrescribir las anteriores y usa el flujo normal de guardado de Unity.

- `2. Validar reglas y escena`: referencias, equipos básicos y reglas del intento.
- `3. Probar solo reglas`: progresión, salud y 256 semillas de mejoras.
- `4. Exportar poses de animacion`: capturas de los seis rigs en Verification/Poses.
- `5. Probar combate y recorrido automatico`: abre la escena de Build Settings y ejecuta pruebas en Play Mode. Esta prueba modifica únicamente el estado del intento de prueba; termina saliendo de Play Mode.

## Validación real

Compilación y generación ejecutadas con Unity 6000.5.5f1 y sus ensamblados reales; no se usaron stubs. Se renderizaron y revisaron poses de los seis personajes con Unity.

El recorrido automático prueba menú/opciones/Jugar, movimiento físico y salto, suelo, agachado y techo bajo, impacto en ambas direcciones, múltiples colliders y objetivos, aliados, paredes, buffer, pausa, cadencia máxima, ausencia de proyectiles del hacha y lobos, arte de flechas/magia, mordida, perfiles de los seis tramos, spawns, cinco elecciones, boss y furia, victoria, muerte y reinicio. Los resultados están en Verification/play-results.txt y Verification/play-pass.txt; los registros de la última ejecución están en Verification/unity-functional-final.log.

Para comprobar la progresión, el recorrido automático elimina enemigos por código y acelera el tiempo. No sustituye una partida manual para valorar dificultad, balance, respuesta a teclas reales y sensación de las transiciones. Esa pasada manual queda pendiente.

Las capturas están en Verification/player-final-poses.png, Verification/humans-poses.png, Verification/animals-poses.png, Verification/axe-play-mode.png y Verification/boss-play-mode.png. Los experimentos intermedios se archivaron fuera de Assets en Verification/IntermediateDemos; no son la escena que debes abrir.

Vista animada: Verification/player-animation.gif. Las primeras ejecuciones de aceptación pudieron elevar el récord local al simular victorias. No se conservó su valor previo; las pruebas actuales desactivan la persistencia del récord y ya no lo escriben. El juego normal sí sigue guardándolo.


## Corrección de orientación y brazos

La misma escena Generated_Hacha_Final queda actualizada; no hace falta regenerar. Se corrigió la fase de apoyo/retorno de los pies, la orientación visual después del Animator y bajo padres reflejados, y el montaje de cada brazo con sus propias piezas y ejes de hombro/codo/muñeca. Durante la recuperación ya puede cambiar la dirección; la ventana de impacto conserva la dirección del golpe.

Se corrigió además un error de compilación del exportador de capturas. La versión actual compiló y ejecutó el recorrido automático completo en Unity 6000.5.5f1: 359 comprobaciones en Play Mode, sin errores de ejecución y con el récord persistido intacto. Incluye dirección de los seis rigs, caminar/saltar/agacharse, combate, menú, pausa, las seis fases, muerte, victoria y reinicio. Último registro: Verification/unity-functional-final.log.


## Empuje y curación al matar

Cada golpe de hacha aceptado empuja a los enemigos normales brevemente hacia atrás. La IA respeta el retroceso durante 0,12 segundos, con velocidad horizontal 3; el desplazamiento medido en la prueba fue 0,18–0,23 unidades. El boss anclado resiste el empuje.

Cada enemigo muerto por daño del jugador le devuelve 5 puntos de vida, sin sobrepasar su máximo. Una baja no cura dos veces; las muertes ambientales no otorgan curación. La cantidad se ajusta con healOnKill en Generated_Hacha_Final/RunConfig.asset. La fuerza se ajusta en MeleeWeapon y la duración/resistencia en EnemyActor. La escena y el generador ya incluyen el comportamiento; no hay que regenerar.

Validación real de esta versión: Unity 6000.5.5f1, 369 comprobaciones en Play Mode, sin errores. Se probaron golpes con IA activa hacia ambos lados, recuperación del control después del empuje, curación por un hachazo letal real, límite de vida y protección contra curación duplicada. Registro: Verification/unity-push-heal.log.

## Mejoras, pactos y sprites de lobos

Las seis cartas tienen beneficios principales distintos: Filo templado (+20% daño por golpe), Paso ligero (+12% movimiento), Vitalidad (+20% vida máxima y recuperación de esa capacidad), Técnica del hacha (+15% ataques/s), Piel de piedra (15% menos daño recibido a cambio de 10% menos movimiento) y Pacto vampírico (+2 vida por baja a cambio de 10% menos vida máxima). Las penalizaciones son parte de los pactos. Cada carta muestra los valores efectivos antes y después; las cartas que alcanzan su límite dejan de ofrecerse en una partida normal.

Al elegir una mejora aparece un anillo verde con partículas; los pactos usan violeta y partículas rojas para señalar su coste. Un aviso muestra el efecto aplicado. El efecto sigue al jugador al cambiar de fase, usa tiempo no escalado y se limpia al reiniciar. El HUD muestra resistencia y curación por baja. La curación inicial sigue siendo 5; Pacto vampírico la eleva a 7 y puede acumularse.

Los lobos usan nuevos recortes derivados. Se conserva el pelaje blanco dentro de los contornos, se restaura el alfa dañado de muslo y cola del lobo blanco usando los colores originales, se eliminan etiquetas y fragmentos de celdas vecinas, y se reúnen espinilla y garra del lobo azul. Los PNG originales no se modifican. Los prefabs de la escena Generated_Hacha_Final se actualizan directamente.

Validación final de estas correcciones: Unity 6000.5.5f1, 400 comprobaciones en Play Mode sin errores de ejecución. Incluye los seis beneficios, curación real de 7 con el pacto, reducción real de daño y velocidad, recuperación de capacidad de Vitalidad, efectos con tiempo pausado y traslado entre fases, reinicio, cartas sin texto recortado y avisos sin superposición. Se mantiene intacto el récord persistido. Resultados: Verification/play-results.txt y Verification/play-pass.txt. Capturas: Verification/upgrade-cards.png, Verification/upgrade-damage.png, Verification/upgrade-stone.png, Verification/upgrade-vampire.png y Verification/animals-poses.png. Los sprites intermedios de esta reparación quedaron archivados fuera de Assets en Verification/IntermediateWolfSprites.

## Sprite sheet del protagonista y biomecánica

La entrega está en Assets/OneMoreTime/Art/HeroAnimationV3/Hero_Axe_72.png: PNG RGBA transparente de 4608×2304, 12 columnas y 6 filas, celdas de 384×384, 96 PPU y pivote común (0.5, 0.1875). Filas: Idle, Walk, Crouch, Attack, Jump y Fall. Se incluyen animations.json, seis clips, HeroSheet.controller y HeroSheetPreview.prefab. La guía de uso está en esa misma carpeta.

Se reutilizaron las piezas originales del guerrero calvo, su paleta, hacha y vestuario. Las poses se renderizan desde Unity; la ilustración adjunta aporta la referencia de carga, postura y seguimiento. La tela roja original se articula por separado. El hacha tiene dos puntos de agarre y ambos brazos los siguen mediante IK. Las botas tienen articulación de tobillo para mantener la suela plana en apoyo. El cuerpo baja al contacto, sube durante el apoyo medio y respira en reposo. El agachado flexiona las piernas; salto y caída tienen poses distintas. El barrido tiene estela transparente sincronizada con la fase de corte y recuperación del arma.

La mejora se aplicó a los Player.prefab de Generated_Hacha_Final y Generated_Hacha_Final 1 sin reemplazar sus componentes de juego ni sus GUID. La escena activa durante la validación fue Generated_Hacha_Final 1/Scenes/OneMoreTime_Demo.unity. La escena anterior también recibe el rig actualizado. El juego conserva su animación por rig, muestreada con el mismo código que genera los frames, para seguir el reloj de MeleeWeapon y la cadencia variable. El sprite sheet y su controlador son una entrega adicional importada y lista para usar.

Validación final: Unity 6000.5.5f1, 415 comprobaciones de Play Mode sin errores de ejecución. Incluye agarre de ambas manos al mirar a izquierda/derecha y con padres reflejados, caminar, salto, agachado, hachazo, empuje, curación, cinco elecciones, boss, muerte y reinicio. Las 72 celdas del PNG coinciden exactamente con sus capturas fuente, tienen bordes transparentes, no se recortan y mantienen el tamaño al importar. Resultados en Verification/play-results.txt, play-pass.txt, hero-sheet-pass.txt y HeroAnimationV3/png-checks.txt. GIF de revisión: Verification/HeroAnimationV3/Hero_All.gif; PNG de poses: Hero_Preview.png. Los fondos de cuadrícula de estas vistas son solo de revisión: el sprite sheet conserva transparencia real.
