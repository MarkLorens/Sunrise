using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Exorcised one time too many: the guards' prayers banish the spirit. Everything slows down, the spirit is
// dragged up into the sky spinning and shrinking, the screen whites out and the BANISHED card comes up.
// R plays again. Possession adds this to the spirit when it happens.
public class BanishCutscene : MonoBehaviour
{
    const float RiseTime = 2.2f;

    float age;
    float sparkleTimer;
    Vector3 from;
    Vector3 scale;

    void Start()
    {
        from = transform.position;
        scale = transform.localScale;
        Possession.InputBlocked = true;
        Sfx.Play(Sfx.Leave, 1f, 0.5f);
        Fx.Shake(0.4f);
        GameHud.Say("Exorcised one time too many...", "UI/cross");
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        Possession.InputBlocked = false;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        age += dt;
        Time.timeScale = age < RiseTime + 0.4f ? 0.35f : 0f;

        // Dragged up into the light, spinning and shrinking.
        float t = Mathf.Clamp01(age / RiseTime);
        transform.position = from + Vector3.up * (t * t * 7f);
        transform.localScale = scale * Mathf.Max(0.05f, 1f - t * 0.9f);
        transform.localRotation = Quaternion.Euler(0f, 0f, t * t * 900f);
        if (t < 1f && (sparkleTimer -= dt) <= 0f)
        {
            sparkleTimer = 0.05f;
            Fx.Burst("Fx/sparkle", transform.position, 4, Fx.Gold, 2f, 0.4f, 0.6f);
        }

        // White-out, then the card.
        float flashIn = Mathf.Clamp01((age - (RiseTime - 0.4f)) / 0.4f);
        float flashOut = Mathf.Clamp01((age - (RiseTime + 0.2f)) / 0.8f);
        float card = Mathf.Clamp01((age - RiseTime) / 0.5f);
        if (GameHud.Instance != null)
            GameHud.Instance.SetBanish(flashIn * (1f - flashOut * 0.85f), card, 1f + Mathf.Max(0f, RiseTime + 0.3f - age), age > RiseTime + 0.8f);

        var kb = Keyboard.current;
        if (age > RiseTime + 0.8f && kb != null && kb.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            Possession.InputBlocked = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
