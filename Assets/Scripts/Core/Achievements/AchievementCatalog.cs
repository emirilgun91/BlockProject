using System.Collections.Generic;

namespace RogueBlockBlast.Core.Achievements
{
    public enum AchievementCategory { Progress, Score, Skill, Cards, Meta, Lifetime, Secret, Classic }

    /// <summary>
    /// Bir achievement'ın tanımı. <see cref="Id"/> Steamworks'teki "API Name" ile
    /// birebir aynı olmalı — Steam'e başka hiçbir bilgi gitmiyor; isim, açıklama,
    /// ikon ve gizlilik Steamworks panelinde girilir.
    ///
    /// <see cref="DevName"/> / <see cref="DevDescription"/> oyunda gösterilmez
    /// (oyuncu bunları Steam overlay'inde Steam'in kendi çevirisiyle görür).
    /// Yalnızca debug paneli ve Steamworks'e girilecek metnin kaynağı olarak durur.
    /// </summary>
    public sealed class AchievementDef
    {
        public readonly string              Id;
        public readonly AchievementCategory Category;
        public readonly bool                Hidden;
        public readonly string              DevName;
        public readonly string              DevDescription;

        /// <summary>Ömür boyu sayaç ile açılanlar için: hangi sayaç, kaçta.</summary>
        public readonly string Stat;
        public readonly int    Target;

        public AchievementDef(string id, AchievementCategory category, string name, string desc,
                              bool hidden = false, string stat = null, int target = 0)
        {
            Id             = id;
            Category       = category;
            Hidden         = hidden;
            DevName        = name;
            DevDescription = desc;
            Stat           = stat;
            Target         = target;
        }
    }

    /// <summary>Ömür boyu sayaçların anahtarları. PlayerPrefs: <c>Ach_Stat_{key}</c>.</summary>
    public static class AchievementStats
    {
        public const string LinesCleared = "lines_cleared";
        public const string PiecesPlaced = "pieces_placed";
        public const string CoinsEarned  = "coins_earned";
        public const string RunsPlayed   = "runs_played";
    }

    /// <summary>Steamworks API adları. Steam panelinde aynen bu adlarla açılmalı.</summary>
    public static class AchievementIds
    {
        // ── İlerleme ─────────────────────────────────────────────────────────
        public const string FirstClear     = "ACH_FIRST_CLEAR";
        public const string FirstMilestone = "ACH_FIRST_MILESTONE";
        public const string Stage5         = "ACH_STAGE_5";
        public const string Stage10        = "ACH_STAGE_10";
        public const string Stage20        = "ACH_STAGE_20";
        public const string AllStages      = "ACH_ALL_STAGES";

        // ── Skor (tek run) ───────────────────────────────────────────────────
        public const string Score10K  = "ACH_SCORE_10K";
        public const string Score100K = "ACH_SCORE_100K";
        public const string Score1M   = "ACH_SCORE_1M";
        public const string Score10M  = "ACH_SCORE_10M";

        // ── Beceri ───────────────────────────────────────────────────────────
        public const string DoubleClear    = "ACH_DOUBLE_CLEAR";
        public const string TripleClear    = "ACH_TRIPLE_CLEAR";
        public const string QuadClear      = "ACH_QUAD_CLEAR";
        public const string Crossfire      = "ACH_CROSSFIRE";
        public const string PerfectClear   = "ACH_PERFECT_CLEAR";
        public const string MaxCharge      = "ACH_MAX_CHARGE";
        public const string ComboX2        = "ACH_COMBO_X2";
        public const string ComboX5        = "ACH_COMBO_X5";
        public const string ComboX10       = "ACH_COMBO_X10";
        public const string QuickMilestone = "ACH_QUICK_MILESTONE";

        // ── Kartlar ──────────────────────────────────────────────────────────
        public const string FirstCard = "ACH_FIRST_CARD";
        public const string Cards10   = "ACH_CARDS_10";
        public const string EpicCard  = "ACH_EPIC_CARD";

        // ── Meta ─────────────────────────────────────────────────────────────
        public const string Tutorial         = "ACH_TUTORIAL";
        public const string FirstShapeUnlock = "ACH_FIRST_SHAPE_UNLOCK";
        public const string AllShapes        = "ACH_ALL_SHAPES";
        public const string ShapeUpgrade     = "ACH_SHAPE_UPGRADE";
        public const string FirstUpgrade     = "ACH_FIRST_UPGRADE";

