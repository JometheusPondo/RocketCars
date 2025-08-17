using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFuelBar : MonoBehaviour
{
    [SerializeField] private Image _fillBar;
    [SerializeField] private TMP_Text _text;

    public void UpdateValue(float fuel, float maxFuel)
    {
        float alpha = fuel / maxFuel;

        _fillBar.transform.localScale = new Vector3(alpha, 1f, 1f);
        _text.SetText("{0}", fuel);
    }
}
