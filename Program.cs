using Microsoft.Data.Sqlite;
using System.Reflection.PortableExecutable;
using System.Threading;
using static Umamusume_Assets_Extractor.Utils;

// Startup
UpdateConsoleTitle();
Console.WriteLine(appName + " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
Console.WriteLine("If you can't find the files you want, please perform a bulk download in the game.");
Console.WriteLine($"Extracted files will be saved to the '{extractFolderName}' folder.");
Console.WriteLine();

// Check if database exists
if (!File.Exists(metaPath))
{
    Console.WriteLine($"Error: Database \"{metaPath}\" not found.");
    Console.WriteLine("Please start the game first and try again.");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    Environment.Exit(0);
}

// Select region
Console.WriteLine("Select your region:");
Console.WriteLine("1: Japan (DMM)");
Console.WriteLine("2: Global (Steam)");
Console.Write("Select (1/2): ");
var regionInput = Console.ReadLine();
if (regionInput == "2")
{
    region = Umamusume_Assets_Extractor.Region.Global;
    Console.WriteLine("Global (Steam) mode selected.");
}
else
{
    region = Umamusume_Assets_Extractor.Region.Jp;
    Console.WriteLine("Japan (DMM) mode selected.");
}

// Select mode (Global only: keys-only option)
if (region == Umamusume_Assets_Extractor.Region.Global)
{
    Console.WriteLine();
    Console.WriteLine("Select extraction mode:");
    Console.WriteLine("1: Extract files (copies files + exports keys.json)");
    Console.WriteLine("2: Export keys only (fast - just creates keys.json)");
    Console.Write("Select (1/2): ");
    var modeInput = Console.ReadLine();
    if (modeInput == "2")
    {
        ExportKeysOnly("keys.json");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        Environment.Exit(0);
    }
}

// Verbose mode
Console.WriteLine();
Console.WriteLine("Show logs in console?");
Console.WriteLine("Enabling logs will slightly slow down execution. You can track progress in the title bar without logs.");
Console.Write("Enter 'y' to show logs, or any other key to skip: ");
verboseMode = Console.ReadLine() == "y";

// File or folder dump
Console.WriteLine();
Console.WriteLine();
Console.WriteLine("Do you want to dump files or folders?");
Console.WriteLine("File dump: All files containing the specified text in their name will be dumped.");
Console.WriteLine("Folder dump: Only the folder matching the specified name will be dumped.");
Console.Write("Enter 'y' for file dump, or any other key for folder dump: ");
isDumpTargetFile = Console.ReadLine() == "y";

var dumpTargetName = isDumpTargetFile ? "file" : "folder";

// Specify dump target
var dumpTarget = "";
Console.WriteLine();
Console.WriteLine();
do
{
    Console.WriteLine($"Specify the {dumpTargetName} to dump. Leave empty to dump all {dumpTargetName}s.");
    Console.WriteLine(isDumpTargetFile ? "Example: Enter '1001' to dump all files containing '1001' in their name." :
                                         "Example: Enter 'sound' to dump only the sound folder containing acb and awb files.");
    Console.WriteLine("Enter 'list' to show available folders.");
    Console.Write($"Enter {dumpTargetName} name: ");
    dumpTarget = Console.ReadLine();

    if (dumpTarget == "list")
        PrintFolders();
} while (dumpTarget == "list");

if (dumpTarget == null)
{
    dumpTarget = "";
}

if (!Directory.Exists(extractFolderName))
{
    PrintLogIfVerboseModeIsOn($"Creating folder {extractFolderName}");
    Directory.CreateDirectory(extractFolderName);
}

// Copy files
CopySourceFiles(dumpTarget);
UpdateConsoleTitle("done");

Console.WriteLine("Open the extracted folder in Explorer? (y)");
if (Console.ReadLine() == "y")
    System.Diagnostics.Process.Start("explorer.exe", extractFolderName);