        // ── Ömür boyu ────────────────────────────────────────────────────────
        public const string Lines1000  = "ACH_LINES_1000";
        public const string Pieces5000 = "ACH_PIECES_5000";
        public const string Coins25K   = "ACH_COINS_25K";
        public const string Runs50     = "ACH_RUNS_50";

        // ── Gizli ────────────────────────────────────────────────────────────
        public const string LastGasp   = "ACH_SECRET_LAST_GASP";
        public const string Monochrome = "ACH_SECRET_MONOCHROME";
        public const string ZeroHero   = "ACH_SECRET_ZERO_HERO";
        public const string Dizzy      = "ACH_SECRET_DIZZY";
        public const string Stiff      = "ACH_SECRET_STIFF";
        public const string CloseCall  = "ACH_SECRET_CLOSE_CALL";
        public const string Lucky      = "ACH_SECRET_LUCKY";
        public const string BlackCat   = "ACH_SECRET_BLACK_CAT";

        // ── Classic Mode (tek oyun; eşikler BalanceSim `classic` ile kalibre) ──
        public const string ClassicScore5K  = "ACH_CLASSIC_SCORE_5K";
        public const string ClassicScore20K = "ACH_CLASSIC_SCORE_20K";
        public const string ClassicLines100 = "ACH_CLASSIC_LINES_100";
        public const string ClassicNoRush   = "ACH_SECRET_CLASSIC_NO_RUSH";
    }

    /// <summary>
    /// Tüm achievement'ların listesi. Yeni bir tane eklerken: buraya ekle,
    /// tetikleyicisini <c>AchievementTracker</c>'a yaz, Steamworks'te aynı API
    /// adıyla aç. Sayaçlı olanlar (<see cref="AchievementDef.Stat"/>) tetikleyici
    /// istemez — <see cref="AchievementService.AddStat"/> eşiği kendisi kontrol eder.
    /// </summary>
    public static class AchievementCatalog
    {
        private const AchievementCategory P = AchievementCategory.Progress;
        private const AchievementCategory S = AchievementCategory.Score;
        private const AchievementCategory K = AchievementCategory.Skill;
        private const AchievementCategory C = AchievementCategory.Cards;
        private const AchievementCategory M = AchievementCategory.Meta;
        private const AchievementCategory L = AchievementCategory.Lifetime;
        private const AchievementCategory X = AchievementCategory.Secret;
        private const AchievementCategory Cl = AchievementCategory.Classic;

