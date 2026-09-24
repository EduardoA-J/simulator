# Continuación de mapas y zona inicial

Proyecto: `F:\Avion_VR\simulator`. Unity 6000.5.6f1, XRI 3.5.1.

## Estado encontrado

El vuelo, el agarre XRI, lanzamiento, cámara, puntuación, aviones y UI estaban implementados.
Las dos escenas de juego ya tenían exactamente tres LevelDefinition asignados. Los assets de
los mapas 4 y 5 son contenido antiguo sin referencias en esos menús; se conservaron.

Los mapas 2 y 3 tenían curvas senoidales y checkpoints, pero faltaba seleccionar un mapa
directamente. El mapa 3 repetía una misma onda de giro/altura. Los obstáculos tenían posiciones
aleatorias. La prueba anterior fallaba porque todavía esperaba GameOver al terminar el mapa 1,
aunque la campaña de tres mapas ya devolvía LevelComplete.

Respaldo anterior a estos cambios: `Backups/Continuation/20260923_214717`.

## Cambios

- Mapa 1, Aula: 70 m de avance, recta introductoria, cuatro obstáculos laterales estáticos,
  sin enemigos y velocidad limitada a 10 m/s.
- Mapa 2, Parque: conserva su trazado de 150 m, curvas alternadas, diez obstáculos colocados
  de forma repetible, obstáculos móviles laterales y diez pasos obligatorios. Límite: 10 m/s.
- Mapa 3, Oficina: 440 m de avance, aproximadamente 479 m de recorrido, alturas de 1,8 a
  8,5 m, giros alternados, ascensos y descensos durante giros, enlaces más cerrados al final,
  26 obstáculos y 36 pasos obligatorios. Aros cada 6 m y límite de 9 m/s para que el avión
  inicial pueda negociar las curvas. Los impulsos dorados permiten sostener el recorrido.
- Geometría por tramos en el LevelDefinition existente, con interpolación suave sin sobrepasar
  las alturas de los puntos de control. El mapa 2 sigue usando el generador anterior.
- Semillas de distribución y movimiento reiniciado en cada intento. El corredor central se
  mantiene abierto; vigas altas/bajas y obstáculos laterales hacen necesario seguir su altura.
- Los tres paneles existentes del menú reciben Button.OnClick: índices 0, 1 y 2.
  Se conservaron tamaños, posiciones, colores base, tipografía y composición. Solo cambian
  contenido y comportamiento según el estado del juego.
- El gatillo sobre UI pertenece al botón apuntado; ya no activa antes el inicio global del mapa 1.
- Retorno a mapas desde el botón principal de la mesa VR y desde `<` en resultados.
  Reintentar conserva el mapa actual; siguiente escenario conserva la progresión de puntuación.
- Mesa VR más próxima, tablero a 90 cm, aviones aproximadamente a 98 cm y orientados hacia
  delante. Alfombra, bases de exposición, flecha de lanzamiento, lámpara sin sombras y una
  estantería lateral. La decoración no interfiere con el agarre.
- Opción adicional para el XR Device Simulator clásico ya instalado. El modo normal de
  simulador, XROrigin, XRGrabInteractable, entradas Quest y assets de aviones se mantienen.

Los mapas se generan dentro de la escena actual a partir de los ScriptableObjects. No son
tres escenas independientes: cargar otra escena para cada botón reemplazaría innecesariamente
el rig XR. Build Settings ya incluye Game_VR_Oculus y Game_Playable. Los builders siguen
siendo opcionales y no se ejecutaron para regenerar escenas.

## Probar mapas y botones

1. Abre `Assets/AvionesPapelVR/Scenes/Game_VR_Oculus.unity`.
2. Elige el modo de entrada antes de Play. Para visor: `Aviones de Papel VR > Input VR > Hardware Quest o Link`.
3. Apunta a una de las tres tarjetas del menú y pulsa/suelta Trigger. Verifica el nombre y
   número del nivel en la cabecera de la selección de avión.
4. Agarra un avión desbloqueado con Grip; mueve la mano hacia delante y suelta.
   Soltar sin impulso debe devolver el avión a su posición de la mesa.
5. Aula: sigue los aros y cruza la meta. Parque: supera cada PASO en orden. Oficina: combina
   altura y giro para atravesar los aros; no basta con volar directamente a la meta.
