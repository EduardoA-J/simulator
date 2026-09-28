using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AvionesPapelVR
{
    /// <summary>
    /// Grab XRI de un avión de mesa. Sólo mueve el Transform mientras está sujeto (Instantaneous).
    /// Nunca escribe velocidad en el Rigidbody: el avión de mesa es cinemático en todo su ciclo y
    /// <see cref="VrGrabPlane"/> es la única fuente de verdad de su estado y de la velocidad de lanzamiento.
    /// </summary>
    public class ThrowGrabInteractable : XRGrabInteractable
    {
        protected override void Awake()
        {
            base.Awake();
            ApplyTablePlaneSettings();
        }

        public void ApplyTablePlaneSettings()
        {
            movementType = MovementType.Instantaneous;
            throwOnDetach = false;
            retainTransformParent = true;
            attachEaseInTime = 0f;
            trackPosition = true;
            trackRotation = true;
            selectMode = InteractableSelectMode.Single;
        }
    }
}
