# Preparación para visor Meta/Oculus

Estado del 24/09/2026: **cambios preparados, sin build ni prueba en visor confirmados**.

## Corregido

- OpenXR vuelve a arrancar en PC/Link. La opción `Input VR > Hardware Quest o Link` ahora activa XR para PC y Android y desactiva las preferencias de simulación.
- El simulador ya no se instancia automáticamente. Se crea solo al elegir un modo de simulación o ejecutar la validación. Esto evita introducir mandos simulados durante una prueba con visor real.
- El gatillo se reserva a los botones apuntados. Antes, el atajo global podía empezar el mapa 1 o reintentar antes de que XRI resolviera el clic. A sigue confirmando la acción principal.
- El botón Menú del mando izquierdo vuelve a mapas desde selección, lanzamiento, resultados o vuelo. Durante vuelo, abandona el intento.
- Se conservan las correcciones anteriores de mesas, retorno a mapas y ratón sin visor.
- La prueba de Unity restaura la configuración de arranque XR de PC al terminar normalmente.
- Se añadieron comandos de compilación que usan únicamente la escena real `Game_VR_Oculus`, sin regenerarla ni incluir las escenas de demostración. Comprueban plataforma, OpenXR y requisitos de Quest antes de compilar.

## Verificado aquí

- `python Tools/check_headset_setup.py`: **20 comprobaciones estáticas aprobadas**, incluyendo referencias de OpenXR y Touch para Android/PC, soporte Meta Quest, IL2CPP, ARM64, Input System, arranque XR y referencias principales de la escena.
- Análisis de sintaxis Roslyn de **44 archivos C#**, con símbolos de Editor y de Android: **0 errores sintácticos**. No es una compilación de Unity ni comprueba todas las referencias de tipos.
- `git diff --check`: sin errores de espacios.
- El nuevo intento de compilación en Unity termina con **código 198**, antes de cargar el proyecto: `No valid Unity Editor license found. Please activate your license.` Véase `Logs/HeadsetBuild.log`.
- Unity 6000.5.6f1 no tiene instalado Android Build Support. No se generó ningún APK ni ejecutable nuevo.

No se han confirmado el apuntado XR, los clics, la comodidad ni los FPS en un dispositivo físico. El fallo histórico del rayo en `Logs/AvionesValidation.json` requiere volver a ejecutar la prueba; ese archivo no es un resultado actual.

## Para terminar en este equipo

1. En Unity Hub, iniciar sesión y activar una licencia válida para Unity 6000.5.6f1.
2. Para instalar dentro de Quest: en esa versión del Editor, `Add modules`, instalar **Android Build Support**, **Android SDK & NDK Tools** y **OpenJDK**. Revisar y aceptar los términos que muestre el instalador. Para PC por Link no hacen falta esos módulos Android.
3. Abrir el proyecto, esperar a que importe y comprobar la consola. Ejecutar `Aviones de Papel VR > Validar circuito (Play Mode)` fuera de Play, con las escenas guardadas. El resultado nuevo queda en `Logs/AvionesValidation.json`.
4. Elegir `Input VR > Hardware Quest o Link`. Para probar con PC, conectar el visor mediante Meta Quest Link y configurar Meta Quest Link como runtime OpenXR activo. Abrir `Assets/AvionesPapelVR/Scenes/Game_VR_Oculus.unity` y pulsar Play.
5. Para APK, cambiar a Android/Meta Quest en `File > Build Profiles`; ejecutar `Aviones de Papel VR > Compilar visor > Quest - APK`. Salida prevista: `Builds/Quest/PaperFlight.apk`.
6. Para PC, cambiar a Windows y ejecutar `Compilar visor > PC - Quest Link o Rift`. Salida prevista: `Builds/Link/PaperFlight.exe`; conservar toda su carpeta junto al ejecutable.

La instalación de un APK de desarrollo requiere el modo desarrollador de Quest y autorizar la conexión USB del PC en el visor. Meta Horizon es el nombre de la aplicación; confirmar el modelo del visor antes de declarar compatibilidad. La configuración existente incluye perfiles Touch, Touch Plus y Touch Pro; no se afirma compatibilidad probada con cada modelo.

## Prueba de aceptación con los mandos

Apuntar y pulsar/soltar Trigger en cada mapa; comprobar el nombre del nivel. Recorrer las mesas 1 → 2 → 3 → 1 y en sentido inverso. Agarrar con Grip, soltar sin impulso y después lanzar. Tras chocar, reintentar y comprobar que se conserva el mapa. Probar el botón completo de volver a mapas desde resultados y desde el taller, y el Menú izquierdo durante el vuelo. Completar un mapa y avanzar al siguiente. Comprobar ambos ojos y fluidez, además de quitarse/ponerse el visor y volver del menú del sistema.

Referencias: [configuración oficial de Meta Quest](https://docs.unity.com/en-us/engine/6000.3/manual/xr/configuring-project-for/meta-quest-develop/build-profile), [módulo de UI XR](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.5/manual/xr-ui-input-module.html).