        public static readonly IReadOnlyList<AchievementDef> All = new[]
        {
            new AchievementDef(AchievementIds.FirstClear,     P, "First Flow",        "Clear your first line."),
            new AchievementDef(AchievementIds.FirstMilestone, P, "On the Board",      "Reach your first milestone."),
            new AchievementDef(AchievementIds.Stage5,         P, "Warming Up",        "Reach Stage 5 in a single run."),
            new AchievementDef(AchievementIds.Stage10,        P, "In the Flow",       "Reach Stage 10 in a single run."),
            new AchievementDef(AchievementIds.Stage20,        P, "Deep Flow",         "Reach Stage 20 in a single run."),
            new AchievementDef(AchievementIds.AllStages,      P, "Blockflow Master",  "Clear every milestone in a single run."),

            new AchievementDef(AchievementIds.Score10K,  S, "Five Digits",     "Score 10,000 points in a single run."),
            new AchievementDef(AchievementIds.Score100K, S, "Six Figures",     "Score 100,000 points in a single run."),
            new AchievementDef(AchievementIds.Score1M,   S, "Millionaire",     "Score 1,000,000 points in a single run."),
            new AchievementDef(AchievementIds.Score10M,  S, "Off the Charts",  "Score 10,000,000 points in a single run."),

            new AchievementDef(AchievementIds.DoubleClear,    K, "Double Trouble",  "Clear 2 lines with one piece."),
            new AchievementDef(AchievementIds.TripleClear,    K, "Hat Trick",       "Clear 3 lines with one piece."),
            new AchievementDef(AchievementIds.QuadClear,      K, "Quad Blast",      "Clear 4 or more lines with one piece."),
            new AchievementDef(AchievementIds.Crossfire,      K, "Crossfire",       "Clear a row and a column with the same piece."),
            new AchievementDef(AchievementIds.PerfectClear,   K, "Clean Slate",     "Empty the entire board with a line clear."),
            new AchievementDef(AchievementIds.MaxCharge,      K, "Fully Charged",   "Fill the combo bar."),
            new AchievementDef(AchievementIds.ComboX2,        K, "Combo x2",        "Reach a x2 combo multiplier."),
            new AchievementDef(AchievementIds.ComboX5,        K, "Combo x5",        "Reach a x5 combo multiplier."),
            new AchievementDef(AchievementIds.ComboX10,       K, "Combo x10",       "Reach a x10 combo multiplier."),
            new AchievementDef(AchievementIds.QuickMilestone, K, "Quick Thinker",   "From Stage 5 on, reach a milestone using 5 pieces or fewer."),

            new AchievementDef(AchievementIds.FirstCard, C, "Card Shark",    "Pick your first card."),
            new AchievementDef(AchievementIds.Cards10,   C, "Full Hand",     "Hold 10 cards in a single run."),
            new AchievementDef(AchievementIds.EpicCard,  C, "Epic Find",     "Pick an Epic card."),

            new AchievementDef(AchievementIds.Tutorial,         M, "Learned the Ropes", "Complete the tutorial."),
            new AchievementDef(AchievementIds.FirstShapeUnlock, M, "New Shape",         "Unlock a shape in the Shape Shop."),
            new AchievementDef(AchievementIds.AllShapes,        M, "Shape Collector",   "Unlock every shape."),
            new AchievementDef(AchievementIds.ShapeUpgrade,     M, "Polished",          "Upgrade a shape's score."),
            new AchievementDef(AchievementIds.FirstUpgrade,     M, "Invested",          "Buy your first upgrade."),

            new AchievementDef(AchievementIds.Lines1000,  L, "Line Worker",   "Clear 1,000 lines in total.",
                               stat: AchievementStats.LinesCleared, target: 1000),
            new AchievementDef(AchievementIds.Pieces5000, L, "Block Stacker", "Place 5,000 pieces in total.",
                               stat: AchievementStats.PiecesPlaced, target: 5000),
            new AchievementDef(AchievementIds.Coins25K,   L, "Treasury",      "Earn 25,000 coins in total.",
                               stat: AchievementStats.CoinsEarned,  target: 25000),
            new AchievementDef(AchievementIds.Runs50,     L, "Regular",       "Play 50 runs.",
                               stat: AchievementStats.RunsPlayed,   target: 50),

            new AchievementDef(AchievementIds.LastGasp,   X, "Last Gasp",      "Reach a milestone with your very last piece.",          hidden: true),
            new AchievementDef(AchievementIds.Monochrome, X, "Monochrome",     "Clear a line made entirely of one color.",                          hidden: true),
            new AchievementDef(AchievementIds.ZeroHero,   X, "Abstract Art",   "End a run with 0 points after placing at least 10 pieces.",          hidden: true),
            new AchievementDef(AchievementIds.Dizzy,      X, "Dizzy",          "Rotate a single piece 20 times before placing it.",                  hidden: true),
            new AchievementDef(AchievementIds.Stiff,      X, "Straight Edge",  "Reach Stage 5 without rotating a single piece.",                     hidden: true),
            new AchievementDef(AchievementIds.CloseCall,  X, "Close Call",     "Get saved when no piece fits, then reach the next milestone.",         hidden: true),
            new AchievementDef(AchievementIds.Lucky,      X, "Lady Luck",      "Win the Gambler's roll 3 times in one run.",                         hidden: true),
            new AchievementDef(AchievementIds.BlackCat,   X, "Black Cat",      "Lose the Gambler's roll 5 times in a row.",                          hidden: true),

            new AchievementDef(AchievementIds.ClassicScore5K,  Cl, "Old School",   "Score 5,000 points in a single Classic game."),
            new AchievementDef(AchievementIds.ClassicScore20K, Cl, "Classic Ace",  "Score 20,000 points in a single Classic game."),
            new AchievementDef(AchievementIds.ClassicLines100, Cl, "Zen",          "Clear 100 lines in a single Classic game."),
            new AchievementDef(AchievementIds.ClassicNoRush,   Cl, "No Rush",      "Play a single Classic game for 20 minutes.",                        hidden: true),
        };

        private static Dictionary<string, AchievementDef> _byId;

        public static AchievementDef Get(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, AchievementDef>();
                foreach (var def in All) _byId[def.Id] = def;
            }
            return _byId.TryGetValue(id, out var d) ? d : null;
        }
    }
}
