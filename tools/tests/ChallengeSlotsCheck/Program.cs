using System;
using System.Collections;
using System.Collections.Generic;
using ForgeBridge;
using SharedClientCore.SharedClientCore.Code.PVPChallenge.Models;
using Wizards.Mtga.PrivateGame.Challenges;

class Widget
{
    private Spinner_OptionSelector _deckTypeSpinner;
    public Widget(Spinner_OptionSelector spinner) { _deckTypeSpinner = spinner; }
}
class Regression
{
    static void Main()
    {
        ChallengeSlots.Install();
        int checkedSlots = 0;
        foreach (var entry in ChallengeUtils.MatchTypeToInfo)
        {
            var spinner = new Spinner_OptionSelector();
            var listeners = spinner.onValueChanged;
            // Reproduce the legacy UI resets: Alchemy -> index 3, Historic -> index 0.
            spinner.ValueIndex = entry.Key == ChallengeMatchTypes.DirectGameTournamentAlchemy ? 3 : 0;
            listeners.Calls = 0;
            ChallengeSlots.RestoreSelection(new Widget(spinner), entry.Key);
            if (spinner.ValueIndex != (int)entry.Value.DeckType.Value || listeners.Calls != 0 || !Object.ReferenceEquals(listeners, spinner.onValueChanged))
                throw new Exception("Selection or listeners incorrect: " + entry.Key);
            checkedSlots++;
        }
        if (checkedSlots != 10) throw new Exception("Expected all ten slots");
        Console.WriteLine("PASS: all ten slots restored; no settings feedback; original listeners preserved.");
    }
}
public class Spinner_OptionSelector
{
    public class SpinnerValueChangeEvent { public int Calls; public void Invoke() { Calls++; } }
    public SpinnerValueChangeEvent onValueChanged = new SpinnerValueChangeEvent();
    private int index;
    public int ValueIndex { get { return index; } set { if(index != value) { index = value; onValueChanged.Invoke(); } } }
}
namespace ForgeBridge { static class Log { public static void Info(string s) {} public static void Error(string s) { throw new Exception(s); } } }
namespace SharedClientCore.SharedClientCore.Code.PVPChallenge.Models
{
    public enum ChallengeMatchTypes { DirectGame, DirectGameBrawl, DirectGameAlchemy, DirectGameLimited, DirectGameTournamentMode, DirectGameTournamentLimited, DirectGameTournamentHistoric, DirectGameTournamentExplorer, DirectGameTournamentTimeless, DirectGameTournamentAlchemy }
}
namespace Wizards.Mtga.PrivateGame.Challenges
{
    public enum ChallengeSpinnerDeckTypeKey { Standard }
    public enum ChallengeSpinnerMatchTypeKey { ChallengeMatch }
    public class ChallengeTypeInfo
    {
        public ChallengeSpinnerDeckTypeKey? DeckType;
        public ChallengeTypeInfo(ChallengeSpinnerMatchTypeKey mode, string format, ChallengeSpinnerDeckTypeKey key) { DeckType = key; }
    }
    public class ChallengeSpinnerDeckType { public string Label; public bool AllowBo3; }
    public static class ChallengeUtils { public static Dictionary<ChallengeMatchTypes, ChallengeTypeInfo> MatchTypeToInfo = new Dictionary<ChallengeMatchTypes, ChallengeTypeInfo>(); }
    public static class ChallengeSpinnerDeckTypes
    {
        public static Dictionary<ChallengeSpinnerDeckTypeKey, ChallengeSpinnerDeckType> All = new Dictionary<ChallengeSpinnerDeckTypeKey, ChallengeSpinnerDeckType>();
        public static List<string> Labels = new List<string>();
    }
    public static class ChallengeSpinnerMatchTypes
    {
        public static Hashtable All = new Hashtable();
        public static List<string> Labels = new List<string>();
    }
}
