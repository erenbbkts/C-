using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ConsoleGame
{
    class Program
    {
        static void Main(string[] args)
        {
            Game game = new Game();
            game.Start();
        }
    }

    class Logger
    {
        private static string logPath = "game_log.txt";
        
        public static void Initialize()
        {
            try
            {
                File.WriteAllText(logPath, "--- OYUN BASLADI ---\n");
                File.AppendAllText(logPath, $"Tarih: {DateTime.Now}\n");
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(logPath, message + "\n");
            }
            catch { }
        }
    }

    class Player
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Symbol { get; set; } = '@';

        public Player(int startX, int startY)
        {
            X = startX;
            Y = startY;
        }
    }

    class FallingItem
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Symbol { get; set; }
        public int ScoreValue { get; set; }
        
        public FallingItem(int x, int y, char symbol, int scoreValue)
        {
            X = x;
            Y = y;
            Symbol = symbol;
            ScoreValue = scoreValue;
        }
    }

    class Game
    {
        private int width = 50;
        private int height = 20;
        private Player player;
        private List<FallingItem> items;
        private int score = 0;
        private int targetScore = 50;
        private int maxTimeSeconds = 60;
        private bool isRunning;
        private Random random;
        private Stopwatch stopwatch;

        // Ekranda titremeyi (flicker) engellemek icin cift-tamponlama matrisleri
        private string[,] buffer;
        private string[,] oldBuffer;

        public Game()
        {
            // Oyuncu karakterini oyun alaninin altinda ortalayarak baslatiyoruz
            player = new Player(width / 2, height - 1);
            items = new List<FallingItem>();
            random = new Random();
            stopwatch = new Stopwatch();
            buffer = new string[width, height];
            oldBuffer = new string[width, height];
        }

        public void Start()
        {
            Logger.Initialize();
            
            Console.CursorVisible = false;
            try 
            { 
                Console.SetWindowSize(width + 1, height + 2); 
                Console.SetBufferSize(width + 1, height + 2); 
            } 
            catch { } // Bazi terminaller boyutlandirmayi desteklemezse yok say.
            
            Console.Clear();
            
            isRunning = true;
            stopwatch.Start();

            // Objelerin dusme zamanini yonetecek sayac
            int fallTimer = 0; 

            while (isRunning)
            {
                HandleInput();
                
                // Oyuncu girdisi icin bekleme saniyesini dusuk tutarak akiciligi sagliyoruz.
                // Objeler ise yalnizca belirli araliklarla (200ms) bir satir duser.
                fallTimer += 50;
                if (fallTimer >= 200) 
                {
                    UpdateItems();
                    fallTimer = 0;
                }

                CheckCollisions();
                Render();
                CheckGameConditions();
                
                Thread.Sleep(50); // Frame Delay (FPS ayari gibi dusunulebilir)
            }
            
            Console.Clear();
            Console.WriteLine("============= OYUN BITTI =============");
            Console.WriteLine($"Skorunuz: {score}");
            Console.WriteLine($"Hedef Skor: {targetScore}");
            if (score >= targetScore)
            {
                Console.WriteLine("TEBRIKLER! HEDEF SKORA ULASTINIZ.");
            }
            else
            {
                Console.WriteLine("SURE DOLDU! KAZANAMADINIZ.");
            }
            Console.WriteLine("\nCikmak icin bir tusa basin...");
            Console.WriteLine($"Loglar '{Path.GetFullPath("game_log.txt")}' dosyasina kaydedildi.");
            Console.ReadKey();
            Console.CursorVisible = true;
        }
        
        private void HandleInput()
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                int oldX = player.X;
                int oldY = player.Y;

                // Yon tuslari ile sinirlara carpana kadar hareket hakki veriyoruz
                if (key == ConsoleKey.LeftArrow && player.X > 0) player.X--;
                else if (key == ConsoleKey.RightArrow && player.X < width - 1) player.X++;
                else if (key == ConsoleKey.UpArrow && player.Y > 0) player.Y--;
                else if (key == ConsoleKey.DownArrow && player.Y < height - 1) player.Y++; 
                
                // Oyuncu konumu degistiyse loglama yapiyoruz
                if (oldX != player.X || oldY != player.Y) {
                    Logger.Log($"INPUT → key={key} playerX={player.X} playerY={player.Y}");
                }
            }
        }

        private void UpdateItems()
        {
            // Yeni obje olusturma (her dusus adiminda belli bir ihtimalle rastgele alanda)
            if (random.Next(0, 100) < 40) // %40 Olasilikla yeni obje firsatlarini dene
            {
                int startX = random.Next(0, width);
                char symbol = random.Next(0, 2) == 0 ? '*' : 'O';
                // * sembolu 5 puan, O sembolu 10 puan
                int val = symbol == '*' ? 5 : 10; 
                
                items.Add(new FallingItem(startX, 0, symbol, val));
                Logger.Log($"UPDATE → itemSpawned x={startX} y=0");
            }

            // Mevcut objeleri asagi indirme
            for (int i = items.Count - 1; i >= 0; i--)
            {
                items[i].Y++;
                Logger.Log($"UPDATE → itemMoved x={items[i].X} y={items[i].Y}");
                
                // Obje ekranin en altina ulasirsa listeden cikartarak siliyoruz
                if (items[i].Y >= height)
                {
                    items.RemoveAt(i); 
                }
            }
        }

        private void CheckCollisions()
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                // Carpisma durumunda koordinatlar tamamen eslesir
                if (items[i].X == player.X && items[i].Y == player.Y)
                {
                    score += items[i].ScoreValue;
                    Logger.Log($"COLLISION → score={items[i].ScoreValue} newTotal={score}");
                    items.RemoveAt(i);
                }
            }
        }

        private void CheckGameConditions()
        {
            bool timeUp = stopwatch.Elapsed.TotalSeconds >= maxTimeSeconds;
            bool scoreReached = score >= targetScore;

            if (timeUp || scoreReached)
            {
                isRunning = false;
                string reason = timeUp ? "Zaman doldu" : "Hedef skora ulasildi";
                Logger.Log($"GAME OVER → reason=\"{reason}\" finalScore={score}");
            }
        }

        private void Render()
        {
            // Bufferi temizleme (bosluklarla doldur)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    buffer[x, y] = " ";
                }
            }

            // Sirasiyla objeleri buffer'a yazdiriyoruz
            foreach (var item in items)
            {
                if (item.X >= 0 && item.X < width && item.Y >= 0 && item.Y < height)
                {
                    buffer[item.X, item.Y] = item.Symbol.ToString();
                }
            }

            // Oyuncuyu en uste kalmasi icin en son ciziyoruz
            if (player.X >= 0 && player.X < width && player.Y >= 0 && player.Y < height)
            {
                buffer[player.X, player.Y] = player.Symbol.ToString();
            }

            // UI metnini ekrana anlik olarak en ust satirdan (Y=0) veriyoruz
            Console.SetCursorPosition(0, 0);
            int remainingSeconds = maxTimeSeconds - (int)stopwatch.Elapsed.TotalSeconds;
            string uiText = $"Skor: {score}/{targetScore}  |  Kalan Sure: {remainingSeconds}s".PadRight(width);
            Console.Write(uiText);

            // Frame'i console ekranina sadece degisen yerleri tarayarak optimize cizim yapiyoruz.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (buffer[x, y] != oldBuffer[x, y])
                    {
                        // +1 ekleniyor zira gercek konsol ekraninin y=0 noktasi yukaridaki Score/Sure yazisidir
                        Console.SetCursorPosition(x, y + 1); 
                        Console.Write(buffer[x, y]);
                        oldBuffer[x, y] = buffer[x, y];
                    }
                }
            }
        }
    }
}









