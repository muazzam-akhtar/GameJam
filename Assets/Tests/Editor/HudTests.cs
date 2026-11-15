using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJamTests
{
    /// <summary>The SoulKeepers HUD and result screen: stats, theme, timer states, slap icon, the end-of-round flow.</summary>
    public class HudTests
    {
        Scene scene;
        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        GameHUD Hud()
        {
            var go = new GameObject("Game");
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<GameHUD>();      // edit mode: no Awake, the public calls build it
        }

        static T Find<T>(Component root, string name) where T : Component
        {
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if (c.name == name) return c;
            return null;
        }

        static void Run(GameHUD hud, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) hud.Step(0.05f);
        }

        static MatchResult KeeperWin() => new MatchResult
        {
            winner = MatchSide.Angel, reason = "3 souls survived the hunt", timeUp = true,
            saved = 3, taken = 5, total = 8, keeperSlaps = 2, reaperSlaps = 1, keeperStunned = 1, reaperStunned = 2,
            keeperStreak = 4, reaperStreak = 3, huntSeconds = 120f,
        };

        // ---------------------------------------------------------------- stats

        [Test]
        public void Stats_SlapsCountForTheOtherSideAndEndTheVictimsStreak()
        {
            var s = new MatchStats();
            s.SoulHidden(); s.SoulHidden(); s.SoulHidden();
            s.Slapped(MatchSide.Angel);                 // the Reaper slapped the Keeper
            s.SoulHidden();
            s.SoulTaken(); s.SoulTaken();
            s.Slapped(MatchSide.Evil);
            s.SoulTaken();
            Assert.AreEqual(3, s.KeeperStreak, "best run before the slap");
            Assert.AreEqual(2, s.ReaperStreak);
            Assert.AreEqual(1, s.ReaperSlaps);
            Assert.AreEqual(1, s.KeeperStunned);
            Assert.AreEqual(1, s.KeeperSlaps);
            Assert.AreEqual(1, s.ReaperStunned);
        }

        [Test]
        public void Result_ComesFromTheRulesAndStats()
        {
            var rules = new MatchRules(8);
            for (int i = 0; i < 5; i++) rules.Devoured();
            rules.TimeUp(0);
            var stats = new MatchStats();
            stats.SoulTaken();
            var r = MatchResult.From(rules, stats, true, 120f);
            Assert.AreEqual(MatchSide.Angel, r.winner);
            Assert.AreEqual(3, r.saved);
            Assert.AreEqual(5, r.taken);
            Assert.AreEqual(8, r.total);
            Assert.AreEqual(1, r.reaperStreak);
            StringAssert.Contains("3 souls survived", r.reason);
        }

        [Test]
        public void Result_UnhiddenSoulsArentSaved()
        {
            var rules = new MatchRules(8);
            rules.Devoured();
            rules.TimeUp(3);                                    // three never left the pouch
            var r = MatchResult.From(rules, new MatchStats(), true, 120f, 3);
            Assert.AreEqual(MatchSide.Evil, r.winner);
            Assert.AreEqual(4, r.saved, "8 - 1 taken - 3 never hidden");
            Assert.AreEqual(1, r.taken);
        }

        // ---------------------------------------------------------------- theme

        [Test]
        public void Theme_IsInResourcesWithEveryReference()
        {
            var theme = HudTheme.Load();
            Assert.NotNull(theme, "Assets/Resources/HudTheme.asset (GameJam ▸ Build HUD Theme)");
            var so = new SerializedObject(theme);
            var it = so.GetIterator();
            it.NextVisible(true);
            while (it.NextVisible(false))
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                    Assert.NotNull(it.objectReferenceValue, $"HudTheme.{it.name} is missing");
            Assert.AreNotEqual(Vector4.zero, theme.pill.border, "the pill is 9-sliced");
        }

        // ---------------------------------------------------------------- HUD

        [Test]
        public void Timer_GoesWarningThenTimeUp()
        {
            var hud = Hud();
            var theme = HudTheme.Load();
            hud.SetTimer(84f);
            var plate = Find<Image>(hud, "Timer");
            var digits = Find<TMP_Text>(plate, "Digits");
            Assert.AreEqual(theme.timerNormal, plate.sprite);
            StringAssert.Contains("01", digits.text);
            StringAssert.Contains("24", digits.text);
            hud.SetTimer(9.2f);
            Assert.AreEqual(theme.timerWarning, plate.sprite, "≤10 s: the warning plate");
            hud.TimeUp();
            Assert.IsTrue(Find<TMP_Text>(plate, "Time Up").gameObject.activeSelf, "TIME!");
            Assert.IsFalse(digits.gameObject.activeSelf);
            hud.ResetForRound();
            hud.SetTimer(120f);
            Assert.AreEqual(theme.timerNormal, plate.sprite, "a new round starts calm");
            Assert.IsTrue(digits.gameObject.activeSelf);
        }

        [Test]
        public void Pods_ShowSoulsSafeAndTaken()
        {
            var hud = Hud();
            hud.SetPouch(2, 4, 2, 8, true);
            Assert.AreEqual("6", Find<TMP_Text>(Find<Image>(hud, "Keeper Pod"), "Score").text);
            Assert.AreEqual("2", Find<TMP_Text>(Find<Image>(hud, "Reaper Pod"), "Score").text);
            StringAssert.Contains("SOULS IN PLAY · 4", Find<Image>(hud, "Souls In Play").GetComponentInChildren<TMP_Text>().text);
        }

        [Test]
        public void Text_StillRendersAfterAnAssetCleanUp()
        {
            Hud().SetPouch(2, 4, 2, 8, true);
            EditorUtility.UnloadUnusedAssetsImmediate();        // what a scene load does: runtime font atlases must survive
            var hud = Hud();
            hud.SetPouch(2, 4, 2, 8, true);
            var score = Find<TMP_Text>(Find<Image>(hud, "Keeper Pod"), "Score");
            score.ForceMeshUpdate();
            Assert.AreEqual(1, score.textInfo.characterCount, "the '6' is laid out");
            Assert.NotNull(score.font.atlasTexture, "the font atlas is alive");
        }

        [Test]
        public void SlapIcon_DimsDuringTheCooldown()
        {
            var hud = Hud();
            hud.SetSlapReady(MatchSide.Angel, false);
            var icons = hud.GetComponentsInChildren<Image>(true);
            Image keeperSlap = null;
            foreach (var i in icons) if (i.name == "Slap Ready") { keeperSlap = i; break; }
            Assert.NotNull(keeperSlap);
            Assert.Less(keeperSlap.color.a, 0.6f);
            hud.SetSlapReady(MatchSide.Angel, true);
            Assert.AreEqual(1f, keeperSlap.color.a, 1e-4f);
        }

        // ---------------------------------------------------------------- result screen

        [Test]
        public void Result_RevealsThenTakesARematch()
        {
            var hud = Hud();
            int rematches = 0, menus = 0;
            hud.RematchPressed += () => rematches++;
            hud.MainMenuPressed += () => menus++;
            hud.ShowResult(KeeperWin());
            var screen = hud.Result;
            Assert.IsTrue(screen.Showing);
            Assert.IsFalse(screen.Interactive, "frozen first, buttons last");

            Run(hud, ResultScreen.Freeze + 2f);
            Assert.IsTrue(screen.Interactive);
            Assert.AreEqual("KEEPERS WIN", Find<TMP_Text>(screen, "Title").text);
            Assert.AreEqual("3", Find<TMP_Text>(screen, "Saved").text, "the score counted up to the end");
            Assert.AreEqual("5", Find<TMP_Text>(screen, "Taken").text);
            Assert.AreEqual(HudTheme.Load().heroReaperGrey, Find<Image>(screen, "Reaper").sprite, "the loser is grey");

            Find<Button>(screen, "Rematch").onClick.Invoke();
            Find<Button>(screen, "Main Menu").onClick.Invoke();     // too late: one choice per result
            Assert.AreEqual(1, rematches);
            Assert.AreEqual(0, menus);

            hud.ResetForRound();
            Assert.IsFalse(screen.Showing);
        }

        [Test]
        public void Result_ReapersWinUsesTheirBannerAndMainMenu()
        {
            var hud = Hud();
            int menus = 0;
            hud.MainMenuPressed += () => menus++;
            var r = KeeperWin();
            r.winner = MatchSide.Evil;
            r.reason = "all 8 souls taken";
            hud.ShowResult(r);
            Run(hud, ResultScreen.Freeze + 2f);
            var screen = hud.Result;
            Assert.AreEqual("REAPERS WIN", Find<TMP_Text>(screen, "Title").text);
            Assert.AreEqual(HudTheme.Load().ribbonReaper, Find<Image>(screen, "Banner").sprite);
            Assert.AreEqual(HudTheme.Load().heroReaper, Find<Image>(screen, "Reaper").sprite);
            Find<Button>(screen, "Main Menu").onClick.Invoke();
            Assert.AreEqual(1, menus);
        }
    }
}
