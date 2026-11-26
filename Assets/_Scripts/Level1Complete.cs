using UnityEngine;

/// <summary>
/// Level 1 (tutorial): use the gas and the brake for a moment each → level complete.
/// Counts the phone pedal buttons AND the VR controller triggers (in VR the on-screen pedals are hidden,
/// so before this the level could never be finished on Quest).
/// </summary>
public class Level1Complete : MonoBehaviour
{
    public RCC_UIController gasButton;
    public RCC_UIController brakeButton;

    [Tooltip("Frames the gas / brake must be held (in total) before the level completes.")]
    public int requiredFrames = 100;

    public int Race_Press_Count;
    public int Brake_Press_Count;
    public bool LevelComplete;

    void Update()
    {
        if (LevelComplete) return;
        // Only while driving (not behind the objective / pause panels).
        if (OverAllGameMannager.OA_GM == null || !OverAllGameMannager.OA_GM.GameStart) return;

        bool gas = (gasButton != null && gasButton.pressing) || VRDriveInput.Throttle > 0.5f;
        bool brake = (brakeButton != null && brakeButton.pressing) || VRDriveInput.Brake > 0.5f;
        if (gas) Race_Press_Count++;
        if (brake) Brake_Press_Count++;

        if (Brake_Press_Count > requiredFrames && Race_Press_Count > requiredFrames)
        {
            LevelComplete = true;
            CanvasMannager.Instance.GameCompleteFun();
        }
    }
}
