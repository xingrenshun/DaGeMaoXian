using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CoinsManager : MonoBehaviour
{
    public static CoinsManager Instance;

    [HideInInspector]
    public int collectedCoins;
    public Text collectedCoinsText;

    private void Awake()
    {
        if (Instance != null)
            Destroy(gameObject);
        else
            Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        collectedCoins = PlayerPrefs.GetInt("CoinsAmount", 100);
        ShowAndSave();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            AddCoins(50000);
    }

    public void AddCoins(int amount)
    {
        collectedCoins += amount;
        ShowAndSave();
    }

    public void LessCoins(int amount)
    {
        collectedCoins -= amount;
        ShowAndSave();
    }

    public void ShowAndSave()
    {
        SetCoinsText(collectedCoins);
        PlayerPrefs.SetInt("CoinsAmount", collectedCoins);
    }

    public void SetCoinsText(int amount)
    {
        collectedCoinsText.text = amount.ToString();
    }
}
