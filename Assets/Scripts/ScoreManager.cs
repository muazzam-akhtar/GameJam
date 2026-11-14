using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int AngelScore;
    public int EvilScore;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        AngelScore = 0;
        EvilScore = 0;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
