namespace RogueBlockBlast.Core
{
    public sealed class RunModel
    {
        public int Score { get; set; }

        public void Reset() => Score = 0;

        public void AddScore(int amount)
        {
            if (amount <= 0) return;
            Score += amount;
        }
    }
}