using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform _textTransform;
    [SerializeField] private Outline _outline;

    [Space]
    [SerializeField] private float _textXTarget = 15f;
    [SerializeField] private float _easeDuration = 0.2f;

    [Space]
    [SerializeField] private Color _colorActive = Color.white;
    [SerializeField] private Color _colorDeactive = new Color(1f, 1f, 1f, 0f);


    private Tween _tweenA;
    private Tween _tweenB;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _tweenA?.Kill();
        _tweenB?.Kill();

        _tweenA = _textTransform.DOLocalMoveX(_textXTarget, _easeDuration);
        _tweenB = _outline.DOColor(_colorActive, _easeDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tweenA?.Kill();
        _tweenB?.Kill();

        _tweenA = _textTransform.DOLocalMoveX(0f, _easeDuration);
        _tweenB = _outline.DOColor(_colorDeactive, _easeDuration);
    }
}
