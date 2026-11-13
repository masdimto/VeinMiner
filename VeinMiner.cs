using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;

namespace VeinMinerPlugin
{
    [ApiVersion(2, 1)]
    public class VeinMiner : TerrariaPlugin
    {
        public override string Name => "VeinMiner";
        public override Version Version => new Version(1, 0, 1);
        public override string Author => "CustomPlugin";
        public override string Description => "Menambang satu urat ore atau Desert Fossil sekaligus (maksimal 60 blok).";

        private HashSet<int> enabledPlayers = new HashSet<int>();
        private HashSet<int> miningPlayers = new HashSet<int>();
        private const int MaxVeinBlocks = 60;

        private static readonly HashSet<ushort> AllowedOres = new HashSet<ushort>
        {
            TileID.Copper,
            TileID.Tin,
            TileID.Iron,
            TileID.Lead,
            TileID.Silver,
            TileID.Tungsten,
            TileID.Gold,
            TileID.Platinum,
            TileID.Demonite,
            TileID.Crimtane,
            TileID.Meteorite,
            TileID.Obsidian,
            TileID.Hellstone,
            TileID.Cobalt,
            TileID.Palladium,
            TileID.Mythril,
            TileID.Orichalcum,
            TileID.Adamantite,
            TileID.Titanium,
            TileID.Chlorophyte,
            TileID.LunarOre,
            TileID.DesertFossil
        };

        public VeinMiner(Main game) : base(game) { }

        public override void Initialize()
        {
            GetDataHandlers.TileEdit += OnTileEdit;
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            Commands.ChatCommands.Add(new Command("veinminer.use", ToggleVeinMiner, "veinminer", "vm")
            {
                HelpText = "Menyalakan atau mematikan fitur VeinMiner."
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GetDataHandlers.TileEdit -= OnTileEdit;
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            }
            base.Dispose(disposing);
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            enabledPlayers.Remove(args.Who);
            miningPlayers.Remove(args.Who);
        }

        private void ToggleVeinMiner(CommandArgs args)
        {
            int index = args.Player.Index;
            if (enabledPlayers.Contains(index))
            {
                enabledPlayers.Remove(index);
                args.Player.SendInfoMessage("[VeinMiner] Dimatikan.");
            }
            else
            {
                enabledPlayers.Add(index);
                args.Player.SendSuccessMessage("[VeinMiner] Diaktifkan! Hancurkan 1 blok ore untuk menambang seluruh uratnya.");
            }
        }

        private void OnTileEdit(object sender, GetDataHandlers.TileEditEventArgs args)
        {
            if (args.Action != GetDataHandlers.EditAction.KillTile || args.EditData != 0)
                return;

            TSPlayer player = args.Player;
            if (player == null || !enabledPlayers.Contains(player.Index) || miningPlayers.Contains(player.Index))
                return;

            int startX = args.X;
            int startY = args.Y;

            if (!WorldGen.InWorld(startX, startY, 10))
                return;

            ITile startTile = Main.tile[startX, startY];
            if (startTile == null || !startTile.active())
                return;

            ushort targetType = startTile.type;
            if (!AllowedOres.Contains(targetType))
                return;

            miningPlayers.Add(player.Index);
            try
            {
                MineConnectedVein(startX, startY, targetType);
            }
            finally
            {
                miningPlayers.Remove(player.Index);
            }
        }

        private void MineConnectedVein(int startX, int startY, ushort targetType)
        {
            Queue<Point> queue = new Queue<Point>();
            HashSet<Point> visited = new HashSet<Point>();

            Point origin = new Point(startX, startY);
            queue.Enqueue(origin);
            visited.Add(origin);

            int brokenCount = 0;

            while (queue.Count > 0 && brokenCount < MaxVeinBlocks)
            {
                Point current = queue.Dequeue();

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = current.X + dx;
                        int ny = current.Y + dy;
                        Point neighbor = new Point(nx, ny);

                        if (visited.Contains(neighbor) || !WorldGen.InWorld(nx, ny, 10))
                            continue;

                        visited.Add(neighbor);

                        ITile tile = Main.tile[nx, ny];
                        if (tile != null && tile.active() && tile.type == targetType)
                        {
                            WorldGen.KillTile(nx, ny, false, false, false);
                            TSPlayer.All.SendTileSquareCentered(nx, ny, 1);
                            brokenCount++;

                            if (brokenCount >= MaxVeinBlocks)
                                return;

                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
    }
}
