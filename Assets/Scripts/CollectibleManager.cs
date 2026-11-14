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
            if (other.gameObject.name == "P1")
                _scoreManager.P1Score++;
            else if (other.gameObject.name == "P2")
                _scoreManager.P2Score++;
            taken = true;
            Destroy(this.gameObject, 1.0f);
        }
    }
}
