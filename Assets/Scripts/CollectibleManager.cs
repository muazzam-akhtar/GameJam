using UnityEngine;

public class CollectibleManager : MonoBehaviour
{
    public ScoreManager _scoreManager;
    private bool        taken;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (_scoreManager == null)
            _scoreManager = FindFirstObjectByType<ScoreManager>();
        taken = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (taken == false && other.CompareTag("Player"))
        {
            if (other.gameObject.name == "Angel")
                _scoreManager.AngelScore++;
            else if (other.gameObject.name == "Evil")
                _scoreManager.EvilScore++;
            // power-up look on the collector (Angel: jump + twirl, Evil: flames + glow)
            var actions = other.GetComponentInParent<ICharacterActions>();
            if (actions != null) actions.PowerUp();
            taken = true;
            Destroy(this.gameObject, 1.0f);
        }
    }
}
