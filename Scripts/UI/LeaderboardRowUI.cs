using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRowUI : MonoBehaviour
{
    public Image BackgroundImage;
    public GameObject NormalBackgroundObject;
    public GameObject HighlightBackgroundObject;
    public Text RankText;
    public Image RankIcon;
    public Image AvatarImage;
    public Text FullNameText;
    public Text TrophiesText;
    public Image TrophyIcon;
    public Sprite HighlightBackgroundSprite;

    private Sprite defaultBackgroundSprite;
    private bool defaultNormalObjectActive;
    private bool defaultHighlightObjectActive;

    private void Awake()
    {
        if (BackgroundImage == null) BackgroundImage = GetComponent<Image>();
        if (BackgroundImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            float bestArea = -1f;
            Image best = null;
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                if (img == null) continue;
                if (img == RankIcon || img == AvatarImage || img == TrophyIcon) continue;
                var rt = img.rectTransform;
                if (rt == null) continue;
                var rect = rt.rect;
                float area = rect.width * rect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = img;
                }
            }
            BackgroundImage = best;
        }
        if (BackgroundImage != null) defaultBackgroundSprite = BackgroundImage.sprite;
        if (NormalBackgroundObject != null) defaultNormalObjectActive = NormalBackgroundObject.activeSelf;
        if (HighlightBackgroundObject != null) defaultHighlightObjectActive = HighlightBackgroundObject.activeSelf;
    }

    public void SetData(int rank, string fullName, int trophies)
    {
        if (RankText != null) RankText.text = rank.ToString();
        if (FullNameText != null) FullNameText.text = string.IsNullOrEmpty(fullName) ? "-" : fullName;
        if (TrophiesText != null) TrophiesText.text = trophies >= 0 ? trophies.ToString() : "0";
    }

    public void SetHighlighted(bool highlighted)
    {
        if (NormalBackgroundObject != null && HighlightBackgroundObject != null)
        {
            NormalBackgroundObject.SetActive(highlighted ? false : defaultNormalObjectActive);
            HighlightBackgroundObject.SetActive(highlighted ? true : defaultHighlightObjectActive);
            return;
        }

        if (BackgroundImage == null) return;
        if (highlighted)
        {
            if (HighlightBackgroundSprite != null) BackgroundImage.sprite = HighlightBackgroundSprite;
        }
        else
        {
            if (defaultBackgroundSprite != null) BackgroundImage.sprite = defaultBackgroundSprite;
        }
    }
}
