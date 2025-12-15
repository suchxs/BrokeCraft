using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple overlay to show chunk preload progress.
/// </summary>
public class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text tmpLabel;
    [SerializeField] private Text uiLabel;

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        SetProgress(0f);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void SetProgress(float value)
    {
        float clamped = Mathf.Clamp01(value);
        string text = "Optimizing chunks...";
        if (tmpLabel != null)
        {
            tmpLabel.SetText(text);
            tmpLabel.ForceMeshUpdate();
        }
        if (uiLabel != null)
        {
            uiLabel.text = text;
        }
    }
}
