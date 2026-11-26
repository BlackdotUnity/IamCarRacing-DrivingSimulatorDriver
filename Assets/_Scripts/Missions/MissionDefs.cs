using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Data model for the 100-level campaign.
//  A mission is a list of generic steps (reach, checkpoints, follow, evade...)
//  plus mission-wide modifiers (environment, hazards, props, rules).
//  MissionRunner turns this data into a playable level at runtime, using the
//  baked NavMesh of the GamePlay city to place markers and routes.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Which ground type mission points should be placed on.</summary>
public enum MissionArea { Road, Sand, Grass, Any }

/// <summary>Lighting / weather preset applied when the mission starts.</summary>
public enum MissionEnv { Day, Dusk, Night, Storm, Sandstorm, Blackout }

public enum StepKind
{
    Reach,          // drive to one marker
    Checkpoints,    // several markers, ordered or any order
    PickupDeliver,  // one pickup, then `count` drop points
    Collect,        // `count` items scattered around
    Hold,           // reach zone(s) and stay inside, slow, for `hold` seconds (hack / park / refuel)
    Follow,         // follow a moving vehicle keeping distance inside [near, far]
    Pace,           // drive side by side with a moving vehicle until the sync bar is full
    Escort,         // a vehicle drives to a destination, stay close to it
    Tow,            // a broken vehicle follows you, reach the garage slowly
    Evade,          // pursuers chase you: survive `duration` or reach the safe zone
    Race,           // lap race against opponents on a checkpoint loop
    SpeedTrial,     // reach `threshold` km/h before the end of the strip
    SpeedBand,      // keep speed inside [min, max] for `duration` seconds
    Survive,        // survive `duration` seconds (hazards do the work)
    Ram,            // hit `count` moving targets `hits` times each
    Jump,           // ramp jump with minimum speed, land in the zone
    Choice          // two endings, reaching either completes the mission
}

public enum MoverKind { Car, Taxi, Van, Truck, Bus, Police, Train, Armored }

public enum HazardKind { None, RedZone, Spotlight, Helicopter, EMP, Glitch, Rocks, Missiles }

public enum PropKind { None, ParkedCars, Cones, Pedestrians, TrafficJam, Convoy, Barricade }

[Serializable]
public class MissionStep
{
    public StepKind kind = StepKind.Reach;
    [Tooltip("Number of markers / items / targets / pursuers / opponents depending on the step kind.")]
    public int count = 1;
    [Tooltip("Checkpoints / collectables must be taken in order.")]
    public bool ordered = true;
    [Tooltip("Reach: go back to where the mission started.")]
    public bool toStart;

    [Header("Distances (m)")]
    public float minDist = 120f;
    public float maxDist = 320f;
    public float radius = 7f;

    [Header("Hold zones")]
    public float hold = 3f;
    [Tooltip("Hold only counts below this speed (0 = any speed).")]
    public float holdMaxSpeedKmh = 25f;

    [Header("Vehicles")]
    public MoverKind mover = MoverKind.Car;
    public float moverSpeedKmh = 45f;
    public float nearDist = 6f;
    public float farDist = 45f;
    [Tooltip("Ram: hits needed per target. Race: laps.")]
    public int hits = 1;

    [Header("Timing / speed")]
    public float duration = 60f;
    public float minKmh;
    public float maxKmh;
    [Tooltip("Race: worst finishing position that still passes.")]
    public int requiredRank = 1;

    [Header("Texts")]
    public string label = "";
    public string altLabel = "";
}

[Serializable]
public class MissionDef
{
    public int chapter;
    public int level;
    public string title = "";
    [TextArea(2, 4)] public string story = "";
    [TextArea(2, 4)] public string objective = "";
    [TextArea(1, 3)] public string completeText = "";
    [TextArea(1, 3)] public string altCompleteText = "";

    [Tooltip("Chapter 1 levels are hand-built in the GamePlay scene; the runner leaves them alone.")]
    public bool handBuilt;

    [Header("Setting")]
    public MissionArea area = MissionArea.Road;
    public MissionEnv env = MissionEnv.Day;

    [Header("Rules")]
    [Tooltip("Crashes into obstacles allowed before failing (0 = first crash fails).")]
    public int maxCrashes = 1;
    [Tooltip("Spotted / hit by hazards this many times = fail.")]
    public int maxStrikes = 3;
    [Tooltip("0 = no limit. Driving above it for 2 s fails.")]
    public float speedLimitKmh;
    public bool noReverse;
    [Tooltip("Multiplies the automatically computed time limit.")]
    public float timeFactor = 1f;
    [Tooltip("0 = automatic. Otherwise the fixed time limit in seconds.")]
    public float fixedTime;
    public int reward = 2500;

    [Header("Fuel")]
    [Tooltip("0 = keep the car's saved fuel. Otherwise the fuel % the mission starts with.")]
    public float startFuel;
    [Tooltip("Extra fuel % drained per second (fuel-rescue missions).")]
    public float fuelDrainPerSecond;

    [Header("World")]
    public HazardKind hazard = HazardKind.None;
    public int hazardCount;
    public PropKind props = PropKind.None;
    public int propCount;

    public List<MissionStep> steps = new List<MissionStep>();

    public int GlobalNumber => chapter * 10 + level + 1;
}

/// <summary>Chapter names, shared by the menu and the gameplay HUD.</summary>
public static class MissionChapters
{
    public const int ChapterCount = 5; // levels 1-50 (chapters 6-10 were removed)
    public const int LevelsPerChapter = 10;

    public static readonly string[] Names =
    {
        "AWAKENING",
        "THE CITY NOTICES",
        "HACKER ALLIANCE",
        "POLICE STRIKE",
        "UNDERGROUND ROADS",
        "DESERT EXPANSION",
        "RE-ENTERING CITY",
        "WAR FOR THE CITY",
        "MOTHER CORE",
        "THE FINAL DRIVE",
    };

    public static string Name(int chapter) =>
        chapter >= 0 && chapter < Names.Length ? Names[chapter] : "CHAPTER " + (chapter + 1);
}
