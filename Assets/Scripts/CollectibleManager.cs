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

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (taken == false && other.CompareTag("Player"))
        {
            if (other.gameObject.name == "Angel")
                _scoreManager.AngelScore++;
            else if (other.gameObject.name == "Evil")
                _scoreManager.EvilScore++;
            taken = true;
            Destroy(this.gameObject, 1.0f);
        }
    }
}
