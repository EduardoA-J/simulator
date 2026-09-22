using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AvionesPapelVR
{
    // Capture the XRI smoothed throw in Late phase, before the next physics step.
    public class ThrowGrabInteractable : XRGrabInteractable
    {
        public Vector3 ReleaseVelocity { get; private set; }
        public bool HasRelease { get; private set; }
        public void ResetRelease() => HasRelease = false;
        protected override void Detach()
        {
            base.Detach();
            ReleaseVelocity = GetComponent<Rigidbody>().linearVelocity;
            HasRelease = true;
        }
    }
}
