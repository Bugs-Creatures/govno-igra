using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class PauseMenu
{
    [System.Serializable]
    private sealed class TutorialCard
    {
        public Texture2D image;
        public string title;
        [TextArea(2, 5)] public string body;
    }

    [Header("Start of game")]
    [SerializeField] private TutorialCard[] tutorialCards;

    private GameObject tutorialOverlay;
    private UnityEngine.UI.RawImage tutorialImage;
    private UnityEngine.UI.AspectRatioFitter tutorialImageFitter;
    private TextMeshProUGUI tutorialTitle;
    private TextMeshProUGUI tutorialBody;
    private TextMeshProUGUI tutorialCounter;
    private TextMeshProUGUI tutorialNextLabel;
    private int tutorialPage;

    private void Start()
    {
        if (tutorialCards != null && tutorialCards.Length > 0)
            OpenTutorial();
    }

    private void OpenTutorial()
    {
        const float ContentWidth = 744f;
        tutorialOverlay = CreateOverlay("Tutorial", new Vector2(826f, 610f), out var panel);

        tutorialTitle = CreateText("", panel, 30f, TextAlignmentOptions.MidlineLeft);
        Place(tutorialTitle.rectTransform, new Vector2(0f, 252f), new Vector2(ContentWidth, 45f));
        AddDivider(panel, 218f, ContentWidth);

        var frame = CreateImage("Screenshot Frame", panel, FrameColor);
        frame.raycastTarget = false;
        Place(frame.rectTransform, new Vector2(0f, 41f), new Vector2(ContentWidth, 326f));
        var viewport = CreateImage("Screenshot Background", frame.transform, new Color(0.025f, 0.025f, 0.026f));
        viewport.raycastTarget = false;
        viewport.rectTransform.anchorMin = Vector2.zero;
        viewport.rectTransform.anchorMax = Vector2.one;
        viewport.rectTransform.offsetMin = Vector2.one;
        viewport.rectTransform.offsetMax = -Vector2.one;

        var picture = new GameObject("Screenshot", typeof(RectTransform), typeof(UnityEngine.UI.RawImage),
            typeof(UnityEngine.UI.AspectRatioFitter));
        picture.transform.SetParent(viewport.transform, false);
        tutorialImage = picture.GetComponent<UnityEngine.UI.RawImage>();
        tutorialImage.raycastTarget = false;
        tutorialImageFitter = picture.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        tutorialImageFitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;

        tutorialBody = CreateText("", panel, 21f, TextAlignmentOptions.TopLeft);
        tutorialBody.textWrappingMode = TextWrappingModes.Normal;
        Place(tutorialBody.rectTransform, new Vector2(0f, -182f), new Vector2(ContentWidth, 82f));
        AddDivider(panel, -231f, ContentWidth);

        tutorialCounter = CreateText("", panel, 16f, TextAlignmentOptions.MidlineLeft);
        tutorialCounter.color = new Color(0.73f, 0.72f, 0.69f);
        Place(tutorialCounter.rectTransform, new Vector2(-312f, -265f), new Vector2(120f, 32f));
        var next = CreateTextButton(panel, "Дальше", new Vector2(308f, -265f));
        tutorialNextLabel = next.GetComponent<TextMeshProUGUI>();
        tutorialNextLabel.alignment = TextAlignmentOptions.MidlineRight;
        var nextColors = next.colors;
        nextColors.highlightedColor = Color.white;
        nextColors.selectedColor = Color.white;
        nextColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        next.colors = nextColors;
        next.onClick.AddListener(AdvanceTutorial);

        PauseGameplay();
        ShowTutorialPage();
        tutorialOverlay.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(next.gameObject);
    }

    private void AdvanceTutorial()
    {
        tutorialPage++;
        if (tutorialPage >= tutorialCards.Length)
            Close();
        else
            ShowTutorialPage();
    }

    private void ShowTutorialPage()
    {
        TutorialCard card = tutorialCards[tutorialPage];
        tutorialTitle.text = card.title;
        tutorialBody.text = card.body;
        tutorialImage.texture = card.image;
        if (card.image != null)
            tutorialImageFitter.aspectRatio = (float)card.image.width / card.image.height;
        tutorialCounter.text = $"{tutorialPage + 1} / {tutorialCards.Length}";
        bool lastPage = tutorialPage == tutorialCards.Length - 1;
        tutorialNextLabel.text = lastPage ? "Начать игру" : "Дальше";
        tutorialNextLabel.color = lastPage ? FocusColor : PaperColor;
    }
}
