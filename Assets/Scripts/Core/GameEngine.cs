using System;
using System.Collections.Generic;
using System.Linq;

namespace Noctyra
{
    /// <summary>
    /// Vampir-Köylü oyununun tüm kuralları ve bot yapay zekası.
    /// UnityEngine'e bağımlılığı yoktur; arayüz (GameController) sadece bu sınıfı çağırır.
    /// </summary>
    public class GameEngine
    {
        class SeerResult
        {
            public int Target;
            public bool IsVampire;
        }

        public GameSettings Settings;
        public List<Player> Players = new List<Player>();
        public Player Human;
        public int Day;
        public Random Rng;
        public List<Claim> Claims = new List<Claim>();
        public Dictionary<int, bool> HumanInspections = new Dictionary<int, bool>();
        public Player PendingHunter;
        public Team? Winner;

        // Bot beyni: susp[bot, hedef] = şüphe (0..1), know[bot, hedef] = 0 bilinmiyor, 1 güvenli, 2 vampir
        float[,] susp;
        int[,] know;

        int vampTarget = -1;
        int docTarget = -1;
        int lastDocSaved = -1;
        bool docSuccessUnclaimed;
        int docSavedId = -1;

        readonly Dictionary<int, int> accuseCount = new Dictionary<int, int>();
        readonly Dictionary<int, int> vampHits = new Dictionary<int, int>();
        readonly List<SeerResult> botSeerResults = new List<SeerResult>();

        // ------------------------------------------------------------------ Kurulum

        public static Dictionary<Role, int> RoleCounts(int n)
        {
            var d = new Dictionary<Role, int>();
            int vamps = Math.Max(1, n / 4);
            d[Role.Vampire] = vamps;
            d[Role.Doctor] = 1;
            d[Role.Seer] = 1;
            d[Role.Hunter] = n >= 7 ? 1 : 0;
            d[Role.Villager] = n - vamps - 2 - d[Role.Hunter];
            return d;
        }

        public static string Describe(int n)
        {
            var d = RoleCounts(n);
            var parts = new List<string>();
            foreach (Role r in new[] { Role.Vampire, Role.Doctor, Role.Seer, Role.Hunter, Role.Villager })
                if (d[r] > 0) parts.Add(RoleInfo.Name(r) + " " + d[r]);
            return string.Join("  •  ", parts.ToArray());
        }

        public void StartGame(GameSettings s, int seed)
        {
            Settings = s;
            Rng = new Random(seed);
            Players.Clear();
            Claims.Clear();
            HumanInspections.Clear();
            accuseCount.Clear();
            vampHits.Clear();
            botSeerResults.Clear();
            PendingHunter = null;
            Winner = null;
            Day = 0;
            vampTarget = docTarget = lastDocSaved = docSavedId = -1;
            docSuccessUnclaimed = false;

            int n = Math.Max(6, Math.Min(12, s.PlayerCount));
            var roles = new List<Role>();
            foreach (var kv in RoleCounts(n))
                for (int i = 0; i < kv.Value; i++) roles.Add(kv.Key);
            Shuffle(roles);

            int humanSeat = Rng.Next(n);
            if (s.RolePreference >= 0)
            {
                int idx = roles.IndexOf((Role)s.RolePreference);
                if (idx >= 0)
                {
                    Role tmp = roles[humanSeat];
                    roles[humanSeat] = roles[idx];
                    roles[idx] = tmp;
                }
            }

            string humanName = string.IsNullOrWhiteSpace(s.HumanName) ? "Sen" : s.HumanName.Trim();
            var pool = new List<string>(Dialogue.Names);
            pool.RemoveAll(x => string.Equals(x, humanName, StringComparison.OrdinalIgnoreCase));
            Shuffle(pool);

            int ni = 0;
            for (int i = 0; i < n; i++)
            {
                bool isHuman = i == humanSeat;
                Players.Add(new Player
                {
                    Id = i,
                    Role = roles[i],
                    IsHuman = isHuman,
                    Name = isHuman ? humanName : pool[ni++]
                });
            }
            Human = Players[humanSeat];

            susp = new float[n, n];
            know = new int[n, n];
            for (int b = 0; b < n; b++)
            {
                for (int t = 0; t < n; t++)
                    susp[b, t] = 0.25f + (float)Rng.NextDouble() * 0.15f;
                know[b, b] = 1;
                if (Players[b].Role != Role.Vampire) continue;
                for (int t = 0; t < n; t++)
                    if (Players[t].Role == Role.Vampire) know[b, t] = 1;
            }
        }

