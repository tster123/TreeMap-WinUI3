using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace TreeMapLib.Models.FileSystem;


public class FileSystemModel : IViewableModel
{
    public const string COL_FileCount = "FileCount", COL_LastModified = "Last Modified";

    public List<FileInfo> Files { get; set; }

    public FileSystemModel(string path)
    {
        Files = new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories).Where(i => i.Length > 100000).ToList();
    }
    

    public ITreeMapInput[] GetTreeMapInputs()
    {
        Folder root = new Folder("", "<root>");
        foreach (var file in Files)
        {
            string[] parts = file.FullName.Split(Path.DirectorySeparatorChar);
            Folder current = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string dirName = parts[i];
                if (!current.Children.TryGetValue(dirName, out Folder? next))
                {
                    next = new Folder(current.FullName, parts[i]);
                    current.Children[dirName] = next;
                }
                current = next;
            }

            current.Files.Add(file);
        }

        Folder baseDir = root;
        while (baseDir.Files.Count == 0 && baseDir.Children.Count == 1)
        {
            baseDir = baseDir.Children.First().Value;
        }

        return baseDir.GetTreeMapInput().Children;

    }

    public RenderMode[] RenderModes { get; } =
    [
        new("Extension", new ExtensionColoring()),
        new("File Age", new AgeColoring()),
        new("Extension + Age", new ExtensionAndAgeColoring())
    ];
    public string GetHoverText(object item) => ((FileSystemNode)item).HoverText;
    public string[] InfoColumns => [COL_FileCount, COL_LastModified];
}

public class FileSystemNode(string folderName, string name, long size, FileInfo? fileInfo = null, DirectoryInfo? dirInfo = null)
{
    public string FolderName { get; } = folderName;
    public string Name { get; } = name;
    public readonly string FullName = Path.Combine(folderName, name);
    public long Size { get; } = size;
    public FileInfo? FileInfo { get; } = fileInfo;
    public DirectoryInfo? DirInfo { get; } = dirInfo;

    public override string ToString() => FileInfo?.FullName ?? Path.Combine(FolderName, Name);
    public string HoverText => ToString() + " - " + Size.ToString("N0");
}

public class FileTreeMapInput(double size, FileSystemNode item, string label, ITreeMapInput[] children)
    : TreeMapInput(size, item, label, children)
{
    private readonly string lastModified = item.FileInfo?.LastWriteTime.ToString("s") ?? "";


    public override string GetInfo(string column)
    {
        switch (column)
        {
            case FileSystemModel.COL_FileCount:
                return "1";
            case FileSystemModel.COL_LastModified:
                return lastModified;
            default:
                throw new ArgumentException("Unknown column", nameof(column));
        }
    }
}

public class FolderTreeMapInput(double size, FileSystemNode item, string label, ITreeMapInput[] children, int fileCount)
    : TreeMapInput(size, item, label, children)
{
    private string fileCount = fileCount.ToString("N0"), lastModifed = item.DirInfo?.LastWriteTime.ToString("s") ?? "";


    public override string GetInfo(string column)
    {
        switch (column)
        {
            case FileSystemModel.COL_FileCount:
                return fileCount;
            case FileSystemModel.COL_LastModified:
                return lastModifed;
            default:
                throw new ArgumentException("Unknown column", nameof(column));
        }
    }
}

public class Folder(string parentName, string name)
{
    public readonly string ParentName = parentName;
    public readonly string Name = name;
    public readonly string FullName = Path.Combine(parentName, name);
    public readonly Dictionary<string, Folder> Children = new();
    public readonly List<FileInfo> Files = new();
    private long? _size;

    public long Size
    {
        get
        {
            if (_size == null)
            {
                long s = Files.Sum(f => f.Length);
                s += Children.Values.Sum(d => d.Size);
                _size = s;
            }

            return _size.Value;
        }
    }

    private int? _fileCount;

    public int FileCount
    {
        get
        {
            if (_fileCount == null)
            {
                int fc = Files.Count;
                foreach (var c in Children.Values)
                {
                    fc += c.FileCount;
                }

                _fileCount = fc;
            }
            return _fileCount.Value;
        }
    }

    public TreeMapInput GetTreeMapInput()
    {
        List<ITreeMapInput> children = new();
        if (Files.Count > 0)
        {
            long filesSize = Files.Sum(f => f.Length);
            List<ITreeMapInput> fileChildren = new();
            foreach (FileInfo file in Files)
            {
                var fileNode = new FileSystemNode(FullName, file.Name, file.Length, file);
                fileChildren.Add(new FileTreeMapInput(file.Length, fileNode, fileNode.FullName, []));
            }

            var filesNode = new FileSystemNode(FullName, "<files>", filesSize);
            children.Add(new FolderTreeMapInput(filesSize, filesNode, filesNode.FullName, fileChildren.ToArray(), fileChildren.Count));
        }

        foreach (var child in Children)
        {
            children.Add(child.Value.GetTreeMapInput());
        }

        var containerNode = new FileSystemNode(ParentName, Name, Size, dirInfo: new DirectoryInfo(FullName));
        return new FolderTreeMapInput(Size, containerNode, containerNode.FullName, children.ToArray(), FileCount);
    }
}

public class ExtensionColoring : TypedColorer<FileSystemNode>
{
    public override bool UsesFlavors => true;
    public override bool UsesStrength => false;

    public override string GetFlavorTyped(FileSystemNode file)
    {
        return file.FileInfo?.Extension.ToLower() ?? "";
    }

    public override double GetColorStrengthTyped(FileSystemNode file)
    {
        return 1;
    }
}

public class AgeColoring : TypedColorer<FileSystemNode>
{
    public override bool UsesFlavors => false;
    public override bool UsesStrength => true;

    public override string GetFlavorTyped(FileSystemNode file)
    {
        return "";
    }

    public override double GetColorStrengthTyped(FileSystemNode file)
    {
        if (file.FileInfo == null) return 1;
        TimeSpan fromStart = GetDate(file.FileInfo).Subtract(minAge);
        return fromStart.Ticks / tickSpan;
    }

    private DateTime minAge = DateTime.MaxValue, maxAge = DateTime.MinValue;
    private double tickSpan;
    public override void InitializeTyped(IEnumerable<FileSystemNode> allItems)
    {
        foreach (FileSystemNode item in allItems)
        {
            if (item.FileInfo == null) continue;
            DateTime date = GetDate(item.FileInfo);
            if (minAge > date) minAge = date;
            if (maxAge < date) maxAge = date;
        }
        tickSpan = (maxAge - minAge).Ticks;
    }

    private static DateTime GetDate(FileInfo file)
    {
        if (file.CreationTime.Year < 1990) return file.LastWriteTime;
        if (file.LastWriteTime.Year < 1990) return file.CreationTime;
        if (file.LastWriteTime < file.CreationTime) return file.LastWriteTime;
        return file.CreationTime;
    }
}

public class ExtensionAndAgeColoring : AgeColoring
{
    public override bool UsesFlavors => true;
    public override string GetFlavorTyped(FileSystemNode file)
    {
        Debug.Assert(file.FileInfo != null);
        if (file.FileInfo == null) return "";
        return file.FileInfo.Extension.ToLower();
    }
}