using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyMusicMannager : MonoBehaviour
{
    public static MyMusicMannager OA_MM;
    
    public AudioSource MyAudioSource;
    public AudioSource MyMusicAudioSource;
    public AudioClip[] CheckPointSound;
    public AudioClip MainMenuMusicSound,GamePlayMusicSound;
    public AudioClip AlaramClipSound;
    // Start is called before the first frame update
    private void Awake()
    {
        if (OA_MM == null)
        {
            OA_MM = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
       
       
        AudioListener.volume =OverAllGameMannager.OA_GM.SaveData.SoundVolume;
        MyAudioSource.volume = OverAllGameMannager.OA_GM.SaveData.SoundVolume;
        MyMusicAudioSource.volume = OverAllGameMannager.OA_GM.SaveData.MusicVolume;
        MyMusicMannager.OA_MM.MainMenuMusicPlayFun();
    }
    public void CheckPointSoundFunction()
    {
        MyAudioSource.PlayOneShot(CheckPointSound[2]);
    }

    public void FinalPointSoundFunction()
    {
        MyAudioSource.PlayOneShot(CheckPointSound[1]);
    }

    public void ClickSoundBtnFunction()
    {
        MyAudioSource.PlayOneShot(CheckPointSound[0]);
    }

    public void MainMenuMusicPlayFun()
    {
        if (MyMusicAudioSource.clip == MainMenuMusicSound &&
       MyMusicAudioSource.isPlaying)
            return;

        MyMusicAudioSource.clip = MainMenuMusicSound;
        MyMusicAudioSource.Play();
    }

    public void GameMusicPlayFun()
    {
        if (MyMusicAudioSource.clip == GamePlayMusicSound &&
        MyMusicAudioSource.isPlaying)
            return;

        MyMusicAudioSource.clip = GamePlayMusicSound;
        MyMusicAudioSource.Play();
    }
    public void AlaramSoundFun()
    {
        MyAudioSource.clip = AlaramClipSound;
        MyAudioSource.Play();
    }
    public void AlaramSoundStopFun()
    {
        MyAudioSource.Stop();
    }


}
