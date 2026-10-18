from pathlib import Path
p=Path('LEEME_HACHA.md');s=p.read_text(encoding='utf-8-sig')
s+='''

## Empuje y curación al matar

Cada golpe de hacha aceptado empuja a los enemigos normales brevemente hacia atrás. La IA respeta el retroceso durante 0,12 segundos, con velocidad horizontal 3; el desplazamiento medido en la prueba fue 0,18–0,23 unidades. El boss anclado resiste el empuje.

Cada enemigo muerto por daño del jugador le devuelve 5 puntos de vida, sin sobrepasar su máximo. Una baja no cura dos veces; las muertes ambientales no otorgan curación. La cantidad se ajusta con healOnKill en Generated_Hacha_Final/RunConfig.asset. La fuerza se ajusta en MeleeWeapon y la duración/resistencia en EnemyActor. La escena y el generador ya incluyen el comportamiento; no hay que regenerar.

Validación real de esta versión: Unity 6000.5.5f1, 369 comprobaciones en Play Mode, sin errores. Se probaron golpes con IA activa hacia ambos lados, recuperación del control después del empuje, curación por un hachazo letal real, límite de vida y protección contra curación duplicada. Registro: Verification/unity-push-heal.log.
'''
p.write_text(s,encoding='utf-8')
