using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class BlockFlowLogoAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RectTransform logoRoot;        // tüm logoyu yukarı/aşağı float'layacak
    [SerializeField] Image[] blocks = new Image[4]; // 0:TL 1:TR 2:BL 3:BR — sırayı koru
    [SerializeField] Image[] glows  = new Image[4]; // glow sprites (matched to blocks)
    [SerializeField] TextMeshProUGUI logoText;

    [Header("Float Settings")]
    [SerializeField] float floatAmount   = 6f;
    [SerializeField] float floatDuration = 2f;

    [Header("Block Glow Pulse")]
    [SerializeField] float glowDelayBetween = 0.4f; // her bloğun glow'u arasındaki delay
    [SerializeField] float glowDuration     = 1.2f; // tek glow'un toplam süresi (in+out)
    [SerializeField] float glowPeakAlpha    = 0.85f;

    [Header("Text Breathing")]
    [SerializeField] Color textBaseColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] Color textPeakColor = new Color(1.4f, 1.4f, 1.6f, 1f); // HDR-ish
    [SerializeField] float textBreathDuration = 1.5f;

    [Header("Glitch Shake")]
    [SerializeField] float glitchInterval = 6f;       // every X seconds
    [SerializeField] float glitchDuration = 0.2f;
    [SerializeField] float glitchStrength = 3f;

    void Start()
    {
        // Tüm glow'ları başlangıçta gizle
        foreach (var g in glows)
        {
            if (g != null)
            {
                Color c = g.color;
                c.a = 0f;
                g.color = c;
            }
        }

        StartFloat();
        StartBlockGlows();
        StartTextBreathing();
        StartGlitchLoop();
    }

    // 1) Tüm logo yumuşak float
    void StartFloat()
    {
        Vector2 start = logoRoot.anchoredPosition;
        logoRoot.DOAnchorPosY(start.y + floatAmount, floatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
    }

    // 2) Bloklar sırayla glow yapsın
    void StartBlockGlows()
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            int idx = i;
            float startDelay = idx * glowDelayBetween;

            // Sequence: alpha 0 → peak → 0, sonsuz loop
            Sequence s = DOTween.Sequence();
            s.AppendInterval(startDelay);
            s.Append(glows[idx].DOFade(glowPeakAlpha, glowDuration * 0.5f).SetEase(Ease.OutQuad));
            s.Append(glows[idx].DOFade(0f, glowDuration * 0.5f).SetEase(Ease.InQuad));
            s.AppendInterval(glowDelayBetween * (blocks.Length - 1)); // diğerleri sırayla yansın diye bekle
            s.SetLoops(-1);

            // Bloğun kendisi de hafifçe scale-up yapsın peak anında
            Sequence ss = DOTween.Sequence();
            ss.AppendInterval(startDelay);
            ss.Append(blocks[idx].rectTransform.DOScale(1.08f, glowDuration * 0.5f).SetEase(Ease.OutQuad));
            ss.Append(blocks[idx].rectTransform.DOScale(1.0f, glowDuration * 0.5f).SetEase(Ease.InQuad));
            ss.AppendInterval(glowDelayBetween * (blocks.Length - 1));
            ss.SetLoops(-1);
        }
    }

    // 3) Yazı sürekli brightness breath
    void StartTextBreathing()
    {
        if (logoText == null) return;
        logoText.DOColor(textPeakColor, textBreathDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
    }

    // 4) Periyodik glitch shake
    void StartGlitchLoop()
    {
        InvokeRepeating(nameof(DoGlitch), glitchInterval, glitchInterval);
    }

    void DoGlitch()
    {
        if (logoText == null) return;
        logoText.rectTransform
                .DOShakePosition(glitchDuration, new Vector3(glitchStrength, 0, 0), 30, 90, false, false)
                .SetEase(Ease.OutQuad);
    }

    void OnDisable()
    {
        // Sahne değişince hayalet tween kalmasın
        DOTween.Kill(logoRoot);
        DOTween.Kill(logoText.rectTransform);
        foreach (var g in glows) if (g != null) DOTween.Kill(g);
        foreach (var b in blocks) if (b != null) DOTween.Kill(b.rectTransform);
        CancelInvoke();
    }
}