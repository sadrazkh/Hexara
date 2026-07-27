using Hexara.Domain.Common;

namespace Hexara.Domain.Tests.Common;

public class RngTests
{
    [Fact]
    public void Same_seed_gives_the_same_sequence()
    {
        var a = new Rng(42);
        var b = new Rng(42);

        Assert.Equal(
            Enumerable.Range(0, 50).Select(_ => a.NextUInt64()),
            Enumerable.Range(0, 50).Select(_ => b.NextUInt64()));
    }

    [Fact]
    public void Neighbouring_seeds_diverge_immediately()
    {
        Assert.NotEqual(new Rng(1).NextUInt64(), new Rng(2).NextUInt64());
    }

    [Fact]
    public void Seed_zero_does_not_collapse()
    {
        var rng = new Rng(0);
        var values = Enumerable.Range(0, 20).Select(_ => rng.NextUInt64()).ToHashSet();

        Assert.True(values.Count > 1);
    }

    /// <summary>ادامه‌ی بازی بعد از بارگذاری از دیتابیس باید دقیقاً همان دنباله را بدهد.</summary>
    [Fact]
    public void Restoring_state_continues_the_same_sequence()
    {
        var original = new Rng(7);
        for (var i = 0; i < 10; i++)
        {
            original.NextUInt64();
        }

        var restored = Rng.FromState(original.State);

        Assert.Equal(original.NextUInt64(), restored.NextUInt64());
    }

    [Fact]
    public void Next_stays_inside_the_range()
    {
        var rng = new Rng(3);
        for (var i = 0; i < 1000; i++)
        {
            var value = rng.Next(5);
            Assert.InRange(value, 0, 4);
        }
    }

    [Fact]
    public void Dice_cover_all_six_faces()
    {
        var rng = new Rng(11);
        var faces = Enumerable.Range(0, 500).Select(_ => rng.RollDie()).ToHashSet();

        Assert.Equal([1, 2, 3, 4, 5, 6], faces.Order());
    }

    [Fact]
    public void Shuffle_keeps_every_item()
    {
        var items = Enumerable.Range(0, 30).ToList();
        new Rng(9).Shuffle(items);

        Assert.Equal(Enumerable.Range(0, 30), items.Order());
        Assert.NotEqual(Enumerable.Range(0, 30), items);
    }
}
