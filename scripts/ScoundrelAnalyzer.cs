using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;

public class ScoundrelAnalyzer
{
    // Path where the CSV will be saved (e.g., in your project folder or user data)
    private readonly string _csvPath = "scoundrel_analysis.csv";

    public ScoundrelAnalyzer()
    {
        // Create the file and header if it doesn't exist
        if (!File.Exists(_csvPath))
        {
            File.WriteAllText(_csvPath, "Seed,Result,StatesChecked,TimeTakenMs\n");
        }
    }

    /// <summary>
    /// Runs a batch of random seeds on a background thread.
    /// </summary>
    public async Task RunBatchAsync(int numberOfGames)
    {
        var random = new Random();

        // Run on a background thread so Godot's UI doesn't freeze
        await Task.Run(() =>
        {
            for (int i = 0; i < numberOfGames; i++)
            {
                // Generate a random seed for this run
                int seed = random.Next();
    	        GD.Print($"Analyzing seed {seed} ({i + 1}/{numberOfGames})...");
                AnalyzeSeed(seed);
            }
        });
    }

    private void AnalyzeSeed(int seed)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        
        // 1. Generate the deck using the seed
        List<CardModel> deck = GenerateDeck(seed);
        var engine = new GameEngine(deck);
        
        // 2. Setup the solver with a safety cap (e.g., 200k states)
        var solver = new ScoundrelSolver(maxStates: 200000);
        string resultStatus;

        try
        {
            bool isSolvable = solver.IsSolvable(engine);
            resultStatus = isSolvable ? "Solvable" : "Unsolvable";
        }
        catch (Exception)
        {
            resultStatus = "Timeout";
        }

        watch.Stop();

        // 3. Append to CSV
        string logLine = $"{seed},{resultStatus},?,{watch.ElapsedMilliseconds}\n";
        
        // Use lock to ensure thread safety if you decide to run parallel tasks later
        lock (_csvPath) 
        {
            File.AppendAllText(_csvPath, logLine);
        }
    }

    private List<CardModel> GenerateDeck(int seed)
    {
        // Initialize a seeded RNG
        var rng = new Random(seed);
        var deck = new List<CardModel>();

        // (Assuming you have a standard way to build your 40 cards)
        // You would generate all 40 cards here, then shuffle them using the rng
        // Example Shuffle:
        // int n = deck.Count;  
        // while (n > 1) {  
        //     n--;  
        //     int k = rng.Next(n + 1);  
        //     CardModel value = deck[k];  
        //     deck[k] = deck[n];  
        //     deck[n] = value;  
        // }  

        return deck;
    }
}