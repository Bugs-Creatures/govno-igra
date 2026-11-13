using System;
using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class LampTarget : MonsterLampTarget
    {
        [SerializeField] private Transform approachPoint;
        [SerializeField] private bool isBroken;

        public bool IsBroken => isBroken;
        public override bool IsAvailable => isActiveAndEnabled && !isBroken && approachPoint != null;
        public override Vector3 ApproachPosition => approachPoint.position;

        public event Action<LampTarget> StateChanged;

        public override bool TryBreak()
        {
            if (!IsAvailable)
                return false;

            isBroken = true;
            StateChanged?.Invoke(this);
            return true;
        }

        public bool TryRepair()
        {
            if (!isBroken)
                return false;

            isBroken = false;
            StateChanged?.Invoke(this);
            return true;
        }
    }
}
