// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using PopVuj.Game;
using PopVuj.Scripting;

namespace PopVuj.AI
{
    /// <summary>
    /// Fate controller for PopVuj — runs a second bytecode program that
    /// triggers random events like a "tarot card draw" each tick.
    ///
    /// Each difficulty tier is a Python script that reads world state and
    /// issues divine/chaotic interventions. Same builtins as the player.
    /// </summary>
    public class PopVujFateController : MonoBehaviour
    {
        private PopVujMatchManager _match;
        private CityGrid _city;
        private FateDifficulty _difficulty;
        private PopVujProgram _program;

        public FateDifficulty Difficulty => _difficulty;
        public PopVujProgram Program => _program;

        public void Initialize(PopVujMatchManager match, CityGrid city, FateDifficulty difficulty)
        {
            _match = match;
            _city = city;
            _program = gameObject.AddComponent<PopVujProgram>();
            SetDifficulty(difficulty);
        }

        public void SetDifficulty(FateDifficulty difficulty)
        {
            _difficulty = difficulty;
            string code = GetSampleCode(difficulty);
            _program.Initialize(_match, _city, code, $"Fate_{difficulty}");
            Debug.Log($"[Fate] Difficulty → {difficulty} (running bytecode)");
        }

        // =================================================================
        // TAROT RIDER SCRIPTS — each Fate tier draws from the 78-card
        // Rider-Waite deck. Card number selected, then large switch.
        // Royal/Major Arcana → character events (Hierophant → wedding,
        // Devil → forced crime). Pip cards → quantity-based events
        // (6 of Cups → 6 minions affected). The code IS the tarot.
        // =================================================================

        public static string GetSampleCode(FateDifficulty difficulty)
        {
            switch (difficulty)
            {
                case FateDifficulty.Gentle:
                    return GENTLE;
                case FateDifficulty.Fickle:
                    return FICKLE;
                case FateDifficulty.Harsh:
                    return HARSH;
                case FateDifficulty.Chaotic:
                    return CHAOTIC;
                default:
                    return GENTLE;
            }
        }



        // ── Gentle: "The Star" ───────────────────────────────────
        private static readonly string GENTLE = @"# ═══════════════════════════════════════
# THE STAR — Gentle Fate
# Rider-Waite Tarot · Benign Draw
# Only blessings touch your kingdom.
# Draws one card every 8 cycles.
# ═══════════════════════════════════════
n = n + 1
if n % 8 == 0:
    seed = n * 31 + 17
    card = seed % 10
    w = get_city_width()
    pos = seed % w
    # Name the card drawn
    name = get_gentle_face(card)
    # The card speaks
    match name:
        case ""High Priestess"":
            faith = get_faith()
            if faith < 0.5:
                send_prophet()
        case ""The Empress"":
            wood = get_wood()
            if wood > 0:
                build_house(pos)
        case ""Strength"":
            set_weather(0)
        case ""The Hermit"":
            pop = get_population()
        case ""Temperance"":
            send_prophet()
            set_weather(0)
        case ""The Star"":
            send_omen()
            set_weather(0)
        case ""The Sun"":
            send_omen()
            wood = get_wood()
            if wood > 0:
                build_house(pos)
        case ""The World"":
            send_omen()
            send_prophet()
            set_weather(0)
        case ""Ace of Cups"":
            send_omen()
        case ""Ace of Pentacles"":
            slot = 0
            while slot < w:
                cell = get_cell(slot)
                if cell == 2:
                    harvest_tree(slot)
                    slot = w
                slot = slot + 1
";

