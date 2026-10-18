# Guía para Codex: corregir montaje y animaciones del guerrero 2D

## Encargo

Corrige en el proyecto existente el montaje por piezas y las animaciones de ESTE personaje. Es el guerrero calvo y barbudo con pecho descubierto, faldón rojo, botas con pelo, hacha corta y escudo redondo de las imágenes adjuntas. Usa los sprites originales del proyecto para el personaje final.

Adjunto una lámina con seis poses de referencia para cada acción: IDLE, WALK, JUMP, ATTACK y DIE. Es una guía visual aproximada, no un atlas listo para recortar ni clips .anim. Las proporciones y piezas originales tienen prioridad sobre cualquier variación del dibujo generado.

La captura de las animaciones actuales muestra figuras pequeñas con poca variación de silueta. El problema reportado es que brazos y piernas quedan metidos dentro del cuerpo. No dispongas nuevas curvas encima de un montaje incorrecto: inspecciona primero el prefab, sus pivotes, escalas, jerarquía y clips. No des por confirmada la causa hasta revisarlos.

## 1. Corregir primero la pose de reposo

1. Localiza el prefab realmente utilizado en la escena, sus sprites, el Animator, los clips y cualquier script que modifique los mismos transforms. Revisa qué se genera automáticamente y qué se guarda en assets, para que las correcciones sobrevivan a otra ejecución.
2. Pausa el Animator y las escrituras procedurales de la pose. Arma una pose neutral que reproduzca el personaje completo de la imagen original.
3. Comprueba que los SpriteRenderer tengan sprites asignados, el mismo criterio de Pixels Per Unit y escalas coherentes. Conserva el tamaño relativo dibujado de cada pieza; no ajustes todas al mismo ancho o alto.
4. Coloca los hombros en los extremos superiores del pecho, las caderas en la pelvis, las rodillas entre muslos y pantorrillas y los tobillos sobre las botas. Usa el contenido visible y el canal alfa para medir: el centro del rectángulo de un sprite con márgenes no identifica una articulación.
5. Las piezas deben solaparse lo justo en cada unión para que no queden huecos. El brazo completo no debe estar hundido en el torso ni el muslo entero dentro del cinturón.
6. Mantén las longitudes de los segmentos y proporciones del original. No uses scale para corregir un pivote, doblar una pierna, agacharse o simular respiración.
7. Guarda una vista ampliada de la pose neutral con ejes/puntos de giro visibles y otra sin ayudas, para comprobar el montaje antes de animarlo.

## 2. Jerarquía y puntos de giro

Adapta la estructura a las piezas que EXISTEN. Este esquema describe articulaciones, no exige inventar sprites:

```text
CharacterRoot
  VisualRoot
    Pelvis
      Torso
        HeadJoint
          HeadSprite
        AxeShoulderJoint
          UpperArmSprite
          AxeElbowJoint
            ForearmSprite
            AxeWristJoint
              HandSprite
              AxeSocket
                AxeSprite
        ShieldShoulderJoint
          UpperArmSprite
          ShieldElbowJoint
            ForearmSprite
            ShieldWristJoint
              HandSprite
              ShieldSocket
                ShieldSprite
      NearHipJoint
        ThighSprite
        NearKneeJoint
          ShinSprite
          NearAnkleJoint
            BootSprite
      FarHipJoint
        ThighSprite
        FarKneeJoint
          ShinSprite
          FarAnkleJoint
            BootSprite
```

- Los objetos Joint/Socket son puntos de giro. Coloca cada SpriteRenderer como hijo con el desplazamiento necesario para alinear su dibujo con la articulación. Así puedes corregir el pivote visual sin depender de que todos los sprites tengan el pivot centrado.
- Si un brazo, una pierna o una mano están dibujados como una única pieza, utiliza esa pieza como tal. No la dupliques para fingir dos segmentos ni estires su textura. Identifica y explica las articulaciones que el material disponible no permite doblar.
- Mantén constantes las posiciones locales que definen las longitudes entre articulaciones. Anima principalmente rotaciones Z. El movimiento suave de pelvis/torso puede tener curvas locales de posición.
- Hacha y escudo deben seguir sus sockets. No necesitan animación independiente en coordenadas globales.
- Define el orden de dibujo por partes. El miembro lejano suele ir detrás del cuerpo; el cercano, delante; el faldón tapa las uniones de las caderas. Comprueba manos, empuñadura y escudo en cada pose, porque un orden incorrecto puede parecer un fallo de posición.
- Voltea el conjunto visual de forma coherente cuando cambie la dirección. No inviertas cada pieza por separado.
- El movimiento y las colisiones del personaje pertenecen a CharacterRoot. El cuerpo visual puede balancearse sin desplazar el collider.
- Una sola fuente debe controlar cada propiedad animada. Elimina conflictos entre Animator, scripts y actualizaciones tardías que sobrescriban la misma pose.
- Al cambiar de estado, las propiedades deben llegar a su pose correspondiente sin heredar rotaciones o escalas residuales de otro clip.

