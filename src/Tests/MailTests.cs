using System.Reflection;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Tests;

public static class MailTests
{
    [SimTest]
    public static void Mail_DeliveryAndUnreadDerivation(TestContext t)
    {
        var day0 = new GameTime(0);
        var day3 = new GameTime(3 * GameTime.MinutesPerDay);

        // The shipped farewell letter: in the mailbox from the very first morning.
        var fresh = new GameData();
        LetterDef farewell = LetterDefs.All[LetterDefs.Farewell];
        t.Assert(MailRules.IsDelivered(farewell, fresh, day0), "farewell delivered on day 0");
        t.AssertEqual(1, MailRules.Delivered(fresh, day0).Count, "one letter ships today");
        t.Assert(MailRules.HasUnread(fresh, day0), "fresh save: unread mail waiting");
        t.Assert(!MailRules.IsRead(farewell, fresh), "fresh save: farewell unread");
        t.Assert(!MailRules.HasUntakenItems(farewell, fresh), "the farewell carries no package");

        // Reading stamps the flag; the letter stays delivered (mail never vanishes).
        fresh.TrySetFlag(farewell.ReadFlag, 0);
        t.Assert(MailRules.IsRead(farewell, fresh), "read stamp lands");
        t.Assert(!MailRules.HasUnread(fresh, day3), "no unread mail after the read");
        t.Assert(MailRules.IsDelivered(farewell, fresh, day3), "a read letter stays delivered");

        // Synthetic defs pin the delivery predicate itself (RequiresFlag + FromDay,
        // both monotone) without coupling the test to shipped content.
        var gated = new LetterDef("t.gated", "T", "body", "mail.t.read",
            RequiresFlag: StoryKeys.RoadCleared, FromDay: 2);
        var data = new GameData();
        t.Assert(!MailRules.IsDelivered(gated, data, day3), "flag-gated: absent flag holds it");
        data.TrySetFlag(StoryKeys.RoadCleared, 1);
        t.Assert(!MailRules.IsDelivered(gated, data, day0), "day-gated: FromDay holds it");
        t.Assert(MailRules.IsDelivered(gated, data, day3), "both conditions met: delivered");
    }

    [SimTest]
    public static void Mail_TakeItemsAllOrNothing(TestContext t)
    {
        var package = new LetterDef("t.package", "T", "body", "mail.t.read",
            TakenFlag: "mail.t.taken",
            Items: new[] { new LetterItem("lumber", 5), new LetterItem("stone", 2) });

        // The trap two per-item HasRoomFor calls would miss: ONE empty slot passes
        // both single-item checks, but the package needs two. Fill 9 of 10 slots
        // with unstackable tools so exactly one slot stays open.
        var data = new GameData();
        for (int i = 0; i < 9; i++)
        {
            data.Player.Inventory.Slots[i] = new ItemStackRecord { ItemId = "hoe", Count = 1 };
        }
        string before = System.Text.Json.JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData);
        t.AssertEqual(MailOutcome.NoRoom, MailActions.TakeItems(package, data),
            "one open slot cannot hold a two-item package");
        t.AssertEqual(before, System.Text.Json.JsonSerializer.Serialize(data, SaveJsonContext.Default.GameData),
            "a refused take leaves the model bit-identical");

        // With room, the whole package lands.
        data.Player.Inventory.Slots[8] = null;
        t.AssertEqual(MailOutcome.Taken, MailActions.TakeItems(package, data), "take succeeds with room");
        t.AssertEqual(5, data.Player.Inventory.CountOf("lumber"), "lumber landed");
        t.AssertEqual(2, data.Player.Inventory.CountOf("stone"), "stone landed");

        // Once-only: the bus stamps TakenFlag after Taken; with it stamped the
        // package refuses forever.
        data.TrySetFlag(package.TakenFlag!, 0);
        t.AssertEqual(MailOutcome.AlreadyTaken, MailActions.TakeItems(package, data),
            "a paid-out package never pays twice");

