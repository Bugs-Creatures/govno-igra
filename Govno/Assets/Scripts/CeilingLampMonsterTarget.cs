using UnityEngine;
using UntilSix.Monster;

[DisallowMultipleComponent]
[RequireComponent(typeof(CeilingLamp))]
public sealed class CeilingLampMonsterTarget : MonsterLampTarget
{
    [SerializeField] private Transform approachPoint;

    private CeilingLamp lamp;

    public override bool IsAvailable =>
        isActiveAndEnabled && approachPoint != null && lamp != null &&
        lamp.isActiveAndEnabled && !lamp.IsBroken;

    public override Vector3 ApproachPosition => approachPoint.position;

    private void Awake()
    {
        lamp = GetComponent<CeilingLamp>();
    }

    public override bool TryBreak() => IsAvailable && lamp.TryBreak();
}
