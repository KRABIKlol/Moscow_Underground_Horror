using UnityEngine;

/// Временный «манекен» оружия под камерой: его двигают обычным гизмо в Scene View,
/// глядя на результат в Game View, а потом жмут «Запомнить позу».
/// В игре ничего не делает и в сцене оставаться не должен.
[AddComponentMenu("")]
[DisallowMultipleComponent]
public class RangeWeaponPosePreview : MonoBehaviour
{
    [Tooltip("Оружие на стеллаже, для которого подбирается поза.")]
    public RangeWeapon target;

    void Start()
    {
        Debug.LogWarning("[Тир] В сцене остался объект настройки позы — удали его.", this);
        gameObject.SetActive(false);
    }
}
