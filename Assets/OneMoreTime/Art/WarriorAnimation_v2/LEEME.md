# Guerrero con hacha — hoja de animación v2

`warrior_animations_v2.png`: PNG RGBA de **2048 × 1536 px**, fondo transparente, cuadrícula exacta de **8 columnas × 6 filas**, celdas de **256 × 256 px**. Los 48 fotogramas conservan la misma escala de píxel. Se verificó que ninguno cruza su celda; el margen mínimo es de 9 px contando los bordes semitransparentes y las estelas.

Las filas se leen de arriba abajo, y los fotogramas de izquierda a derecha:

| Fila | Nombre | Fotogramas | FPS sugeridos | Reproducción |
| --- | --- | --- | --- | --- |
| 1 | Idle | Idle_00–07 | 8 | Bucle |
| 2 | Walk | Walk_00–07 | 12 | Bucle |
| 3 | AxeSlash | AxeSlash_00–07 | 12 | Una vez |
| 4 | Crouch | Crouch_00–07 | 12 | Bajar, sostener, subir |
| 5 | JumpUp | JumpUp_00–07 | 12 | Una vez; transición según velocidad vertical |
| 6 | FallDown | FallDown_00–07 | 10 | Bucle mientras cae |

## Unity

La hoja ya está en Assets/OneMoreTime/Art/WarriorAnimation_v2. El archivo `.png.meta` adjunto contiene los 48 rectángulos y nombres, Sprite Mode Multiple, Point filtering, sin mipmaps y sin compresión en la configuración predeterminada. Conserva ese `.meta` al trasladarla entre proyectos. La importación todavía no se ha ejecutado en el Editor de Unity durante esta entrega.

Si importas solamente el PNG, configura Texture Type = Sprite (2D and UI), Sprite Mode = Multiple, Filter Mode = Point, Compression = None y Max Size = 2048 o superior. En Sprite Editor usa Slice > Grid by Cell Size, Pixel Size 256 × 256, Offset 0, Padding 0. No uses recorte automático de siluetas.

El pivote común es **Custom (0.5, 0.1015625)**: x = 128 y línea de suelo a 230 px desde arriba, equivalente a 26 px desde abajo. Pixels Per Unit = 100 como punto de partida. Mantén ese pivote en las seis filas para evitar saltos entre estados.

Para crear clips, selecciona los ocho sprites de una fila en orden y arrástralos a Animation. Usa los FPS de la tabla y activa Loop Time para Idle, Walk y FallDown. Crouch es una secuencia completa de descenso y salida: usa 00–03 al bajar, mantén Crouch_03 o alterna 03–04 mientras se mantiene la tecla, y usa 05–07 al levantarse. No reproduzcas la fila completa en bucle mientras la tecla siga pulsada. Cambia de JumpUp a FallDown al dejar de ascender; no dependas únicamente del tiempo del clip.

La duración sugerida de AxeSlash es 8/12 s. Su ventana de daño debe sincronizarse con los fotogramas de barrido, no con la carga. Ajusta la reproducción al tiempo del ataque que use tu juego.

## Referencia y revisión

Es una recreación generada a partir de la imagen adjunta: conserva los rasgos principales, armadura, visor rojo, cuernos, capa granate y hacha de doble filo. La referencia adjunta no contiene un ciclo original completo que permita comprobar una conservación exacta píxel por píxel. La generación puede introducir variaciones menores de detalle entre fotogramas.

Se revisaron visualmente las poses, el apoyo, el agarre, la orientación del hacha en caída y la transparencia. Los GIF de revisión están en Verification/WarriorSpriteSheet; su fondo gris es únicamente de previsualización. El GIF de conjunto muestra arriba Idle/Walk/AxeSlash y abajo Crouch/JumpUp/FallDown a 10 FPS comunes; los GIF individuales usan los FPS de la tabla. No se ha sustituido el protagonista de la escena ni conectado esta hoja a su Animator.

Creado con la herramienta integrada image_gen en dos pasadas. Los prompts completos están en Verification/WarriorSpriteSheet/generation-prompt.txt y correction-prompt.txt. El empaquetado posterior solo recoloca los píxeles generados, sin escalarlos ni cambiar su paleta; elimina fragmentos aislados de separación. El manifiesto animations.json incluye rectángulos, pivotes, nombres y velocidades sugeridas.
