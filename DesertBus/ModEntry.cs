using HarmonyLib; // el diavolo
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;
using StardewValley.Minigames;

namespace DesertBus;

public class ModConfig
{
    public bool ArcadeGame { get; set; } = true;
    public bool AbigailGame { get; set; } = true;
}

public class ModState
{
    public Game Game { get; set; }
}

public class ModEntry : Mod
{
    public static readonly string GSQ_ID = "BLUEBERRY_DESERT_BUS_AVAILABLE";

    public static string MINIGAME_ID => $"{ModEntry.Instance.ModManifest.UniqueID}.Game";
    public static string STATS_ID => $"{ModEntry.Instance.ModManifest.UniqueID}";

    public static ModEntry Instance { get; private set; }
    public static ModConfig Config { get; private set; }
    public static PerScreen<ModState> State { get; private set; }
    public static NPC Pam => Game1.getCharacterFromName("Pam");
    public static NPC Abigail => Game1.getCharacterFromName("Abigail");

    public override void Entry(IModHelper helper)
    {
        ModEntry.Instance = this;
        ModEntry.Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Player.Warped += this.OnWarped;

        helper.ConsoleCommands.Add("bbdb", "desert bus", (string s, string[] args) => ModEntry.TryStartGame(from: null, to: null, driver: Game1.player, defaultRules: true));

        GameStateQuery.Register(ModEntry.GSQ_ID, (string[] query, GameStateQueryContext context) => ModEntry.CanDriveTheBus(context.Player, context.Location));

        // evil doings
        Harmony harmony = new(id: this.Helper.ModRegistry.ModID);
        harmony.PatchAll();
    }

    public void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        ModEntry.State = new(() => new ModState());
    }

    public void OnWarped(object sender, WarpedEventArgs e)
    {
        if (Game1.CurrentEvent is not null || Game1.player.passedOut)
            return;

        if (e.OldLocation != e.NewLocation)
        {
            Character driver = ModEntry.IsPamDriving(e.OldLocation) ? ModEntry.Pam : Game1.player;
            ModEntry.TryStartGame(from: e.OldLocation.Name, to: e.NewLocation.Name, driver: driver, onEnd: success =>
            {
                if (success)
                {
                    // you get nothing
                }
                else
                {
                    // then perish
                    Farmer.passOutFromTired(Game1.player);
                }
            });
        }
    }

    public static bool IsPamDriving(GameLocation location)
    {
        NPC pam = ModEntry.Pam;
        bool isBusStop = location is BusStop
            && location.characters.Contains(pam);
        bool isDesert = location is Desert
            && !Desert.warpedToDesert;
        return pam is not null && (isBusStop || isDesert);
    }

    public static bool IsPlayerDriving(GameLocation location)
    {
        return Game1.netWorldState.Value.canDriveYourselfToday.Value || (location is Desert && Desert.warpedToDesert);
    }

    public static bool CanDriveTheBus(Farmer player, GameLocation location)
    {
        bool isBusReady = player.mailReceived.Contains("ccVault");
        bool isPamDriving = ModEntry.IsPamDriving(location);
        bool isPlayerDriving = ModEntry.IsPlayerDriving(location);
        return isBusReady && (isPamDriving || isPlayerDriving);
    }

    public static void TryStartGame(string from, string to, Character driver, bool defaultRules = false, double logoTimer = -1000, Game.OnEndDelegate onEnd = null)
    {
        if (Game1.currentMinigame is Game currentGame)
        {
            currentGame.unload();
        }

        GameLocation location = Game1.getLocationFromName(from) ?? Game1.currentLocation;
        GameStateQueryContext context = new(location, Game1.player, null, null, Game1.random);
        GameData data = ModEntry.Instance.Helper.ModContent.Load<GameData>("assets/data.json");
        GameRules rules = defaultRules ? data.Rules.FirstOrDefault() : data.Rules.FirstOrDefault(rules => rules.From == from && rules.To == to && GameStateQuery.CheckConditions(rules.Condition, context));

        if (rules is not null)
        {
            GameState state = new()
            {
                From = rules.From,
                To = rules.To,
                Position = rules.Width / 4
            };
            Game game = new(data, rules, state, location, driver: driver, logoTimer);
            game.OnEnd += onEnd;
            Game1.currentMinigame = game;
            ModEntry.State.Value.Game = game;
        }
    }
}

[HarmonyPatch]
public static class HarmonyPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLocation))]
    [HarmonyPatch(nameof(GameLocation.showPrairieKingMenu))]
    public static bool GameLocation_ShowPrairieKingMenu_Prefix()
    {
        // the real journey of the prairie king
        if (ModEntry.Config.ArcadeGame)
        {
            ModEntry.TryStartGame(from: null, to: null, driver: Game1.player, defaultRules: true, logoTimer: 3000, onEnd: success =>
            {
                // do nothing
            });
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Event.DefaultCommands))]
    [HarmonyPatch(nameof(Event.DefaultCommands.Cutscene))]
    public static void EventCommands_Cutscene_Postfix(Event @event, string[] args, EventContext context)
    {
        // go on. get your precious hearts. i'll wait
        if (ModEntry.Config.AbigailGame && Game1.currentMinigame is AbigailGame)
        {
            ModEntry.TryStartGame(from: null, to: null, driver: ModEntry.Abigail, defaultRules: true, logoTimer: 3000, onEnd: success =>
            {
                if (Game1.currentLocation.currentEvent is Event e)
                {
                    e.CurrentCommand++;
                    if (success)
                    {
                        e.specialEventVariable1 = true;
                    }
                }
            });
        }
    }
}
