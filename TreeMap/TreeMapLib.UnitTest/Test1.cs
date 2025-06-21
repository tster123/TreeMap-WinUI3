namespace TreeMapLib.UnitTest
{
    public class UnitTestTreeMapInput(double size, object item, string label, ITreeMapInput[] children) : TreeMapInput(size, item, label, children)
    {
        public override string GetInfo(string column)
        {
            throw new ArgumentException("not used");
        }
    }

    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void TestMethod1()
        {
            TreeMapPlacer placer = new();
            var files = new DirectoryInfo("C:\\Users\\thboo\\OneDrive").GetFiles("*", SearchOption.AllDirectories).Where(i => i.Length > 100).ToList();
            var placements = placer.GetPlacements(files.Select(f => new UnitTestTreeMapInput(f.Length, f, f.FullName, [])), 1200, 700).ToList();
            Assert.IsTrue(placements.Count == files.Count);
        }
    }
}
