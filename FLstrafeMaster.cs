using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace FLstrafeMaster
{
    [MinimumApiVersion(160)]
    public class FLstrafeMaster : BasePlugin
    {
        public override string ModuleName => "[FruchtLabor] BHOP Chat HUD";
        public override string ModuleVersion => "1.0.0";
        public override string ModuleAuthor => "Jumper";
        public override string ModuleDescription => "Speed / Gain / Sync / Jumps im Chat pro Jump. Toggle: !strafehud, Reset: !r";

        private const string Brand = "FruchtLabor";

        private const int   MinAirTicksToReport = 2;
        private const int   PrintThrottleTicks  = 8;
        private const float AccelDeadzone       = 0.20f;

        private const int   GroundSettleTicksAir    = 3;
        private const int   GroundSettleTicksGround = 0;

        private const float TurnDeadzoneRad     = 0.0035f;

        private const float JumpImpulseDelta    = 150f;
        private const float FallingVzThreshold  = -20f;

        private class AirState
        {
            public bool  InAir;
            public int   StableAirCount;
            public int   StableGroundCount;
            public int   TotalAirTicks;
            public int   GoodTicks;
            public long  LastPrintTick;
            public float StartHorizSpeed;
            public float LastVx, LastVy, LastVz;
            public float LastHeadingDeg;
        }

        private class PlayerState
        {
            public bool  HudEnabled = true;
            public int   Jumps;
            public long  LastHintMs = 0;
            public AirState Air = new();
        }

        private readonly Dictionary<int, PlayerState> _players = new();

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnTick>(OnTick);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        }

        private void OnClientDisconnect(int slot) => _players.Remove(slot);

        [ConsoleCommand("css_strafehud", "Toggle BHOP Chat HUD")]
        [ConsoleCommand("strafehud", "Toggle BHOP Chat HUD")]
        public void CmdStrafeHud(CCSPlayerController? player, CommandInfo _)
        {
            if (player is null || !player.IsValid) return;
            var ps = GetPS(player);
            ps.HudEnabled = !ps.HudEnabled;
            if (ps.HudEnabled) ps.LastHintMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            player.PrintToChat($"{Prefix()} {ChatColors.Grey}Chat HUD:{ChatColors.Default} {(ps.HudEnabled ? ChatColors.Green + "ON" : ChatColors.LightRed + "OFF")}{ChatColors.Default}");
        }

        [ConsoleCommand("css_r", "Reset HUD-Stats")]
        [ConsoleCommand("r", "Reset HUD-Stats")]
        public void CmdReset(CCSPlayerController? player, CommandInfo _)
        {
            if (player is null || !player.IsValid) return;
            ResetStats(player);
            player.PrintToChat($"{Prefix()} {ChatColors.Grey}HUD-Stats reset.{ChatColors.Default}");
        }

        private void OnTick()
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var player in Utilities.GetPlayers())
            {
                if (player is null || !player.IsValid || !player.PawnIsAlive)
                    continue;

                var ps = GetPS(player);
                var st = ps.Air;

                if (ps.HudEnabled)
                {
                    if (ps.LastHintMs == 0) ps.LastHintMs = nowMs;
                    else if (nowMs - ps.LastHintMs >= 60_000)
                    {
                        player.PrintToChat($"{Prefix()} {ChatColors.Grey}Tipp:{ChatColors.Default} BHOP HUD an/aus mit {ChatColors.Yellow}!strafehud{ChatColors.Default}");
                        ps.LastHintMs = nowMs;
                    }
                }

                var pawn = player.PlayerPawn.Value;
                if (pawn is null || !pawn.IsValid)
                    continue;

                var v  = pawn.Velocity;
                float vx = v.X, vy = v.Y, vz = v.Z;
                float horizSpeed = MathF.Sqrt(vx * vx + vy * vy);

                bool rawOnGround = false;
                try { rawOnGround = pawn.GroundEntity.Value is not null; } catch { }

                if (rawOnGround)
                {
                    st.StableGroundCount++;
                    st.StableAirCount = 0;

                    if (st.InAir && st.StableGroundCount >= (GroundSettleTicksGround + 1))
                    {
                        HandleLanding(player, ps, st, horizSpeed, MathF.Sqrt(st.LastVx * st.LastVx + st.LastVy * st.LastVy));
                        ResetAir(st);
                    }
                }
                else
                {
                    st.StableAirCount++;
                    st.StableGroundCount = 0;

                    if (!st.InAir && st.StableAirCount >= GroundSettleTicksAir)
                    {
                        st.InAir = true;
                        st.TotalAirTicks   = 0;
                        st.GoodTicks       = 0;
                        st.StartHorizSpeed = horizSpeed;
                        st.LastVx = vx; st.LastVy = vy; st.LastVz = vz;
                        st.LastHeadingDeg = SafeHeadingDeg(st.LastVx, st.LastVy);
                    }
                }

                if (st.InAir)
                {
                    float dvz = vz - st.LastVz;
                    bool wasFalling = st.LastVz <= FallingVzThreshold;
                    if (wasFalling && dvz >= JumpImpulseDelta)
                    {
                        float endHorizFromPrevTick = MathF.Sqrt(st.LastVx * st.LastVx + st.LastVy * st.LastVy);
                        HandleLanding(player, ps, st, endHorizFromPrevTick, endHorizFromPrevTick);

                        st.InAir = true;
                        st.TotalAirTicks   = 0;
                        st.GoodTicks       = 0;
                        st.StartHorizSpeed = horizSpeed;
                        st.StableAirCount  = GroundSettleTicksAir;
                        st.StableGroundCount = 0;
                        st.LastVx = vx; st.LastVy = vy; st.LastVz = vz;
                        st.LastHeadingDeg = SafeHeadingDeg(vx, vy);
                    }
                }

                if (st.InAir)
                {
                    st.TotalAirTicks++;

                    float pvx = st.LastVx, pvy = st.LastVy;
                    float cvx = vx,        cvy = vy;

                    float dot   = pvx * cvx + pvy * cvy;
                    float cross = pvx * cvy - pvy * cvx;
                    float ang   = MathF.Atan2(cross, dot);

                    int turnSign = 0;
                    if      (ang >  TurnDeadzoneRad) turnSign = +1;
                    else if (ang < -TurnDeadzoneRad) turnSign = -1;

                    const float Deg2Rad = (float)(Math.PI / 180.0);
                    float hPrevRad  = st.LastHeadingDeg * Deg2Rad;
                    float leftPX    = -MathF.Sin(hPrevRad);
                    float leftPY    =  MathF.Cos(hPrevRad);

                    float dvx       = cvx - pvx;
                    float dvy       = cvy - pvy;
                    float projLeft  = dvx * leftPX + dvy * leftPY;

                    int sideSign = 0;
                    if      (projLeft >  AccelDeadzone) sideSign = +1;
                    else if (projLeft < -AccelDeadzone) sideSign = -1;

                    if (turnSign != 0 && sideSign != 0 && turnSign == sideSign)
                        st.GoodTicks++;

                    st.LastVx = cvx; st.LastVy = cvy; st.LastVz = vz;
                    st.LastHeadingDeg = SafeHeadingDeg(cvx, cvy);
                }
                else
                {
                    st.LastVz = vz;
                }
            }
        }

        private void HandleLanding(CCSPlayerController player, PlayerState ps, AirState st, float endHorizSpeedCurrent, float endHorizFromPrevTick)
        {
            if (st.TotalAirTicks >= MinAirTicksToReport)
            {
                ps.Jumps++;

                float endHoriz = endHorizFromPrevTick > 0 ? endHorizFromPrevTick : endHorizSpeedCurrent;

                int sync = st.TotalAirTicks > 0
                    ? (int)MathF.Round(100f * st.GoodTicks / st.TotalAirTicks)
                    : 0;

                float gain    = endHoriz - st.StartHorizSpeed;
                string gainFt = $"{(gain >= 0 ? "+" : "")}{gain:0.0}";

                long tick = Server.TickCount;
                if (ps.HudEnabled && tick - st.LastPrintTick >= PrintThrottleTicks)
                {
                    player.PrintToChat(
                        $"{Prefix()} " +
                        $"{ChatColors.Grey}Speed:{ChatColors.Default} {ChatColors.Green}{endHoriz:0.0}{ChatColors.Default}   " +
                        $"{ChatColors.Grey}Gain:{ChatColors.Default} {ChatColors.Lime}{gainFt}{ChatColors.Default}   " +
                        $"{ChatColors.Grey}Sync:{ChatColors.Default} {ChatColors.LightBlue}{sync}%{ChatColors.Default}   " +
                        $"{ChatColors.Grey}Jumps:{ChatColors.Default} {ChatColors.Yellow}{ps.Jumps}{ChatColors.Default}"
                    );
                    st.LastPrintTick = tick;
                }
            }
        }

        private void ResetAir(AirState st)
        {
            st.InAir = false;
            st.TotalAirTicks = 0;
            st.GoodTicks = 0;
            st.StartHorizSpeed = 0f;
        }

        private PlayerState GetPS(CCSPlayerController player)
        {
            int id = player.Slot;
            if (!_players.TryGetValue(id, out var ps))
                _players[id] = ps = new PlayerState();
            return ps;
        }

        private void ResetStats(CCSPlayerController player)
        {
            var ps = GetPS(player);
            ps.Jumps = 0;
            ps.Air   = new AirState();
        }

        private static float SafeHeadingDeg(float x, float y)
        {
            if (x == 0f && y == 0f) return 0f;
            return MathF.Atan2(y, x) * (180f / MathF.PI);
        }

        private static string Prefix() => $"{ChatColors.Purple}[{Brand}]{ChatColors.Default}";
    }
}
