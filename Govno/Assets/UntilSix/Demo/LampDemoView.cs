using UnityEngine;

namespace UntilSix.Monster.Demo
{
    [DisallowMultipleComponent]
    public sealed class LampDemoView : MonoBehaviour
    {
        [SerializeField] private LampTarget lamp;
        [SerializeField] private GameObject intactPanel;
        [SerializeField] private GameObject brokenPanel;

        private void OnEnable()
        {
            if (lamp == null)
                return;

            lamp.StateChanged += Refresh;
            Refresh(lamp);
        }

        private void OnDisable()
        {
            if (lamp != null)
                lamp.StateChanged -= Refresh;
        }

        private void Refresh(LampTarget target)
        {
            intactPanel.SetActive(!target.IsBroken);
            brokenPanel.SetActive(target.IsBroken);
        }
    }
}
