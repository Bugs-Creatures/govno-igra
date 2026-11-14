using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(Animator))]
    public sealed class MonsterAnimation : MonoBehaviour
    {
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private static readonly int DoorBend = Animator.StringToHash("DoorBend");
        private static readonly int WalkRate = Animator.StringToHash("WalkRate");

        [SerializeField] private NavMeshMonsterMovement movement;
        [SerializeField] private MonsterLightPresentation presentation;
        [SerializeField] private AnimationClip bentWalk;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        private Animator animator;
        private bool moving;
        private readonly float[] lastStepAt = { float.NegativeInfinity, float.NegativeInfinity };

        private void Awake() => animator = GetComponent<Animator>();

        private void Update()
        {
            float speed = movement.TravelSpeed;
            moving = speed > 0.08f;
            animator.SetBool(IsMoving, moving);
            animator.SetFloat(DoorBend, movement.CrouchAmount);
            animator.SetFloat(WalkRate, Mathf.Max(0.15f, speed / movement.WalkSpeed));
        }

        public void OnMonsterStep(AnimationEvent step)
        {
            if (!moving || !step.isFiredByAnimator || step.intParameter < 0 || step.intParameter > 1)
                return;

            // Both clips can emit events during a blend. Use the dominant gait.
            bool bentIsDominant = movement.CrouchAmount >= 0.5f;
            if ((step.animatorClipInfo.clip == bentWalk) != bentIsDominant)
                return;

            int foot = step.intParameter;
            if (Time.time - lastStepAt[foot] < 0.10f)
                return;

            lastStepAt[foot] = Time.time;
            presentation.PlayFootstep(foot == 0 ? leftFoot : rightFoot, foot == 0);
        }
    }
}