        // ── Fickle: "Wheel of Fortune" ───────────────────────────
        private static readonly string FICKLE = @"# ═══════════════════════════════════════
# WHEEL OF FORTUNE — Fickle Fate
# Rider-Waite Tarot · 22 Major Arcana
# What the wheel turns, none can undo.
# Draws one card every 5 cycles.
# ═══════════════════════════════════════
n = n + 1
if n % 5 == 0:
    seed = n * 31 + 17
    card = seed % 22
    w = get_city_width()
    pos = seed % w
    pop = get_population()
    faith = get_faith()
    heretics = get_heretics()
    wood = get_wood()
    weather = get_weather()
    # Name the Arcana
    name = get_face(card)
    # The Arcana speaks
    match name:
        case ""The Fool"":
            nw = (weather + 1) % 4
            set_weather(nw)
        case ""The Magician"":
            slot = 0
            while slot < w:
                cell = get_cell(slot)
                if cell == 2:
                    harvest_tree(slot)
                    slot = w
                slot = slot + 1
        case ""High Priestess"":
            if faith < 0.5:
                send_prophet()
        case ""The Empress"":
            if wood > 0:
                build_house(pos)
        case ""The Emperor"":
            set_weather(0)
        case ""The Hierophant"":
            send_prophet()
            send_omen()
        case ""The Lovers"":
            send_omen()
            if wood > 0:
                build_house(pos)
        case ""The Chariot"":
            expand_building(pos)
        case ""Strength"":
            if heretics > 2:
                summon_bears()
            else:
                set_weather(0)
        case ""The Hermit"":
            pop = get_population()
        case ""Wheel of Fortune"":
            nw = (weather + 1) % 4
            set_weather(nw)
        case ""Justice"":
            if heretics > 0:
                smite()
        case ""The Hanged Man"":
            send_prophet()
            set_weather(1)
        case ""Death"":
            set_weather(2)
            if heretics > 0:
                smite()
        case ""Temperance"":
            send_prophet()
            set_weather(0)
        case ""The Devil"":
            set_weather(3)
        case ""The Tower"":
            set_weather(2)
            shrink_building(pos)
        case ""The Star"":
            send_omen()
            set_weather(0)
        case ""The Moon"":
            set_weather(1)
        case ""The Sun"":
            send_omen()
            set_weather(0)
            if wood > 0:
                build_house(pos)
        case ""Judgement"":
            if heretics > 0:
                summon_bears()
        case ""The World"":
            send_omen()
            send_prophet()
            set_weather(0)
";

        // ── Harsh: "The Tower" ───────────────────────────────────
        private static readonly string HARSH = @"# ═══════════════════════════════════════
# THE TOWER — Harsh Fate
# Rider-Waite Tarot · Major + Swords
# 36 cards. Mostly punishment.
# Draws one card every 3 cycles.
# ═══════════════════════════════════════

# ── Sword helper: pip is the body count ──
def draw_sword():
    if pip < 10:
        count = pip + 1
        i = 0
        while i < count:
            smite()
            i = i + 1
    elif pip == 10:
        smite()
    elif pip == 11:
        summon_bears()
    elif pip == 12:
        smite()
        smite()
        set_weather(0)
    elif pip == 13:
        summon_bears()
        smite()
        set_weather(2)

n = n + 1
if n % 3 == 0:
    seed = n * 31 + 17
    card = seed % 36
    w = get_city_width()
    pos = seed % w
    pop = get_population()
    faith = get_faith()
    heretics = get_heretics()
    wood = get_wood()
    weather = get_weather()
    # Name the card
    pip = 0
    if card < 22:
        name = get_face(card)
    else:
        pip = card - 22
        name = ""Swords""
    # Fate speaks
    match name:
        case ""The Fool"":
            nw = (weather + 2) % 4
            set_weather(nw)
        case ""The Magician"":
            slot = 0
            while slot < w:
                cell = get_cell(slot)
                if cell == 2:
                    harvest_tree(slot)
                    slot = w
                slot = slot + 1
        case ""High Priestess"":
            send_prophet()
        case ""The Empress"":
            if wood > 0:
                build_house(pos)
            else:
                set_weather(1)
        case ""The Emperor"":
            set_weather(0)
            if heretics > 0:
                smite()
        case ""The Hierophant"":
            send_prophet()
            send_omen()
            if heretics > 1:
                smite()
        case ""The Lovers"":
            send_omen()
        case ""The Chariot"":
            expand_building(pos)
        case ""Strength"":
            if heretics > 0:
                summon_bears()
        case ""The Hermit"":
            set_weather(3)
        case ""Wheel of Fortune"":
            nw = (weather + 1) % 4
            set_weather(nw)
            if heretics > 0:
                smite()
        case ""Justice"":
            smite()
        case ""The Hanged Man"":
            send_prophet()
            set_weather(2)
        case ""Death"":
            set_weather(2)
            smite()
            smite()
        case ""Temperance"":
            send_prophet()
            set_weather(0)
        case ""The Devil"":
            set_weather(3)
            if faith > 0.6:
                smite()
        case ""The Tower"":
            set_weather(2)
            shrink_building(pos)
            shrink_building(pos + 1)
        case ""The Star"":
            send_omen()
        case ""The Moon"":
            set_weather(1)
            if heretics > 0:
                summon_bears()
        case ""The Sun"":
            send_omen()
            set_weather(0)
        case ""Judgement"":
            summon_bears()
            smite()
        case ""The World"":
            send_omen()
            send_prophet()
        case ""Swords"":
            draw_sword()
";

