using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(MonsterLightPresentation))]
    public sealed class MonsterJumpscare : MonoBehaviour
    {
        private const float TriggerDistance = 1.65f;
        private const float CameraTurnSpeed = 720f;
        private const float FadeDuration = 0.2f;
        private const float BlackoutDuration = 0.5f;
        private const float RecoveryDuration = 5f;
        private const float MinimumTeleportDistance = 4f;
        private const float ZoomDuration = 0.22f;
        private const float MinimumCloseupFieldOfView = 25f;
        private const float HeadRadius = 0.29f;

        [SerializeField] private FirstPersonController player;
        [SerializeField] private Transform neck;
        [SerializeField] private AudioClip scream;
        [SerializeField] private AudioClip recoveryBreath;

        private NavMeshAgent agent;
        private MonsterLightPresentation presentation;
        private LampInteraction lampInteraction;
        private NightClock nightClock;
        private AudioSource screamSource;
        private CanvasGroup blackout;
        private Coroutine sequence;
        private Transform head;
        private float normalFieldOfView;
        private float zoomElapsed;
        private float resumeMonsterAt;
        private bool agentPoseLocked;
        private bool followAgentPosition;
        private bool followAgentRotation;

        public bool IsHoldingMonster => sequence != null || Time.time < resumeMonsterAt;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            presentation = GetComponent<MonsterLightPresentation>();
            if (player != null)
            {
                lampInteraction = player.GetComponent<LampInteraction>();
                nightClock = player.GetComponent<NightClock>();
            }
            if (neck != null)
                head = neck.Find("head");
        }

        public bool TryBegin(MonsterLampTarget[] teleportTargets)
        {
            if (!isActiveAndEnabled || IsHoldingMonster || player == null || !player.isActiveAndEnabled ||
                player.IsPaused || player.IsCinematicLocked || scream == null || !agent.isOnNavMesh ||
                (transform.position - player.transform.position).sqrMagnitude > TriggerDistance * TriggerDistance ||
                !presentation.CanStartJumpscare(player.ViewPosition, NeckPosition))
                return false;

            followAgentPosition = agent.updatePosition;
            followAgentRotation = agent.updateRotation;
            agentPoseLocked = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.ResetPath();
            agent.isStopped = true;
            Vector3 towardsPlayer = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (towardsPlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(towardsPlayer);
            presentation.SetJumpscareVisibility(true);
            player.SetCinematicLocked(true);
            if (nightClock != null)
                nightClock.OnJumpscare();
            if (lampInteraction != null)
                lampInteraction.BeginJumpscare();
            normalFieldOfView = player.ViewCamera.fieldOfView;
            zoomElapsed = 0f;
            sequence = StartCoroutine(Play(teleportTargets));
            return true;
        }

        private Vector3 NeckPosition => neck != null ? neck.position : transform.position + Vector3.up * 1.8f;
        private Vector3 FacePosition => head != null ? head.position + Vector3.up * 0.08f : NeckPosition + Vector3.up * 0.22f;

        private IEnumerator Play(MonsterLampTarget[] teleportTargets)
        {
            if (screamSource == null)
            {
                screamSource = gameObject.AddComponent<AudioSource>();
                screamSource.playOnAwake = false;
                screamSource.spatialBlend = 0f;
            }

            GameAudioSettings audio = GameAudioSettings.Current;
            screamSource.outputAudioMixerGroup = audio?.AudioOutput;
            screamSource.volume = audio?.JumpscareVolume ?? 1f;
            screamSource.clip = scream;
            screamSource.Play();

            do
            {
                UpdateCloseup();
                yield return null;
            }
            while (screamSource.isPlaying || AudioListener.pause);

            ShowBlackout();
            yield return Fade(0f, 1f, true);
            player.ViewCamera.fieldOfView = normalFieldOfView;
            Teleport(teleportTargets);
            RestoreAgentPose();
            Vector3 lookDirection = player.transform.forward;
            BulbBox box = lampInteraction != null ? lampInteraction.DropJumpscareBox() : null;
            if (box != null)
            {
                Vector3 center = box.transform.TransformPoint(box.GetComponent<BoxCollider>().center);
                Vector3 towardsBox = Vector3.ProjectOnPlane(center - player.ViewPosition, Vector3.up);
                if (towardsBox.sqrMagnitude > 0.01f)
                    lookDirection = towardsBox;
            }
            player.TurnCinematicView(player.ViewPosition + lookDirection, 360f);
            resumeMonsterAt = Time.time + RecoveryDuration;
            presentation.SetJumpscareVisibility(false);
            yield return new WaitForSeconds(BlackoutDuration);

            player.SetCinematicLocked(false);
            if (lampInteraction != null)
                lampInteraction.RestoreReticle();
            player.PlayScaredBreath(recoveryBreath, 0.5f);
            yield return Fade(1f, 0f, false);
            blackout.gameObject.SetActive(false);
            sequence = null;
        }

        private void UpdateCloseup()
        {
            Camera camera = player.ViewCamera;
            Vector3 face = FacePosition;
            player.TurnCinematicView(face, CameraTurnSpeed * Time.deltaTime);
            zoomElapsed += Time.deltaTime;

            float distance = Vector3.Distance(camera.transform.position, face);
            float halfAngle = Mathf.Asin(Mathf.Clamp(HeadRadius / distance, 0f, 0.85f));
            float fitFieldOfView = 2f * Mathf.Atan(Mathf.Tan(halfAngle) / Mathf.Min(1f, camera.aspect)) * Mathf.Rad2Deg;
            float closeup = Mathf.Max(MinimumCloseupFieldOfView, fitFieldOfView);
            camera.fieldOfView = Mathf.Lerp(normalFieldOfView, closeup, Mathf.SmoothStep(0f, 1f, zoomElapsed / ZoomDuration));
        }

        private void Teleport(MonsterLampTarget[] targets)
        {
            if (targets == null)
                return;

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
            Vector3 selected = agent.nextPosition;
            int count = 0;
            foreach (MonsterLampTarget target in targets)
            {
                if (target == null ||
                    !NavMesh.SamplePosition(target.ApproachPosition, out NavMeshHit hit, 1.35f, filter) ||
                    (hit.position - player.transform.position).sqrMagnitude <
                        MinimumTeleportDistance * MinimumTeleportDistance ||
                    !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;

                if (Random.Range(0, ++count) == 0)
                    selected = hit.position;
            }

            if (count > 0 && agent.Warp(selected))
            {
                transform.position = selected;
                agent.isStopped = true;
            }
        }

        private void RestoreAgentPose()
        {
            if (!agentPoseLocked)
                return;

            agentPoseLocked = false;
            if (agent.isOnNavMesh)
                agent.nextPosition = transform.position;
            agent.updatePosition = followAgentPosition;
            agent.updateRotation = followAgentRotation;
        }

        private void ShowBlackout()
        {
            if (blackout == null)
            {
                var overlay = new GameObject("Jumpscare Blackout", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
                overlay.transform.SetParent(player.transform, false);
                var canvas = overlay.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                var image = overlay.GetComponent<UnityEngine.UI.Image>();
                image.color = Color.black;
                image.raycastTarget = false;
                blackout = overlay.GetComponent<CanvasGroup>();
                blackout.blocksRaycasts = false;
            }

            blackout.alpha = 0f;
            blackout.gameObject.SetActive(true);
        }

        private IEnumerator Fade(float from, float to, bool trackNeck)
        {
            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                if (trackNeck)
                    UpdateCloseup();
                blackout.alpha = Mathf.Lerp(from, to, elapsed / FadeDuration);
                yield return null;
                elapsed += trackNeck ? Time.deltaTime : Time.unscaledDeltaTime;
            }

            blackout.alpha = to;
        }

        internal void Cancel()
        {
            resumeMonsterAt = 0f;
            if (sequence == null)
                return;

            StopCoroutine(sequence);
            sequence = null;
            if (screamSource != null)
                screamSource.Stop();
            if (blackout != null)
                blackout.gameObject.SetActive(false);
            if (lampInteraction != null)
                lampInteraction.RestoreReticle();
            if (player != null)
            {
                player.ViewCamera.fieldOfView = normalFieldOfView;
                if (player.IsCinematicLocked)
                    player.SetCinematicLocked(false);
            }
            if (presentation != null)
                presentation.SetJumpscareVisibility(false);
            RestoreAgentPose();
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            if (blackout != null)
                Destroy(blackout.gameObject);
        }
    }
}
