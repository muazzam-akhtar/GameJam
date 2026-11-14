using UnityEngine;
using TMPro;

public class CountdownTimer : MonoBehaviour
{
    public TMP_Text timerText;
    public float totalTime = 90f;
    private float timeRemaining;

    void Start()
    {
        timeRemaining = totalTime;
    }

    void Update()
    {
        timeRemaining = Mathf.Max(0, timeRemaining - Time.deltaTime);
        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }
}