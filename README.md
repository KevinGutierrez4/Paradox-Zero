# Paradox Zero

La versión activa está en `Assets/OneMoreTime/Generated_Hacha_Final 2`.
Play siempre abre su escena `Scenes/OneMoreTime_Demo.unity`. No hace falta generar una demo.

Herramientas disponibles en **Tools > One More Time**:
- **Abrir juego**: abre la escena activa y conserva una copia de cualquier edición sin guardar.
- **Ir directo al jefe**: inicia una prueba de la fase 6, con su presentación y sin modificar récords.
- **Editar terreno de las fases**: selecciona una fase y permite mover los puntos verdes del suelo en Scene.
- **Editar plataformas y trampas**: abre el editor visual; elige una fase y pulsa **Abrir fase en Scene**.
- **Ajustar siempre Game a la ventana**: evita que el zoom del editor recorte la imagen o la interfaz.

Las seis fases comparten una escena. La distribución de cada una se guarda en
`Assets/OneMoreTime/Levels/Fase_01.prefab` hasta `Fase_06.prefab` (jefe).
El editor abre estos prefabs con el fondo como referencia. Dentro de **Plataformas y trampas**
selecciona los objetos y muévelos con **W**; puedes duplicarlos con **Ctrl+D**, borrarlos o añadirlos
desde la ventana. Guarda con **Ctrl+S** o Auto Save. Usa la flecha de Hierarchy para volver al juego.
La partida utiliza estos mismos prefabs y conserva sus posiciones y ajustes.
En la fase 6, **Jefe y escamas** contiene **Base del jefe**, sus montículos sólidos y las ocho
escamas editables. El origen de cada escama marca su contacto con el piso; el juego conserva su
posición y adapta el dibujo a su pivote. Puedes duplicarlas o eliminarlas. El botón **Ajustar escamas
y base al suelo** vuelve a colocarlas sobre el relieve actual. **Base del jefe / Bloqueo del jefe
(no escalable)** contiene una pared física vertical que impide trepar al montículo. Se muestra
con una línea naranja en Scene y se puede seleccionar desde el editor de fases; su techo queda
fuera del alcance del salto. No depende de un límite de posición oculto en el código del jugador.
Los prefabs editables de plataformas, coberturas y trampas están en `Assets/OneMoreTime/Prefabs`;
`Resources/OMT_ArenaPropLibrary.asset` contiene sus referencias. Se instancian durante la partida.
El relieve y el fondo de cada fase se guardan en `Environment/SerpentBack_Surface` de la escena.
Guarda las correcciones fuera de Play. `RunConfig.asset` contiene la longitud y cantidad de enemigos.

Se conservaron las animaciones editables de los enemigos, las piernas al caminar y atacar, el escudo,
los portales, el tutorial y el jefe con sus tres fases, ataques especiales y QTE.
Los sprites de `Art/JormungandrPhases` usan **72 pixels per unit**: las articulaciones de la mandíbula
están medidas a esa escala. Cambiarla a 100 desajusta el dibujo y reduce el tamaño visible.
La fase final usa `Art/Fondos/BossArena.png`.

Los generadores antiguos de escenas y las herramientas de regeneración/exportación ya no están
expuestos en el menú. Las copias antiguas se retiraron del proyecto y se respaldaron antes de restaurar
la versión reciente, sin cambiar tu rama Git.
