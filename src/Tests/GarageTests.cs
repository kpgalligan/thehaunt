using System.Text.Json;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

public static class GarageTests
{
    [SimTest]
    public static void Garage_RulesBoundaries(TestContext t)
    {
        // Pure model — no autoloads, no scene.
        GameData data = GameData.NewGame();
        t.AssertEqual(100_000L, GarageRules.Price, "Kevin's price: $100k, for now");
        t.Assert(!GarageRules.IsOwned(data), "a new game owns no garage");
        // The TEMPORARY DevScaffold start (150k) is deliberately over the asking
        // price — Kevin's "that will allow me to buy the garage". This pin flips
        // back to InsufficientFunds when the scaffold is deleted.
        t.AssertEqual(GarageSaleResult.Ok, GarageRules.CanBuy(data),
            "the scaffolded start can afford the garage on day 1");

        data.Player.Money = GarageRules.Price - 1;
        t.AssertEqual(GarageSaleResult.InsufficientFunds, GarageRules.CanBuy(data),
            "one g short refuses");
        data.Player.Money = GarageRules.Price;
        t.AssertEqual(GarageSaleResult.Ok, GarageRules.CanBuy(data),
            "the exact asking price is enough");

        data.TrySetFlag(StoryKeys.GarageDeed, 3);
        t.Assert(GarageRules.IsOwned(data), "the deed is ownership");
        t.AssertEqual(GarageSaleResult.AlreadyOwned, GarageRules.CanBuy(data),
            "owned wins over funds — nothing left to buy");
    }

    [SimTest]
    public static void Garage_BuyHappyPath(TestContext t)
    {
        SaveService service = SaveService.Instance;
        var sequence = new List<string>();
        void OnMoney(long m) => sequence.Add($"money:{m}");
        void OnFlag(string id, long day) => sequence.Add($"flag:{id}:{day}");
        void OnOpened() => sequence.Add("opened");
        void OnClosed() => sequence.Add("closed");
        WorldSim.Instance.MoneyChanged += OnMoney;
        WorldSim.Instance.StoryFlagSet += OnFlag;
        WorldSim.Instance.GarageSaleOpened += OnOpened;
        WorldSim.Instance.GarageSaleClosed += OnClosed;
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            service.Current.Player.Money = GarageRules.Price + 500;

            t.Assert(WorldSim.Instance.OpenGarageSale(), "the sale opens from Playing");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "the sale is a Menu session");

