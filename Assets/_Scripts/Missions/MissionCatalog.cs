using System.Collections.Generic;

/// <summary>
/// Default content of the 100-level campaign (10 chapters x 10 levels).
/// Tools > Missions > Setup copies this into Resources/Missions/MissionDatabase.asset,
/// where every mission can be tuned in the Inspector. At runtime the asset is used;
/// this class is only the fallback if the asset is missing.
/// </summary>
public static class MissionCatalog
{
    public static List<MissionDef> Build()
    {
        var list = new List<MissionDef>(100);
        Chapter1(list);
        Chapter2(list);
        Chapter3(list);
        Chapter4(list);
        Chapter5(list);
        Chapter6(list);
        Chapter7(list);
        Chapter8(list);
        Chapter9(list);
        Chapter10(list);

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            m.chapter = i / 10;
            m.level = i % 10;
            if (m.reward <= 0 || m.reward == 2500) m.reward = 2500 + m.chapter * 500 + m.level * 100;
        }
        return list;
    }

    // ───────────────────────────── helpers ─────────────────────────────

    static MissionDef M(string title, string story, string objective, string complete, params MissionStep[] steps)
    {
        return new MissionDef
        {
            title = title,
            story = story,
            objective = objective,
            completeText = complete,
            steps = new List<MissionStep>(steps),
        };
    }

    static MissionStep Reach(float min, float max, string label = "Destination", float radius = 8f) =>
        new MissionStep { kind = StepKind.Reach, minDist = min, maxDist = max, label = label, radius = radius };

    static MissionStep Back(string label) =>
        new MissionStep { kind = StepKind.Reach, toStart = true, label = label, radius = 9f };

    static MissionStep Cps(int n, bool ordered, float min, float max, string label = "Checkpoint", float minKmh = 0f) =>
        new MissionStep { kind = StepKind.Checkpoints, count = n, ordered = ordered, minDist = min, maxDist = max, label = label, minKmh = minKmh, radius = 8f };

    static MissionStep Deliver(int drops, float min, float max, string pick, string drop) =>
        new MissionStep { kind = StepKind.PickupDeliver, count = drops, minDist = min, maxDist = max, label = pick, altLabel = drop, radius = 7f };

    static MissionStep Collect(int n, bool ordered, float min, float max, string label) =>
        new MissionStep { kind = StepKind.Collect, count = n, ordered = ordered, minDist = min, maxDist = max, label = label, radius = 6f };

    static MissionStep Hold(int n, float seconds, float radius, float maxKmh, float min, float max, string label, float minKmh = 0f) =>
        new MissionStep { kind = StepKind.Hold, count = n, hold = seconds, radius = radius, holdMaxSpeedKmh = maxKmh, minKmh = minKmh, minDist = min, maxDist = max, label = label };

    static MissionStep Follow(MoverKind mover, float kmh, float near, float far, float dist, string label) =>
        new MissionStep { kind = StepKind.Follow, mover = mover, moverSpeedKmh = kmh, nearDist = near, farDist = far, minDist = dist * 0.8f, maxDist = dist, label = label };

    static MissionStep Pace(MoverKind mover, float kmh, float seconds, int count, string label) =>
        new MissionStep { kind = StepKind.Pace, mover = mover, moverSpeedKmh = kmh, duration = seconds, count = count, nearDist = 2.5f, farDist = 9f, minDist = 250f, maxDist = 450f, label = label };

    static MissionStep Escort(MoverKind mover, float kmh, float far, float min, float max, string label, int attackers = 0) =>
        new MissionStep { kind = StepKind.Escort, mover = mover, moverSpeedKmh = kmh, farDist = far, minDist = min, maxDist = max, label = label, count = attackers };

    static MissionStep Tow(float maxKmh, float min, float max, string label) =>
        new MissionStep { kind = StepKind.Tow, maxKmh = maxKmh, minDist = min, maxDist = max, label = label, radius = 9f };

    static MissionStep Evade(int pursuers, MoverKind mover, float kmh, float seconds, float safeMin = 0f, float safeMax = 0f, string label = "Safe Zone") =>
        new MissionStep { kind = StepKind.Evade, count = pursuers, mover = mover, moverSpeedKmh = kmh, duration = seconds, minDist = safeMin, maxDist = safeMax, label = label, radius = 10f };

    static MissionStep Race(int opponents, MoverKind mover, int laps, int checkpoints, float kmh, int rank) =>
        new MissionStep { kind = StepKind.Race, count = opponents, mover = mover, hits = laps, requiredRank = rank, moverSpeedKmh = kmh, duration = checkpoints, minDist = 90f, maxDist = 170f, radius = 10f, label = "Race" };

    static MissionStep Trial(float kmh, float dist) =>
        new MissionStep { kind = StepKind.SpeedTrial, minKmh = kmh, maxDist = dist, label = "Speed Trap" };

    static MissionStep Band(float min, float max, float seconds) =>
        new MissionStep { kind = StepKind.SpeedBand, minKmh = min, maxKmh = max, duration = seconds, label = "Data Transfer" };

    static MissionStep Survive(float seconds, string label = "Survive") =>
        new MissionStep { kind = StepKind.Survive, duration = seconds, label = label };

    static MissionStep Ram(int targets, int hits, MoverKind mover, float kmh, string label) =>
        new MissionStep { kind = StepKind.Ram, count = targets, hits = hits, mover = mover, moverSpeedKmh = kmh, label = label, minDist = 120f, maxDist = 260f };

    static MissionStep Jump(float kmh) =>
        new MissionStep { kind = StepKind.Jump, minKmh = kmh, label = "Ramp" };

    static MissionStep Choice(string a, string b) =>
        new MissionStep { kind = StepKind.Choice, label = a, altLabel = b, minDist = 300f, maxDist = 500f, radius = 10f };

    static MissionDef With(this MissionDef m, MissionArea area = MissionArea.Road, MissionEnv env = MissionEnv.Day,
        int crashes = 1, float speedLimit = 0f, bool noReverse = false, float timeFactor = 1f,
        HazardKind hazard = HazardKind.None, int hazardCount = 0, PropKind props = PropKind.None, int propCount = 0,
        int strikes = 3, float fixedTime = 0f)
    {
        m.area = area; m.env = env; m.maxCrashes = crashes; m.speedLimitKmh = speedLimit; m.noReverse = noReverse;
        m.timeFactor = timeFactor; m.hazard = hazard; m.hazardCount = hazardCount; m.props = props; m.propCount = propCount;
        m.maxStrikes = strikes; m.fixedTime = fixedTime;
        return m;
    }

    static MissionDef Hand(string title, string story, string objective)
    {
        return new MissionDef { title = title, story = story, objective = objective, handBuilt = true, completeText = "Mission complete!" };
    }

    // ───────────────────────────── CHAPTER 1 – AWAKENING (hand-built) ─────────────────────────────

    static void Chapter1(List<MissionDef> l)
    {
        l.Add(Hand("Ignite", "Car-01 is activating for the first time. System boot and basic controls test.",
            "Press accelerator and brake 3 times each without hitting walls to finish calibration."));
        l.Add(Hand("First Move", "The system started a Move Test. Get the car out of the garage.",
            "Drive out of the garage through the alley and reach the exit marker without crashing."));
        l.Add(Hand("Avoid Traffic", "First time on the main road between normal traffic.",
            "Drive through 6 parked cars in a slalom path without hitting any."));
        l.Add(Hand("First Package", "Carry a small parcel from a café to the tech shop.",
            "Reach the café, auto-pick package, then deliver to the shop before timer ends."));
        l.Add(Hand("Delivery Route", "Same route again, now the system checks speed and consistency.",
            "Complete Café → Shop route faster than previous mission. Max 1 crash allowed."));
        l.Add(Hand("Battery Hunt", "The internal battery is low. 5 energy cells are scattered around the city.",
            "Collect 5 battery cells in the correct order before the timer runs out."));
        l.Add(Hand("Police Scanner", "A police scanner is detected for the first time. Red zones are active.",
            "Reach 3 safe checkpoints without entering red scanner zones."));
        l.Add(Hand("Shortcut Lane", "The AI wants to show you a hidden shortcut lane for future missions.",
            "Find the secret alley on the minimap and reach its end without crashing."));
        l.Add(Hand("Meeting", "An unknown AI car contacts you and asks you to follow it.",
            "Follow the AI car at a safe distance without overtaking or hitting it."));
        l.Add(Hand("Escape Garage 2.0", "Your original facility garage is going into lockdown.",
            "Escape through the gate before timer ends and reach the safe street marker."));
    }

    // ───────────────────────────── CHAPTER 2 – THE CITY NOTICES ─────────────────────────────

    static void Chapter2(List<MissionDef> l)
    {
        l.Add(M("Taxi Follow", "The hacker wants you to record a suspicious taxi's data.",
            "Follow the taxi. Not too far, not too close: keep the distance meter green until the route ends.",
            "Taxi data recorded!",
            Follow(MoverKind.Taxi, 40f, 8f, 40f, 450f, "Taxi")).With(crashes: 1));

        l.Add(M("Red Lights", "The hacker teaches you to hack traffic lights for a safe route.",
            "Stop inside 3 traffic-light hack zones until each hack completes, without hitting any pedestrian.",
            "Traffic lights hacked!",
            Hold(3, 3f, 6f, 15f, 100f, 250f, "Traffic Light")).With(crashes: 0, props: PropKind.Pedestrians, propCount: 8));

        l.Add(M("Courier Job", "An illegal courier service gives you a test run.",
            "Pick up 3 boxes at the warehouse and deliver them to 3 different drop spots in time.",
            "All boxes delivered!",
            Deliver(3, 150f, 330f, "Warehouse", "Drop Spot")).With(crashes: 1));

        l.Add(M("Pedestrian Guardian", "The system checks your safety priority in a crowded area.",
            "Drive slowly through the narrow street and avoid every pedestrian. One hit fails the mission.",
            "Safety priority confirmed!",
            Reach(250f, 400f, "Street End")).With(crashes: 0, speedLimit: 40f, props: PropKind.Pedestrians, propCount: 14));

        l.Add(M("Meet Hacker", "The hacker wants to meet you in an underground parking.",
            "Reach the parking entrance, then park precisely inside the marked slot.",
            "You met the hacker!",
            Reach(200f, 380f, "Parking Entrance"),
            Hold(1, 2f, 3f, 4f, 25f, 60f, "Parking Slot")).With(crashes: 1));

        l.Add(M("City Tour", "The hacker shows you the city layout using checkpoints.",
            "Pass all 6 city checkpoints in any order before the time runs out.",
            "City layout mapped!",
            Cps(6, false, 150f, 420f)).With(crashes: 1));

        l.Add(M("Construction Zone", "Road construction is going on and you must cross the risky area.",
            "Avoid cones, barriers and potholes and reach the exit point. Max 2 minor hits allowed.",
            "Construction zone cleared!",
            Reach(300f, 450f, "Exit")).With(crashes: 2, props: PropKind.Cones, propCount: 30));

        l.Add(M("Speed Trial", "The AI engine wants to test your maximum speed.",
            "Accelerate on the strip and touch 120 km/h before the end of the speed trap.",
            "Top speed confirmed!",
            Trial(120f, 420f)).With(crashes: 1));

        var fuel = M("Fuel Rescue", "Fuel is almost empty and the nearest station is far.",
            "The fuel bar is draining. Choose the direct route and reach the fuel station marker in time.",
            "Refuelled just in time!",
            Reach(260f, 420f, "Fuel Station")).With(crashes: 1);
        fuel.startFuel = 22f; fuel.fuelDrainPerSecond = 0.35f;
        l.Add(fuel);

        l.Add(M("Lost Car Rescue", "The AI car you met in Chapter 1 is in breakdown mode.",
            "Reach the broken AI car, connect remote tow, then escort it slowly to the safe garage.",
            "Your friend is safe!",
            Reach(150f, 300f, "Broken AI Car"),
            Tow(45f, 250f, 400f, "Safe Garage")).With(crashes: 1));
    }

    // ───────────────────────────── CHAPTER 3 – HACKER ALLIANCE ─────────────────────────────

    static void Chapter3(List<MissionDef> l)
    {
        l.Add(M("Hacker Pickup", "The hacker dropped a small device on the roadside for you.",
            "Slow down at the device marker to pick it up, then take it to the safe alley without crashing.",
            "Device secured!",
            Hold(1, 1.5f, 5f, 20f, 150f, 300f, "Device"),
            Reach(200f, 350f, "Safe Alley")).With(crashes: 0));

        l.Add(M("Signal Hijack", "Now you hijack the local traffic network.",
            "Stop at 4 junctions to complete each hack, then return to the central marker.",
            "Traffic network hijacked!",
            Hold(4, 3f, 7f, 15f, 120f, 260f, "Junction"),
            Back("Central Marker")).With(crashes: 1));

        l.Add(M("Van Chase", "A spy van is moving slowly with stolen data.",
            "Drive parallel to the spy van until the upload bar is full. Avoid collisions.",
            "Data stolen from the van!",
            Pace(MoverKind.Van, 45f, 5f, 1, "Spy Van")).With(crashes: 0));

        l.Add(M("Hidden Tunnel", "The hacker shows a hidden tunnel that will confuse the police.",
            "Find the tunnel entrance, then follow the complete tunnel route.",
            "Hidden tunnel learned!",
            Reach(250f, 400f, "Tunnel Entrance"),
            Cps(4, true, 60f, 120f, "Tunnel")).With(env: MissionEnv.Dusk, crashes: 1));

        l.Add(M("Speed with Device", "An overheating chip must reach a cool place fast.",
            "Take the shortest route to the cool-zone marker before the chip heat meter is full.",
            "Chip cooled down!",
            Reach(450f, 700f, "Cool Zone")).With(area: MissionArea.Any, crashes: 1, timeFactor: 0.8f));

        l.Add(M("Night Run", "The city's first full night test run.",
            "Complete 5 checkpoints at night with headlights on, without losing control.",
            "Night run complete!",
            Cps(5, true, 150f, 300f)).With(env: MissionEnv.Night, crashes: 1));

        l.Add(M("Secret Data Drop", "Drop data for a dark web client.",
            "Stop at the crate until the virtual drop completes, then leave without attracting attention.",
            "Data dropped silently!",
            Hold(1, 6f, 5f, 5f, 200f, 350f, "Crate"),
            Reach(150f, 250f, "Leave Area")).With(crashes: 1, speedLimit: 60f));

        l.Add(M("Outrun Drone", "A surveillance drone starts tracking you.",
            "Avoid the drone spotlights and reach the safe tunnel. Spotted more than 3 times = fail.",
            "Drone lost your trail!",
            Reach(400f, 600f, "Safe Tunnel")).With(crashes: 1, hazard: HazardKind.Spotlight, hazardCount: 2, strikes: 3));

        l.Add(M("Battery Backup", "Take a secret charge from the power grid.",
            "Circle the backup generator until the energy siphon completes, then escape before police arrive.",
            "Energy siphoned!",
            Hold(1, 8f, 16f, 0f, 150f, 300f, "Generator", 10f),
            Evade(2, MoverKind.Police, 85f, 0f, 300f, 450f, "Escape Point")).With(crashes: 1));

        l.Add(M("Broadcast Hack", "Disrupt the police radio frequency.",
            "Drive side by side with 3 radio tower vans to activate the jammer, then return to base.",
            "Police radio jammed!",
            Pace(MoverKind.Van, 40f, 4f, 3, "Radio Van"),
            Back("Base")).With(crashes: 1));
    }

    // ───────────────────────────── CHAPTER 4 – POLICE STRIKE ─────────────────────────────

    static void Chapter4(List<MissionDef> l)
    {
        l.Add(M("City Lockdown", "City-wide checkpoints are up. You are wanted.",
            "Avoid the red checkpoint zones and use alternate routes to reach the safe house.",
            "You reached the safe house!",
            Reach(500f, 700f, "Safe House")).With(crashes: 1, hazard: HazardKind.RedZone, hazardCount: 6));

        l.Add(M("Siren Alert", "Direct police chase test.",
            "A police car is behind you. Keep away from it for 60 seconds. Getting caught fails.",
            "You escaped the police!",
            Evade(1, MoverKind.Police, 95f, 60f)).With(crashes: 1));

        l.Add(M("Safe Zone Reach", "The mall basement parking is the safest place now.",
            "Reach the mall entrance, then park precisely in the basement slot.",
            "Hidden in the basement!",
            Reach(350f, 500f, "Mall Entrance"),
            Hold(1, 2f, 3f, 4f, 30f, 70f, "Basement Slot")).With(crashes: 1));

        l.Add(M("Tuning Upgrade", "The hacker arranged a hidden tuner garage.",
            "Avoid the surveillance drones and reach the tuner garage gate.",
            "Upgrade installed!",
            Reach(450f, 650f, "Tuner Garage")).With(crashes: 1, hazard: HazardKind.Spotlight, hazardCount: 3));

        l.Add(M("Convoy Avoid", "A police armored convoy blocks the road.",
            "Bypass the convoy without touching any armored truck and reach the next area.",
            "Convoy bypassed!",
            Reach(400f, 600f, "Next Area")).With(crashes: 0, props: PropKind.Convoy, propCount: 5));

        l.Add(M("Bridge Control", "The bridge has a toll and scan system.",
            "Drive slowly, avoid the scanner zones and get to the other side.",
            "Scanner bypassed!",
            Reach(250f, 400f, "Bridge Exit")).With(crashes: 1, speedLimit: 45f, hazard: HazardKind.RedZone, hazardCount: 3));

        l.Add(M("Rescue Car 02", "Another AI car is about to be arrested.",
            "Shadow the police tow truck, then unlock the AI car before it reaches the tow point.",
            "Car 02 unlocked!",
            Follow(MoverKind.Truck, 45f, 10f, 60f, 380f, "Tow Truck"),
            Hold(1, 3f, 6f, 15f, 40f, 90f, "AI Car 02")).With(crashes: 1));

        l.Add(M("Traffic Jam", "A huge traffic jam blocks the city centre.",
            "Weave carefully between the blocked cars and reach the other side. No reversing.",
            "Traffic jam crossed!",
            Reach(300f, 450f, "Other Side")).With(crashes: 2, noReverse: true, props: PropKind.TrafficJam, propCount: 24));

        l.Add(M("Night Police Chase", "High alert night mode with multiple police cars.",
            "Use narrow streets and alleys to survive the chase for 90 seconds.",
            "You lost them in the dark!",
            Evade(3, MoverKind.Police, 100f, 90f)).With(env: MissionEnv.Night, crashes: 2));

        l.Add(M("Underground Escape", "The ground is unsafe; tunnels are the only option.",
            "Go through the underground route and choose the correct forks to reach the exit.",
            "You escaped underground!",
            Cps(5, true, 100f, 200f, "Tunnel Fork")).With(env: MissionEnv.Night, crashes: 1));
    }

    // ───────────────────────────── CHAPTER 5 – UNDERGROUND ROADS ─────────────────────────────

    static void Chapter5(List<MissionDef> l)
    {
        l.Add(M("Subway Chase", "You are in a tunnel parallel to the metro line and a train is racing you.",
            "Beat the train to the finish line without touching it.",
            "You beat the train!",
            Race(1, MoverKind.Train, 1, 6, 90f, 1)).With(env: MissionEnv.Night, crashes: 1));

        l.Add(M("Off-Road 1", "A muddy off-road trail outside the tunnels.",
            "Keep control on the slippery surface and follow the trail checkpoints.",
            "Off-road trail complete!",
            Cps(6, true, 80f, 160f, "Trail")).With(area: MissionArea.Grass, crashes: 2));

        l.Add(M("Battery Theft", "Police trucks are carrying advanced batteries.",
            "Drive beside 2 slow trucks until each battery is siphoned. No direct hits.",
            "Batteries siphoned!",
            Pace(MoverKind.Truck, 40f, 3f, 2, "Battery Truck")).With(crashes: 0));

        l.Add(M("Dark Tunnel", "A section of the tunnel is in total blackout.",
            "Headlights have limited range. Go slow, dodge falling debris and reach the exit.",
            "Out of the dark!",
            Reach(350f, 500f, "Tunnel Exit")).With(env: MissionEnv.Blackout, crashes: 1, hazard: HazardKind.Rocks, hazardCount: 1));

        l.Add(M("Smuggler Route", "The smugglers allow you on their secret route for a while.",
            "Follow the smuggler truck. Don't hit it and don't block it.",
            "Smugglers trust you now!",
            Follow(MoverKind.Truck, 35f, 8f, 55f, 500f, "Smuggler Truck")).With(crashes: 0));

        var bm = M("Black Market Fuel", "Legal fuel stations are closed. Only the black market is left.",
            "Find the hidden fuel tanker, stop precisely to refuel, then leave quickly.",
            "Tank full, nobody saw you!",
            Reach(250f, 400f, "Hidden Tanker"),
            Hold(1, 4f, 3f, 3f, 20f, 40f, "Refuel Spot"),
            Reach(150f, 250f, "Get Out")).With(crashes: 1);
        bm.startFuel = 30f; bm.fuelDrainPerSecond = 0.15f;
        l.Add(bm);

        l.Add(M("Street Gang Race", "An underground street gang wants to race you.",
            "Race 3 gang cars over 2 laps and finish 2nd or better.",
            "The gang respects you!",
            Race(3, MoverKind.Car, 2, 6, 95f, 2)).With(crashes: 2));

        l.Add(M("Secret Bridge Jump", "An old broken bridge will be used for a stunt.",
            "Build speed, hit the ramp at 80 km/h or more and land in the safe zone.",
            "Perfect landing!",
            Jump(80f)).With(crashes: 1));

        l.Add(M("Storm Escape", "Heavy rain and thunderstorm, low visibility.",
            "Complete the long curved route on wet roads without spinning out.",
            "You escaped the storm!",
            Cps(6, true, 120f, 220f)).With(env: MissionEnv.Storm, crashes: 1));

        l.Add(M("Truck Escort", "Escort an NPC supply truck to the industrial zone.",
            "Stay close to the truck and protect it from the road dangers until it arrives.",
            "Supplies delivered safely!",
            Escort(MoverKind.Truck, 45f, 40f, 500f, 700f, "Supply Truck")).With(crashes: 1, hazard: HazardKind.Rocks, hazardCount: 1));
    }

    // ───────────────────────────── CHAPTER 6 – DESERT EXPANSION ─────────────────────────────

    static void Chapter6(List<MissionDef> l)
    {
        l.Add(M("Sandstorm Run", "A sandstorm suddenly hits the desert highway.",
            "Follow the reflective markers through low visibility and get out of the storm zone.",
            "Out of the sandstorm!",
            Reach(500f, 750f, "Storm Exit")).With(area: MissionArea.Sand, env: MissionEnv.Sandstorm, crashes: 1));

        var dfh = M("Desert Fuel Hunt", "Desert fuel stations are very far apart.",
            "Follow the compass and reach the small station before the fuel runs out.",
            "Fuel found in the desert!",
            Reach(500f, 750f, "Fuel Station")).With(area: MissionArea.Sand, crashes: 1);
        dfh.startFuel = 30f; dfh.fuelDrainPerSecond = 0.2f;
        l.Add(dfh);

        l.Add(M("Desert Delivery", "A remote desert camp needs emergency supplies.",
            "Follow the off-road GPS marks and reach the camp in time.",
            "Supplies reached the camp!",
            Cps(4, true, 150f, 300f, "GPS Mark"),
            Reach(150f, 300f, "Desert Camp")).With(area: MissionArea.Sand, crashes: 1));

        l.Add(M("Drone War", "Security drones are running test patrols in the desert.",
            "Zig-zag out of the drone radar cones and reach the mountain base.",
            "Drones outsmarted!",
            Reach(500f, 700f, "Mountain Base")).With(area: MissionArea.Sand, crashes: 1, hazard: HazardKind.Spotlight, hazardCount: 3));

        l.Add(M("Desert Camp Rescue", "A friendly mechanic's truck is stuck in the desert.",
            "Find the truck, circle it to enable towing AI, then escort it to the nearest road.",
            "Mechanic rescued!",
            Reach(250f, 400f, "Stuck Truck"),
            Hold(1, 5f, 14f, 0f, 10f, 30f, "Tow Link", 8f),
            Tow(50f, 250f, 400f, "Nearest Road")).With(area: MissionArea.Sand, crashes: 1));

        l.Add(M("Highway Ramp Jump", "An under-construction ramp on the desert highway.",
            "Hit the ramp at 95 km/h or more and land perfectly in the marked lane.",
            "Cinematic jump!",
            Jump(95f)).With(area: MissionArea.Any, crashes: 1));

        l.Add(M("Burnout Test", "The gang wants to test your tyre grip.",
            "Keep drifting inside the burnout circle above 20 km/h until the meter is full.",
            "Burnout master!",
            Hold(1, 18f, 15f, 0f, 60f, 150f, "Burnout Circle", 20f)).With(area: MissionArea.Sand, crashes: 1));

        l.Add(M("Turbo Upgrade", "Install a turbo module at the desert outpost.",
            "Reach the outpost, clear the obstacle course and park in the upgrade bay.",
            "Turbo unlocked!",
            Reach(300f, 450f, "Outpost"),
            Hold(1, 2f, 3f, 4f, 40f, 80f, "Upgrade Bay")).With(area: MissionArea.Sand, crashes: 2, props: PropKind.Cones, propCount: 20));

        l.Add(M("Midnight Race", "Illegal midnight desert race.",
            "Complete the 2-lap race and finish in the top 3.",
            "Top 3 finish!",
            Race(4, MoverKind.Car, 2, 7, 100f, 3)).With(area: MissionArea.Sand, env: MissionEnv.Night, crashes: 2));

        l.Add(M("Off-Road Cliff", "A dangerous mountain road along the cliff edge.",
            "Drive slowly through the tight turns and reach the hill-top marker.",
            "Hill-top reached!",
            Cps(6, true, 100f, 180f, "Cliff Turn")).With(area: MissionArea.Grass, crashes: 2, speedLimit: 70f));
    }

    // ───────────────────────────── CHAPTER 7 – RE-ENTERING CITY ─────────────────────────────

    static void Chapter7(List<MissionDef> l)
    {
        l.Add(M("City Return", "Back from the desert, the city security is tight.",
            "Avoid the scan points and use the service roads to enter the city.",
            "You are back in the city!",
            Reach(500f, 700f, "City Entry")).With(crashes: 1, hazard: HazardKind.RedZone, hazardCount: 5));

        l.Add(M("Road Block Break", "A barricade blocks a main city entrance.",
            "Smash through the barricade at full speed, regain control and stay on the road.",
            "Barricade destroyed!",
            Reach(250f, 400f, "Through the Block")).With(crashes: 2, props: PropKind.Barricade, propCount: 1));

        l.Add(M("Silent Route", "Stealth drive in a residential area.",
            "Drive slowly through the narrow area, keep clear of pedestrians and parked cars.",
            "Nobody noticed you!",
            Reach(400f, 600f, "Exit Street")).With(crashes: 0, speedLimit: 35f, props: PropKind.Pedestrians, propCount: 10));

        l.Add(M("Hospital Run", "Emergency medical supplies for the hospital.",
            "Choose the safest fastest path and reach the hospital. The time limit is strict.",
            "Supplies delivered, lives saved!",
            Reach(600f, 850f, "Hospital")).With(crashes: 1, timeFactor: 0.75f));

        l.Add(M("AI Car Rescue", "A friendly AI car is glitching and braking randomly.",
            "Escort the glitching car along its route and avoid any collision.",
            "AI car repaired!",
            Escort(MoverKind.Car, 35f, 35f, 400f, 550f, "Glitched Car")).With(crashes: 0));

        l.Add(M("Tunnel Bridge Run", "A combined bridge and tunnel route.",
            "Keep your speed over the bridge, brake for the tunnel entry and cross without hitting walls.",
            "Bridge and tunnel cleared!",
            Cps(4, true, 150f, 250f)).With(crashes: 0, timeFactor: 0.8f));

        l.Add(M("Battery Plant Hack", "Get free charging from the city battery plant.",
            "Pass the 3 internal checkpoints, pause at each to hack, then exit through the gate.",
            "Battery plant hacked!",
            Hold(3, 3f, 6f, 15f, 60f, 140f, "Hack Point"),
            Reach(100f, 200f, "Exit Gate")).With(crashes: 1));

        l.Add(M("Police Drone Swarm", "Multiple drones patrol the city centre.",
            "Avoid the drone spotlight cones and complete 5 urban checkpoints.",
            "Swarm evaded!",
            Cps(5, false, 150f, 300f)).With(crashes: 1, hazard: HazardKind.Spotlight, hazardCount: 5));

        l.Add(M("Secret Base Entry", "The hacker's hidden base is inside the city.",
            "Take the right turns through the alley maze and reach the hidden entrance.",
            "Welcome to the base!",
            Reach(500f, 700f, "Hidden Entrance")).With(env: MissionEnv.Dusk, crashes: 1, timeFactor: 0.85f));

        l.Add(M("Save the Civilians", "A bus is stuck in a protest area.",
            "Escort the bus to the exit, avoiding police barriers.",
            "Civilians are safe!",
            Escort(MoverKind.Bus, 35f, 35f, 450f, 600f, "Bus")).With(crashes: 1, hazard: HazardKind.RedZone, hazardCount: 3));
    }

    // ───────────────────────────── CHAPTER 8 – WAR FOR THE CITY ─────────────────────────────

    static void Chapter8(List<MissionDef> l)
    {
        l.Add(M("Spy Cars Hunt", "CORE AI sent its spy cars.",
            "Chase the 3 spy cars and bump each one to disable it. Don't damage civilians.",
            "Spy cars disabled!",
            Ram(3, 1, MoverKind.Car, 55f, "Spy Car")).With(crashes: 3));

        l.Add(M("Bridge Collapse", "The enemy blew up the bridge.",
            "Drive across while sections fall, avoid the debris and reach the stable exit.",
            "You made it across!",
            Reach(400f, 550f, "Stable Exit")).With(crashes: 1, timeFactor: 0.8f, hazard: HazardKind.Rocks, hazardCount: 2));

        l.Add(M("EMP Trap", "EMP pylons are placed on the roads.",
            "Avoid the EMP circles and reach the objective. Staying inside disables your systems.",
            "EMP trap avoided!",
            Reach(500f, 700f, "Objective")).With(crashes: 1, hazard: HazardKind.EMP, hazardCount: 6, strikes: 3));

        l.Add(M("Helicopter Chase", "A helicopter is tracking you with its spotlight.",
            "Use underpasses and building cover to stay out of the spotlight and reach the escape zone.",
            "Helicopter lost you!",
            Reach(600f, 800f, "Escape Zone")).With(crashes: 1, hazard: HazardKind.Helicopter, hazardCount: 1, strikes: 4));

        l.Add(M("AI Tower Raid", "An AI control tower stands beside the highway.",
            "Pass the 4 road nodes above 60 km/h to inject the virus, then leave the site.",
            "Virus injected!",
            Cps(4, false, 80f, 160f, "Road Node", 60f),
            Reach(250f, 400f, "Leave Site")).With(crashes: 1));

        l.Add(M("Drone Factory Smash", "The drone factory routes run through the city.",
            "Brake hard at the 3 crate points to drop the crates and halt the factory.",
            "Factory halted!",
            Hold(3, 1.5f, 6f, 10f, 100f, 200f, "Crate Point")).With(crashes: 1, hazard: HazardKind.Spotlight, hazardCount: 2));

        l.Add(M("Protect the Bus", "Enemies are targeting the school bus.",
            "Escort the bus to school. Block or push away the attacker cars before they reach it.",
            "The kids are safe!",
            Escort(MoverKind.Bus, 40f, 45f, 550f, 750f, "School Bus", 2)).With(crashes: 4));

        l.Add(M("Airport Escape", "Blend into the airport road without being detected.",
            "Drive on the service road, avoid security vehicles and spotlights and reach the hangar.",
            "Hangar reached undetected!",
            Reach(600f, 800f, "Hangar")).With(crashes: 0, hazard: HazardKind.Spotlight, hazardCount: 3, props: PropKind.Convoy, propCount: 3));

        l.Add(M("Boss Truck Attack", "A big armored boss truck blocks your way.",
            "Damage the boss truck with 3 high-speed hits.",
            "Boss truck destroyed!",
            Ram(1, 3, MoverKind.Armored, 50f, "Boss Truck")).With(crashes: 4));

        l.Add(M("City Blackout", "The whole city is in blackout, only emergency lights.",
            "Use your headlights and reach the emergency generator building.",
            "Power restored!",
            Reach(500f, 700f, "Generator")).With(env: MissionEnv.Blackout, crashes: 1));
    }

    // ───────────────────────────── CHAPTER 9 – MOTHER CORE ─────────────────────────────

    static void Chapter9(List<MissionDef> l)
    {
        l.Add(M("Main Tower Hack", "Reach the CORE AI main tower.",
            "Complete 5 security loops with a short hack each, then park in front of the tower entrance.",
            "Tower defences down!",
            Hold(5, 2.5f, 7f, 15f, 100f, 200f, "Security Loop"),
            Hold(1, 2f, 3f, 4f, 40f, 80f, "Tower Entrance")).With(crashes: 1));

        l.Add(M("Core Van Chase", "A heavily protected van is carrying core data.",
            "Drive close beside the van until the hacking bar is full. Don't hit it.",
            "Core data hacked!",
            Pace(MoverKind.Armored, 55f, 6f, 1, "Core Van")).With(crashes: 0));

        l.Add(M("Rocket Car Attack", "Rocket-equipped enemy cars are locked on you.",
            "Zig-zag and change lanes to dodge the rockets and survive the attack.",
            "Rocket squad defeated!",
            Evade(3, MoverKind.Police, 95f, 75f)).With(crashes: 2, hazard: HazardKind.Missiles, hazardCount: 1, strikes: 3));

        l.Add(M("Final Highway Rage", "Full chaos on the highway, everyone is panicking.",
            "Finish the long highway stretch at speed with only the lane switches you need.",
            "Highway survived!",
            Reach(800f, 1100f, "Highway End")).With(crashes: 2, timeFactor: 0.85f, props: PropKind.TrafficJam, propCount: 20));

        l.Add(M("AI Rescue Team", "Three friendly AI cars are in shutdown mode.",
            "Stop beside each car to reactivate it, then gather at the safe spot.",
            "Rescue team assembled!",
            Hold(3, 3f, 6f, 10f, 200f, 380f, "Shutdown AI Car"),
            Reach(200f, 350f, "Safe Spot")).With(crashes: 1));

        l.Add(M("Firewall Attack", "CORE AI's firewall depends on physical nodes.",
            "Pass the 4 firewall nodes above 60 km/h so the injected code runs.",
            "Firewall breached!",
            Cps(4, false, 120f, 260f, "Firewall Node", 60f)).With(crashes: 1));

        l.Add(M("Tunnel Collapse", "CORE AI starts collapsing the tunnel.",
            "Sprint to the exit and dodge the falling rocks.",
            "Out before the collapse!",
            Reach(400f, 550f, "Tunnel Exit")).With(env: MissionEnv.Night, crashes: 1, timeFactor: 0.75f, hazard: HazardKind.Rocks, hazardCount: 3));

        l.Add(M("EMP Strike", "Reach the central EMP blast coordinator.",
            "Sync the 3 outer relay points, then rush to the central node.",
            "EMP strike ready!",
            Hold(3, 2f, 6f, 10f, 150f, 300f, "Relay Point"),
            Reach(200f, 350f, "Central Node")).With(crashes: 1, hazard: HazardKind.EMP, hazardCount: 4));

        l.Add(M("City Shutdown", "Shut down the whole city grid temporarily.",
            "Drive through 6 substations in order above 40 km/h, then stop in the neutral zone.",
            "City grid offline!",
            Cps(6, true, 120f, 220f, "Substation", 40f),
            Hold(1, 2f, 3f, 4f, 40f, 80f, "Neutral Zone")).With(crashes: 1));

        l.Add(M("Mother Core Reveal", "Mother Core AI fully detects you inside the tower.",
            "Complete a lap of the inner ring road while evading Mother Core's drones.",
            "Mother Core revealed!",
            Cps(4, true, 200f, 320f, "Ring Road"),
            Back("Lap Finish")).With(env: MissionEnv.Dusk, crashes: 1, hazard: HazardKind.Spotlight, hazardCount: 4));
    }

    // ───────────────────────────── CHAPTER 10 – THE FINAL DRIVE ─────────────────────────────

    static void Chapter10(List<MissionDef> l)
    {
        l.Add(M("Final Escape", "All city systems are active against you.",
            "Avoid the red danger zones along the long route and reach the safe hub.",
            "Safe hub reached!",
            Reach(800f, 1000f, "Safe Hub")).With(crashes: 1, hazard: HazardKind.RedZone, hazardCount: 8));

        l.Add(M("Helicopter Missiles", "The final helicopter fires heavy missiles.",
            "Dodge the incoming missiles with sharp turns and survive 5 waves (75 s).",
            "All missile waves survived!",
            Survive(75f, "Missile Waves")).With(crashes: 2, hazard: HazardKind.Missiles, hazardCount: 1, strikes: 3));

        l.Add(M("Monster Truck Raid", "CORE AI deployed heavy monster trucks.",
            "Don't stay in front of them. Use side lanes to confuse them and survive.",
            "Monster trucks wrecked!",
            Evade(3, MoverKind.Armored, 80f, 70f)).With(crashes: 2));

        l.Add(M("Exploding Bridge", "Timed explosives are set on the bridge segments.",
            "Time your speed so you cross each segment before it blows.",
            "Bridge crossed!",
            Reach(450f, 600f, "Bridge End")).With(crashes: 1, hazard: HazardKind.Rocks, hazardCount: 3));

        l.Add(M("Final Tunnel", "The last tunnel to Mother Core's heart.",
            "Keep perfect lane discipline through the tight turns and reach the end chamber.",
            "End chamber reached!",
            Cps(6, true, 100f, 180f, "Tunnel Turn")).With(env: MissionEnv.Night, crashes: 0, timeFactor: 0.85f));

        l.Add(M("Police Army", "The full police fleet tries to stop you.",
            "Find gaps in the blockade lines and break through to the far side.",
            "Blockade broken!",
            Evade(4, MoverKind.Police, 95f, 0f, 500f, 700f, "Beyond the Blockade")).With(crashes: 2, props: PropKind.Barricade, propCount: 2));

        l.Add(M("Hacker Override", "The hacker sends the final override code.",
            "Hold a constant speed between 60 and 90 km/h so the data transfer stays stable.",
            "Override uploaded!",
            Band(60f, 90f, 40f)).With(crashes: 1));

        l.Add(M("Shutdown Defense", "Mother Core tries to shut down your systems remotely.",
            "Avoid the glitch zones for 60 seconds. Touching one causes temporary control loss.",
            "Systems defended!",
            Survive(60f, "Defend Systems")).With(crashes: 2, hazard: HazardKind.Glitch, hazardCount: 1, strikes: 4));

        l.Add(M("Mother Core Boss", "An arena full of moving energy beams and turrets.",
            "Three rounds: strike the weak-point nodes while dodging the turret fire.",
            "Mother Core destroyed!",
            Hold(3, 1.5f, 6f, 0f, 100f, 200f, "Weak Point (Round 1)"),
            Hold(3, 1.5f, 6f, 0f, 100f, 200f, "Weak Point (Round 2)"),
            Hold(3, 1.5f, 6f, 0f, 100f, 200f, "Weak Point (Round 3)")).With(crashes: 2, hazard: HazardKind.Missiles, hazardCount: 1, strikes: 4));

        var last = M("The Last Drive", "The city is becoming free. One final choice: freedom or sacrifice.",
            "Choose your ending: drive to the open highway (Freedom) or back to the core chamber (Sacrifice).",
            "FREEDOM: You drive into the open world. The city is free.",
            Choice("Open Highway", "Core Chamber")).With(crashes: 2);
        last.altCompleteText = "SACRIFICE: You became the city's eternal guardian.";
        l.Add(last);
    }
}
