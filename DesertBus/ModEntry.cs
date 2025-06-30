using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;

namespace DesertBus;

public class ModState
{
    public Game Game { get; set; }
}

public class ModConfig
{
    public bool Metric { get; set; } = true;
}

public class ModEntry : Mod
{
    public static readonly string GSQ_ID = "BLUEBERRY_DESERT_BUS_AVAILABLE";

    public static string MINIGAME_ID => $"{ModEntry.Instance.ModManifest.UniqueID}.Game";

    public static ModEntry Instance { get; private set; }
    public static ModConfig Config { get; private set; }
    public static ITranslationHelper I18n => ModEntry.Instance.Helper.Translation;
    public static PerScreen<ModState> State { get; private set; }

    public override void Entry(IModHelper helper)
    {
        ModEntry.Instance = this;
        ModEntry.Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Player.Warped += this.OnWarped;

        helper.ConsoleCommands.Add("bbdb", "desert bus", (string s, string[] args) => this.TryStartGame(from: null, to: null, test: true));

        GameStateQuery.Register(ModEntry.GSQ_ID, (string[] query, GameStateQueryContext context) => this.CanDriveTheBus(context.Player, context.Location));
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
            this.TryStartGame(from: e.OldLocation.Name, to: e.NewLocation.Name);
        }
    }

    public bool CanDriveTheBus(Farmer player, GameLocation location)
    {
        bool isBusReady = player.mailReceived.Contains("ccVault");
        bool isPamDriving = Game1.getCharacterFromName("Pam") is NPC pam
            && location is not null
            && location.characters.Contains(pam)
            && pam.TilePoint.X == 21 && pam.TilePoint.Y == 10;
        return !isPamDriving && Game1.netWorldState.Value.canDriveYourselfToday.Value;
    }

    public void TryStartGame(string from, string to, bool test = false)
    {
        GameStateQueryContext context = new(Game1.getLocationFromName(from) ?? Game1.currentLocation, Game1.player, null, null, Game1.random);
        GameData data = ModEntry.Instance.Helper.ModContent.Load<GameData>("assets/data.json");
        GameRules rules = test ? data.Rules.FirstOrDefault() : data.Rules.FirstOrDefault(rules => rules.From == from && rules.To == to && GameStateQuery.CheckConditions(rules.Condition, context));

        if (rules is not null)
        {
            GameState state = rules is null ? null : new GameState()
            {
                PlayerID = Game1.player.UniqueMultiplayerID,
                PlayerName = Game1.player.Name,
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
