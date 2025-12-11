using UnityEngine;

public class SimpleAudioPlayer : MonoBehaviour
{
  [SerializeField] private AudioSource  _audioSource;
  public AudioSource                    AudioSource => _audioSource;
  public static SimpleAudioPlayer       Instance { get; private set; }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void Main()
  {
    SimpleAudioPlayer audioPlayer = Resources.Load<SimpleAudioPlayer>(nameof(SimpleAudioPlayer));
    Object.Instantiate(audioPlayer.gameObject);
  }

  private void Awake()
  {
    Instance = this;
    DontDestroyOnLoad(gameObject);
  }
}

