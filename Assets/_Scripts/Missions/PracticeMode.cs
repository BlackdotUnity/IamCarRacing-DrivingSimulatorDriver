/// <summary>
/// Practice = free drive in the GamePlay city: no objective, no timer, no crash fail,
/// fuel never runs out and nothing is saved from the session.
/// Set from the chapter screen, cleared when a real level is chosen.
/// </summary>
public static class PracticeMode
{
    public static bool Active;

    /// <summary>Street start point outside the garage (Chapter 1 / level 3 start).</summary>
    public const int StartLevelSlot = 2;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active = false;
}
