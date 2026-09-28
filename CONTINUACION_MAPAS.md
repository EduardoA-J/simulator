# Continuación del proyecto — 27/09/2026

Proyecto: `F:\Avion_VR\simulator`. Unity 6000.5.6f1, XR Interaction Toolkit 3.5.1.
Base revisada: commit `8857045`. Se conservaron las escenas, XROrigin, XR Grab Interactable, ambos simuladores y Meta/Oculus Quest.

## Contraste con el análisis anterior

- Las dos escenas contienen **cinco mapas** y doce definiciones de avión.
- El rayo del mando y el Trigger sobre el mapa 3 **pasaron en el Editor con ventana**, antes de modificar código. No se cambiaron bindings ni raycasters.
- El agarre, la cancelación, el lanzamiento y el vuelo estaban implementados. Las pruebas ahora avanzan más allá del fallo histórico.
- Las curvas de Ciudad y Bosque Mágico sí excedían los 45 grados/s del avión inicial: 48,4 y 89,0 respectivamente.
- Esta máquina sí abre y compila Unity con licencia Personal. Tiene Android Build Support, SDK, NDK y OpenJDK instalados. No había ningún dispositivo conectado en `adb devices -l`.
- `.gitignore` ya excluye Logs, UserSettings y csproj. El ruido persiste porque varios de esos archivos ya están versionados; no se modificó el índice de Git.

## Grupo 1: rutas y validación

Se cambió un punto lateral de Ciudad y tres de Bosque Mágico. Se conservaron longitudes, alturas, velocidades y la física del avión. La exigencia máxima de giro medida en Unity quedó en **41,2 grados/s** y **38,2 grados/s**.

La prueba de saltarse los pasos ahora mueve el avión por fuera de la ruta antes de cruzar la meta. Antes, el segmento recto del teletransporte atravesaba legítimamente el primer checkpoint y hacía fallar la expectativa de cero pasos.

La validación desactiva el arranque XR solo en memoria y restaura el valor al terminar. No guarda ese cambio temporal en el asset de configuración. El objeto de validación sobrevive al cambio a Game_Playable. Antes de iniciar el simulador clásico se destruye el simulador moderno: ambos comparten el singleton de dispositivos de XRI, aunque estén desactivados.

**Cómo probar:** con las escenas guardadas y fuera de Play, ejecutar `Aviones de Papel VR > Validar circuito (Play Mode)`. Ver el resultado en `Logs/AvionesValidation.json`. La prueba pilota los cinco mapas con entrada simulada y física real, además de verificar checkpoints, reintento, siguiente nivel y final de campaña.

## Grupo 2: agarre, aviones y drones

Un agarre rechazado por GameManager ahora cancela la selección XRI y devuelve el avión a la mesa.

**Por qué aparecen cuatro aviones:** la mesa tiene tres páginas de cuatro modelos, incluidos los bloqueados. Usar las flechas del panel o el stick izquierdo. No son solo cuatro aviones disponibles: seis están desbloqueados desde el inicio (Dardo Clásico, Planeador Largo, ZigZag, Flecha Veloz, Delta Racer y Raspanubes).

Los demás se desbloquean al finalizar un intento cuyo récord alcance:

- **300 puntos:** Canard Ace.
- **600 puntos:** Bombardero Pesado, Night Hawk y Cortatormentas.
- **900 puntos:** Halcón de Papel y Fénix Origami.

El requisito usa la mejor puntuación de una partida/campaña, no la suma de intentos separados. Se guarda en PlayerPrefs cuando termina el vuelo; recoger piezas da puntos, no desbloquea un modelo directamente.

**Drones:** Aula Escolar no tiene enemigos. Los mapas siguientes contienen 3, 5, 6 y 8 enemigos, alternando drones, aves y ventiladores. Los drones y aves se mueven y disparan; los ventiladores aplican viento.

Antes, las balas enemigas tenían daño cero, ignoraban al jugador y podían consumirse dentro del emisor. Ahora usan el Projectile existente, ignoran al emisor, reconocen los colliders hijos del avión y aplican un empuje de 1,5 m/s una sola vez por impacto. Por defecto no terminan la partida; el escudo bloquea el empuje. Chocar físicamente con un enemigo conserva la regla de choque existente. Los disparos enemigos pertenecen al nivel y se limpian al volver a mapas.

**Cómo probar:** en Parque, acercarse a un dron y recibir un disparo: el avión debe desviarse y continuar volando. Agarrar y soltar sin movimiento devuelve el avión a la mesa; mover la mano y soltar inicia vuelo. La validación automatizada comprueba impacto físico, ausencia de impacto contra el emisor, impulso único y desbloqueos en 299/300, 599/600 y 899/900 puntos sin guardar progreso de prueba.

## Grupo 3: nombres de mapas

Las capturas confirmaron que los nombres se cortaban. Solo se redujo su fuente de 24 a 20 dentro de las mismas tarjetas; se conservaron tamaños, posiciones, colores, botones y comportamiento.

**Cómo probar:** abrir el menú de cinco mapas y comprobar que se leen completos Parque de las Curvas, Oficina en las Alturas, Ciudad en Miniatura y Bosque Mágico.

## Verificación y límites

**Resultado final del 27/09/2026, 18:08 (hora local): 547 comprobaciones aprobadas, cero errores registrados por la prueba.** Incluye rayo/Trigger XR, agarre rechazado/cancelado/normal, lanzamiento, desbloqueos, impacto y bloqueo de disparos enemigos, cinco vuelos físicos, checkpoints, campaña, Game_Playable e inicio del XR Device Simulator clásico. Los nombres completos de las tarjetas se verificaron también en la captura del menú.

Consultar el resultado más reciente de `Logs/AvionesValidation.json` y las capturas de esta revisión en `Backups/Continuation/Codex_20260927_Screenshots`. Las futuras ejecuciones generan nuevas capturas en Logs. `python Tools/check_headset_setup.py` aprobó las 20 comprobaciones estáticas de configuración de Quest y PC/Link.

La primera ejecución tras ajustar las rutas completó físicamente los cinco mapas con el avión inicial: 7,4; 16,9; 53,4; 44,6 y 58,6 segundos de simulación. Quedó pendiente entonces el arranque del simulador clásico por duplicación del singleton; se corrigió en el grupo 1.

Las capturas de esta revisión y los resultados intermedios se conservaron en `Backups/Continuation/Codex_20260927_Screenshots` y `Backups/Continuation/Codex_20260927_*.json`. Se restauraron los csproj, logs auxiliares, ajustes y layouts regenerados por Unity para evitar cambios ajenos a estas correcciones. El resultado final se mantiene en `Logs/AvionesValidation.json`.

La validación automática usa mandos simulados y no equivale a una prueba humana de comodidad, rendimiento, pérdida de tracking o renderizado estéreo. Falta probar el hardware Quest/Link y generar e instalar un APK. Los comandos de compilación existentes se conservan y no regeneran las escenas.

Para probar con visor, salir de Play y elegir `Aviones de Papel VR > Input VR > Hardware Quest o Link`. Para teclado, abrir Game_Playable; para simulación, elegir uno de los dos modos del menú Input VR antes de Play. No añadir otro simulador manualmente.
