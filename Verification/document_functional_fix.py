from pathlib import Path
p=Path('LEEME_HACHA.md');s=p.read_text(encoding='utf-8-sig').replace('Verification/unity-acceptance-final.log','Verification/unity-functional-final.log')
s+='''

## Corrección de orientación y brazos

La misma escena Generated_Hacha_Final queda actualizada; no hace falta regenerar. Se corrigió la fase de apoyo/retorno de los pies, la orientación visual después del Animator y bajo padres reflejados, y el montaje de cada brazo con sus propias piezas y ejes de hombro/codo/muñeca. Durante la recuperación ya puede cambiar la dirección; la ventana de impacto conserva la dirección del golpe.

Se corrigió además un error de compilación del exportador de capturas. La versión actual compiló y ejecutó el recorrido automático completo en Unity 6000.5.5f1: 359 comprobaciones en Play Mode, sin errores de ejecución y con el récord persistido intacto. Incluye dirección de los seis rigs, caminar/saltar/agacharse, combate, menú, pausa, las seis fases, muerte, victoria y reinicio. Último registro: Verification/unity-functional-final.log.
'''
p.write_text(s,encoding='utf-8')
