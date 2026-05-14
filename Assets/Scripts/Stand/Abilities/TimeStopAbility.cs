using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ZA WARUDO — останавливает всех врагов и их анимации на duration секунд.
/// Игрок и стенд продолжают двигаться в обычном темпе.
/// Time.timeScale не трогаем — игрок остаётся свободным.
/// </summary>
[CreateAssetMenu(fileName = "TimeSlow", menuName = "Stand/Abilities/TimeSlow")]
public class TimeStopAbility : StandAbility
{
    [Header("Time Stop")]
    [SerializeField] private float duration = 3f;

    protected override void Execute(StandController stand)
    {
        stand.StartCoroutine(TimeStopRoutine(stand));
    }

    private IEnumerator TimeStopRoutine(StandController stand)
    {
        // ── Собираем иерархии игрока и стенда для исключения ────────
        Transform playerRoot = stand.Owner;           // Transform игрока (может быть null)
        Transform standRoot  = stand.transform;

        // ── Заморозить врагов (AI + Rigidbody2D) ────────────────────
        EnemyAI[] enemies  = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);
        var enemySnapshots = new EnemySnapshot[enemies.Length];

        for (int i = 0; i < enemies.Length; i++)
        {
            var ai = enemies[i];
            var rb = ai.GetComponent<Rigidbody2D>();

            enemySnapshots[i] = new EnemySnapshot
            {
                ai         = ai,
                rb         = rb,
                wasEnabled = ai.enabled,
                savedVel   = rb != null ? rb.linearVelocity : Vector2.zero,
                savedType  = rb != null ? rb.bodyType       : RigidbodyType2D.Dynamic,
            };

            ai.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType       = RigidbodyType2D.Kinematic;
            }
        }

        // ── Заморозить все анимации (кроме игрока и стенда) ─────────
        Animator[] allAnimators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude);
        var animSnapshots = new List<AnimSnapshot>();

        foreach (var anim in allAnimators)
        {
            // Пропускаем анимации игрока и стенда
            if (playerRoot != null && anim.transform.IsChildOf(playerRoot)) continue;
            if (anim.transform.IsChildOf(standRoot))                        continue;

            animSnapshots.Add(new AnimSnapshot { anim = anim, speed = anim.speed });
            anim.speed = 0f;
        }

        // ── Ждём реального времени ───────────────────────────────────
        yield return new WaitForSeconds(duration);

        // ── Размораживаем врагов ─────────────────────────────────────
        foreach (var s in enemySnapshots)
        {
            if (s.ai == null) continue;   // враг погиб за это время

            s.ai.enabled = s.wasEnabled;
            if (s.rb != null)
            {
                s.rb.bodyType       = s.savedType;
                s.rb.linearVelocity = s.savedVel;
            }
        }

        // ── Размораживаем анимации ───────────────────────────────────
        foreach (var s in animSnapshots)
        {
            if (s.anim != null)
                s.anim.speed = s.speed;
        }
    }

    // ──────────────────────────────────────────
    // Вспомогательные структуры
    // ──────────────────────────────────────────

    private struct EnemySnapshot
    {
        public EnemyAI         ai;
        public Rigidbody2D     rb;
        public bool            wasEnabled;
        public Vector2         savedVel;
        public RigidbodyType2D savedType;
    }

    private struct AnimSnapshot
    {
        public Animator anim;
        public float    speed;   // сохраняем оригинальную скорость, не просто 1f
    }
}
