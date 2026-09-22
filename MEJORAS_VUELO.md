# Mejoras del circuito de vuelo

Escena principal: `Assets/AvionesPapelVR/Scenes/Game_VR_Oculus.unity`.
Los cambios se aplican al iniciar Play; no es necesario regenerar escenas ni modelos.

## Diagnóstico y cambios

- Antes, el lanzamiento físico usaba la posición de la mano y cualquier dirección, mientras el nivel se construía en +Z global. Ahora `FlightStartAnchor` define la salida y la orientación horizontal de todo el circuito, incluidos aros, obstáculos, viento y meta.
- La asistencia de salida convierte gestos hacia atrás en lanzamientos hacia el recorrido. Conserva la intensidad y el componente lateral, limita el ángulo lateral a 25 grados y la elevación entre -8 y 18 grados. Los lanzamientos válidos reciben al menos 6 m/s; soltar sin impulso sigue devolviendo el avión a la mesa.
- El mando de vuelo tiene suavizado y recuperación gradual del cabeceo al dejar los controles en reposo. Continúan vigentes las diferencias entre aviones, la pérdida de sustentación y los límites de velocidad.
- Pista con marcas, aros azules y dorados con emisión, trayectoria suavemente descendente, obstáculos espaciados y mayor visibilidad en la niebla. La meta tiene un marco abierto y detecta el cruce del avión.
- HUD compacto durante el vuelo: velocidad, altura, progreso, aros, impulsos y aviso de proximidad al suelo. Mensajes breves al recoger aros.
- Materiales de la señalización compartidos y liberados al reconstruir el nivel.
- Se desactiva el simulador automático duplicado. El modo de simulación existente sigue disponible en el menú del proyecto.

## Probar

1. Abre la escena principal.
2. Para visor: `Aviones de Papel VR > Input VR > Hardware Quest o Link`.
3. Sin visor: `Aviones de Papel VR > Input VR > Simulador (sin visor)`.
4. Agarra con Grip y suelta con movimiento. Comprueba que la cabina aparece frente al recorrido, incluso al lanzar hacia atrás.
5. Tras lanzar, relaja la mano durante medio segundo: esa orientación se toma como neutra. Apunta el mando derecho a izquierda/derecha para girar y arriba/abajo para cambiar el cabeceo. También puedes ladear la muñeca. Vuelve a la orientación neutra para estabilizar.
6. El stick derecho permite dirigir el avión incluso usando el mando por inclinación. X cambia el modo de control; Y recentra la cabina y recalibra la mano. La cabina sigue el rumbo de forma predeterminada, sin inclinar el horizonte. El clic del stick izquierdo alterna ese seguimiento.
7. Aros azules: 100 puntos. Dorados: impulso. Cruza la meta abierta o intenta un aterrizaje suave.

## Interfaz y control en primera persona

La interfaz utiliza Inter, paneles con jerarquía tipográfica, tarjetas de atributos, barras de progreso, botones de navegación y un informe de resultados. Durante el vuelo, el centro de la vista queda libre y la instrumentación se concentra en los bordes. El indicador del mando permite comprobar qué dirección se está interpretando. Si se pierde seguimiento, se informa de ello y se conserva el control por stick.

El giro responde al rumbo del mando, además del alabeo. El cabeceo establece un ángulo objetivo limitado: mantener el mando inclinado ya no acumula giros verticales. El movimiento del avión converge más rápidamente hacia el morro y la cámara acompaña su rumbo horizontal, conservando el movimiento libre de la cabeza.

Capturas reales generadas en Unity: `Logs/Interface_Menu.png`, `Logs/Interface_Selection.png`, `Logs/Interface_Flight.png` y `Logs/Interface_Results.png`.

Validación automatizada: `Aviones de Papel VR > Validar circuito (Play Mode)`.
Resultado: `Logs/AvionesValidation.json`. Vista de comprobación: `Logs/CoursePreview.png`.
La prueba usa dispositivos XR simulados; la comodidad, latencia y rendimiento con visor físico requieren una sesión en el dispositivo.

Copia de los scripts previos: `Backups/Improvements/20260922_105844/Scripts`.
