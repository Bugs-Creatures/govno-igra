using System.Collections.Generic;
using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class PlayerLightZones : MonoBehaviour
    {
        [SerializeField] private LampLightZone[] zones = new LampLightZone[0];

        private Vector3 ZonePosition => transform.position + Vector3.up * 0.25f;

        public bool IsInLight => IsLitAt(ZonePosition);

        internal HashSet<CeilingLamp> CollectFixtures()
        {
            var fixtures = new HashSet<CeilingLamp>();
            foreach (LampLightZone zone in zones)
                if (zone != null && zone.Fixture != null)
                    fixtures.Add(zone.Fixture);
            return fixtures;
        }

        public void FindBrokenLamps(HashSet<CeilingLamp> lamps)
        {
            lamps.Clear();
            Vector3 position = ZonePosition;
            foreach (LampLightZone zone in zones)
            {
                if (zone == null || zone.Fixture == null || !zone.Fixture.isActiveAndEnabled ||
                    !zone.Fixture.IsBroken || !zone.ContainsPosition(position))
                    continue;

                lamps.Add(zone.Fixture);
            }
        }

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
