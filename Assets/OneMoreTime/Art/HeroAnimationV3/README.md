# Protagonista: 72 frames

PNG RGBA 4608x2304. Cuadrícula 12 columnas × 6 filas, celdas 384x384, sin margen. Filas de arriba abajo: Idle, Walk, Crouch, Attack, Jump, Fall. Todos miran a la derecha. Pivote común: (0.5, 0.1875), pies en el origen; 96 PPU, Point, sin mipmaps ni compresión. El PNG ya está cortado en 72 sprites y tiene seis clips y un controlador de previsualización.

El prefab HeroSheetPreview usa Speed, Grounded, Crouch, VerticalSpeed y Attack. Grounded=true en reposo; Speed>0.1 camina, Crouch agacha, Grounded=false y VerticalSpeed positivo/negativo cambia salto/caída, Attack dispara el hachazo. Voltea Visual.localScale.x para mirar a la izquierda. Los clips de aire mantienen su último frame.

Duraciones: Idle 2.4s, Walk 0.60s, Crouch 2.4s, Attack 0.625s (1.6 ataques/s), Jump 0.4s, Fall 0.6s. Idle/Walk/Crouch forman bucles. Attack contiene anticipación 0–30%, impacto 30–50%, seguimiento 50–66% y recuperación hasta 100%.

La escena existente sigue usando el rig original mejorado, con el mismo muestreador de poses y la ventana real de MeleeWeapon. Así mantiene daño, cadencia variable, giro, colisiones, empuje y curación. Los sprites son una entrega adicional lista para Sprite Editor/Animator. Se conservan diseño, piezas, insignia, paleta y hacha originales; el protagonista no lleva la capa ni el casco de la referencia. La tela roja de su vestuario sí se articula.
