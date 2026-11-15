using System.Collections.Generic;
using TMPro;
using UntilSix.Monster;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(FirstPersonController), typeof(PlayerLightZones))]
public sealed class NightClock : MonoBehaviour
{
    private const int HoursToSurvive = 6;
    private const int MinutesPerHour = 60;
    private const float HudWidth = 252f;
    private const float BarWidth = 156f;
    private static readonly float[] LightSpeeds = { 1f, 0.8f, 0.6f, 0.35f };
    private static readonly Vector2 TopRight = new(1f, 1f);

    [SerializeField] private Canvas hud;
    [SerializeField, Min(0.1f)] private float secondsPerHour = 30f;
    [SerializeField, Min(0.1f)] private float darknessGraceSeconds = 5f;
    [SerializeField, Min(0.1f)] private float lightResetSeconds = 5f;
    [SerializeField, Range(0f, 1f)] private float rewindSpeed = 0.5f;
    [SerializeField, Min(1)] private int brokenLampStopThreshold = 4;

    private FirstPersonController player;
    private PlayerLightZones lightZones;
    private HashSet<CeilingLamp> fixtures;
    private RectTransform panel;
    private TextMeshProUGUI clock;
    private TextMeshProUGUI status;
    private TextMeshProUGUI stopped;
    private RectTransform bar;
    private RectTransform barFill;
    private float progress;
    private float darkness;
    private float continuousLight;
    private float timeScaleBeforeWin;
    private int shownMinute = -1;
    private bool won;
    private bool restarting;

    public bool HasWon => won;
    private bool RewindThresholdReached => darkness >= darknessGraceSeconds;

    private void Awake()
    {
        player = GetComponent<FirstPersonController>();
        lightZones = GetComponent<PlayerLightZones>();
        fixtures = lightZones.CollectFixtures();
        if (hud == null)
            return;

        var digitFont = Resources.Load<TMP_FontAsset>("NightClock/Clock Digits");
        var labelFont = Resources.Load<TMP_FontAsset>("NightClock/Clock Labels");
        panel = CreateRect("Night Clock", hud.transform, new Vector2(-24f, -20f), new Vector2(HudWidth, 112f));
        clock = CreateLabel("Time", Vector2.zero, 36, 32f, digitFont);
        status = CreateLabel("Light Status", new Vector2(0f, -40f), 22, 16f, labelFont);
        stopped = CreateLabel("Broken Lamps Status", new Vector2(0f, -40f), 22, 16f, labelFont);
        bar = CreateRect("Countdown", panel, new Vector2(0f, -67f), new Vector2(BarWidth, 4f));
        AddImage(bar, new Color(0f, 0f, 0f, 0.55f));
        barFill = CreateRect("Remaining", bar, Vector2.zero, new Vector2(BarWidth, 4f));
        barFill.anchorMin = barFill.anchorMax = barFill.pivot = new Vector2(0f, 1f);
        barFill.anchoredPosition = Vector2.zero;
        AddImage(barFill, Color.white);
    }

    private void Update()
    {
        if (won)
        {
            if (!restarting && Keyboard.current?.rKey.wasPressedThisFrame == true)
            {
                restarting = true;
                SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
            }
            return;
        }

        bool showHud = !player.IsPaused && !player.IsCinematicLocked;
        if (panel != null)
            panel.gameObject.SetActive(showHud);
        if (!showHud)
            return;

        bool inLight = lightZones.IsInLight;
        int broken = 0;
        foreach (CeilingLamp fixture in fixtures)
            if (fixture != null && fixture.isActiveAndEnabled && fixture.IsBroken)
                broken++;

        Advance(Time.deltaTime, inLight, broken);
        UpdateHud(inLight, broken >= brokenLampStopThreshold);
    }

    private void Advance(float delta, bool inLight, int broken)
    {
        if (inLight)
        {
            float forwardSeconds = delta;
            if (darkness > 0f)
            {
                continuousLight += delta;
                if (RewindThresholdReached)
                    forwardSeconds = Mathf.Max(0f, continuousLight - lightResetSeconds);

                if (continuousLight >= lightResetSeconds)
                {
                    darkness = 0f;
                    continuousLight = 0f;
                }
            }

            if (broken < brokenLampStopThreshold)
                progress += forwardSeconds * LightSpeeds[Mathf.Min(broken, LightSpeeds.Length - 1)];
            if (progress >= HoursToSurvive * secondsPerHour)
            {
                progress = HoursToSurvive * secondsPerHour;
                won = true;
                player.SetCinematicLocked(true);
                timeScaleBeforeWin = Time.timeScale;
                Time.timeScale = 0f;
            }
            return;
        }

        continuousLight = 0f;
        float rewindSeconds = Mathf.Max(0f, darkness + delta - darknessGraceSeconds);
        darkness = Mathf.Min(darkness + delta, darknessGraceSeconds);
        progress = Mathf.Max(0f, progress - rewindSeconds * rewindSpeed);
    }

    internal void OnJumpscare()
    {
        if (!won)
            progress = Mathf.Max(0f, progress - secondsPerHour);
    }

    private void UpdateHud(bool inLight, bool lampsStopped)
    {
        if (panel == null)
            return;

        int minute = won ? HoursToSurvive * MinutesPerHour :
            Mathf.FloorToInt(progress / secondsPerHour * MinutesPerHour);
        if (shownMinute != minute)
        {
            shownMinute = minute;
            clock.text = $"{minute / MinutesPerHour:00}:{minute % MinutesPerHour:00}";
        }

        bool rewinding = !inLight && RewindThresholdReached;
        clock.color = rewinding && !won ? new Color(1f, 0.55f, 0.49f) : Color.white;
        bool resetting = inLight && RewindThresholdReached;
        status.gameObject.SetActive(won || !inLight || resetting);
        status.text = won ? "Вы пережили ночь" : inLight ? "Оставайтесь на свету" : "В темноте опасно";

        bar.gameObject.SetActive(!won && ((!inLight && !rewinding) || resetting));
        if (bar.gameObject.activeSelf)
        {
            float remaining = resetting ? 1f - continuousLight / lightResetSeconds : 1f - darkness / darknessGraceSeconds;
            barFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BarWidth * Mathf.Clamp01(remaining));
        }

        stopped.gameObject.SetActive(won || lampsStopped);
        stopped.text = won ? "R — начать заново" : "Время стоит, чините лампы";
        stopped.rectTransform.anchoredPosition = new Vector2(0f,
            won || !inLight || resetting ? (bar.gameObject.activeSelf ? -76f : -62f) : -40f);
    }

    private void OnDisable()
    {
        if (panel != null)
            panel.gameObject.SetActive(false);
        if (!won)
            return;

        Time.timeScale = timeScaleBeforeWin;
        if (player != null)
            player.SetCinematicLocked(false);
    }

    private void OnDestroy()
    {
        if (panel != null)
            Destroy(panel.gameObject);
    }

    private TextMeshProUGUI CreateLabel(string objectName, Vector2 position, int height, float fontSize, TMP_FontAsset font)
    {
        RectTransform rect = CreateRect(objectName, panel, position, new Vector2(HudWidth, height));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        var element = new GameObject(objectName, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rect = (RectTransform)element.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = TopRight;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void AddImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
