# Guerrero con hacha y escudo: 96 frames

PNG RGBA 4608x3072. Cuadrícula de 12 columnas y 8 filas, celdas 384x384 sin margen. Filas: Idle, Walk, Crouch, Attack, Jump, Fall, Block, Die. Fondo transparente, mirada a la derecha. Pivote (0.5,0.1875), 96 PPU, Point, sin mipmaps ni compresión. Incluye 96 sprites, ocho clips y HeroSheetPreview.prefab.

Duraciones: Idle/Crouch/Block 1.2s, Walk 0.8s, Attack 0.625s, Jump 0.4s, Fall 0.6s, Die 0.9s. Idle/Walk/Crouch/Block forman bucles; los demás mantienen su último frame. Die es terminal. El controlador de previsualización usa Speed, Grounded, Crouch, VerticalSpeed, Attack, Blocking y Dead. Voltea Visual.localScale.x para invertir la dirección.

En la escena jugable, ArticulatedRig controla las articulaciones y Animator queda desactivado para evitar escrituras simultáneas. Los clips de transforms corregidos quedan disponibles como referencia. El movimiento y los colliders continúan en la raíz. Los estados de aire siguen la velocidad vertical real. El hachazo se sincroniza con MeleeWeapon: carga 0–30%, golpe 30–50%, seguimiento 50–66% y recuperación. Una sola mano sostiene el hacha; la otra conserva el escudo. Cada objeto es hijo de su muñeca. Las escalas de los segmentos son uniformes y constantes.

Controles: A/D o flechas para moverse, espacio para saltar, S/abajo para agacharse, J/clic izquierdo para atacar. Mantén K/clic derecho para bloquear: reduce 65% el daño enemigo, permite caminar al 45% de velocidad e impide atacar. Soltar, pausar o morir cancela el bloqueo. El daño ambiental no se bloquea. Se conservan empuje al golpear, curación por bajas, mejoras y enemigos.

Las piezas proceden de WarriorParts.png sin modificar el original. Los pivotes se midieron sobre el contenido visible. Se usan muslo, pantorrilla y pie separados; no se duplica una bota completa como otro segmento. Las vistas Neutral.png y NeutralJoints.png y los frames de cada acción se guardan en Verification/ShieldHeroV4.
