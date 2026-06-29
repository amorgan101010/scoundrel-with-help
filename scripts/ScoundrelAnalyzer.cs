using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var solver = new ScoundrelSolver(maxStates: 800000);
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
        string logLine = $"{seed},{resultStatus},{solver.StatesChecked},{watch.ElapsedMilliseconds}\n";
        
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
        var deck = BuildDeck();
        var shuffledDeck = deck.OrderBy(_ => rng.Next()).ToList();
        GD.Print($"Generated deck for seed {seed}: {string.Join(", ", shuffledDeck.Select(c => c.Name))}");

        return deck.OrderBy(_ => rng.Next()).ToList();
    }

    private static readonly string[] Ranks =
        { "ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "jack", "queen", "king" };
    private static readonly string[] MonsterSuits = { "clubs", "spades" };
    private static readonly string[] RedRanks     = { "2", "3", "4", "5", "6", "7", "8", "9", "10" };
    private static readonly string[] RedSuits     = { "hearts", "diamonds" };

    private static int RankToInt(string rank) => rank switch
    {
        "ace"   => 1,
        "jack"  => 11,
        "queen" => 12,
        "king"  => 13,
        _       => int.Parse(rank),
    };

    private List<CardModel> BuildDeck()
    {
        var deck = new List<CardModel>();
        foreach (var suit in MonsterSuits)
            foreach (var rank in Ranks)
            {
                var s = suit == "clubs" ? Suit.Clubs : Suit.Spades;
                deck.Add(new CardModel(s, RankToInt(rank), $"{rank}_{suit}"));
            }
        foreach (var suit in RedSuits)
            foreach (var rank in RedRanks)
            {
                var s = suit == "hearts" ? Suit.Hearts : Suit.Diamonds;
                deck.Add(new CardModel(s, int.Parse(rank), $"{rank}_{suit}"));
            }
        return deck;
    }
}