## 3. Poses y tiempos iniciales

Estos tiempos son puntos de partida ajustables, no requisitos del motor. Las seis columnas de cada fila son poses clave; interpola entre ellas. En WALK/IDLE cierra el ciclo hacia la primera pose sin repetirla como una pausa.

| Acción | Duración orientativa | Loop | Secuencia |
| --- | --- | --- | --- |
| Idle | 1,2 s | Sí | Reposo → inspiración leve → punto alto → exhalación → punto bajo → regreso |
| Walk | 0,8 s por ciclo | Sí | Contacto → apoyo bajo → paso → contacto opuesto → apoyo bajo opuesto → paso opuesto |
| Jump | Depende del movimiento real | No | Preparación → impulso → ascenso → ápice → descenso → recepción |
| Attack | 0,6 s | No | Guardia → preparación → hacha arriba → golpe → seguimiento → recuperación |
| Die | 0,9 s | No | Impacto → desequilibrio → ceden las rodillas → caída → contacto con suelo → reposo final |

### Idle

Pies apoyados y separados con naturalidad, rodillas ligeramente flexionadas. Pecho y cabeza se elevan muy poco al respirar. El hacha y el escudo acompañan los brazos. No escales el torso ni hundas las manos en el cuerpo.

### Caminata

Alterna realmente el apoyo de ambas piernas. Cuando una avanza, la otra queda atrás; durante el paso, la rodilla de la pierna que se mueve se flexiona y su pie despega del suelo. Evita mover ambas piernas a la vez en la misma dirección. El pie de apoyo debe tener un contacto creíble con el suelo.

La pelvis oscila suavemente en altura y el torso permanece estable. Los brazos tienen un contramovimiento moderado, limitado por el peso del hacha y escudo. Conserva el agarre. Ajusta la frecuencia del ciclo a la velocidad horizontal del juego para reducir el deslizamiento de las botas.

### Salto

La preparación y el aterrizaje se hacen doblando caderas, rodillas y tobillos, sin aplastar ni estirar los sprites. Durante el vuelo las piernas se recogen un poco, y antes de tocar suelo vuelven a extenderse.

Sincroniza ascenso, ápice, descenso y recepción con el estado real del controlador de movimiento. Un clip fijo que siempre termina en aterrizaje puede fallar si cambia la duración del salto. Reutiliza los estados o parámetros que ya existan; añade sólo los necesarios. No retrases la respuesta al botón si el juego requiere un salto inmediato.

### Ataque

Es un golpe cuerpo a cuerpo con el hacha, con una mano. La otra conserva el escudo. El movimiento nace del hombro, flexiona el codo y arrastra la muñeca y el arma. Añade una preparación legible, un golpe rápido hacia delante/abajo y una recuperación algo más lenta.

El hacha no debe soltarse ni atravesar arbitrariamente el pecho. Sincroniza la ventana de daño con el tramo del golpe. No produzcas varios impactos sobre el mismo objetivo por activar una comprobación cada frame si el diseño prevé un impacto por ataque.

### Muerte

La acción termina con el personaje derrumbado en el suelo. No lo dejes de pie inclinado ni repitas la caída. Mantén la pose final y coordínala con el sistema de muerte existente, sin que Idle o Walk vuelvan a activarse. En esta referencia sencilla el hacha y escudo acompañan las manos; no hace falta añadir física de objetos.

## 4. Comprobación y entrega en el proyecto

- Muestra primero el personaje neutral, ampliado, con todas las piezas conectadas.
- Revisa Idle y Walk completos, después salto, ataque y muerte, a velocidad normal y lenta.
- Comprueba ambas direcciones y las transiciones: no debe haber piezas que salten de posición, cambien de tamaño o queden con una pose del estado anterior.
- Verifica que los clips usados en ejecución sean los corregidos, que los bindings apunten a rutas válidas del prefab y que las correcciones persistan al cerrar y abrir la escena.
- Entrega los cambios concretos en prefabs, clips/controlador y scripts necesarios, con una explicación breve de la causa que encontraste.
- Si puedes ejecutar Unity, adjunta una captura o vista previa de cada acción. Si no puedes ejecutarlo, indica exactamente qué revisaste y qué validación queda pendiente; no afirmes que se vio funcionar.

Objetivo visual: el mismo guerrero original, reconocible y bien armado, con movimiento articulado legible. La prioridad es corregir las uniones y la silueta antes de añadir efectos.

