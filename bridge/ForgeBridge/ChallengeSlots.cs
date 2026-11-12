using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SharedClientCore.SharedClientCore.Code.PVPChallenge.Models;
using Wizards.Mtga.PrivateGame.Challenges;

namespace ForgeBridge
{
    /// <summary>
    /// The direct challenge screen offers a fixed set of deck types (Standard / Brawl / Limited / Alchemy). Its tables are plain
    /// static collections, so at startup they are rebuilt to offer constructed formats and Cube / 40-card, all under
    /// "Challenge Match". Each deck type needs a match type name the client can parse (the server sends ChallengeName), so the ten
    /// slots reuse ten existing ones: the server's ChallengeService.FORMAT_OF and ForgeFormats.DIRECT_SLOTS carry the same table.
    /// Labels: FormatLoc.
    /// </summary>
    public static class ChallengeSlots
    {
        // spinner position == ChallengeSpinnerDeckTypeKey value
        private static readonly (ChallengeMatchTypes slot, string deckFormat, string label)[] Slots =
        {
            (ChallengeMatchTypes.DirectGame, "DirectGame", "MainNav/PrivateGame/DeckType_60_Card"),
            (ChallengeMatchTypes.DirectGameBrawl, "DirectGameBrawlRebalanced", "MainNav/PrivateGame/DeckType_Brawl"),
            (ChallengeMatchTypes.DirectGameAlchemy, "DirectGameAlchemy", "MainNav/PrivateGame/DeckType_60_Card_Alchemy"),
            (ChallengeMatchTypes.DirectGameLimited, "DirectGameLimited", "Forge/Challenge/DeckType_Legacy"),
            (ChallengeMatchTypes.DirectGameTournamentMode, "DirectGameVintage", "Forge/Challenge/DeckType_Vintage"),
            (ChallengeMatchTypes.DirectGameTournamentLimited, "DirectGamePauper", "Forge/Challenge/DeckType_Pauper"),
            (ChallengeMatchTypes.DirectGameTournamentHistoric, "DirectGamePremodern", "Forge/Challenge/DeckType_Premodern"),
            (ChallengeMatchTypes.DirectGameTournamentExplorer, "DirectGameBrawl", "Forge/Challenge/DeckType_Brawl"),
            (ChallengeMatchTypes.DirectGameTournamentTimeless, "DirectGameHistoric", "Forge/Challenge/DeckType_Historic"),
            (ChallengeMatchTypes.DirectGameTournamentAlchemy, "DirectGameCube", "Forge/Challenge/DeckType_Cube"),
        };

        public static void Install()
        {
            try
            {
                var infos = ChallengeUtils.MatchTypeToInfo;
                infos.Clear();
                var deckTypes = ChallengeSpinnerDeckTypes.All;
                deckTypes.Clear();
                var labels = ChallengeSpinnerDeckTypes.Labels;
                labels.Clear();
                for (int i = 0; i < Slots.Length; i++)
                {
                    var key = (ChallengeSpinnerDeckTypeKey)i;
                    infos[Slots[i].slot] = new ChallengeTypeInfo(ChallengeSpinnerMatchTypeKey.ChallengeMatch, Slots[i].deckFormat, key);
                    deckTypes.Add(key, new ChallengeSpinnerDeckType { Label = Slots[i].label, AllowBo3 = true });
                    labels.Add(Slots[i].label);
                }
                // only "Challenge Match" stays in the match type spinner (the tournament variants are not offered)
                var matchTypes = ChallengeSpinnerMatchTypes.All;
                var drop = new List<object>();
                foreach (DictionaryEntry e in matchTypes) if (!ChallengeSpinnerMatchTypeKey.ChallengeMatch.Equals(e.Key)) drop.Add(e.Key);
                foreach (var k in drop) matchTypes.Remove(k);
                if (ChallengeSpinnerMatchTypes.Labels is List<string> ml && ml.Count > 1) ml.RemoveRange(1, ml.Count - 1);
                Log.Info("challenge slots installed (" + Slots.Length + " formats)");
            }
            catch (System.Exception ex)
            {
                Log.Error("challenge slots: " + ex);
            }
        }

        // Arena's widget still forces legacy tournament slots to fixed deck positions after
        // reading our table. Run after that refresh and restore the mapped position without
        // firing a settings change back to the server.
        public static void RestoreSelection(object widget, ChallengeMatchTypes matchType)
        {
            if (!ChallengeUtils.MatchTypeToInfo.TryGetValue(matchType, out var info) || !info.DeckType.HasValue) return;
            var field = widget.GetType().GetField("_deckTypeSpinner", BindingFlags.Instance | BindingFlags.NonPublic);
            var spinner = field?.GetValue(widget) as Spinner_OptionSelector;
            if (spinner == null || spinner.ValueIndex == (int)info.DeckType.Value) return;
            var listeners = spinner.onValueChanged;
            try
            {
                spinner.onValueChanged = new Spinner_OptionSelector.SpinnerValueChangeEvent();
                spinner.ValueIndex = (int)info.DeckType.Value;
            }
            finally { spinner.onValueChanged = listeners; }
        }
    }
}
