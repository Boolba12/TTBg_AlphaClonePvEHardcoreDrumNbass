using System;

public interface IBattleRandomSource
{
    float Next01();
}

public sealed class SeededBattleRandomSource : IBattleRandomSource
{
    private readonly Random random;

    public SeededBattleRandomSource(int battleSeed)
    {
        random = new Random(battleSeed);
    }

    public float Next01() => (float)random.NextDouble();
}
