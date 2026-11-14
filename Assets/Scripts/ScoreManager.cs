using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int P1Score;
    public int P2Score;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        P1Score = 0;
        P2Score = 0;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
