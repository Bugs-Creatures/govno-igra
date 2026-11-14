#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UntilSix.Monster;

public sealed class VerifyRecoveryBoxes : MonoBehaviour
{
    [MenuItem("Tools/Verify Recovery Bulb Boxes")]
    private static void Run()
    {
        if (EditorApplication.isPlaying)
            new GameObject("Test Recovery Bulb Boxes").AddComponent<VerifyRecoveryBoxes>();
    }

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private IEnumerator Start()
    {
        const string log = "Temp/RecoveryBoxCheck.txt";
        File.WriteAllText(log, "");
        var hunter = FindAnyObjectByType<LampHunter>();
        var scare = hunter.GetComponent<MonsterJumpscare>();
        var agent = hunter.GetComponent<NavMeshAgent>();
        var targets = (MonsterLampTarget[])typeof(LampHunter).GetField("lamps", PrivateInstance).GetValue(hunter);
        var player = FindAnyObjectByType<FirstPersonController>();
        var interaction = player.GetComponent<LampInteraction>();
        var reticle = FindAnyObjectByType<InteractionReticle>();
        var original = FindAnyObjectByType<BulbBox>();
        var hall = FindObjectsByType<LampLightZone>().Single(z => z.name == "Lower Hall Zone");
        var lamp = (CeilingLamp)new SerializedObject(hall).FindProperty("fixture").objectReferenceValue;
        lamp.TryBreak();
        var controller = player.GetComponent<CharacterController>();
        var operate = typeof(LampInteraction).GetMethod("UpdateInteraction", PrivateInstance);
        var cancel = typeof(MonsterJumpscare).GetMethod("Cancel", PrivateInstance);
        for (int scenario = 0; scenario < 3; scenario++)
        {
            Vector3 p = new(.05f, .04f, 8.93f);
            controller.enabled = false;
            player.transform.SetPositionAndRotation(p + new Vector3(0f, .08f, .92f), Quaternion.identity);
            controller.enabled = true;
            agent.Warp(p);
            if (scenario > 0)
                operate.Invoke(interaction, new object[] {original.GetComponent<BoxCollider>(), null, true, false, false});
            bool preHeld = original.IsHeld;
            if (scenario == 2)
            {
                while (original.TryUseBulb()) {}
            }

            int before = FindObjectsByType<BulbBox>().Length;
            bool started = scare.TryBegin(targets);
            bool hidden = !reticle.enabled;
            bool dropped = original != null && !original.IsHeld && !original.GetComponent<Rigidbody>().isKinematic;
            if (!started)
            {
                File.AppendAllText(log, $"scenario={scenario} failed to start.\n");
                yield break;
            }

            bool spawnedDuringBlack = false;
            bool emptiedDuringBlack = false;
            while (player.IsCinematicLocked)
            {
                var overlay = player.GetComponentInChildren<CanvasGroup>();
                if (overlay != null && overlay.alpha >= .999f)
                {
                    spawnedDuringBlack |= FindObjectsByType<BulbBox>().Any(b => b != original && b.name.StartsWith("Recovery Bulb Box"));
                    emptiedDuringBlack |= original == null;
                }
                yield return null;
            }
            var boxes = FindObjectsByType<BulbBox>();
            var spawn = boxes.SingleOrDefault(b => b != original && b.name.StartsWith("Recovery Bulb Box"));
            int bulbCount = spawn == null ? -1 : (int)typeof(BulbBox).GetField("remaining", PrivateInstance).GetValue(spawn);
            float distance = spawn == null ? -1f : Vector3.Distance(spawn.transform.position, player.transform.position);
            bool canUseOne = false;
            if (spawn != null)
            {
                operate.Invoke(interaction, new object[] {spawn.GetComponent<BoxCollider>(), null, true, false, false});
                canUseOne = spawn.TryUseBulb() && !spawn.TryUseBulb();
                Destroy(spawn.gameObject);
            }
            File.AppendAllText(log,
                $"scenario={scenario} preHeld={preHeld} hidden={hidden} dropped={dropped} new={spawn != null} blackSpawn={spawnedDuringBlack} blackEmptyRemoved={emptiedDuringBlack} bulbs={bulbCount} distance={distance:F2} oneUsable={canUseOne} reticleRestored={reticle.enabled} total={before}->{boxes.Length}\n");
            if (spawn != null)
                typeof(LampInteraction).GetField("heldBox", PrivateInstance).SetValue(interaction, null);
            yield return new WaitForSeconds(.25f);
            cancel.Invoke(scare, null);
        }
        Destroy(gameObject);
    }
}
#endif