        // ------------------------------------------------------------------ Yardımcılar

        void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        public List<Player> AlivePlayers() { return Players.Where(p => p.Alive).ToList(); }

        Player FindAlive(Role r) { return Players.FirstOrDefault(p => p.Alive && p.Role == r); }

        public bool HasClaimed(Player p) { return Claims.Any(c => c.Claimer == p); }

        int AccusedCount(int id)
        {
            int v;
            return accuseCount.TryGetValue(id, out v) ? v : 0;
        }

        void AddAccuse(int id, int amount)
        {
            accuseCount[id] = AccusedCount(id) + amount;
        }

        Player WeightedPick(List<Player> cands, List<float> weights)
        {
            if (cands.Count == 0) return null;
            float total = 0f;
            foreach (float w in weights) total += Math.Max(0.01f, w);
            float roll = (float)Rng.NextDouble() * total;
            for (int i = 0; i < cands.Count; i++)
            {
                roll -= Math.Max(0.01f, weights[i]);
                if (roll <= 0f) return cands[i];
            }
            return cands[cands.Count - 1];
        }

        Death Kill(Player p, DeathCause cause)
        {
            p.Alive = false;
            p.Cause = cause;
            p.DeathDay = Day;
            if (p.Role == Role.Hunter) PendingHunter = p;
            return new Death { Victim = p, Cause = cause };
        }

        public Team? CheckWinner()
        {
            int vamps = 0, others = 0;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                if (p.Role == Role.Vampire) vamps++; else others++;
            }
            if (vamps == 0) Winner = Team.Villagers;
            else if (vamps >= others) Winner = Team.Vampires;
            return Winner;
        }

        // ------------------------------------------------------------------ Gece

        public void BeginNight()
        {
            Day++;
            vampTarget = -1;
            docTarget = -1;
        }

        public bool HumanHasNightAction
        {
            get
            {
                return Human.Alive &&
                       (Human.Role == Role.Vampire || Human.Role == Role.Doctor || Human.Role == Role.Seer);
            }
        }

