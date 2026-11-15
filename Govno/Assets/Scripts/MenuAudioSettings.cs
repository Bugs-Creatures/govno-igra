using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuAudioSettings : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float startVolume = 0.85f;

    public float MusicVolume => masterVolume * musicVolume;
    public float StartVolume => masterVolume * startVolume;
}
