using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class PlayerLightZones : MonoBehaviour
    {
        [SerializeField] private LampLightZone[] zones = new LampLightZone[0];

        public bool IsInLight => IsLitAt(transform.position);

        public bool IsLitAt(Vector3 position)
        {
            foreach (LampLightZone zone in zones)
            {
                if (zone != null && zone.ContainsLight(position))
                    return true;
            }

            return false;
        }
    }
}