        // ── Chaotic: "Death" ─────────────────────────────────────
        private static readonly string CHAOTIC = @"# ═══════════════════════════════════════
# DEATH — Chaotic Fate
# Rider-Waite Tarot · Full 78-Card Deck
# Every card drawn. Every 2 cycles.
# Pip number = magnitude of the event.
# ═══════════════════════════════════════

# ── Suit helpers (use global pip) ──

def draw_cups():
    # Water — each pip sends a wave of emotion
    count = pip + 1
    i = 0
    while i < count:
        send_omen()
        i = i + 1

def draw_wands():
    # Fire — each pip fuels the spirit
    if pip < 5:
        count = pip + 1
        i = 0
        while i < count:
            send_prophet()
            i = i + 1
    else:
        expand_building(pos)

def draw_swords():
    # Air — each pip is a blade
    count = pip + 1
    i = 0
    while i < count:
        smite()
        i = i + 1

def draw_pentacles():
    # Earth — each pip gathers a resource
    slot = 0
    gathered = 0
    w = get_city_width()
    while slot < w:
        cell = get_cell(slot)
        if cell == 2:
            harvest_tree(slot)
            gathered = gathered + 1
            if gathered > pip:
                slot = w
        slot = slot + 1

n = n + 1
if n % 2 == 0:
    seed = n * 31 + 17
    card = seed % 78
    w = get_city_width()
    pos = seed % w
    pop = get_population()
    faith = get_faith()
    heretics = get_heretics()
    wood = get_wood()
    weather = get_weather()
    # Identify the card
    name = ""unknown""
    suit = ""none""
    pip = 0
    if card < 22:
        name = get_face(card)
    elif card < 36:
        suit = ""Cups""
        pip = card - 22
    elif card < 50:
        suit = ""Wands""
        pip = card - 36
    elif card < 64:
        suit = ""Swords""
        pip = card - 50
    else:
        suit = ""Pentacles""
        pip = card - 64
    # ═══ Major Arcana ═══
    match name:
        case ""The Fool"":
            nw = (weather + 2) % 4
            set_weather(nw)
            smite()
        case ""The Magician"":
            slot = 0
            while slot < w:
                cell = get_cell(slot)
                if cell == 2:
                    harvest_tree(slot)
                slot = slot + 1
        case ""High Priestess"":
            send_prophet()
            set_weather(1)
        case ""The Empress"":
            if wood > 0:
                build_house(pos)
            if wood > 0:
                build_house(pos + 1)
            send_omen()
        case ""The Emperor"":
            set_weather(0)
            smite()
            smite()
        case ""The Hierophant"":
            send_prophet()
            send_omen()
            smite()
        case ""The Lovers"":
            send_omen()
            send_omen()
            if wood > 0:
                build_house(pos)
        case ""The Chariot"":
            expand_building(pos)
            expand_building(pos + 1)
        case ""Strength"":
            summon_bears()
            summon_bears()
        case ""The Hermit"":
            set_weather(3)
        case ""Wheel of Fortune"":
            nw = (weather + 1) % 4
            set_weather(nw)
            smite()
            send_omen()
        case ""Justice"":
            smite()
            smite()
        case ""The Hanged Man"":
            send_prophet()
            set_weather(2)
            smite()
        case ""Death"":
            set_weather(2)
            i = 0
            while i < 3:
                smite()
                i = i + 1
        case ""Temperance"":
            send_prophet()
            set_weather(0)
        case ""The Devil"":
            set_weather(3)
            smite()
            shrink_building(pos)
        case ""The Tower"":
            set_weather(2)
            shrink_building(pos)
            shrink_building(pos + 1)
            shrink_building(pos + 2)
        case ""The Star"":
            send_omen()
            set_weather(0)
        case ""The Moon"":
            set_weather(1)
            summon_bears()
        case ""The Sun"":
            send_omen()
            send_omen()
            set_weather(0)
            if wood > 0:
                build_house(pos)
        case ""Judgement"":
            summon_bears()
            i = 0
            while i < 5:
                smite()
                i = i + 1
        case ""The World"":
            send_omen()
            send_prophet()
            set_weather(0)
            if wood > 0:
                build_house(pos)
            if wood > 1:
                build_chapel(pos + 2)
        case _:
            # ═══ Suit card drawn ═══
            match suit:
                case ""Cups"":
                    draw_cups()
                case ""Wands"":
                    draw_wands()
                case ""Swords"":
                    draw_swords()
                case ""Pentacles"":
                    draw_pentacles()
";
    }
}