        // Info letters have nothing to take.
        var info = new LetterDef("t.info", "T", "body", "mail.t2.read");
        t.AssertEqual(MailOutcome.NothingToTake, MailActions.TakeItems(info, data),
            "an info letter refuses as NothingToTake");
    }

    [SimTest]
    public static void Mail_DefsValidate(TestContext t)
    {
        HashSet<string> legalFlags = typeof(StoryKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        t.Assert(LetterDefs.All.Count >= 1, "the catalog is not empty");
        foreach ((string id, LetterDef letter) in LetterDefs.All)
        {
            t.AssertEqual(id, letter.Id, $"letter '{id}': key matches Id");
            t.Assert(letter.Title.Length > 0, $"letter '{id}': Title non-empty");
            t.Assert(letter.Body.Length > 0, $"letter '{id}': Body non-empty");
            t.Assert(legalFlags.Contains(letter.ReadFlag),
                $"letter '{id}': ReadFlag is a StoryKeys constant");
            bool hasItems = letter.Items is { Count: > 0 };
            t.AssertEqual(hasItems, letter.TakenFlag is not null,
                $"letter '{id}': Items and TakenFlag come together or not at all");
            if (letter.TakenFlag is { } takenFlag)
            {
                t.Assert(legalFlags.Contains(takenFlag),
                    $"letter '{id}': TakenFlag is a StoryKeys constant");
                t.Assert(takenFlag != letter.ReadFlag,
                    $"letter '{id}': ReadFlag and TakenFlag differ");
            }
            if (letter.RequiresFlag is { } requires)
            {
                t.Assert(legalFlags.Contains(requires),
                    $"letter '{id}': RequiresFlag is a StoryKeys constant");
            }
            t.Assert(letter.FromDay >= 0, $"letter '{id}': FromDay non-negative");
            if (hasItems)
            {
                foreach (LetterItem item in letter.Items!)
                {
                    t.Assert(ItemDefs.TryGet(item.ItemId) != null,
                        $"letter '{id}': package item '{item.ItemId}' resolves in ItemDefs");
                    t.Assert(item.Count > 0, $"letter '{id}': package counts positive");
                }
            }
        }

        // The shipped farewell letter: unconditional, day 0, info-only — and its
        // read stamp is exactly the first-crops quest's hand-out flag.
        LetterDef farewell = LetterDefs.All[LetterDefs.Farewell];
        t.Assert(farewell.RequiresFlag == null && farewell.FromDay == 0,
            "the farewell waits in the mailbox from first arrival");
        t.Assert(farewell.Items is null && farewell.TakenFlag is null,
            "the farewell is an info letter");
        t.AssertEqual(StoryKeys.FarewellRead, farewell.ReadFlag,
            "reading the farewell is the first-crops hand-out");
    }

    [SimTest]
    public static void Mail_SessionThroughTheBus(TestContext t)
    {
        SaveService service = SaveService.Instance;
        int mailboxClosed = 0;
        int mailboxOpened = 0;
        Action onOpened = () => mailboxOpened++;
        Action onClosed = () => mailboxClosed++;
        WorldSim.Instance.MailboxOpened += onOpened;
        WorldSim.Instance.MailboxClosed += onClosed;
        try
        {
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);

            // Without control the open refuses.
            GameState.Instance.TransitionTo(GameState.Phase.Dialogue);
            t.Assert(!WorldSim.Instance.OpenMailbox(), "OpenMailbox refused without control");
            t.Assert(!WorldSim.Instance.MailboxOpen, "no session leaked by the refusal");
            GameState.Instance.TransitionTo(GameState.Phase.Playing);

            // The mailbox session is the third Menu session: same phase moves, and
            // mutual exclusion runs BOTH ways against chest and shop.
            t.Assert(WorldSim.Instance.OpenMailbox(), "OpenMailbox accepted from Playing");
            t.AssertEqual(1, mailboxOpened, "MailboxOpened fired once");
            t.AssertEqual(GameState.Phase.Menu, GameState.Instance.Current,
                "mailbox session moved the phase to Menu");
            t.Assert(!GameState.Instance.ClockRuns, "clock frozen in Menu");
            t.Assert(!t.Tree.Paused, "tree NOT paused in Menu");
            t.Assert(!WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest),
                "chest refused while the mailbox is open");
            t.Assert(!WorldSim.Instance.OpenShop(ShopCatalog.GeneralStore),
                "shop refused while the mailbox is open");
            t.Assert(!WorldSim.Instance.OpenMailbox(), "second mailbox open refused");

            // Reading through the bus: stamps the flag once, unknown ids safe.
            t.Assert(WorldSim.Instance.ReadLetter(LetterDefs.Farewell), "first read stamps");
            t.Assert(service.Current.HasFlag(StoryKeys.FarewellRead), "read flag landed");
            t.Assert(!WorldSim.Instance.ReadLetter(LetterDefs.Farewell), "re-read is a no-op");
            t.Assert(!WorldSim.Instance.ReadLetter("no_such_letter"), "unknown letter id safe");
            t.AssertEqual(MailOutcome.NothingToTake, WorldSim.Instance.TakeLetterItems("no_such_letter"),
                "unknown letter id refuses the take");
            t.AssertEqual(MailOutcome.NothingToTake, WorldSim.Instance.TakeLetterItems(LetterDefs.Farewell),
                "the info letter has nothing to take");

            WorldSim.Instance.CloseMailbox();
            t.Assert(!WorldSim.Instance.MailboxOpen, "mailbox session cleared on close");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "close restored Playing");
            t.AssertEqual(1, mailboxClosed, "MailboxClosed fired once");
            WorldSim.Instance.CloseMailbox();
            t.AssertEqual(1, mailboxClosed, "second close is a safe no-op");

            // The package payout through the bus, on a synthetic letter (no shipped
            // letter carries one yet): Taken stamps the TakenFlag via SetStoryFlag
            // and fires InventoryChanged; a second take refuses AlreadyTaken; and
            // with no session open the take refuses before touching anything.
            var package = new LetterDef("t.bus_package", "T", "body", "mail.t.bus_read",
                TakenFlag: "mail.t.bus_taken",
                Items: new[] { new LetterItem("lumber", 3) });
            int inventoryChanged = 0;
            (string flagId, long day)? flagSeen = null;
            Action onInventory = () => inventoryChanged++;
            Action<string, long> onFlag = (flagId, day) => flagSeen = (flagId, day);
            WorldSim.Instance.InventoryChanged += onInventory;
            WorldSim.Instance.StoryFlagSet += onFlag;
            try
            {
                t.AssertEqual(MailOutcome.NothingToTake, WorldSim.Instance.TakeLetterItems(package),
                    "no session open: the take refuses");
                t.Assert(!service.Current.HasFlag("mail.t.bus_taken"), "refused take stamped nothing");

                t.Assert(WorldSim.Instance.OpenMailbox(), "mailbox open for the payout");
                t.AssertEqual(MailOutcome.Taken, WorldSim.Instance.TakeLetterItems(package),
                    "package taken through the bus");
                t.AssertEqual(3, service.Current.Player.Inventory.CountOf("lumber"), "items landed");
                t.Assert(service.Current.HasFlag("mail.t.bus_taken"),
                    "the bus stamped the TakenFlag");
                t.AssertEqual(("mail.t.bus_taken", Clock.Instance.Now.DayIndex), flagSeen,
                    "the stamp went through SetStoryFlag (StoryFlagSet fired)");
                t.AssertEqual(1, inventoryChanged, "InventoryChanged fired once");
                t.AssertEqual(MailOutcome.AlreadyTaken, WorldSim.Instance.TakeLetterItems(package),
                    "a paid-out package refuses forever through the bus");
                t.AssertEqual(3, service.Current.Player.Inventory.CountOf("lumber"),
                    "the refusal paid nothing again");
                t.AssertEqual(1, inventoryChanged, "no second InventoryChanged");
                WorldSim.Instance.CloseMailbox();
            }
            finally
            {
                WorldSim.Instance.InventoryChanged -= onInventory;
                WorldSim.Instance.StoryFlagSet -= onFlag;
            }

            // The other direction of the exclusion: no mailbox over an open chest.
            t.Assert(WorldSim.Instance.OpenStorage(StorageIds.FarmHouseChest), "chest opens");
            t.Assert(!WorldSim.Instance.OpenMailbox(), "mailbox refused while the chest is open");
            WorldSim.Instance.CloseStorage();

            // Reads gate on the session being open.
            t.Assert(!WorldSim.Instance.ReadLetter(LetterDefs.Farewell),
                "ReadLetter refuses with no session open");

            // AfterLoad mid-session force-closes and restores Playing.
            t.Assert(WorldSim.Instance.OpenMailbox(), "mailbox reopened for the load test");
            service.NewGame(); // fires AfterLoad
            t.Assert(!WorldSim.Instance.MailboxOpen, "AfterLoad cleared the mailbox session");
            t.AssertEqual(GameState.Phase.Playing, GameState.Instance.Current,
                "AfterLoad restored Playing");
            t.AssertEqual(3, mailboxClosed, "the discarded session fired its close event");
        }
        finally
        {
            WorldSim.Instance.MailboxOpened -= onOpened;
            WorldSim.Instance.MailboxClosed -= onClosed;
            WorldSim.Instance.CloseMailbox();
            WorldSim.Instance.CloseStorage();
            service.NewGame();
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
    }
}
