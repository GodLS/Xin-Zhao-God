using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using System.Drawing;
using Color = System.Drawing.Color;
using LeagueSharp.Common.Data;


namespace Xin
{
    class Program
    {
        private static Obj_AI_Hero Player;
        private static Menu Config;
        private static Orbwalking.Orbwalker Orbwalker;

        private static Spell Q, W, E, R;
        private static SpellSlot ignite;
        private static Items.Item youmuu, cutlass, blade, tiamat, hydra;


        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += GameOnOnGameLoad;
        }

        private static void GameOnOnGameLoad(EventArgs args)
        {
            Player = ObjectManager.Player;

            if (Player.BaseSkinName != "XinZhao")
            {
                return;
            }

            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 600);
            R = new Spell(SpellSlot.R, 500);

            ignite = Player.GetSpellSlot("summonerdot");
            youmuu = new Items.Item(3142, 0f);
            cutlass = new Items.Item(3144, 450f);
            blade = new Items.Item(3153, 450f);
            tiamat = new Items.Item(3077, 400f);
            hydra = new Items.Item(3074, 400f);
           
            Config = new Menu("Xin Zhao God", "XinZhaoGod", true);

            Menu orbwalkerMenu = new Menu("Orbwalker", "Orbwalker");
            Orbwalker = new Xin.Orbwalking.Orbwalker(orbwalkerMenu);
            Config.AddSubMenu(orbwalkerMenu);

            var TargetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(TargetSelectorMenu);
            Config.AddSubMenu(TargetSelectorMenu);

            var comboMenu = new Menu("Combo", "Combo");
            comboMenu.AddItem(new MenuItem("UseQCombo", "Use Q in Combo").SetValue(true));
            comboMenu.AddItem(new MenuItem("UseWCombo", "Use W in Combo").SetValue(true));
            comboMenu.AddItem(new MenuItem("UseECombo", "Use E in Combo").SetValue(true));
            comboMenu.AddItem(new MenuItem("MinERangeCombo", "Minimum range to E").SetValue(new Slider(350, 0, 600)));
            comboMenu.AddItem(new MenuItem("UseRCombo", "Use R Always").SetValue(true));
            comboMenu.AddItem(new MenuItem("UseRComboKillable", "Use R Killable").SetValue(true));
            comboMenu.AddItem(new MenuItem("UseRAoE", "Use R AoE ").SetValue(true));
            comboMenu.AddItem(new MenuItem("MinRTargets", "Minimum targets to R").SetValue(new Slider(3, 1, 5)));
            Config.AddSubMenu(comboMenu);

            var harassMenu = new Menu("Harass", "Harass");
            harassMenu.AddItem(new MenuItem("UseQHarass", "Use Q in Harass").SetValue(true));
            harassMenu.AddItem(new MenuItem("UseWHarass", "Use W in Harass").SetValue(true));
            harassMenu.AddItem(new MenuItem("UseEHarass", "Use E in Harass").SetValue(true));
            harassMenu.AddItem(new MenuItem("MinERangeHarass", "Minimum Range to E").SetValue(new Slider(350, 0, 600)));
            Config.AddSubMenu(harassMenu);

            var drawingsMenu = new Menu("Drawings", "Drawings");            
            drawingsMenu.AddItem(new MenuItem("eRangeMin", "E Range Minimum").SetValue(new Circle(true, Color.FromArgb(100, 255, 255, 255))));
            drawingsMenu.AddItem(new MenuItem("eRangeMax", "E Range Maximum").SetValue(new Circle(true, Color.FromArgb(100, 255, 255, 255))));
            drawingsMenu.AddItem(new MenuItem("rRange", "R Range").SetValue(new Circle(true, Color.FromArgb(100, 255, 255, 255))));
            drawingsMenu.AddItem(new MenuItem("challenged", "Circle Challenged Target").SetValue(new Circle(true, Color.FromArgb(100, 255, 255, 255))));
            var dmgAfterCombo = new MenuItem("DamageAfterCombo", "Draw Damage After Combo").SetValue(true);
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Utility.HpBarDamageIndicator.Enabled = dmgAfterCombo.GetValue<bool>();
            drawingsMenu.AddItem(dmgAfterCombo);
            Config.AddSubMenu(drawingsMenu);

