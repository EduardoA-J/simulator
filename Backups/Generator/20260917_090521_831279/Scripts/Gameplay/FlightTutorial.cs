using UnityEngine;

namespace AvionesPapelVR
{
    public class FlightTutorial : MonoBehaviour
    {
        public enum Step { Grab, Throw, Control, Ring, Boost, Dodge, Land, Complete }
        public Step Current { get; private set; }
        public bool EnabledForRun { get; private set; }
        float _pitchTime, _rollTime;
        static readonly string[] Instructions =
        {
            "1/7 Acerca un mando al avion y manten Grip para agarrarlo.",
            "2/7 Mueve la mano hacia delante y suelta Grip para lanzarlo. Soltar quieto devuelve el avion a la mesa.",
            "3/7 Inclina el mando: cabeceo y alabeo. X cambia mando/cabeza/sticks; Y centra. En teclado: W/S y A/D.",
            "4/7 Atraviesa un aro azul por el centro: +100 puntos.",
            "5/7 Atraviesa un aro dorado para recibir un impulso.",
            "6/7 Pasa al lado del obstaculo sin chocar.",
            "7/7 Baja suavemente hasta el suelo, con alas niveladas, para aterrizar."
        };
        public string Instruction => EnabledForRun && Current < Step.Complete ? Instructions[(int)Current] : "";

        public void Begin()
        {
            if (PlayerPrefs.GetInt("AvionesPapelVR.TutorialComplete", 0) == 1) { EnabledForRun = false; return; }
            Current = Step.Grab;
            _pitchTime = _rollTime = 0f;
            EnabledForRun = true;
        }

        public void Record(Step action)
        {
            if (!EnabledForRun || Current != action) return;
            Current++;
            if (Current == Step.Complete)
            {
                EnabledForRun = false;
                if (GameManager.Instance != null && GameManager.Instance.persistProgress)
                {
                    PlayerPrefs.SetInt("AvionesPapelVR.TutorialComplete", 1);
                    PlayerPrefs.Save();
                }
            }
        }

        public void ObserveControls(float pitch, float roll)
        {
            if (!EnabledForRun || Current != Step.Control) return;
            if (Mathf.Abs(pitch) > 0.2f) _pitchTime += Time.deltaTime;
            if (Mathf.Abs(roll) > 0.2f) _rollTime += Time.deltaTime;
            if (_pitchTime >= 0.15f && _rollTime >= 0.15f) Record(Step.Control);
        }
    }
}
