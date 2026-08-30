using System.Text.Json;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Tests;

public static class GarageOpsTests
{
    // ------------------------------------------------------------------
    // Pure pins
    // ------------------------------------------------------------------

    [SimTest]
    public static void GarageOps_ServiceAndHourPins(TestContext t)
    {
        // Kevin's starting services, work ≡ price. Order is ALSO the roll table.
        t.AssertEqual(3, GarageServices.All.Count, "the three starting services");
        var oil = GarageServices.All[0];
        var lights = GarageServices.All[1];
        var trans = GarageServices.All[2];
        t.AssertEqual(("oil_change", "Oil change", 100L, 100), (oil.Id, oil.Name, oil.Price, oil.Work),
            "oil change: $100");
        t.AssertEqual(("lights", "Fix headlight/taillight", 150L, 150),
            (lights.Id, lights.Name, lights.Price, lights.Work), "lights: $150");
        t.AssertEqual(("transmission", "Fix transmission", 350L, 350),
            (trans.Id, trans.Name, trans.Price, trans.Work), "transmission: $350");
        t.Assert(GarageServices.TryGet("brakes") is null, "unknown service ids resolve null");

        // 9 AM - 6 PM, nine rolls a day, and the minute window Mike's schedule
        // derives from — the drift guard that keeps clerk and arrivals in step.
        // The 6% is pinned EXACTLY: the statistical band in the determinism test
        // tolerates 4-8% (it checks the mixer's health, not the tuning), so this
        // line is the only thing standing between Kevin's number and a drift.
        t.AssertEqual(6, GarageOpsRules.ArrivalPercent, "6% chance per open hour");
        t.AssertEqual(2, GarageOpsRules.MaxCars, "no more than two cars");
        t.Assert(!GarageOpsRules.IsOpenHour(8), "8 AM is closed");
        t.Assert(GarageOpsRules.IsOpenHour(9), "9 AM opens");
        t.Assert(GarageOpsRules.IsOpenHour(17), "the 5 o'clock hour still rolls");
        t.Assert(!GarageOpsRules.IsOpenHour(18), "6 PM is closed");
        t.AssertEqual(180, GarageOpsRules.OpenMinuteOfDay, "9 AM in schedule minutes");
        t.AssertEqual(720, GarageOpsRules.CloseMinuteOfDay, "6 PM in schedule minutes");
        ScheduleEntry mike = NpcSchedules.Mike[0];
        t.AssertEqual(StoryKeys.GarageDeed, mike.RequiresFlag!, "Mike is hired with the deed");
        t.AssertEqual(GarageOpsRules.OpenMinuteOfDay, mike.StartMinuteOfDay,
            "Mike clocks in when the window opens");
        t.AssertEqual(GarageOpsRules.CloseMinuteOfDay, mike.EndMinuteOfDay,
            "Mike clocks out when it shuts");
        t.AssertEqual(MapIds.GarageInterior, mike.Placement.MapId, "Mike works the shop floor");

        // Mike's line tracks the shop floor, and only OPEN work counts: a shop
        // holding nothing, or only finished cars waiting for dawn pickup, gets the
        // idle line — "work's waiting" must never be a lie.
        GameData data = GameData.NewGame();
        var now = new GameTime(300);
        t.AssertEqual("mike_idle", DialogueSelector.ForNpc("mike", data, now)!, "empty shop");
        data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange });
        t.AssertEqual("mike_jobs", DialogueSelector.ForNpc("mike", data, now)!, "a car needs work");
        data.GarageJobs[0].Completed = true;
        t.AssertEqual("mike_idle", DialogueSelector.ForNpc("mike", data, now)!,
            "a finished car awaiting pickup is not work waiting");
        t.Assert(DialogueDefs.TryGet("mike_idle") != null && DialogueDefs.TryGet("mike_jobs") != null,
            "both of Mike's defs exist");
    }

    [SimTest]
    public static void GarageOps_CustomerRollDeterminism(TestContext t)
    {
        // Same triple, same answer, forever — replaying an hour can never re-roll.
        var first = GarageOpsRules.CustomerRoll(123, 45, 11);
        t.AssertEqual(first, GarageOpsRules.CustomerRoll(123, 45, 11), "pure hash");

        // Seed 0 is every migrated save: the mixer must not be degenerate there.
        // 400 days x 9 open hours = 3600 rolls; a healthy 6% lands well inside
        // [4%, 8%] (the actual value is ~6.4%), and every service gets drawn.
        int arrivals = 0;
        var services = new HashSet<int>();
        for (long day = 0; day < 400; day++)
        {
            for (int hour = GarageOpsRules.OpenHour; hour < GarageOpsRules.CloseHour; hour++)
            {
                (bool arrived, int service) = GarageOpsRules.CustomerRoll(0, day, hour);
                t.Assert(service >= 0 && service < GarageServices.All.Count, "index in range");
                if (arrived)
                {
                    arrivals++;
                    services.Add(service);
                }
            }
        }
        t.Assert(arrivals >= 144 && arrivals <= 288,
            $"seed-0 arrival rate ~6% (got {arrivals}/3600)");
        t.AssertEqual(GarageServices.All.Count, services.Count, "every service arrives eventually");
    }

    // ------------------------------------------------------------------
    // The work model — Kevin's anchor numbers
    // ------------------------------------------------------------------

    [SimTest]
    public static void GarageOps_WorkMathHoldsKevinsAnchors(TestContext t)
    {
        // WorkPerPress: 6 at level 1 rising to 15 at level 10 (clamped outside).
        t.AssertEqual(6, GarageOpsRules.WorkPerPress(1), "level 1 presses 6 units");
        t.AssertEqual(15, GarageOpsRules.WorkPerPress(10), "level 10 presses 15 units");
        t.AssertEqual(6, GarageOpsRules.WorkPerPress(-3), "hostile level clamps low");
        t.AssertEqual(15, GarageOpsRules.WorkPerPress(99), "hostile level clamps high");

        // Level 1: an oil change costs exactly 33 stamina (16 full presses at 2 +
        // a pro-rata final press at 1), so a 100-stamina day holds EXACTLY three —
        // Kevin's "3 oil changes per day at level 1".
        GameData data = GameData.NewGame();
        data.TrySetFlag(StoryKeys.GarageDeed, 0);
        for (int change = 1; change <= 3; change++)
        {
            data.GarageJobs.Clear();
            data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange });
            int before = data.Player.Stamina;
            int presses = 0;
            GarageWorkResult result = GarageWorkResult.Worked;
            while (result == GarageWorkResult.Worked)
            {
                result = GarageOpsRules.DoWork(data, 0);
                presses++;
            }
            t.AssertEqual(GarageWorkResult.CompletedJob, result, $"change {change} completes");
            t.AssertEqual(17, presses, $"change {change}: 17 presses at level 1");
            t.AssertEqual(before - 33, data.Player.Stamina, $"change {change}: exactly 33 stamina");
            t.Assert(data.GarageJobs[0].Completed, "the record flips Completed");
        }
        t.AssertEqual(1, data.Player.Stamina, "three changes spend 99 of 100");
        data.GarageJobs.Clear();
        data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange });
        t.AssertEqual(GarageWorkResult.NotEnoughStamina, GarageOpsRules.DoWork(data, 0),
            "a fourth change does not fit the day");

        // Level 10: 6 full presses + a 1-stamina finisher = 13 per change — seven
        // whole changes and change (Kevin's 'about 8', arithmetically 7.5).
        data.Player.Stamina = 100;
        data.Player.SkillXp[SkillIds.MechanicalRepair] = 90;
        data.GarageJobs.Clear();
        data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange });
        int presses10 = 0;
        while (GarageOpsRules.DoWork(data, 0) == GarageWorkResult.Worked)
        {
            presses10++;
        }
        t.AssertEqual(7, presses10 + 1, "level 10: seven presses per oil change");
        t.AssertEqual(87, data.Player.Stamina, "level 10: 13 stamina per oil change");

        // Partial work banks and survives sessions: Kevin's transmission at level 1
        // (117 stamina) is deliberately more than one day's energy.
        data.Player.SkillXp.Remove(SkillIds.MechanicalRepair);
        data.Player.Stamina = 10;
        data.GarageJobs.Clear();
        var transmission = new GarageJobRecord { ServiceId = GarageServices.Transmission };
        data.GarageJobs.Add(transmission);
        for (int i = 0; i < 5; i++)
        {
            t.AssertEqual(GarageWorkResult.Worked, GarageOpsRules.DoWork(data, 0), "banked press");
        }
        t.AssertEqual(GarageWorkResult.NotEnoughStamina, GarageOpsRules.DoWork(data, 0),
            "out of energy mid-job");
        t.AssertEqual(30, transmission.WorkDone, "five presses banked 30 units");
        t.Assert(!transmission.Completed, "the job waits for another session");
    }

    [SimTest]
    public static void GarageOps_WorkRefusalsMutateNothing(TestContext t)
    {
        // FarmActions discipline at the bus: every refusal is a bit-identical no-op
        // and fires no events.
        SaveService service = SaveService.Instance;
        int events = 0;
        void Count() => events++;
        void CountStamina(int a, int b) => events++;
        void CountJob(GarageJobRecord j) => events++;
        WorldSim.Instance.GarageJobsChanged += Count;
        WorldSim.Instance.SkillsChanged += Count;
        WorldSim.Instance.StaminaChanged += CountStamina;
        WorldSim.Instance.GarageJobCompleted += CountJob;
        try
        {
            service.NewGame();
            GameData data = service.Current;

            // Not owned: no deed, whatever is on the list.
            data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange });
            string before = Snapshot();
            t.AssertEqual(GarageWorkResult.NotOwned, WorldSim.Instance.WorkOnGarageJob(0), "no deed");
            t.AssertEqual(before, Snapshot(), "NotOwned mutates nothing");

            // Empty bay, and a bay index off the end.
            data.GarageJobs.Clear();
            data.TrySetFlag(StoryKeys.GarageDeed, 0);
            before = Snapshot();
            t.AssertEqual(GarageWorkResult.NoJob, WorldSim.Instance.WorkOnGarageJob(0), "empty bay");
            t.AssertEqual(GarageWorkResult.NoJob, WorldSim.Instance.WorkOnGarageJob(7), "no such bay");
            t.AssertEqual(before, Snapshot(), "NoJob mutates nothing");

            // A finished car takes no more work (and pays no more XP).
            data.GarageJobs.Add(new GarageJobRecord
            {
                ServiceId = GarageServices.OilChange, WorkDone = 100, Completed = true,
            });
            before = Snapshot();
            t.AssertEqual(GarageWorkResult.AlreadyDone, WorldSim.Instance.WorkOnGarageJob(0),
                "done is done");
            t.AssertEqual(before, Snapshot(), "AlreadyDone mutates nothing");

            // Out of stamina: checked BEFORE any mutation.
            data.GarageJobs.Clear();
            data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange });
            data.Player.Stamina = 1;
            before = Snapshot();
            t.AssertEqual(GarageWorkResult.NotEnoughStamina, WorldSim.Instance.WorkOnGarageJob(0),
                "one stamina is under a full press");
            t.AssertEqual(before, Snapshot(), "NotEnoughStamina mutates nothing");

            t.AssertEqual(0, events, "refusals fire no events");
        }
        finally
        {
            WorldSim.Instance.GarageJobsChanged -= Count;
            WorldSim.Instance.SkillsChanged -= Count;
            WorldSim.Instance.StaminaChanged -= CountStamina;
            WorldSim.Instance.GarageJobCompleted -= CountJob;
            service.NewGame();
        }
    }

    [SimTest]
    public static void GarageOps_CompletionGrantsXpAndOrdersEvents(TestContext t)
    {
        // The completing press: repaint → StaminaChanged → GarageJobsChanged →
        // SkillsChanged → SkillLeveledUp (edge crossed here on purpose) →
        // GarageJobCompleted, per the WorkOnGarageJob doc.
        SaveService service = SaveService.Instance;
        var sequence = new List<string>();
        void OnStamina(int a, int b) => sequence.Add("stamina");
        void OnJobs() => sequence.Add("jobs");
        void OnSkills() => sequence.Add("skills");
        void OnLevel(string id, int level) => sequence.Add($"level:{id}:{level}");
        void OnDone(GarageJobRecord job) => sequence.Add($"done:{job.ServiceId}");
        WorldSim.Instance.StaminaChanged += OnStamina;
        WorldSim.Instance.GarageJobsChanged += OnJobs;
        WorldSim.Instance.SkillsChanged += OnSkills;
        WorldSim.Instance.SkillLeveledUp += OnLevel;
        WorldSim.Instance.GarageJobCompleted += OnDone;
        try
        {
            service.NewGame();
            GameData data = service.Current;
            data.TrySetFlag(StoryKeys.GarageDeed, 0);
            data.Player.SkillXp[SkillIds.MechanicalRepair] = 9;   // one repair from level 2
            data.GarageJobs.Add(new GarageJobRecord
            {
                ServiceId = GarageServices.OilChange, WorkDone = 99,   // one press left
            });

            t.AssertEqual(GarageWorkResult.CompletedJob, WorldSim.Instance.WorkOnGarageJob(0),
                "the last press completes");
            t.AssertEqual(10L, SkillRules.Xp(data, SkillIds.MechanicalRepair),
                "any completed repair is one point");
            t.AssertEqual("stamina|jobs|skills|level:mechanical_repair:2|done:oil_change",
                string.Join("|", sequence), "the completing press's event order");
            t.Assert(data.GarageJobs[0].Completed, "the car waits on the lift for pickup");

            // An ordinary banked press: stamina + jobs only.
            sequence.Clear();
            data.GarageJobs.Add(new GarageJobRecord
            {
                ServiceId = GarageServices.Lights, Lift = 1,
            });
            t.AssertEqual(GarageWorkResult.Worked, WorldSim.Instance.WorkOnGarageJob(1), "banked");
            t.AssertEqual("stamina|jobs", string.Join("|", sequence), "no skill events mid-job");
        }
        finally
        {
            WorldSim.Instance.StaminaChanged -= OnStamina;
            WorldSim.Instance.GarageJobsChanged -= OnJobs;
            WorldSim.Instance.SkillsChanged -= OnSkills;
            WorldSim.Instance.SkillLeveledUp -= OnLevel;
            WorldSim.Instance.GarageJobCompleted -= OnDone;
            service.NewGame();
        }
    }

    // ------------------------------------------------------------------
    // Arrivals — the HourTicked wiring
    // ------------------------------------------------------------------

    /// <summary>First seed whose roll arrives at (day, hour) — tests stay valid
    /// however the mixer is tuned.</summary>
    private static int ArrivingSeed(long day, int hour)
    {
        for (int seed = 0; ; seed++)
        {
            if (GarageOpsRules.CustomerRoll(seed, day, hour).Arrived)
            {
                return seed;
            }
        }
    }

    [SimTest]
    public static void GarageOps_HourlyArrivalWiring(TestContext t)
    {
        SaveService service = SaveService.Instance;
        var sequence = new List<string>();
        void OnJobs() => sequence.Add("jobs");
        void OnArrived(GarageJobRecord job) => sequence.Add($"arrived:{job.ServiceId}");
        WorldSim.Instance.GarageJobsChanged += OnJobs;
        WorldSim.Instance.GarageCustomerArrived += OnArrived;
        try
        {
            service.NewGame();
            GameData data = service.Current;
            data.Seed = ArrivingSeed(0, 9);
            (_, int expectedService) = GarageOpsRules.CustomerRoll(data.Seed, 0, 9);

            // Not owned: the 9:00 tick rolls nothing however the dice landed.
            Clock.Instance.SetTime(new GameTime(179));   // 8:59 AM, day 0
            Clock.Instance.AdvanceMinutes(1);
            t.AssertEqual(0, data.GarageJobs.Count, "no deed, no customers");

            // Owned: the same tick lands the customer, stamped and bayed.
            data.TrySetFlag(StoryKeys.GarageDeed, 0);
            Clock.Instance.SetTime(new GameTime(179));
            sequence.Clear();
            Clock.Instance.AdvanceMinutes(1);
            t.AssertEqual(1, data.GarageJobs.Count, "the 6% hour lands its customer");
            GarageJobRecord job = data.GarageJobs[0];
            t.AssertEqual(GarageServices.All[expectedService].Id, job.ServiceId,
                "the roll picked the service");
            t.AssertEqual((0L, 9, 0, 0, false),
                (job.ArrivalDay, job.ArrivalHour, job.Lift, job.WorkDone, job.Completed),
                "arrival stamps day, hour, and the first free bay");
            t.AssertEqual($"jobs|arrived:{job.ServiceId}", string.Join("|", sequence),
                "GarageJobsChanged before GarageCustomerArrived");

            // Replaying the same hour is idempotent — the stamp is the guard.
            Clock.Instance.SetTime(new GameTime(179));
            Clock.Instance.AdvanceMinutes(1);
            t.AssertEqual(1, data.GarageJobs.Count, "a re-fired hour lands nobody twice");

            // A full shop turns customers away: both bays taken, an arriving hour
            // rolls nothing.
            data.GarageJobs.Clear();
            data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.OilChange, Lift = 0 });
            data.GarageJobs.Add(new GarageJobRecord { ServiceId = GarageServices.Lights, Lift = 1 });
            Clock.Instance.SetTime(new GameTime(179));
            Clock.Instance.AdvanceMinutes(1);
            t.AssertEqual(2, data.GarageJobs.Count, "no more than two cars, ever");

            // Closed hours never roll, however the dice landed: 8 PM arriving seed.
            data.GarageJobs.Clear();
            data.Seed = ArrivingSeed(0, 20);
            Clock.Instance.SetTime(new GameTime(839));   // 7:59 PM
            Clock.Instance.AdvanceMinutes(1);
            t.AssertEqual(0, data.GarageJobs.Count, "the window gates arrivals");

            // Sleeping through open hours rolls nothing: AdvanceToDayStart fires no
            // hour ticks (the documented v1 limitation, pinned).
            data.Seed = ArrivingSeed(0, 13);
            Clock.Instance.SetTime(new GameTime(360));   // noon, day 0
            Clock.Instance.AdvanceToDayStart();
            t.AssertEqual(0, data.GarageJobs.Count, "slept-through hours land nobody");
        }
        finally
        {
            WorldSim.Instance.GarageJobsChanged -= OnJobs;
            WorldSim.Instance.GarageCustomerArrived -= OnArrived;
            service.NewGame();
        }
    }

    // ------------------------------------------------------------------
    // The dawn resolution
    // ------------------------------------------------------------------

    [SimTest]
    public static void GarageOps_OvernightPaysExpiresAndFloors(TestContext t)
    {
        GameData data = GameData.NewGame();
        data.TrySetFlag(StoryKeys.GarageDeed, 0);

        // Payment: completed day D, paid at the dawn after ("money is collected
        // the next day"), on top of the floor.
        data.Player.Money = 50;
        data.GarageJobs.Add(new GarageJobRecord
        {
            ServiceId = GarageServices.OilChange, ArrivalDay = 0, WorkDone = 100, Completed = true,
        });
        OvernightReport report = OvernightSim.Run(data, dayEnding: 0);
        t.AssertEqual(DevScaffold.DailyMoneyFloor + 100, data.Player.Money,
            "the floor lands first, the payment on top of it");
        t.AssertEqual(0, data.GarageJobs.Count, "the paid car leaves at dawn");
        t.AssertEqual(1, report.Garage!.Count, "one garage line");
        t.AssertEqual(new GarageLine(GarageServices.OilChange, 100, Reclaimed: false),
            report.Garage![0], "the paid line");

        // In-progress inside the window: survives the dawn with its work intact —
        // Kevin's "work can be partially complete over multiple sessions".
        var partial = new GarageJobRecord
        {
            ServiceId = GarageServices.Transmission, ArrivalDay = 1, WorkDone = 210,
        };
        data.GarageJobs.Add(partial);
        report = OvernightSim.Run(data, dayEnding: 1);   // waking into day 2 = ArrivalDay + 1
        t.AssertEqual(1, data.GarageJobs.Count, "day D+1 is still the customer's window");
        t.AssertEqual(210, partial.WorkDone, "banked work survives the night untouched");
        t.Assert(report.Garage is null or { Count: 0 }, "nothing resolved, no lines");

        // Expiry: at dawn of ArrivalDay + 2 the unfinished car is reclaimed, unpaid.
        long moneyBefore = data.Player.Money;
        report = OvernightSim.Run(data, dayEnding: 2);
        t.AssertEqual(0, data.GarageJobs.Count, "the 2-day deadline reclaims the car");
        t.AssertEqual(moneyBefore, data.Player.Money, "no money for unfinished work");
        t.AssertEqual(new GarageLine(GarageServices.Transmission, 0, Reclaimed: true),
            report.Garage![0], "the reclaimed line");

        // PINNED invariant: a job COMPLETED on its deadline's last day matches both
        // rules, and payment must win — the customer found the work done.
        data.GarageJobs.Add(new GarageJobRecord
        {
            ServiceId = GarageServices.Lights, ArrivalDay = 3, WorkDone = 150, Completed = true,
        });
        moneyBefore = data.Player.Money;
        report = OvernightSim.Run(data, dayEnding: 4);   // dawn of ArrivalDay + 2
        t.AssertEqual(moneyBefore + 150, data.Player.Money,
            "completed on the last day is PAID, never reclaimed");
        t.AssertEqual(new GarageLine(GarageServices.Lights, 150, Reclaimed: false),
            report.Garage![0], "the last-day line is a payment");

        // The floor only ever tops UP (earnings above it are kept)...
        data.Player.Money = 400_000;
        OvernightSim.Run(data, dayEnding: 5);
        t.AssertEqual(400_000L, data.Player.Money, "the floor never takes");
        // ...and shipping income lands ON TOP of the floor, never inside it.
        data.Player.Money = 10;
        data.ShippingBin.Add(new ItemStackRecord { ItemId = "turnip", Count = 1 });
        long turnipPrice = ItemDefs.Get("turnip").SellPrice;
        OvernightSim.Run(data, dayEnding: 6);
        t.AssertEqual(DevScaffold.DailyMoneyFloor + turnipPrice, data.Player.Money,
            "floor first, then the night's income — earnings stay visible");
    }

    [SimTest]
    public static void GarageOps_LiftsAreStableAndReused(TestContext t)
    {
        GameData data = GameData.NewGame();
        data.TrySetFlag(StoryKeys.GarageDeed, 0);
        var a = new GarageJobRecord { ServiceId = GarageServices.OilChange, Lift = 0, Completed = true, WorkDone = 100 };
        var b = new GarageJobRecord { ServiceId = GarageServices.Lights, Lift = 1, ArrivalDay = 0, WorkDone = 5 };
        data.GarageJobs.Add(a);
        data.GarageJobs.Add(b);
        t.Assert(GarageOpsRules.FreeLift(data) is null, "both bays taken");

        OvernightSim.Run(data, dayEnding: 0);   // pays A out, keeps B (day D+1)
        t.AssertEqual(1, data.GarageJobs.Count, "one car left");
        t.AssertEqual(1, data.GarageJobs[0].Lift, "the survivor keeps ITS bay — cars never hop lifts");
        t.AssertEqual(0, GarageOpsRules.FreeLift(data)!.Value, "the freed bay is the next one filled");
        t.Assert(GarageOpsRules.JobAt(data, 1) == b, "JobAt answers by bay");
        t.Assert(GarageOpsRules.JobAt(data, 0) is null, "the paid bay is empty");
    }

    // ------------------------------------------------------------------
    // Save shape
    // ------------------------------------------------------------------

    [SimTest]
    public static void GarageOps_LoadRepairDefendsTheShop(TestContext t)
    {
        SaveService service = SaveService.Instance;
        try
        {
            // A hostile jobs list: unknown service, limbo work-done, a lift
            // collision, over-capacity, negative fields.
            service.DeserializeFrom("""
                {"SaveVersion":7,"TotalMinutes":0,"Seed":42,"GarageJobs":[
                    {"ServiceId":"warp_drive","Lift":0},
                    {"ServiceId":"oil_change","Lift":0,"WorkDone":999,"ArrivalDay":-4},
                    {"ServiceId":"lights","Lift":0,"WorkDone":-5,"ArrivalHour":99},
                    {"ServiceId":"transmission","Lift":1,"WorkDone":10}]}
                """);
            GameData data = service.Current;
            t.AssertEqual(42, data.Seed, "the seed rides the save");
            t.AssertEqual(2, data.GarageJobs.Count,
                "unknown service dropped (deliberate transient-record deviation), "
                + "overflow beyond the two bays dropped");

            GarageJobRecord first = data.GarageJobs[0];
            t.AssertEqual(GarageServices.OilChange, first.ServiceId, "first well-formed job kept");
            t.AssertEqual(100, first.WorkDone, "WorkDone clamps to the service's Work");
            t.Assert(first.Completed, "full work IS completion — no limbo state");
            t.AssertEqual(0L, first.ArrivalDay, "negative day clamps");
            t.AssertEqual(0, first.Lift, "keeps its claimed bay");

            GarageJobRecord second = data.GarageJobs[1];
            t.AssertEqual("lights", second.ServiceId, "collider kept");
            t.AssertEqual(1, second.Lift, "the lift collision moved it to the free bay");
            t.AssertEqual(0, second.WorkDone, "negative work clamps");
            // (transmission wanted lift 1, now taken, no bay free -> dropped.)

            // Round-trip a healthy shop.
            service.NewGame();
            data = service.Current;
            data.Seed = 7;
            data.GarageJobs.Add(new GarageJobRecord
            {
                ServiceId = GarageServices.Lights, ArrivalDay = 3, ArrivalHour = 14, Lift = 1, WorkDone = 60,
            });
            service.DeserializeFrom(service.SerializeToString());
            data = service.Current;
            t.AssertEqual(7, data.Seed, "seed round-trips");
            t.AssertEqual(1, data.GarageJobs.Count, "the job round-trips");
            GarageJobRecord loaded = data.GarageJobs[0];
            t.AssertEqual(("lights", 3L, 14, 1, 60, false),
                (loaded.ServiceId, loaded.ArrivalDay, loaded.ArrivalHour, loaded.Lift,
                 loaded.WorkDone, loaded.Completed),
                "every field survives");
        }
        finally
        {
            service.NewGame();
        }
    }

    [SimTest]
    public static void GarageOps_MigrationV6ToV7(TestContext t)
    {
        // The V5->V6 shape: purely a version-gate bump; absent fields read their
        // intended defaults after the stamp.
        SaveService service = SaveService.Instance;
        try
        {
            using var file = Godot.FileAccess.Open(
                "res://src/Tests/fixtures/v6_minimal.json", Godot.FileAccess.ModeFlags.Read);
            t.Assert(file != null, $"fixture opens: {Godot.FileAccess.GetOpenError()}");
            service.DeserializeFrom(file!.GetAsText());
            GameData data = service.Current;
            t.AssertEqual(SaveMigrations.CurrentVersion, data.SaveVersion, "stamped current");
            t.AssertEqual(0, data.Seed, "absent seed reads 0");
            t.AssertEqual(0, data.GarageJobs.Count, "absent jobs read empty");
            t.AssertEqual(0, data.Player.SkillXp.Count, "absent skills read empty");
            t.AssertEqual(100L, data.Player.Money, "the v6 payload survives");
            t.Assert(data.HasFlag(StoryKeys.GarageDeed), "a v6 deed survives");

            // Idempotence: re-serialize, re-load, byte-stable.
            string once = service.SerializeToString();
            service.DeserializeFrom(once);
            t.AssertEqual(once, service.SerializeToString(), "round-trip is a fixed point");
        }
        finally
        {
            service.NewGame();
        }
    }

    private static string Snapshot() =>
        JsonSerializer.Serialize(SaveService.Instance.Current, SaveJsonContext.Default.GameData);
}
