using System.Collections.Generic;

namespace Noctyra
{
    public enum Role { Villager = 0, Vampire = 1, Doctor = 2, Seer = 3, Hunter = 4 }

    public enum Team { Villagers, Vampires }

    public enum DeathCause { Vampire, Lynch, HunterShot }

    public enum StatementKind { Accuse, Defend, ClaimSeer, ClaimDoctor, Agree, Counter, Neutral }

    public static class RoleInfo
    {
        public static string Name(Role r)
        {
            switch (r)
            {
                case Role.Villager: return "Köylü";
                case Role.Vampire: return "Vampir";
                case Role.Doctor: return "Doktor";
                case Role.Seer: return "Falcı";
                default: return "Avcı";
            }
        }

        public static Team TeamOf(Role r)
        {
            return r == Role.Vampire ? Team.Vampires : Team.Villagers;
        }

        public static string Description(Role r)
        {
            switch (r)
            {
                case Role.Villager:
                    return "Özel bir gücün yok. Gözlerine, mantığına ve diline güven. Tüm vampirleri bulup as.";
                case Role.Vampire:
                    return "Geceleri köylülerden birini avlarsın. Diğer vampirleri tanırsın. Gündüz masum görün, sayıca köylülere eşitlen.";
                case Role.Doctor:
                    return "Her gece birini korursun (kendin dahil). Aynı kişiyi üst üste iki gece koruyamazsın.";
                case Role.Seer:
                    return "Her gece birinin vampir olup olmadığını öğrenirsin. Bildiklerini ne zaman açıklayacağın sana kalmış.";
                default:
                    return "Öldüğün an (gece ya da oylamada) son bir kurşun sıkar, birini yanına alırsın.";
            }
        }
    }

    public class Player
    {
        public int Id;
        public string Name;
        public Role Role;
        public bool Alive = true;
        public bool IsHuman;
        public DeathCause Cause;
        public int DeathDay;
    }

    public class Death
    {
        public Player Victim;
        public DeathCause Cause;
    }

    public class GameSettings
    {
        public string HumanName = "Sen";
        public int PlayerCount = 8;
        /// <summary>-1 = rastgele, aksi halde (int)Role.</summary>
        public int RolePreference = -1;
        public bool RevealRoles = true;
    }

    public class Statement
    {
        public Player Speaker;
        public StatementKind Kind;
        public Player Target;
        public string Text;
    }

    public class Claim
    {
        public Player Claimer;
        public Role Role;
        public Player Target;
        public bool TargetIsVampire;
        public int Day;
    }

    public class NightReport
    {
        public Death Death;
        public bool AttackSaved;
        public Player SavedPlayer;
    }

    public class VoteReport
    {
        public List<Player> Voters = new List<Player>();
        public List<Player> Targets = new List<Player>();
        public Dictionary<int, int> Tally = new Dictionary<int, int>();
        public Player Lynched;
    }
}
