using UnityEngine;
using UnityEngine.UI;
using Taeyang;

namespace Integration
{
    // Reuses the team's result panel, font and button artwork for both sequences.
    public class CampaignNarrative : MonoBehaviour
    {
        [Min(1)] public float subtitleSeconds = 3.4f;
        static readonly string[] Lines = {
            "서기 3024년. 지구로 돌아가는 길.\n긴 항해가 끝나고 있었다.",
            "정체불명의 외계 함대가 나타났다.\n귀환선에 미사일이 쏟아진다.",
            "적 미사일이 나를 따라온다.\n다른 적에게 유도해 탈출하자!",
            "세 전선을 돌파하고 지구로 귀환하라.\n보스는 몸통박치기로 격파한다."
        };
        IntegratedGame game;
        Button[] originalButtons;
        bool[] originalVisibility;
        int line;
        float nextLine;
        bool opening;
        GameObject action;
        InputField idInput;
        GameObject openingActors;
        Transform openingShip, openingMissile;
        Vector2 openingOrigin;
        float openingTime;
        public bool Opening => opening;

        public void PlayOpening(IntegratedGame owner)
        {
            game = owner;
            if (game.resultPanel == null || game.result == null) { game.StartGame(); return; }
            originalButtons = game.resultPanel.GetComponentsInChildren<Button>(true);
            originalVisibility = new bool[originalButtons.Length];
            for (int i = 0; i < originalButtons.Length; i++)
            {
                originalVisibility[i] = originalButtons[i].gameObject.activeSelf;
                originalButtons[i].gameObject.SetActive(false);
            }
            game.resultPanel.SetActive(true);
            opening = true;
            line = 0;
            CreateOpeningActors();
            SetSubtitle();
            action = CloneAction("건너뛰기 · Space / Enter / Esc", FinishOpening);
        }
        void SetSubtitle()
        {
            game.result.text = Lines[line];
            nextLine = Time.unscaledTime + subtitleSeconds;
        }
        void Update()
        {
            if (!opening) return;
            AnimateOpening();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
            { FinishOpening(); return; }
            if (Time.unscaledTime < nextLine) return;
            if (++line >= Lines.Length) FinishOpening();
            else SetSubtitle();
        }
        public void FinishOpening()
        {
            if (!opening) return;
            opening = false;
            if (action != null) Destroy(action);
            if (openingActors != null) Destroy(openingActors);
            for (int i = 0; i < originalButtons.Length; i++) originalButtons[i].gameObject.SetActive(originalVisibility[i]);
            game.resultPanel.SetActive(false);
            game.StartGame();
        }
        void CreateOpeningActors()
        {
            openingActors = new GameObject("Opening space sequence");
            // The story uses world sprites beside the existing result panel.
            // No team UI transform, image or font is changed.
            Rect view = BattleArena.ViewBounds();
            openingOrigin = new Vector2(view.xMin + view.width * .15f, view.center.y);
            openingShip = StorySprite("opening-ship", 1.6f, 12);
            openingMissile = StorySprite("boss-missile", .7f, 13);
            if (openingMissile != null) openingMissile.rotation = Quaternion.Euler(0, 0, -90);
            openingTime = Time.unscaledTime;
        }
        Transform StorySprite(string resource, float height, int order)
        {
            Sprite sprite = Resources.Load<Sprite>("CampaignArt/" + resource);
            if (sprite == null) return null;
            var visual = new GameObject(resource).AddComponent<SpriteRenderer>();
            visual.transform.SetParent(openingActors.transform, false);
            visual.sprite = sprite;
            var template = game.player.GetComponentInChildren<SpriteRenderer>();
            if (template != null) visual.sharedMaterial = template.sharedMaterial;
            visual.sortingOrder = order;
            visual.transform.localScale = Vector3.one * (height / Mathf.Max(.01f, sprite.bounds.size.y));
            return visual.transform;
        }
        void AnimateOpening()
        {
            float elapsed = Time.unscaledTime - openingTime;
            Vector2 ship = openingOrigin + new Vector2(Mathf.Sin(elapsed) * .3f, Mathf.Sin(elapsed * 1.5f) * .4f);
            if (openingShip != null) openingShip.position = ship;
            if (openingMissile != null)
            {
                openingMissile.gameObject.SetActive(line >= 1);
                float approach = Mathf.Repeat(elapsed * .4f, 1);
                openingMissile.position = ship + new Vector2(-3 + approach * 2.1f, line >= 2 ? Mathf.Sin(approach * Mathf.PI) * 2 : 0);
            }
        }
        public void ShowEnding(IntegratedGame owner)
        {
            game = owner;
            game.result.text = "지구 귀환 성공!\n미지의 함대는 물러났다. Score : " + game.score.score;
            if (!game.RankedRun)
            {
                game.result.text = "지구 귀환 성공! Score : " + game.score.score + "\n개발·테스트 기록 (랭킹 제외)";
                return;
            }
            // The existing restart/menu buttons retain their original transforms.
            originalButtons = game.resultPanel.GetComponentsInChildren<Button>(true);
            Button restart = FindRestart();
            if (restart != null) restart.gameObject.SetActive(false);
            var field = Instantiate(game.result.gameObject, game.result.transform.parent);
            field.name = "Player ID input";
            var rect = field.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 42);
            var label = field.GetComponent<Text>();
            label.text = "";
            label.supportRichText = false;
            label.raycastTarget = true;
            idInput = field.AddComponent<InputField>();
            idInput.textComponent = label;
            idInput.targetGraphic = label;
            idInput.characterLimit = 16;
            idInput.lineType = InputField.LineType.SingleLine;
            idInput.text = "Player";
            action = CloneAction("ID 입력 후 점수 등록", Submit);
            idInput.ActivateInputField();
        }
        void Submit()
        {
            if (idInput == null || !game.SubmitVictory(idInput.text)) return;
            game.result.text = CampaignRules.NormalizePlayerId(idInput.text) + " · " + game.score.score + "점\n상위 5명: 메뉴 랭킹에서 확인";
            Destroy(idInput.gameObject);
            Destroy(action);
            Button restart = FindRestart();
            if (restart != null) restart.gameObject.SetActive(true);
        }
        Button FindRestart()
        {
            if (originalButtons == null) return null;
            foreach (var button in originalButtons)
                if (button.name == "RESTART") return button;
            return originalButtons.Length > 0 ? originalButtons[0] : null;
        }
        GameObject CloneAction(string title, UnityEngine.Events.UnityAction callback)
        {
            Button template = FindRestart();
            if (template == null) return null;
            var clone = Instantiate(template.gameObject, template.transform.parent);
            clone.name = "Campaign action";
            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(callback);
            var text = clone.GetComponentInChildren<Text>(true);
            if (text != null) text.text = title;
            clone.SetActive(true);
            return clone;
        }
    }
}
