# Aviones de Papel VR

Unity 6000.5.6f1. Prototipo de agarre, lanzamiento vectorial y planeo arcade en primera persona.

## Abrir y jugar

Abre `Assets/AvionesPapelVR/Scenes/Game_VR_Oculus.unity` y pulsa Play. No necesitas regenerar escenas para jugar.
En `Aviones de Papel VR > Input VR` elige **Hardware Quest o Link** o **Simulador (sin visor)** antes de Play.
Hardware es el valor predeterminado. El simulador es una elección explícita del editor; no se instancia en Android.
Si inicia una pantalla XR física, el monitor desactiva el simulador. Reinicia Play para cambiar de modo.
La instanciación automática de XRI está desactivada deliberadamente para evitar dos loaders compitiendo.

Para teclado abre `Game_Playable.unity`. Enter confirma, flechas seleccionan, Espacio carga y suelta el lanzamiento,
W/S cabecea, A/D alabea, ratón izquierdo dispara, F misil y G bomba. Escape libera el cursor.

## Controles VR

| Contexto | Entrada | Acción |
|---|---|---|
| Menú/resultados | Trigger, A o Enter | Empezar/reintentar |
| Mesa | Stick izquierdo horizontal | Consultar avión y cambiar página |
| Mesa | Grip junto al avión | Agarrar un modelo desbloqueado |
| Agarre | Mover la mano y soltar Grip | Lanzar con la velocidad suavizada de XRI |
| Agarre | Soltar quieto o cancelar | Devolver a la mesa y recuperar selección |
| Vuelo | Inclinar mando derecho | Pitch y roll; modo predeterminado |
| Vuelo | X | Cambiar entre mando, cabeza y sticks |
| Vuelo | Y | Centrar asiento y calibrar posición neutra del control |
| Vuelo | Pulsar stick izquierdo | Activar/desactivar seguimiento de yaw |
| Vuelo con sticks | Derecho Y / izquierdo X / derecho X | Pitch / roll / yaw |
| Vuelo | Trigger derecho / A / B | Bala / misil / bomba |

La locomoción del rig se bloquea durante selección, agarre y vuelo. Al terminar se restaura su estado anterior.
El control por inclinación toma una posición neutra al activarse; usa Y con la mano relajada si hace falta.
Si se pierde el tracking del mando se usan los sticks; al recuperarlo se vuelve a calibrar.
Los aros azules dan **100 puntos una sola vez al atravesar su plano**. Los dorados activan turbo.
Los modelos bloqueados muestran el récord necesario; el mejor resultado se guarda al finalizar el vuelo.

## Confort y cámara

La cámara entra al `PilotSeat` compensando la altura inicial del visor, conservando después el movimiento físico de la cabeza.
El horizonte no hereda pitch/roll. El yaw permanece estable por defecto; se puede activar desde el stick izquierdo.
`VrRigFollower.positionLerp = 0` mantiene el asiento fijo respecto al avión; valores positivos activan suavizado exponencial,
que introduce retraso respecto a la cabina. Entrada, salida y recentrado usan un breve fundido (`blinkTransitions`).
El recentrado admite uso sentado. Estas opciones requieren evaluación de confort con personas en visor real.

## Datos efectivos de gameplay

- `PlaneDefinition`: velocidad máxima, pérdida, sensibilidad, giro, gravedad, resistencia, sustentación, boost y umbral de desbloqueo.
- `GameManager.weapons`: cuatro `WeaponDefinition`; bala/misil/bomba leen daño, velocidad, prefab y cooldown. El avión aplica su multiplicador de potencia al daño.
- `GameManager.powerUps`: tres `PowerUpDefinition`; duración de turbo, escudo y triple disparo. `boostAcceleration` mantiene el impulso del turbo durante su duración, además del impulso inicial del avión.
- Tanto las escenas existentes como los builders tienen referencias explícitas a estos assets; no es necesario moverlos a Resources.

## Regeneración segura

Los builders **no se ejecutan al importar**. Los menús de construcción son regeneradores explícitos,
no accesos necesarios para jugar. Antes de reconstruir ofrecen guardar escenas modificadas y copian los assets a
`Backups/Builders/<fecha>`. En batch rechazan escenas dirty. Los datos existentes de aviones, niveles, armas y power-ups se conservan.
Al finalizar `BuildAll` guarda también los ScriptableObjects. Una regeneración sigue reemplazando escenas/prefabs:
recupera personalizaciones visuales desde el backup si lo necesitas.

El generador requiere Python 3.10+ y Pillow (`python -m pip install Pillow`).

```powershell
python Tools/generate_avion_assets.py --output work/GeneratedPreview --models-only
python Tools/generate_avion_assets.py --models-only --overwrite
python Tools/generate_avion_assets.py --overwrite
python Tools/test_geometry.py
```

Sin `--overwrite` no reemplaza una carpeta Models existente. Con esa opción copia antes todo el directorio de salida a
`Backups/Generator/<fecha>`. `--models-only` conserva texturas y audio. Los `.meta` existentes se respetan siempre.
Las alas tienen una envolvente cerrada; las piezas decorativas de papel son de doble cara geométrica.
Los cilindros y conos tienen orientación exterior; cada malla se comprueba para evitar triángulos degenerados.

## Validación

Menú `Aviones de Papel VR > Validar circuito (Play Mode)`. Rechaza escenas sin guardar, conserva la configuración de escenas
y no persiste récords del test. El simulador se activa solo durante esa prueba; se necesita no tener un visor XR activo.
Resultado: `Temp/AvionesValidation.json`. Cubre selección, cancelación XRI, devolución quieta, lanzamiento con dirección,
cabina, boost, referencias de balance, recentrado, aro sin doble puntuación, fin y reinicio.

Para automatizar en una copia aislada del proyecto:

```powershell
Unity.exe -batchmode -nographics -projectPath <copia> -executeMethod AvionesPapelVR.Editor.GameplayValidation.RunBatch -logFile <log>
```

El runner batch desactiva XR de Standalone en esa copia para usar dispositivos simulados; Android no cambia.
El test termina el editor con código 0 si pasa o 1 si falla. No añadir `-quit`, porque terminaría antes del Play Mode.

Falta validar en Quest: instalación de APK, FPS sostenidos, térmico, seguimiento real de mandos y confort de cámara.
Las pruebas automáticas no equivalen a una sesión de juego en hardware ni a una certificación para distribución.