6. Tras un fallo, pulsa REINTENTAR: debe conservar el mapa. Tras superar 1 o 2, SIGUIENTE
   ESCENARIO lleva al siguiente. Tras el mapa 3, el botón vuelve al mapa 1.
7. Desde resultados, `<` vuelve al menú. Desde la mesa VR, VOLVER A MAPAS hace lo mismo.
   Las flechas de la mesa siguen recorriendo los modelos de avión.

Para teclado, abre `Game_Playable.unity`: clic en la tarjeta o teclas 1/2/3, Enter para
seleccionar avión, Espacio para cargar/soltar el lanzamiento, W/S para pitch y A/D para roll.
Escape vuelve al menú desde selección/resultados; durante vuelo conserva su función de cursor.

## XR Device Simulator, sin visor

1. Sal de Play. Selecciona `Aviones de Papel VR > Input VR > XR Device Simulator (clasico)`.
2. Abre Game_VR_Oculus y pulsa Play. No añadas otro simulador a la escena.
3. Mantén Espacio para manipular el mando derecho o pulsa Y para mantenerlo seleccionado.
   Shift izquierdo manipula el izquierdo. Botón derecho del ratón manipula la cabeza.
4. Usa el ratón y los ejes de traslación para colocar el mando; Ctrl cambia temporalmente
   a rotación y R alterna transformación del ratón. Consulta también el panel de ayuda del simulador.
5. Apunta el rayo al mapa; clic izquierdo pulsa Trigger. Cerca del avión, mantén G (Grip),
   mueve el mando hacia delante y suelta G mientras se mueve.
6. Después de lanzar, deja el mando neutro medio segundo. Cambia su orientación para dirigir
   pitch y roll. Para pilotar por sticks, selecciona el eje 2D primario con 1 y usa WASD;
   confirma el dispositivo seleccionado en el panel del simulador.

La otra opción, `Simulador (sin visor)`, utiliza XR Interaction Simulator, la variante más
reciente ya instalada: Tab cambia de dispositivo, `]` selecciona el derecho, `[` el izquierdo,
WASD/QE trasladan, flechas rotan, T es Trigger y G es Grip. Sus teclas son distintas a las del
XR Device Simulator clásico. Cambia de modo fuera de Play.

## Validación reproducible

Menú: `Aviones de Papel VR > Validar circuito (Play Mode)` con las escenas guardadas.
El runner restaura las escenas abiertas y no guarda récords. Usa su propio modo de simulación.

La prueba incluye conexiones de tarjetas y navegación, rayo XR real contra UI, agarre/cancelación/
lanzamiento, cámara, puntuación, impulsos, checkpoints en orden, rechazo de atajos a meta,
reintento, avance de campaña, vuelo con entradas simuladas y física real, entrada por teclado
mediante SceneManager y arranque del prefab de XR Device Simulator clásico.

Resultados: `Logs/AvionesValidation.json`. Capturas: `Logs/Interface_Menu.png`,
`Logs/Interface_Selection.png`, `Logs/Lobby_Workshop.png`, `Logs/Map2_Course.png`,
`Logs/Map3_Course.png`. El resultado definitivo de esta ejecución se consigna al terminar.

La prueba automática de vuelo usa un piloto de prueba que envía entradas de stick al sistema
real. La comodidad del agarre, el pilotaje humano y FPS/latencia en Meta Quest requieren prueba
con visor físico; no se han medido en hardware.

## Archivos afectados

- `Data/Levels/Level_01_Aula.asset`, `Level_02_Parque.asset`, `Level_03_Oficina.asset`.
- `Scenes/Game_VR_Oculus.unity`: posición de SelectAnchor.
- `Scripts/Core/LevelDefinition.cs`, `GameManager.cs`.
- `Scripts/Gameplay/LevelRunner.cs`, `CurvedCourse.cs`, `MovingObstacleMotion.cs`,
  `GameHUD.cs`, `PlaneSelector.cs`, `XrSimulatorGuard.cs`.
- `Scripts/Editor/PlayableGameBuilder.cs`, `PlayableVrGameBuilder.cs`, `XrPlayMode.cs`,
  `GameplayValidation.cs`.

Las rutas de esta lista son relativas a `Assets/AvionesPapelVR`. No se añadieron managers
ni controladores de juego duplicados. Los cambios anteriores del usuario en configuración,
paquetes y proyecto se conservaron.