            Config.AddItem(new MenuItem("KillstealE", "Killsteal with E").SetValue(true));
            Config.AddItem(new MenuItem("KillstealR", "Killsteal with R").SetValue(true));
            Config.AddItem(new MenuItem("UseIgnite", "Ignite if Combo Killable").SetValue(true));
            Config.AddItem(new MenuItem("UseItems", "Use Items").SetValue(true));

            Config.AddToMainMenu();

            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
            Orbwalking.OnAttack += onAttack;
            Orbwalking.BeforeAttack += OrbwalkingOnBeforeAttack;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
        }

        // taken from honda
        private static float ComboDamage(Obj_AI_Hero hero)
        {
            var dmg = 0d;

            if (Q.IsReady() || Player.GetSpell(SpellSlot.Q).State == SpellState.Surpressed)
            {
                dmg += Player.GetSpellDamage(hero, SpellSlot.Q) * 3;
                dmg += Player.BaseAttackDamage + Player.FlatPhysicalDamageMod * 3;
            }

            if (E.IsReady())
            {
                dmg += Player.GetSpellDamage(hero, SpellSlot.E);
            }

            if (R.IsReady())
            {
                dmg += Player.GetSpellDamage(hero, SpellSlot.R);
            }

            if (Player.Spellbook.CanUseSpell(Player.GetSpellSlot("summonerdot")) == SpellState.Ready)
            {
                dmg += Player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            }
            return (float)dmg;
        }


        private static void OnUpdate(EventArgs args)
        {
            switch (Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
            }
            Killsteal();
        }

        private static void Combo()
        {
            var dist = Config.Item("MinERangeCombo").GetValue<Slider>().Value;
            var target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
            var target2 = TargetSelector.GetTarget(E.Range + 500, TargetSelector.DamageType.Physical);
            var qCombo = Config.Item("UseQCombo").GetValue<bool>();
            var eCombo = Config.Item("UseECombo").GetValue<bool>();
            var rCombo = Config.Item("UseRCombo").GetValue<bool>();
            var rComboKillable = Config.Item("UseRComboKillable").GetValue<bool>();
            var rComboAoE = Config.Item("UseRAoE").GetValue<bool>();
            var UseIgnite = Config.Item("UseIgnite").GetValue<bool>();
            var UseItems = Config.Item("UseItems").GetValue<bool>();

            if (E.IsReady() && Player.Distance(target.ServerPosition) >= dist && eCombo && target.IsValidTarget(E.Range))
            {
                E.Cast(target);
            }

            if (R.IsReady() && rCombo && target.IsValidTarget(R.Range))
            {
                if (target.HasBuff("xenzhaointimidate"))
                {
                    R.Cast();
                }
            }

            if (ComboDamage(target) > target.Health && R.IsReady() && rComboKillable && target.IsValidTarget(R.Range))
            {
                if (target.HasBuff("xenzhaointimidate"))
                {
                    R.Cast();
                }
            }

            if (R.IsReady() && rComboAoE)
            {
                // xsalice :v)
                foreach (var target1 in HeroManager.Enemies.Where(x => x.IsValidTarget(R.Range)))
                {
                    var poly = new Geometry.Polygon.Circle(Player.Position, R.Range);
                    var nearByEnemies = 1;
                    nearByEnemies +=
                        HeroManager.Enemies.Where(x => x.NetworkId != target1.NetworkId)
                            .Count(enemy => poly.IsInside(enemy));
                    if (nearByEnemies >= Config.Item("MinRTargets").GetValue<Slider>().Value)
                    {
                        R.Cast();
                    }
                }
            }

            if (Player.Distance(target.ServerPosition) <= 600 && ComboDamage(target) >= target.Health && UseIgnite)
            {
                Player.Spellbook.CastSpell(ignite, target);
            }

            if (UseItems && youmuu.IsReady() && target.IsValidTarget(E.Range)) 
            {
                youmuu.Cast();
            }

            if (UseItems && Player.Distance(target.ServerPosition) <= 450 && cutlass.IsReady())
            {
                cutlass.Cast(target);
            }

            if (UseItems && Player.Distance(target.ServerPosition) <= 450 && blade.IsReady())
            {
                blade.Cast(target);
            }
        }

        private static float igniteDamage(Obj_AI_Hero target)
        {
            if (ignite == SpellSlot.Unknown || Player.Spellbook.CanUseSpell(ignite) != SpellState.Ready)
            {
                return 0f;
            }
            return (float)Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
        }

           //if (E.IsReady() && Player.Distance(target2.ServerPosition) > E.Range)
            //{
                // minion shit  
               // not sure if actually works, fuck it
                //foreach (var minion in MinionManager.GetMinions(ObjectManager.Player.Position, E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.Health))
                //{
                    //if (target.Distance(minion.ServerPosition) <= 100)
                    //{
                     //   E.Cast(minion);
                    //}
                //}
            //}
        
            // e if minion near enemy and enemy out of range - not done

        private static void Harass()
        {
            var dist = Config.Item("MinERangeHarass").GetValue<Slider>().Value;
            var target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
            var eHarass = Config.Item("UseEHarass").GetValue<bool>();

            if (E.IsReady() && Player.Distance(target.ServerPosition) >= dist && eHarass)
            {
                E.Cast(target);
            }
        }

        private static void Killsteal()
        {
            var target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
            var eKS = Config.Item("KillstealE").GetValue<bool>();
            var rKS = Config.Item("KillstealR").GetValue<bool>();

            if (E.IsReady() && E.GetDamage(target) >= target.Health && eKS)
            {
                E.Cast(target);
            }

            if (R.IsReady() && R.GetDamage(target) >= target.Health && rKS)
            {
                R.Cast(target);
            }
        }

        private static void OrbwalkingOnBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (args.Target is Obj_AI_Hero && W.IsReady())
            {
                W.Cast();
            }
        }

        private static void onAttack(AttackableUnit unit, AttackableUnit target)
        {
            var qCombo = Config.Item("UseQCombo").GetValue<bool>();
            var qHarass = Config.Item("UseQHarass").GetValue<bool>();
            var UseItems = Config.Item("UseItems").GetValue<bool>();


            if (!unit.IsMe)
                return;

            // badao
            if (!target.Name.ToLower().Contains("minion") && !target.Name.ToLower().Contains("sru") && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                if (Q.IsReady() && qCombo)
                {
                    // chewy
                    var aaDelay = Player.AttackDelay * 100 + Game.Ping / 2f;

                    Utility.DelayAction.Add(
                       (int)(aaDelay), () =>
                       {
                           Q.Cast();

                           if (Items.CanUseItem(3074) && UseItems && Player.Distance(target) <= 400)
                               Items.UseItem(3074);

                           if (Items.CanUseItem(3077) && UseItems && Player.Distance(target) <= 400)
                               Items.UseItem(3077);
                       });

                    
                }
            }

            // badao
            if (!target.Name.ToLower().Contains("minion") && !target.Name.ToLower().Contains("sru") && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed)
            {
                if (Q.IsReady() && qHarass)
                {
                    // chewy
                    var aaDelay = Player.AttackDelay * 100 + Game.Ping / 2f;

                    Utility.DelayAction.Add(
                       (int)(aaDelay), () =>
                       {
                           Q.Cast();
                       });
                }
            }
        }
               

        private static void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.SData.Name == "XenZhaoComboTarget")
            {
                Utility.DelayAction.Add(0, Orbwalking.ResetAutoAttackTimer); 
            }
        }

        
        private static void OnDraw(EventArgs args)
        {            
            var dist = Config.Item("MinERangeCombo").GetValue<Slider>().Value;
            var eRangeMin = Config.Item("eRangeMin").GetValue<Circle>();
            var eRangeMax = Config.Item("eRangeMax").GetValue<Circle>();
            var rRange = Config.Item("rRange").GetValue<Circle>();
            var challenged = Config.Item("challenged").GetValue<Circle>();
            var target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);

            if (eRangeMin.Active)
            {
                Render.Circle.DrawCircle(Player.Position, dist, eRangeMin.Color);
            }

            if (eRangeMax.Active)
            {
                Render.Circle.DrawCircle(Player.Position, E.Range, eRangeMax.Color);
            }

            if (rRange.Active)
            {
                Render.Circle.DrawCircle(Player.Position, R.Range, rRange.Color);
            }

            if (target.HasBuff("xenzhaointimidate"))
            {
                Render.Circle.DrawCircle(target.Position, 100, challenged.Color);
            }
        }
    }
}
