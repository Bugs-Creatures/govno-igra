using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class PlayerLightZones : MonoBehaviour
    {
        [SerializeField] private LampLightZone[] zones = new LampLightZone[0];

        public bool IsInLight
        {
            get
            {
                Vector3 position = transform.position;
                foreach (LampLightZone zone in zones)
                {
                    if (zone != null && zone.ContainsLight(position))
                        return true;
                }

                return false;
            }
        }
    }
}
