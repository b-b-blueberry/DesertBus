using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;

namespace DesertBus;

public class ModConfig
{
    public bool Intro { get; set; } = true;
    public bool Metric { get; set; } = true;
}

public class ModState
{
    public Game Game { get; set; }
}

public class ModEntry : Mod
{
    public static readonly string GSQ_ID = "BLUEBERRY_DESERT_BUS_AVAILABLE";

    public static string MINIGAME_ID => $"{ModEntry.Instance.ModManifest.UniqueID}.Game";

    public static ModEntry Instance { get; private set; }
    public static ModConfig Config { get; private set; }
    public static ITranslationHelper I18n => ModEntry.Instance.Helper.Translation;
    public static PerScreen<ModState> State { get; private set; }
    public static NPC Pam => Game1.getCharacterFromName("Pam");

    public const bool Debug = false;

    public override void Entry(IModHelper helper)
    {
        ModEntry.Instance = this;
        ModEntry.Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Player.Warped += this.OnWarped;

        helper.ConsoleCommands.Add("bbdb", "desert bus", (string s, string[] args) => ModEntry.TryStartGame(from: null, to: null, test: true));

        GameStateQuery.Register(ModEntry.GSQ_ID, (string[] query, GameStateQueryContext context) => ModEntry.CanDriveTheBus(context.Player, context.Location));
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
            ModEntry.TryStartGame(from: e.OldLocation.Name, to: e.NewLocation.Name);
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

    public static void TryStartGame(string from, string to, bool test = false)
    {
        GameLocation location = Game1.getLocationFromName(from) ?? Game1.currentLocation;
        Farmer player = Game1.player;
        NPC pam = ModEntry.Pam;

        GameStateQueryContext context = new(location, player, null, null, Game1.random);
        GameData data = ModEntry.Instance.Helper.ModContent.Load<GameData>("assets/data.json");
        GameRules rules = test ? data.Rules.FirstOrDefault() : data.Rules.FirstOrDefault(rules => rules.From == from && rules.To == to && GameStateQuery.CheckConditions(rules.Condition, context));

        bool isPamDriving = ModEntry.IsPamDriving(location);

        string name = isPamDriving ? pam.displayName : player.Name;
        long id = isPamDriving ? -1 : player.UniqueMultiplayerID;

        if (rules is not null)
        {
            GameState state = new GameState()
            {
                PlayerID = id,
                PlayerName = name,
                From = rules.From,
                To = rules.To,
                Position = rules.Width / 4
            };
            Game game = new Game(data, rules, state);
            Game1.currentMinigame = game;
            ModEntry.State.Value.Game = game;
        }
    }
}
