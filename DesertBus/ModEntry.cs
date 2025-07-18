using HarmonyLib; // el diavolo
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData.Powers;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace DesertBus;

public class ModConfig
{
    public bool BusTravel { get; set; } = true;
    public bool ArcadeGame { get; set; } = true;
    public bool AbigailGame { get; set; } = true;
    public bool PassOut { get; set; } = true;
}

public class ModState
{
    public Game Game { get; set; }
}

public class ModEntry : Mod
{
    public static readonly string GSQ_ID = "BLUEBERRY_DESERT_BUS_AVAILABLE";

    public static string MINIGAME_ID => $"Mods/{ModEntry.Instance.ModManifest.UniqueID}.Game";
    public static string STATS_ID => $"Mods/{ModEntry.Instance.ModManifest.UniqueID}";
    public static string MAIL_ID => $"{ModEntry.Instance.ModManifest.UniqueID}";
    public static string ITEM_ID => $"{ModEntry.Instance.ModManifest.UniqueID}";
    public static string ASSETS_PATH => $"Mods/{ModEntry.Instance.ModManifest.UniqueID}";
    public static string STRINGS_PATH => $"Mods/{ModEntry.Instance.ModManifest.UniqueID}/I18n";
    public static string MESSAGE_KEY => $"{ModEntry.Instance.ModManifest.UniqueID}";

    public static ModEntry Instance { get; private set; }
    public static ModConfig Config { get; private set; }
    public static PerScreen<ModState> State { get; private set; }

    public static NPC Pam => Game1.getCharacterFromName("Pam");
    public static NPC Abigail => Game1.getCharacterFromName("Abigail");

    public static bool HasFreeBusTravel => Game1.player.mailReceived.Contains($"{ModEntry.MAIL_ID}_1000km");

