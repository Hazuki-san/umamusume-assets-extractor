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

// Check for available game installations
var availablePaths = GetAllGamePaths();
bool needsRegionPrompt = false;

if (availablePaths.Count == 0)
{
    Console.WriteLine("Error: No game installation found automatically.");
    Console.WriteLine();

    bool pathSet = false;
    while (!pathSet)
    {
        Console.WriteLine("Please enter the game data folder path manually.");
        Console.WriteLine(@"(e.g. C:\Program Files (x86)\Steam\steamapps\common\UmamusumePrettyDerby\UmamusumePrettyDerby_Data\Persistent)");
        Console.Write("Path: ");
        string? customPath = Console.ReadLine();

        if (string.IsNullOrEmpty(customPath))
        {
            Console.WriteLine("Error: No path entered. Please try again.");
            Console.WriteLine();
            continue;
        }

        if (SetCustomGameDataPath(customPath))
        {
            pathSet = true;
            needsRegionPrompt = true; // Manual path - ask for region
            Console.WriteLine($"Path set: {gameDataPath}");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("Please try again.");
            Console.WriteLine();
        }
    }
}
else if (availablePaths.Count == 1)
{
    // Single installation - use it automatically
    SetCustomGameDataPath(availablePaths[0].path);
    needsRegionPrompt = availablePaths[0].needsRegionPrompt;
    Console.WriteLine($"Found: {availablePaths[0].name}");
    Console.WriteLine($"Path: {gameDataPath}");
}
else
{
    // Multiple installations - let user choose
    Console.WriteLine("Multiple installations found:");
    for (int i = 0; i < availablePaths.Count; i++)
    {
        Console.WriteLine($"{i + 1}: {availablePaths[i].name}");
    }
    Console.Write($"Select (1-{availablePaths.Count}): ");
    
    int selectedIndex = 0;
    if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= availablePaths.Count)
    {
        selectedIndex = choice - 1;
        Console.WriteLine($"Selected: {availablePaths[selectedIndex].name}");
    }
    else
    {
        Console.WriteLine($"Defaulting to: {availablePaths[0].name}");
    }
    SetCustomGameDataPath(availablePaths[selectedIndex].path);
    needsRegionPrompt = availablePaths[selectedIndex].needsRegionPrompt;
}

// Verify database exists
if (!File.Exists(metaPath))
{
    Console.WriteLine($"Error: Database \"{metaPath}\" not found.");
    Console.WriteLine("Please start the game first and try again.");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    Environment.Exit(0);
}

// Determine region
if (needsRegionPrompt)
{
    // Shared path - ask user
    Console.WriteLine();
    Console.WriteLine("This path is shared by Global Steam and Japan DMM. Select your region:");
    Console.WriteLine("1: Global (Steam)");
    Console.WriteLine("2: Japan (DMM)");
    Console.Write("Select (1/2): ");
    if (Console.ReadLine() == "1")
    {
        region = Umamusume_Assets_Extractor.Region.Global;
        Console.WriteLine("Region: Global");
    }
    else
    {
        region = Umamusume_Assets_Extractor.Region.Jp;
        Console.WriteLine("Region: Japan");
    }
}
else
{
    // Japan Steam (only non-shared path) or new DMM path
    region = Umamusume_Assets_Extractor.Region.Jp;
    Console.WriteLine("Region: Japan");
}

// Select extraction mode (available for all regions now)
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