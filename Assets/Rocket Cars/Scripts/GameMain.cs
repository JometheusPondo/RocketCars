using UnityEngine;

public static class GameMain
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Main()
    {
        SimpleAudioPlayer audioPlayer = Resources.Load<SimpleAudioPlayer>(nameof(SimpleAudioPlayer));
        Object.Instantiate(audioPlayer.gameObject);
    }
}
