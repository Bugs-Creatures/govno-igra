using System.Collections.Generic;
using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class LampHunter : MonoBehaviour
    {
        private enum Phase
        {
            SelectTarget,
            Approach,
            WindUp,
            PursuePlayer
        }

        [SerializeField] private MonsterMovement movement;
        [SerializeField] private MonsterLampTarget[] lamps;
        [SerializeField] private PlayerLightZones player;
        [SerializeField, Min(0f)] private float startDelaySeconds;
        [SerializeField, Min(0f)] private float breakIntervalSeconds;
        [SerializeField, Min(0f)] private float windUpSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float retrySeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float unreachableRetrySeconds = 2f;

        private readonly List<MonsterLampTarget> candidates = new List<MonsterLampTarget>();
        private readonly Dictionary<MonsterLampTarget, float> retryAfter = new Dictionary<MonsterLampTarget, float>();
        private Phase phase;
        private float nextSelectionAt;
        private float nextBreakAt;
        private float strikeAt;
        private MonsterLampTarget lastBrokenLamp;

        public MonsterLampTarget CurrentTarget { get; private set; }
        public bool IsPursuingPlayer => phase == Phase.PursuePlayer;

        private void OnEnable()
        {
            nextBreakAt = Time.time;
            nextSelectionAt = Time.time + startDelaySeconds;
        }

        private void OnDisable()
        {
            ResetTarget(0f);
            retryAfter.Clear();
        }

        private void Update()
        {
            if (movement == null || !movement.isActiveAndEnabled)
                return;

            if (player != null && player.isActiveAndEnabled && !player.IsInLight)
            {
                if (!IsPursuingPlayer)
                {
                    movement.Stop();
                    CurrentTarget = null;
                    phase = Phase.PursuePlayer;
                }

                movement.ChaseTowards(player.transform.position, Time.deltaTime);
                return;
            }

            if (IsPursuingPlayer)
                ResetTarget(0f);

            if (phase == Phase.SelectTarget)
            {
                if (Time.time >= nextSelectionAt)
                    SelectTarget();
                return;
            }

            if (CurrentTarget == null || !CurrentTarget.IsAvailable)
            {
                ResetTarget(0f);
                return;
            }

            if (phase == Phase.Approach)
            {
                switch (movement.MoveTowards(CurrentTarget.ApproachPosition, Time.deltaTime))
                {
                    case MoveResult.Arrived:
                        movement.Stop();
                        strikeAt = Mathf.Max(Time.time + windUpSeconds, nextBreakAt);
                        phase = Phase.WindUp;
                        break;
                    case MoveResult.Unreachable:
                        retryAfter[CurrentTarget] = Time.time + unreachableRetrySeconds;
                        ResetTarget(0f);
                        break;
                }

                return;
            }

            if (Time.time >= strikeAt)
            {
                MonsterLampTarget lamp = CurrentTarget;
                ResetTarget(retrySeconds);
                if (lamp.TryBreak())
                {
                    nextBreakAt = Time.time + breakIntervalSeconds;
                    lastBrokenLamp = lamp;
                }
            }
        }

        private void SelectTarget()
        {
            candidates.Clear();
            if (lamps != null)
            {
                foreach (MonsterLampTarget lamp in lamps)
                {
                    if (lamp == null || !lamp.IsAvailable || candidates.Contains(lamp))
                        continue;

                    if (retryAfter.TryGetValue(lamp, out float time) && Time.time < time)
                        continue;

                    candidates.Add(lamp);
                }
            }

            if (candidates.Count == 0)
            {
                nextSelectionAt = Time.time + retrySeconds;
                return;
            }

            if (candidates.Count > 1)
                candidates.Remove(lastBrokenLamp);

            CurrentTarget = candidates[Random.Range(0, candidates.Count)];
            phase = Phase.Approach;
        }

        private void ResetTarget(float delay)
        {
            if (movement != null)
                movement.Stop();

            CurrentTarget = null;
            phase = Phase.SelectTarget;
            nextSelectionAt = Time.time + delay;
        }
    }
}