            t.AssertEqual(GarageSaleResult.Ok, WorldSim.Instance.BuyGarage(), "the buy lands");
            t.AssertEqual(500L, service.Current.Player.Money, "exact debit of the asking price");
            t.Assert(service.Current.HasFlag(StoryKeys.GarageDeed), "the deed is stamped");
            long day = Clock.Instance.Now.DayIndex;
            t.AssertEqual(day, service.Current.FlagDay(StoryKeys.GarageDeed),
                "stamped with today's index");
            t.Assert(!WorldSim.Instance.GarageSaleOpen, "the deal closes its own session");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "and gives the phase back");

            t.AssertEqual($"opened|flag:{StoryKeys.GarageDeed}:{day}|money:500|closed",
                string.Join("|", sequence),
                "event order: deed stamp, then MoneyChanged, then the close");

            t.AssertEqual(GarageSaleResult.NotOpen, WorldSim.Instance.BuyGarage(),
                "no session survives the sale — no double debit path");

            // The deed and the debit survive a save round-trip.
            service.DeserializeFrom(service.SerializeToString());
            t.Assert(service.Current.HasFlag(StoryKeys.GarageDeed), "deed survives the round-trip");
            t.AssertEqual(500L, service.Current.Player.Money, "money survives the round-trip");
        }
        finally
        {
            WorldSim.Instance.MoneyChanged -= OnMoney;
            WorldSim.Instance.StoryFlagSet -= OnFlag;
            WorldSim.Instance.GarageSaleOpened -= OnOpened;
            WorldSim.Instance.GarageSaleClosed -= OnClosed;
            WorldSim.Instance.CloseGarageSale();
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static void Garage_BuyFailuresMutateNothing(TestContext t)
    {
        SaveService service = SaveService.Instance;
        int events = 0;
        int opened = 0;
        int closed = 0;
        void OnMoney(long m) => events++;
        void OnFlag(string id, long day) => events++;
        void OnOpened() => opened++;
        void OnClosed() => closed++;
        WorldSim.Instance.MoneyChanged += OnMoney;
        WorldSim.Instance.StoryFlagSet += OnFlag;
        WorldSim.Instance.GarageSaleOpened += OnOpened;
        WorldSim.Instance.GarageSaleClosed += OnClosed;
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);

            // No session: refused before any look at the model.
            string before = Snapshot(service.Current);
            t.AssertEqual(GarageSaleResult.NotOpen, WorldSim.Instance.BuyGarage(),
                "no session, no sale");
            t.AssertEqual(before, Snapshot(service.Current), "refusal left the model bit-identical");

            // Without control (a dialogue up, say) the open itself refuses, silently.
            GameState.Instance.TransitionTo(GameState.Phase.Dialogue);
            t.Assert(!WorldSim.Instance.OpenGarageSale(), "refused without control");
            t.AssertEqual(GameState.Phase.Dialogue, GameState.Instance.Current,
                "the refused open never touched the phase");
            GameState.Instance.TransitionTo(GameState.Phase.Playing);

            // One g short at the boundary, in a live session.
            service.Current.Player.Money = GarageRules.Price - 1;
            t.Assert(WorldSim.Instance.OpenGarageSale(), "the session opens regardless of funds");
            before = Snapshot(service.Current);
            t.AssertEqual(GarageSaleResult.InsufficientFunds, WorldSim.Instance.BuyGarage(),
                "one g short refuses");
            t.AssertEqual(before, Snapshot(service.Current), "refused buy left the model bit-identical");
            t.Assert(WorldSim.Instance.GarageSaleOpen, "the session survives a refusal");
            t.AssertEqual(0, events, "no MoneyChanged/StoryFlagSet fired by refusals");

            // A deed landing mid-session (a future quest reward could stamp it):
            // BuyGarage's AlreadyOwned leg is the last backstop against a second
            // 100,000g debit, and it must be as mutation-free as the others.
            service.Current.Player.Money = GarageRules.Price;
            WorldSim.Instance.SetStoryFlag(StoryKeys.GarageDeed);
            int afterStamp = events; // the stamp's own StoryFlagSet is legitimate
            before = Snapshot(service.Current);
            t.AssertEqual(GarageSaleResult.AlreadyOwned, WorldSim.Instance.BuyGarage(),
                "a stamped deed refuses the buy however rich the buyer");
            t.AssertEqual(before, Snapshot(service.Current), "the backstop debits nothing");
            t.Assert(WorldSim.Instance.GarageSaleOpen, "and the session survives it");
            t.AssertEqual(afterStamp, events, "no MoneyChanged/StoryFlagSet from the refusal");
            WorldSim.Instance.CloseGarageSale();

            // A second session cannot open over the first, in either direction.
            // (From Menu the control gate already refuses; the session-flag legs
            // are belt-and-suspenders the gates keep anyway — mail/storage's
            // precedent, and the refusals must also leak no Opened event.)
            service.NewGame(); // deed gone; no session was open, so no discard fires
            opened = 0;
            closed = 0;
            t.Assert(WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore), "shop opens");
            t.Assert(!WorldSim.Instance.OpenGarageSale(), "no garage sale over a shop");
            // A stray CloseGarageSale over someone else's session is a strict no-op.
            WorldSim.Instance.CloseGarageSale();
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "the no-op close never touched the shop's phase");
            t.AssertEqual(0, closed, "and fired no GarageSaleClosed");
            WorldSim.Instance.CloseShop();
            t.Assert(WorldSim.Instance.OpenGarageSale(), "garage sale opens");
            t.Assert(!WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore), "no shop over the sale");
            t.Assert(!WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest), "no chest either");
            t.Assert(!WorldSim.Instance.OpenMailbox(), "no mailbox either");
            t.AssertEqual(1, opened, "the refused opens fired no GarageSaleOpened");

            // AfterLoad mid-session force-closes and restores Playing.
            closed = 0;
            service.NewGame(); // fires AfterLoad
            t.Assert(!WorldSim.Instance.GarageSaleOpen, "AfterLoad cleared the sale session");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "AfterLoad restored Playing");
            t.AssertEqual(1, closed, "AfterLoad fired the missing GarageSaleClosed");

            // Once owned there is nothing to sell: the open itself refuses, silently.
            opened = 0;
            service.Current.Player.Money = GarageRules.Price;
            t.Assert(WorldSim.Instance.OpenGarageSale(), "a fresh game sells again");
            t.AssertEqual(GarageSaleResult.Ok, WorldSim.Instance.BuyGarage(), "the deed lands");
            t.Assert(!WorldSim.Instance.OpenGarageSale(), "a sold garage never reopens the sale");
            t.AssertEqual(1, opened, "the refused open fired no GarageSaleOpened");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "the refused open never touched the phase");
        }
        finally
        {
            WorldSim.Instance.MoneyChanged -= OnMoney;
            WorldSim.Instance.StoryFlagSet -= OnFlag;
            WorldSim.Instance.GarageSaleOpened -= OnOpened;
            WorldSim.Instance.GarageSaleClosed -= OnClosed;
            WorldSim.Instance.CloseGarageSale();
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    [SimTest]
    public static async Task Garage_SignSellsOnceThenAnswersSold(TestContext t)
    {
        SaveService.Instance.NewGame();
        MapRoot map = MapRegistry.Create(MapIds.WestEntry);
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            var sign = map.GetNode<GarageSaleSign>("GarageSaleSign");
            t.Assert(sign.CanInteract(map), "the board reads under Playing");
            t.AssertEqual("For sale", sign.PromptText, "the prompt is the asking notice");

            // The footprint blocks like every facade; the frontage row stays open.
            t.Assert(!map.IsStandable(new Godot.Vector2I(34, 19)), "the garage footprint blocks");
            t.Assert(map.IsStandable(new Godot.Vector2I(34, 21)), "its frontage stays walkable");

            sign.Interact(map);
            t.Assert(WorldSim.Instance.GarageSaleOpen, "the board opens the sale session");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "the session runs in Menu");
            WorldSim.Instance.CloseGarageSale();

            // Once the deed lands the SAME live node answers SOLD — checked live at
            // every interact like Door.RequiredFlag, no repaint, no session.
            SaveService.Instance.Current.Player.Money = GarageRules.Price;
            t.Assert(WorldSim.Instance.OpenGarageSale(), "reopens before the deed");
            t.AssertEqual(GarageSaleResult.Ok, WorldSim.Instance.BuyGarage(), "the buy lands");
            t.AssertEqual("Read", sign.PromptText, "the prompt drops the asking notice");
            sign.Interact(map);
            t.Assert(!WorldSim.Instance.GarageSaleOpen, "a sold garage opens no session");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "and the answer line never touches the phase");
        }
        finally
        {
            map.Free();
            SaveService.Instance.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }

    // Whole-model snapshot: a refused buy must leave the save graph bit-identical
    // (EconTests' pattern).
    private static string Snapshot(GameData data) =>
        JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);
}
