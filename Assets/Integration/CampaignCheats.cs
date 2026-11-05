using UnityEngine;
using Taeyang;

namespace Integration
{
    public class CampaignCheats : MonoBehaviour
    {
        public static bool ResumeAfterChapterSkip;
        [Tooltip("Development builds only. Default off; use a separate practice run.")]
        public bool enableDevelopmentCheats;
        public KeyCode toggleKey = KeyCode.F12;
        public KeyCode debugKey = KeyCode.F1, invulnerableKey = KeyCode.F2,
            defeatEnemiesKey = KeyCode.F3, moneyKey = KeyCode.F4;
        public KeyCode advanceTimeKey = KeyCode.F5, nextStageKey = KeyCode.F6;
        [Min(1)] public int moneyAmount = 1000;
        public bool ShowColliders { get; private set; }
        public bool CheatModeAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        public bool Allowed => CheatModeAvailable && enableDevelopmentCheats;
        void Start()
        {
            bool resume = ResumeAfterChapterSkip;
            ResumeAfterChapterSkip = false;
            if (!resume || !CheatModeAvailable) return;
            enableDevelopmentCheats = true;
            var game = IntegratedGame.Active;
            if (game != null && game.RankedRun) game.MarkPracticeRun();
        }
        void OnValidate() => ValidateToggleKey();
        void ValidateToggleKey()
        {
            if (IsFunctionKey(toggleKey) && toggleKey != debugKey && toggleKey != invulnerableKey &&
                toggleKey != defeatEnemiesKey && toggleKey != moneyKey &&
                toggleKey != advanceTimeKey && toggleKey != nextStageKey) return;
            // Reserve a distinct function key so toggling cannot also grant an action.
            for (int value = (int)KeyCode.F12; value >= (int)KeyCode.F1; value--)
            {
                var candidate = (KeyCode)value;
                if (candidate == debugKey || candidate == invulnerableKey || candidate == defeatEnemiesKey ||
                    candidate == moneyKey || candidate == advanceTimeKey || candidate == nextStageKey) continue;
                toggleKey = candidate;
                return;
            }
        }
        public bool ToggleDevelopmentMode()
        {
            var game = IntegratedGame.Active;
            if (!CheatModeAvailable)
            {
                enableDevelopmentCheats = false;
                ClearActiveCheats(game);
                return false;
            }
            if (game == null) return false;
            enableDevelopmentCheats = !enableDevelopmentCheats;
            if (enableDevelopmentCheats)
            {
                if (game.RankedRun) game.MarkPracticeRun();
            }
            else ClearActiveCheats(game);
            // Switching off never restores rank eligibility or checkpoint status.
            return enableDevelopmentCheats;
        }
        void Update()
        {
            var game = IntegratedGame.Active;
            if (!CheatModeAvailable)
            {
                enableDevelopmentCheats = false;
                ClearActiveCheats(game);
                return;
            }
            ValidateToggleKey();
            if (Pressed(toggleKey)) { ToggleDevelopmentMode(); return; }
            if (!Allowed) { ClearActiveCheats(game); return; }
            if (game != null && game.RankedRun) game.MarkPracticeRun();
            if (Pressed(debugKey)) ExecuteCheat(0);
            if (Pressed(invulnerableKey)) ExecuteCheat(1);
            if (Pressed(defeatEnemiesKey)) ExecuteCheat(2);
            if (Pressed(moneyKey)) ExecuteCheat(3);
            if (Pressed(advanceTimeKey)) ExecuteCheat(4);
            if (Pressed(nextStageKey)) ExecuteCheat(5);
        }
        public bool ExecuteCheat(int action)
        {
            var game = IntegratedGame.Active;
            if (!Allowed || game == null || !game.HasStarted || game.Ended || game.player == null || action < 0 || action > 5) return false;
            // Currency and toggles remain available while paused or shopping.
            // Enemy defeat stays in running play so its ordinary reward path runs.
            if (action == 2 && !game.Running) return false;
            if (action == 3 && game.shop == null) return false;
            if (game.RankedRun) game.MarkPracticeRun();
            if (action == 0) ShowColliders = !ShowColliders;
            else if (action == 1) game.player.CheatInvulnerable = !game.player.CheatInvulnerable;
            else if (action == 2)
            {
                Rect view = BattleArena.ViewBounds();
                foreach (var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None))
                    if (enemy.GetComponentInParent<IntegratedBossTarget>() == null && view.Contains(enemy.transform.position))
                        enemy.TakeDamage(int.MaxValue, Vector2.zero);
            }
            else if (action == 3)
            {
                game.shop.coin = (int)System.Math.Min(int.MaxValue, (long)game.shop.coin + Mathf.Max(1, moneyAmount));
                game.shop.SyncLegacyData(false);
                if (game.shop.coinUI != null) game.shop.coinUI.text = "남은 돈 : " + game.shop.coin + "벨";
            }
            else if (action == 4) return game.AdvanceSurvivalForTesting(60);
            else if (action == 5) return game.SkipToNextStageForTesting();
            return true;
        }
        void ClearActiveCheats(IntegratedGame game)
        {
            ShowColliders = false;
            if (game != null && game.player != null) game.player.CheatInvulnerable = false;
        }
        void OnDisable() => ClearActiveCheats(IntegratedGame.Active);
        static bool IsFunctionKey(KeyCode key) => CampaignRules.IsFunctionKey((int)key, (int)KeyCode.F1, (int)KeyCode.F12);
        static bool Pressed(KeyCode key) => IsFunctionKey(key) && Input.GetKeyDown(key);
        void OnGUI()
        {
            var game = IntegratedGame.Active;
            if (!CheatModeAvailable || game == null) return;
            GUI.color = Allowed ? Color.yellow : Color.white;
            GUI.Label(new Rect(12, Screen.height - 58, Mathf.Max(1, Screen.width - 24), 40), toggleKey + " 테스트 치트: " +
                (Allowed ? "끄기 · 랭킹 제외 | " + debugKey + " 충돌 · " + invulnerableKey + " 무적 · " + defeatEnemiesKey + " 적 제거 · " + moneyKey + " +" + moneyAmount : "켜기 (기본 OFF · 활성화 시 랭킹 제외)") +
                "\n" + advanceTimeKey + " 생존 +60초 · " + nextStageKey + " 다음 챕터 (1→2→3)" + (Allowed ? "" : " (치트를 켠 후 사용)"));
            if (Allowed && ShowColliders && Camera.main != null)
            {
                foreach (var enemy in FindObjectsByType<EnemyShip>(FindObjectsSortMode.None)) DrawColliders(enemy.gameObject);
                foreach (var bullet in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) DrawColliders(bullet.gameObject);
                foreach (var boss in FindObjectsByType<IntegratedBossTarget>(FindObjectsSortMode.None)) DrawColliders(boss.gameObject);
            }
            GUI.color = Color.white;
        }
        void DrawColliders(GameObject target)
        {
            foreach (var collider in target.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled) continue;
                var circle = collider as CircleCollider2D;
                if (circle != null)
                {
                    Vector3 scale = circle.transform.lossyScale;
                    float radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    Vector2 center = circle.transform.TransformPoint(circle.offset);
                    Vector2 previous = center + Vector2.right * radius;
                    for (int i = 1; i <= 48; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 48;
                        Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        Line(previous, point); previous = point;
                    }
                }
                var box = collider as BoxCollider2D;
                if (box != null)
                {
                    Vector2 h = box.size * .5f;
                    Vector2[] points = { new Vector2(-h.x,-h.y),new Vector2(h.x,-h.y),new Vector2(h.x,h.y),new Vector2(-h.x,h.y) };
                    for (int i = 0; i < 4; i++) Line(box.transform.TransformPoint(points[i] + box.offset), box.transform.TransformPoint(points[(i+1)%4] + box.offset));
                }
                var polygon = collider as PolygonCollider2D;
                if (polygon != null)
                    for (int path = 0; path < polygon.pathCount; path++)
                    {
                        var points = polygon.GetPath(path);
                        for (int i = 0; i < points.Length; i++) Line(polygon.transform.TransformPoint(points[i]+polygon.offset), polygon.transform.TransformPoint(points[(i+1)%points.Length]+polygon.offset));
                    }
            }
        }
        static void Line(Vector2 a, Vector2 b)
        {
            Vector3 p = Camera.main.WorldToScreenPoint(a), q = Camera.main.WorldToScreenPoint(b);
            if (p.z <= 0 || q.z <= 0) return;
            Vector2 start = new Vector2(p.x, Screen.height-p.y), end = new Vector2(q.x, Screen.height-q.y);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y-start.y,end.x-start.x)*Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x,start.y,(end-start).magnitude,1.5f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
