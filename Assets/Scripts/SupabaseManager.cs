using Supabase;
using UnityEngine;
using Postgrest.Attributes;
using Postgrest.Models;
using System;

/*
 * SUPABASE MANAGER (SINGLETON)
 * * WHAT IT DOES:
 * - Creates a single, persistent connection to your Supabase database.
 * - Can be accessed from any other script using "SupabaseManager.Instance".
 *
 * HOW TO USE IN UNITY:
 * 1. Create an empty GameObject in your scene named "SupabaseManager".
 * 2. Attach this script to it.
 * 3. Fill in your URL and Key in the Inspector.
 */
public class SupabaseManager : MonoBehaviour
{
    // Make it a singleton so you can access it from anywhere
    public static Supabase.Client Instance { get; private set; }

    [Header("Supabase Project")]
    [SerializeField]
    private string supabaseUrl = "https://tzeudbyojqpzfkbbwwcs.supabase.co";

    [SerializeField]
    private string supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InR6ZXVkYnlvanFwemZrYmJ3d2NzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzQ0NzcwNDgsImV4cCI6MjA4OTgzNzA0OH0.iFwDnWKaYD31cHu06egAzL8YMoG4EIanPrHXYh6gqlI";

    void Awake()
    {
        // --- Singleton Pattern ---
        if (Instance == null)
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = true // Good for chat
            };
            Instance = new Supabase.Client(supabaseUrl, supabaseAnonKey, options);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        // CRITICAL FOR REALTIME: The client must be initialized to open the WebSocket connection.
        if (Instance != null)
        {
            await Instance.InitializeAsync();
            Debug.Log("Supabase Client Initialized & Realtime Connected.");
        }
    }
}

// ==========================================
// DATABASE MODELS
// ==========================================