        public List<Player> NightTargets()
        {
            var list = new List<Player>();
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                switch (Human.Role)
                {
                    case Role.Vampire:
                        if (p.Role != Role.Vampire) list.Add(p);
                        break;
                    case Role.Doctor:
                        if (p.Id != lastDocSaved) list.Add(p);
                        break;
                    case Role.Seer:
                        if (p != Human && !HumanInspections.ContainsKey(p.Id)) list.Add(p);
                        break;
                }
            }
            return list;
        }

        /// <summary>İnsan oyuncunun gece hamlesi. Falcıysa sonucu metin olarak döner.</summary>
        public string SubmitHumanNight(Player target)
        {
            if (target == null) return null;
            switch (Human.Role)
            {
                case Role.Vampire:
                    vampTarget = target.Id;
                    return null;
                case Role.Doctor:
                    docTarget = target.Id;
                    return null;
                case Role.Seer:
                    bool v = target.Role == Role.Vampire;
                    HumanInspections[target.Id] = v;
                    return target.Name + (v ? " bir VAMPİR!" : " temiz çıktı, vampir değil.");
            }
            return null;
        }

        public NightReport ResolveNight()
        {
            if (vampTarget < 0 && FindAlive(Role.Vampire) != null)
                vampTarget = BotChooseKill();

            var doc = FindAlive(Role.Doctor);
            if (doc != null && !doc.IsHuman && docTarget < 0)
                docTarget = BotChooseSave();

            var seer = FindAlive(Role.Seer);
            if (seer != null && !seer.IsHuman)
                BotInspect(seer);

            var rep = new NightReport();
            if (vampTarget >= 0)
            {
                var victim = Players[vampTarget];
                if (vampTarget == docTarget && doc != null)
                {
                    rep.AttackSaved = true;
                    rep.SavedPlayer = victim;
                    if (!doc.IsHuman)
                    {
                        docSuccessUnclaimed = true;
                        docSavedId = victim.Id;
                    }
                }
                else
                {
                    rep.Death = Kill(victim, DeathCause.Vampire);
                }
            }
            lastDocSaved = doc != null ? docTarget : -1;
            return rep;
        }

        int BotChooseKill()
        {
            var cands = new List<Player>();
            var weights = new List<float>();
            foreach (var p in Players)
            {
                if (!p.Alive || p.Role == Role.Vampire) continue;
                float w = 1f;
                foreach (var c in Claims)
                {
                    if (c.Claimer != p) continue;
                    w += c.Role == Role.Seer ? 4f : 2.5f;
                }
                int hits;
                if (vampHits.TryGetValue(p.Id, out hits)) w += 1.5f * hits;
                if (p.IsHuman) w += 0.3f;
                cands.Add(p);
                weights.Add(w);
            }
            var pick = WeightedPick(cands, weights);
            return pick != null ? pick.Id : -1;
        }

        int BotChooseSave()
        {
            var cands = new List<Player>();
            var weights = new List<float>();
            var doc = FindAlive(Role.Doctor);
            foreach (var p in Players)
            {
                if (!p.Alive || p.Id == lastDocSaved) continue;
                float w = 1f;
                if (p == doc) w = 1.5f;
                if (Claims.Any(c => c.Claimer == p && c.Role == Role.Seer)) w += 3f;
                cands.Add(p);
                weights.Add(w);
            }
            var pick = WeightedPick(cands, weights);
            return pick != null ? pick.Id : -1;
        }

        void BotInspect(Player seer)
        {
            var cands = new List<Player>();
            var weights = new List<float>();
            foreach (var p in Players)
            {
                if (!p.Alive || p == seer) continue;
                if (know[seer.Id, p.Id] != 0) continue;
                cands.Add(p);
                weights.Add(susp[seer.Id, p.Id] + 0.2f);
            }
            var pick = WeightedPick(cands, weights);
            if (pick == null) return;
            bool v = pick.Role == Role.Vampire;
            know[seer.Id, pick.Id] = v ? 2 : 1;
            susp[seer.Id, pick.Id] = v ? 1f : 0f;
            botSeerResults.Add(new SeerResult { Target = pick.Id, IsVampire = v });
        }

        // ------------------------------------------------------------------ Avcı

        public Player BotHunterTarget()
        {
            var h = PendingHunter;
            if (h == null) return null;
            Player best = null;
            float bestScore = -999f;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                float s = susp[h.Id, p.Id] + (float)Rng.NextDouble() * 0.2f;
                if (know[h.Id, p.Id] == 2) s += 10f;
                if (know[h.Id, p.Id] == 1) s -= 10f;
                if (s > bestScore) { bestScore = s; best = p; }
            }
            // Hiçbir şüphelisi yoksa (hepsi güvenli) ateş etmez
            return bestScore < -5f ? null : best;
        }

        public Death ResolveHunter(Player target)
        {
            PendingHunter = null;
            if (target == null || !target.Alive) return null;
            return Kill(target, DeathCause.HunterShot);
        }

        // ------------------------------------------------------------------ Gündüz tartışma

        public void BeginDay()
        {
            accuseCount.Clear();
        }

        public List<Statement> OpeningRound()
        {
            var list = new List<Statement>();
            var bots = Players.Where(p => p.Alive && !p.IsHuman).ToList();
            Shuffle(bots);
            int count = Math.Min(bots.Count, 3 + Rng.Next(2));
            for (int i = 0; i < count; i++)
            {
                if (!bots[i].Alive) continue;
                list.AddRange(BotSpeak(bots[i]));
            }
            return list;
        }

        List<Statement> BotSpeak(Player b)
        {
            var res = new List<Statement>();

            // 1) Falcı: bulduğu bilgiyi açıklar
            if (b.Role == Role.Seer && !HasClaimed(b))
            {
                SeerResult pick = null;
                foreach (var r in botSeerResults)
                {
                    if (!Players[r.Target].Alive) continue;
                    if (pick == null || (r.IsVampire && !pick.IsVampire) || r.IsVampire == pick.IsVampire) pick = r;
                }
                if (pick != null)
                {
                    bool accused = AccusedCount(b.Id) > 0;
                    bool go = (pick.IsVampire && Rng.NextDouble() < 0.85)
                              || accused && Rng.NextDouble() < 0.7
                              || (Day >= 3 && Rng.NextDouble() < 0.4);
                    if (go)
                    {
                        res.AddRange(MakeClaim(b, Role.Seer, Players[pick.Target], pick.IsVampire));
                        return res;
                    }
                }
            }

            // 2) Doktor: başarılı korumayı açıklar
            if (b.Role == Role.Doctor && docSuccessUnclaimed && !HasClaimed(b) && Rng.NextDouble() < 0.8)
            {
                docSuccessUnclaimed = false;
                res.AddRange(MakeClaim(b, Role.Doctor, Players[docSavedId], false));
                return res;
            }

            // 3) Vampir blöfü
            if (b.Role == Role.Vampire && !HasClaimed(b) && Day >= 2 && Rng.NextDouble() < 0.12)
            {
                bool seerOut = Claims.Any(c => c.Role == Role.Seer && c.Claimer.Alive);
                if (!seerOut)
                {
                    var victims = Players.Where(p => p.Alive && p.Role != Role.Vampire).ToList();
                    if (victims.Count > 0)
                    {
                        res.AddRange(MakeClaim(b, Role.Seer, victims[Rng.Next(victims.Count)], true));
                        return res;
                    }
                }
                else if (!Claims.Any(c => c.Role == Role.Doctor))
                {
                    res.AddRange(MakeClaim(b, Role.Doctor, null, false));
                    return res;
                }
            }

            // 4) Normal konuşma
            double roll = Rng.NextDouble();
            if (AccusedCount(b.Id) > 0 && roll < 0.65)
            {
                ApplyDefense(b);
                res.Add(new Statement { Speaker = b, Kind = StatementKind.Defend, Text = Dialogue.Defend(Rng) });
            }
            else if (roll < 0.78)
            {
                var t = BotPickSuspect(b);
                if (t != null) res.Add(MakeAccuse(b, t, false));
                else res.Add(Neutral(b));
            }
            else
            {
                res.Add(Neutral(b));
            }
            return res;
        }

        Statement Neutral(Player b)
        {
            return new Statement { Speaker = b, Kind = StatementKind.Neutral, Text = Dialogue.Neutral(Rng) };
        }

        Player BotPickSuspect(Player b)
        {
            var cands = new List<Player>();
            var weights = new List<float>();
            foreach (var p in Players)
            {
                if (!p.Alive || p == b) continue;
                if (know[b.Id, p.Id] == 1) continue;
                float w;
                if (b.Role == Role.Vampire)
                {
                    w = 1f + AccusedCount(p.Id);
                    if (Claims.Any(c => c.Claimer == p && c.Role == Role.Seer)) w += 3f;
                }
                else
                {
                    w = susp[b.Id, p.Id] * 3f + 0.2f;
                    if (know[b.Id, p.Id] == 2) w += 20f;
                }
                cands.Add(p);
                weights.Add(w);
            }
            return WeightedPick(cands, weights);
        }

        Statement MakeAccuse(Player speaker, Player target, bool human)
        {
            ApplyAccusation(speaker, target);
            return new Statement
            {
                Speaker = speaker,
                Kind = StatementKind.Accuse,
                Target = target,
                Text = human ? Dialogue.HumanAccuse(Rng, target.Name) : Dialogue.Accuse(Rng, target.Name)
            };
        }

        void ApplyAccusation(Player accuser, Player target)
        {
            AddAccuse(target.Id, 1);
            if (target.Role == Role.Vampire && accuser.Role != Role.Vampire)
            {
                int v;
                vampHits.TryGetValue(accuser.Id, out v);
                vampHits[accuser.Id] = v + 1;
            }
            foreach (var b in Players)
            {
                if (!b.Alive || b.IsHuman || b == accuser) continue;
                int bi = b.Id;
                if (b == target)
                {
                    susp[bi, accuser.Id] = Clamp01(susp[bi, accuser.Id] + 0.1f);
                    continue;
                }
                if (b.Role == Role.Vampire)
                {
                    // Müttefikini suçlayanı fark eder
                    if (target.Role == Role.Vampire && accuser.Role != Role.Vampire)
                        susp[bi, accuser.Id] = Clamp01(susp[bi, accuser.Id] + 0.25f);
                    continue;
                }
                float trust = 1f - susp[bi, accuser.Id];
                float gain = 0.10f * trust * (accuser.IsHuman ? 1.2f : 1f);
                if (know[bi, target.Id] == 0)
                    susp[bi, target.Id] = Clamp01(susp[bi, target.Id] + gain);
            }
        }

        void ApplyDefense(Player defender)
        {
            foreach (var b in Players)
            {
                if (!b.Alive || b.IsHuman || b == defender || b.Role == Role.Vampire) continue;
                if (know[b.Id, defender.Id] != 0) continue;
                susp[b.Id, defender.Id] = Clamp01(susp[b.Id, defender.Id] - 0.06f);
            }
        }

        List<Statement> MakeClaim(Player speaker, Role role, Player target, bool isVamp)
        {
            var claim = new Claim
            {
                Claimer = speaker,
                Role = role,
                Target = target,
                TargetIsVampire = isVamp,
                Day = Day
            };
            ApplyClaim(claim);

            string text;
            if (role == Role.Seer)
                text = isVamp ? Dialogue.SeerVamp(Rng, target.Name) : Dialogue.SeerClean(Rng, target.Name);
            else
                text = target != null ? Dialogue.DoctorSaved(Rng, target.Name) : Dialogue.DoctorGeneric(Rng);

            var res = new List<Statement>();
            res.Add(new Statement
            {
                Speaker = speaker,
                Kind = role == Role.Seer ? StatementKind.ClaimSeer : StatementKind.ClaimDoctor,
                Target = target,
                Text = text
            });
            res.AddRange(ClaimReactions(claim));
            return res;
        }

        void ApplyClaim(Claim claim)
        {
            Claims.Add(claim);
            var claimer = claim.Claimer;
            if (claim.Role == Role.Seer && claim.TargetIsVampire && claim.Target != null)
                AddAccuse(claim.Target.Id, 2);

            var seerClaimants = Claims.Where(c => c.Role == Role.Seer && c.Claimer.Alive).Select(c => c.Claimer).Distinct().ToList();
            var docClaimants = Claims.Where(c => c.Role == Role.Doctor && c.Claimer.Alive).Select(c => c.Claimer).Distinct().ToList();

            foreach (var b in Players)
            {
                if (!b.Alive || b.IsHuman || b == claimer) continue;
                int bi = b.Id;

                if (b.Role == Role.Vampire)
                {
                    if (claimer.Role == Role.Vampire) continue;
                    if (claimer.Role == claim.Role) susp[bi, claimer.Id] = 1f;
                    else susp[bi, claimer.Id] = Math.Max(susp[bi, claimer.Id], 0.5f);
                    continue;
                }

                // Gerçek rol sahibi, taklitçiyi hemen tanır
                if (b.Role == claim.Role)
                {
                    susp[bi, claimer.Id] = 1f;
                    know[bi, claimer.Id] = 2;
                    continue;
                }

                if (claim.Role == Role.Seer)
                {
                    if (seerClaimants.Count <= 1)
                    {
                        susp[bi, claimer.Id] = Clamp01(susp[bi, claimer.Id] - 0.25f);
                        if (claim.Target == b)
                        {
                            if (claim.TargetIsVampire) susp[bi, claimer.Id] = 1f;
                        }
                        else if (claim.Target != null && know[bi, claim.Target.Id] == 0)
                        {
                            float delta = claim.TargetIsVampire ? 0.6f : -0.3f;
                            susp[bi, claim.Target.Id] = Clamp01(susp[bi, claim.Target.Id] + delta);
                        }
                    }
                    else
                    {
                        foreach (var c in seerClaimants)
                            if (c != b && know[bi, c.Id] == 0)
                                susp[bi, c.Id] = Math.Max(susp[bi, c.Id], 0.7f);
                    }
                }
                else if (claim.Role == Role.Doctor)
                {
                    if (docClaimants.Count <= 1)
                        susp[bi, claimer.Id] = Clamp01(susp[bi, claimer.Id] - 0.15f);
                    else
                        foreach (var c in docClaimants)
                            if (c != b && know[bi, c.Id] == 0)
                                susp[bi, c.Id] = Math.Max(susp[bi, c.Id], 0.65f);
                }
            }
        }

        List<Statement> ClaimReactions(Claim claim)
        {
            var res = new List<Statement>();
            var bots = Players.Where(p => p.Alive && !p.IsHuman && p != claim.Claimer).ToList();
            Shuffle(bots);
            int added = 0;
            foreach (var b in bots)
            {
                if (added >= 2) break;
                if (b.Role == Role.Vampire)
                {
                    if (claim.Claimer.Role == Role.Vampire) continue;
                    bool hitsAlly = claim.Role == Role.Seer && claim.TargetIsVampire
                                    && claim.Target != null && claim.Target.Role == Role.Vampire;
                    bool trueClaim = claim.Claimer.Role == claim.Role;
                    if (hitsAlly || (trueClaim && Rng.NextDouble() < 0.6))
                    {
                        ApplyAccusation(b, claim.Claimer);
                        res.Add(new Statement
                        {
                            Speaker = b,
                            Kind = StatementKind.Counter,
                            Target = claim.Claimer,
                            Text = Dialogue.CounterClaim(Rng, claim.Claimer.Name)
                        });
                        added++;
                    }
                }
                else if (susp[b.Id, claim.Claimer.Id] < 0.5f && Rng.NextDouble() < 0.6)
                {
                    res.Add(new Statement { Speaker = b, Kind = StatementKind.Agree, Target = claim.Claimer, Text = Dialogue.Agree(Rng) });
                    added++;
                }
            }
            return res;
        }

        // ------------------------------------------------------------------ İnsan oyuncunun gündüz hamleleri

        public List<Statement> HumanAccuse(Player target)
        {
            var res = new List<Statement>();
            res.Add(MakeAccuse(Human, target, true));

            if (target.Alive && !target.IsHuman && Rng.NextDouble() < 0.9)
            {
                ApplyDefense(target);
                res.Add(new Statement { Speaker = target, Kind = StatementKind.Defend, Text = Dialogue.Defend(Rng) });
            }

            var bots = Players.Where(p => p.Alive && !p.IsHuman && p != target).ToList();
            Shuffle(bots);
            int added = 0;
            foreach (var b in bots)
            {
                if (added >= 2) break;
                bool agrees;
                if (b.Role == Role.Vampire) agrees = target.Role != Role.Vampire;
                else agrees = susp[b.Id, target.Id] > 0.4f;

                if (agrees && Rng.NextDouble() < 0.7)
                {
                    AddAccuse(target.Id, 1);
                    res.Add(new Statement { Speaker = b, Kind = StatementKind.Agree, Target = target, Text = Dialogue.Agree(Rng) });
                    added++;
                }
                else if (!agrees && susp[b.Id, target.Id] < 0.25f && Rng.NextDouble() < 0.5)
                {
                    res.Add(new Statement { Speaker = b, Kind = StatementKind.Counter, Target = Human, Text = Dialogue.Counter(Rng) });
                    added++;
                }
            }
            return res;
        }

        public List<Statement> HumanDefend()
        {
            var res = new List<Statement>();
            res.Add(new Statement { Speaker = Human, Kind = StatementKind.Defend, Text = Dialogue.HumanDefend(Rng) });
            foreach (var b in Players)
            {
                if (!b.Alive || b.IsHuman || b.Role == Role.Vampire) continue;
                if (know[b.Id, Human.Id] != 0) continue;
                susp[b.Id, Human.Id] = Clamp01(susp[b.Id, Human.Id] - 0.08f);
            }
            var bots = Players.Where(p => p.Alive && !p.IsHuman).ToList();
            if (bots.Count > 0)
            {
                var b = bots[Rng.Next(bots.Count)];
                bool doubt = AccusedCount(Human.Id) > 1 && Rng.NextDouble() < 0.5;
                res.Add(new Statement
                {
                    Speaker = b,
                    Kind = doubt ? StatementKind.Counter : StatementKind.Agree,
                    Target = Human,
                    Text = doubt ? Dialogue.Counter(Rng) : Dialogue.Agree(Rng)
                });
            }
            return res;
        }

        public List<Statement> HumanClaim(Role role, Player target, bool isVampire)
        {
            return MakeClaim(Human, role, target, isVampire);
        }

        // ------------------------------------------------------------------ Oylama

        public VoteReport RunVote(Player humanVote)
        {
            var rep = new VoteReport();
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                Player choice = p.IsHuman ? humanVote : BotVote(p);
                rep.Voters.Add(p);
                rep.Targets.Add(choice);
                if (choice != null)
                {
                    int c;
                    rep.Tally.TryGetValue(choice.Id, out c);
                    rep.Tally[choice.Id] = c + 1;
                }
            }

            if (rep.Tally.Count > 0)
            {
                int max = rep.Tally.Values.Max();
                var top = rep.Tally.Where(kv => kv.Value == max).ToList();
                if (top.Count == 1) rep.Lynched = Players[top[0].Key];
            }
            return rep;
        }

        Player BotVote(Player p)
        {
            var cands = new List<Player>();
            var weights = new List<float>();
            foreach (var c in Players)
            {
                if (!c.Alive || c == p) continue;
                if (know[p.Id, c.Id] == 1) continue;

                if (p.Role != Role.Vampire && know[p.Id, c.Id] == 2) return c;

                float w;
                if (p.Role == Role.Vampire)
                {
                    w = 1f + 1.5f * AccusedCount(c.Id);
                    if (Claims.Any(x => x.Claimer == c && x.Role == Role.Seer)) w += 4f;
                    if (c.IsHuman) w += 0.5f;
                }
                else
                {
                    float noise = (float)Rng.NextDouble() * 0.2f;
                    float score = susp[p.Id, c.Id] + 0.08f * AccusedCount(c.Id) + noise;
                    w = score * score * score * 8f + 0.05f;
                }
                cands.Add(c);
                weights.Add(w);
            }

            if (p.Role != Role.Vampire)
            {
                // Köylü taraf: en şüpheli kişiye (küçük bir gürültüyle) oy verir
                Player best = null;
                float bestW = -1f;
                for (int i = 0; i < cands.Count; i++)
                    if (weights[i] > bestW) { bestW = weights[i]; best = cands[i]; }
                return best;
            }
            return WeightedPick(cands, weights);
        }

        public Death ApplyLynch(VoteReport rep)
        {
            LearnFromVote(rep);
            if (rep.Lynched == null) return null;
            return Kill(rep.Lynched, DeathCause.Lynch);
        }

        void LearnFromVote(VoteReport rep)
        {
            if (rep.Lynched == null || !Settings.RevealRoles) return;
            bool vamp = rep.Lynched.Role == Role.Vampire;
            foreach (var b in Players)
            {
                if (!b.Alive || b.IsHuman || b.Role == Role.Vampire) continue;
                know[b.Id, rep.Lynched.Id] = vamp ? 2 : 1;
                for (int i = 0; i < rep.Voters.Count; i++)
                {
                    var x = rep.Voters[i];
                    if (x == b || !x.Alive || know[b.Id, x.Id] != 0) continue;
                    if (rep.Targets[i] == rep.Lynched)
                        susp[b.Id, x.Id] = Clamp01(susp[b.Id, x.Id] + (vamp ? -0.12f : 0.12f));
                    else if (vamp && rep.Targets[i] != null)
                        susp[b.Id, x.Id] = Clamp01(susp[b.Id, x.Id] + 0.06f);
                }
            }
        }
    }
}
