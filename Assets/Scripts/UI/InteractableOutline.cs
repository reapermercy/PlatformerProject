using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рисует кайму вокруг спрайта смещёнными копиями.
/// Добавляется автоматически PlayerInteraction при подсветке.
/// </summary>
public class InteractableOutline : MonoBehaviour
{
    [SerializeField] private float thickness = 0.05f;

    private SpriteRenderer          _source;
    private readonly List<SpriteRenderer> _parts = new();
    private bool _visible;

    private static readonly Vector2[] Directions =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2( 0.707f,  0.707f),
        new Vector2(-0.707f,  0.707f),
        new Vector2( 0.707f, -0.707f),
        new Vector2(-0.707f, -0.707f),
    };

    // ──────────────────────────────────────────
    // API
    // ──────────────────────────────────────────

    public void Show(SpriteRenderer source, Color color)
    {
        _source = source;

        if (_parts.Count == 0)
            BuildParts(color);
        else
            SetColor(color);

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
        _source = null;
    }

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void LateUpdate()
    {
        if (!_visible || _source == null) return;

        // Синхронизируем спрайт (на случай анимации)
        foreach (var part in _parts)
            if (part != null) part.sprite = _source.sprite;
    }

    private void OnDestroy()
    {
        foreach (var part in _parts)
            if (part != null) Destroy(part.gameObject);
    }

    // ──────────────────────────────────────────
    // Вспомогательные
    // ──────────────────────────────────────────

    private void BuildParts(Color color)
    {
        if (_source == null) return;

        foreach (var dir in Directions)
        {
            var go = new GameObject("OutlinePart")
            {
                hideFlags = HideFlags.HideInHierarchy
            };
            go.transform.SetParent(_source.transform);
            go.transform.localPosition = (Vector3)(dir * thickness);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite         = _source.sprite;
            sr.color          = color;
            sr.sortingLayerID = _source.sortingLayerID;
            sr.sortingOrder   = _source.sortingOrder - 1;

            _parts.Add(sr);
        }
    }

    private void SetColor(Color color)
    {
        foreach (var part in _parts)
            if (part != null) part.color = color;
    }

    private void SetVisible(bool on)
    {
        _visible = on;
        foreach (var part in _parts)
            if (part != null) part.gameObject.SetActive(on);
    }
}