    public override void Entry(IModHelper helper)
    {
        ModEntry.Instance = this;
        ModEntry.Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Content.AssetRequested += this.TrySortPowers;

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

        if (ModEntry.Config.BusTravel && e.OldLocation != e.NewLocation)
        {
            Character driver = ModEntry.IsPamDriving(e.OldLocation) ? ModEntry.Pam : Game1.player;
            ModEntry.TryStartGame(from: e.OldLocation.Name, to: e.NewLocation.Name, driver: driver, onEnd: result =>
            {
                if (result.Success)
                {
                    // you get nothing
                }
                else if (result.Failure && ModEntry.Config.PassOut)
                {
                    // then perish
                    Farmer.passOutFromTired(Game1.player);
                }
                else
                {
                    // do nothing
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
        GameData data = ModEntry.Instance.Helper.GameContent.Load<GameData>($"{ModEntry.ASSETS_PATH}/Game/Data");
        GameAudio audio = data.Audio.FirstOrDefault(rules => GameStateQuery.CheckConditions(rules.Condition, context));
        GameAppearance appearance = data.Appearances.FirstOrDefault(rules => GameStateQuery.CheckConditions(rules.Condition, context));
        GameRules rules = defaultRules ? data.Rules.FirstOrDefault() : data.Rules.FirstOrDefault(rules => rules.From == from && rules.To == to && GameStateQuery.CheckConditions(rules.Condition, context));

        if (rules is not null)
        {
            GameState state = new()
            {
                From = rules.From,
                To = rules.To,
                Position = rules.Width / 4
            };
            Game game = new(data, audio, appearance, rules, state, location, driver, logoTimer);
            game.OnEnd += onEnd;
            Game1.currentMinigame = game;
            ModEntry.State.Value.Game = game;
        }
    }

    // keys to the bus
    public void TrySortPowers(object sender, AssetRequestedEventArgs e)
    {
        // reorder dictionary to place keys before books and mastery powers
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Powers"))
        {
            e.Edit((asset) =>
            {
                var data = asset.AsDictionary<string, PowersData>().Data;
                string key = $"{ModEntry.ITEM_ID}_Keys";
                if (data.TryGetValue(key, out PowersData value))
                {
                    data.Remove(key);
                    var list = data.ToList();
                    list.Insert(list.FindIndex(other => other.Key.StartsWith("Book_")), new(key, value));
                    data = list.ToDictionary(pair => pair.Key, pair => pair.Value);
                    asset.AsDictionary<string, PowersData>().ReplaceWith(data);
                }
            },
            priority: (AssetEditPriority)((int)AssetEditPriority.Late * 1000));
        }
    }

    public static bool CheckForActionOnDesertBusArcade(StardewValley.Object machine, GameLocation location, Farmer player)
    {
        // absolute dominion
        ModEntry.TryStartGame(from: machine.QualifiedItemId, to: machine.QualifiedItemId, driver: Game1.player, defaultRules: false, logoTimer: 3000, onEnd: success =>
        {
            // do nothing
        });
        return true;
    }
}

[HarmonyPatch]
public static class HarmonyPatches
{
    // arcade game
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

    // abigail game
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Event.DefaultCommands))]
    [HarmonyPatch(nameof(Event.DefaultCommands.Cutscene))]
    public static void EventCommands_Cutscene_Postfix(Event @event, string[] args, EventContext context)
    {
        // go on. get your precious hearts. i'll wait
        if (ModEntry.Config.AbigailGame && Game1.currentMinigame is AbigailGame)
        {
            ModEntry.TryStartGame(from: null, to: null, driver: ModEntry.Abigail, defaultRules: true, logoTimer: 3000, onEnd: result =>
            {
                if (Game1.currentLocation.currentEvent is Event e)
                {
                    e.CurrentCommand++;
                    if (result.Success)
                    {
                        e.specialEventVariable1 = true;
                    }
                }
            });
        }
    }

    // calico desert bus arcade system
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StardewValley.Object))]
    [HarmonyPatch(nameof(StardewValley.Object.minutesElapsed))]
    public static void Object_MinutesElapsed_Postfix(StardewValley.Object __instance, int minutes)
    {
        // animate arcade machine
        string qid = $"(BC){ModEntry.ITEM_ID}_Arcade";
        if (__instance.QualifiedItemId == qid)
        {
            __instance.showNextIndex.Value = !__instance.showNextIndex.Value;
        }
    }

    // keys to the bus
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LetterViewerMenu))]
    [HarmonyPatch(nameof(LetterViewerMenu.HandleItemCommand))]
    public static void LetterViewerMenu_HandleItem_Postfix(ref LetterViewerMenu __instance)
    {
        // stolen from love of cooking (dont tell the author)
        string qid = $"(O){ModEntry.ITEM_ID}_Keys";
        if (__instance.itemsToGrab?.Any(item => item.item.QualifiedItemId == qid) ?? false)
        {
            LetterViewerMenu menu = __instance;
            DelayedAction.functionAfterDelay(
            func: () =>
            {
                menu.exitFunction = () =>
                {
                    DelayedAction.functionAfterDelay(
                    func: () =>
                    {
                        // block any item overflow menus created to collect dummy item
                        Game1.activeClickableMenu = null;
                        Game1.nextClickableMenu.Clear();

                        // remove dummy item at all costs
                        Game1.player.removeFirstOfThisItemFromInventory(qid);

                        // play animation
                        Game1.player.completelyStopAnimatingOrDoingAction();
                        Game1.player.FarmerSprite.ClearAnimation();
                        Game1.player.faceDirection(2);

                        DelayedAction.playSoundAfterDelay("getNewSpecialItem", 750);

                        Game1.player.freezePause = 7500;
                        Game1.player.FarmerSprite.animateOnce(
                        [
                            new( // face forwards
					            frame: 57,
                                milliseconds: 0),
                            new( // hold item overhead
					            frame: 57,
                                milliseconds: 2500,
                                secondaryArm: false,
                                flip: false,
                                frameBehavior: delegate {
                                    Item item = ItemRegistry.Create<StardewValley.Object>(qid, 0);
                                    Farmer.showHoldingItem(who: Game1.player, item: item);
                                }),
                            new( // wait for dialogue
					            frame: (short)Game1.player.FarmerSprite.CurrentFrame,
                                milliseconds: 750,
                                secondaryArm: false,
                                flip: false,
                                frameBehavior: delegate {
								    const int delay = 2000;
                                    Game1.player.freezePause = delay;
                                    Game1.delayedActions.Add(new(delay: delay, behavior: delegate
                                    {
									    // Show dialogue
									    Game1.drawObjectDialogue(
                                        [
                                            Game1.content.LoadString($"{ModEntry.STRINGS_PATH}:UI_Keys_Mail")
                                        ]);
                                    }));
                                },
                                behaviorAtEndOfFrame: false)
                        ]);
                    },
                    delay: 1);
                };
            },
            delay: 1);
        }
    }

    // keys to the bus
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BusStop))]
    [HarmonyPatch(nameof(BusStop.TicketPrice), MethodType.Getter)]
    public static bool BusStop_Get_TicketPrice_Prefix(ref int __result)
    {
        // tickets cost 0g
        if (ModEntry.HasFreeBusTravel)
        {
            __result = 0;
            return false;
        }
        return true;
    }

    // keys to the bus
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BusStop))]
    [HarmonyPatch(nameof(BusStop.answerDialogue))]
    public static void BusStop_AnswerDialogue_Prefix(BusStop __instance, Response answer)
    {
        // bus can be driven without pam around
        // don't care if pam eventually turns up, we have the keys, we drive all day
        if (__instance.lastQuestionKey is not null
            && __instance.afterQuestion is null
            && ArgUtility.SplitBySpaceAndGet(__instance.lastQuestionKey, 0) + "_" + answer.responseKey == "Bus_Yes")
        {
            if (ModEntry.HasFreeBusTravel && !ModEntry.IsPamDriving(__instance))
            {
                Game1.netWorldState.Value.canDriveYourselfToday.Value = true;
            }
        }
    }
}
