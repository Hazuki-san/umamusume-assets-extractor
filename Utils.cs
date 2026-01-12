using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Umamusume_Assets_Extractor
{
    public class Utils
    {
        // App settings
        public static string appName = "Umamusume Assets Extractor";
        public static bool verboseMode = false;
        public static bool isDumpTargetFile = false;
        public static int willBeCopiedFilesAmount = 0;
        public static int copiedFilesAmount = 0;
        public static int skippedFilesAmount = 0;
        // Directory settings
        public static string extractFolderName = "Contents";
        public static string gameDataPath = GetGameDataPath();
        public static string metaPath = gameDataPath + @"\meta";
        public static string datPath = gameDataPath + @"\dat";

        /// <summary>
        /// Get all available game data paths
        /// </summary>
        public static List<(string path, string name, bool needsRegionPrompt)> GetAllGamePaths()
        {
            var paths = new List<(string path, string name, bool needsRegionPrompt)>();

            // Steam JP path (only JP Steam has Persistent in Steam folder)
            string steamJpPath = @"C:\Program Files (x86)\Steam\steamapps\common\UmamusumePrettyDerby_Jpn\UmamusumePrettyDerby_Jpn_Data\Persistent";
            if (Directory.Exists(steamJpPath))
                paths.Add((steamJpPath, "Japan (Steam)", false));

            // Standard AppData path - shared by Global Steam and old DMM Japan
            string standardPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"Low\Cygames\umamusume";
            if (Directory.Exists(standardPath))
                paths.Add((standardPath, "AppData (Global/DMM shared)", true));

            // New DMM path (post-9/24 update) - in user folder, not AppData!
            string newDmmPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Umamusume\umamusume_Data\Persistent";
            if (Directory.Exists(newDmmPath))
                paths.Add((newDmmPath, "Japan (DMM)", false));

            return paths;
        }

        /// <summary>
        /// Auto-detect game data path from known locations (returns first found)
        /// </summary>
        private static string GetGameDataPath()
        {
            var paths = GetAllGamePaths();
            return paths.Count > 0 ? paths[0].path : "";
        }

        /// <summary>
        /// Manually set the game data path
        /// </summary>
        public static bool SetCustomGameDataPath(string customPath)
        {
            if (!Directory.Exists(customPath))
            {
                Console.WriteLine($"Error: Specified path \"{customPath}\" not found.");
                return false;
            }

            gameDataPath = customPath;
            metaPath = gameDataPath + @"\meta";
            datPath = gameDataPath + @"\dat";

            if (!File.Exists(metaPath))
            {
                Console.WriteLine($"Warning: Database \"{metaPath}\" not found at specified path.");
                return false;
            }

            return true;
        }
        // Database settings
        public static string filePathColumn = "n";
        public static string sourceFileNameColumn = "h";
        public static string isDownloadedColumn = "s";
        public static string fileTypeColumn = "m";
        public static string encryptionKeyColumn = "e";  // Per-entry encryption key for Global
        public static string excludeFileTypeWord = "manifest";
        public static string tableName = "a";
        public static string searchCriteria = @$"
                                                    FROM {tableName}
                                                    WHERE {isDownloadedColumn} = 1
                                                    AND {fileTypeColumn} NOT LIKE '%{excludeFileTypeWord}%'
                                                ";

        // Region setting
        public static Region region = Region.Jp;

        // Per-entry encryption keys dictionary (filepath -> key)
        public static Dictionary<string, long> encryptionKeys = new Dictionary<string, long>();

        // Encryption keys (from UmaViewer)
        public static byte[] DBBaseKey = new byte[]
        {
            0xF1, 0x70, 0xCE, 0xA4, 0xDF, 0xCE, 0xA3, 0xE1,
            0xA5, 0xD8, 0xC7, 0x0B, 0xD1, 0x00, 0x00, 0x00
        };

        public static byte[] DBKey = new byte[]
        {
            0x6D, 0x5B, 0x65, 0x33, 0x63, 0x36, 0x63, 0x25,
            0x54, 0x71, 0x2D, 0x73, 0x50, 0x53, 0x63, 0x38,
            0x6D, 0x34, 0x37, 0x7B, 0x35, 0x63, 0x70, 0x23,
            0x37, 0x34, 0x53, 0x29, 0x73, 0x43, 0x36, 0x33
        };

        public static byte[] GlobalDBKey = new byte[]
        {
            0x56, 0x63, 0x6B, 0x63, 0x42, 0x72, 0x37, 0x76,
            0x65, 0x70, 0x41, 0x62
        };

        /// <summary>
        /// Check if database is encrypted by reading the file header.
        /// Unencrypted SQLite databases start with "SQLite format 3"
        /// </summary>
        public static bool IsDbEncrypted(string dbPath)
        {
            try
            {
                using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[16];
                    fs.Read(header, 0, 16);
                    // "SQLite format 3\0" = unencrypted
                    string headerStr = System.Text.Encoding.ASCII.GetString(header);
                    return !headerStr.StartsWith("SQLite format 3");
                }
            }
            catch
            {
                // If we can't read, assume encrypted
                return true;
            }
        }

        public static byte[] GenFinalKey(byte[] key)
        {
            var result = (byte[])key.Clone();
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (byte)(result[i] ^ DBBaseKey[i % 13]);
            }
            return result;
        }

        public static int CalucateWillBeCopiedFilesAmount(string dumpTarget = "")
        {
            var amount = 0;
            var sql = $"SELECT count(*) {searchCriteria} {GetRefinementString(dumpTarget)}";

            if (region == Region.Global)
            {
                // Use encrypted database access for Global
                var dbKey = region == Region.Global ? GlobalDBKey : DBKey;
                var finalKey = GenFinalKey(dbKey);
                IntPtr db = IntPtr.Zero;
                try
                {
                    db = Sqlite3MC.Open(metaPath);
                    Sqlite3MC.MC_Config(db, "cipher", 3); // cipher index
                    int rcKey = Sqlite3MC.Key_SetBytes(db, finalKey);
                    if (rcKey != Sqlite3MC.SQLITE_OK)
                        throw new InvalidOperationException($"sqlite3_key failed: {Sqlite3MC.GetErrMsg(db)}");
                    if (!Sqlite3MC.ValidateReadable(db, out string? err))
                        throw new InvalidOperationException($"DB validation failed: {err}");

                    Sqlite3MC.ForEachRow(sql, db, (stmt) =>
                    {
                        amount = Sqlite3MC.ColumnInt(stmt, 0);
                    });
                }
                finally
                {
                    if (db != IntPtr.Zero) Sqlite3MC.Close(db);
                }
            }
            else
            {
                // Use standard SqliteConnection for Japan
                using (var connection = new SqliteConnection($"Data Source={metaPath}"))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = sql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            amount = reader.GetInt32(0);
                        }
                    }
                }
            }

            return amount;
        }

        public static void CopySourceFiles(string dumpTarget = "")
        {
            willBeCopiedFilesAmount = CalucateWillBeCopiedFilesAmount(dumpTarget);

            if (willBeCopiedFilesAmount == 0)
                return;

            var sql = $"SELECT {filePathColumn}, {sourceFileNameColumn}, {encryptionKeyColumn} {searchCriteria} {GetRefinementString(dumpTarget)}";
            Console.WriteLine("Starting file copy...");
            encryptionKeys.Clear();  // Clear any previous keys

            if (region == Region.Global)
            {
                // Use encrypted database access for Global
                var dbKey = GlobalDBKey;
                var finalKey = GenFinalKey(dbKey);
                IntPtr db = IntPtr.Zero;
                try
                {
                    db = Sqlite3MC.Open(metaPath);
                    Sqlite3MC.MC_Config(db, "cipher", 3);
                    int rcKey = Sqlite3MC.Key_SetBytes(db, finalKey);
                    if (rcKey != Sqlite3MC.SQLITE_OK)
                        throw new InvalidOperationException($"sqlite3_key failed: {Sqlite3MC.GetErrMsg(db)}");
                    if (!Sqlite3MC.ValidateReadable(db, out string? err))
                        throw new InvalidOperationException($"DB validation failed: {err}");

                    Sqlite3MC.ForEachRow(sql, db, (stmt) =>
                    {
                        var fileDir = Sqlite3MC.ColumnText(stmt, 0);
                        var sourceFileName = Sqlite3MC.ColumnText(stmt, 1);
                        var encKey = Sqlite3MC.ColumnInt64(stmt, 2);
                        if (fileDir != null && sourceFileName != null)
                        {
                            ProcessFileCopy(fileDir, sourceFileName, dumpTarget);
                            if (encKey != 0)
                                encryptionKeys[fileDir] = encKey;
                        }
                    });
                }
                finally
                {
                    if (db != IntPtr.Zero) Sqlite3MC.Close(db);
                }
            }
            else
            {
                // Use standard SqliteConnection for Japan
                using (var connection = new SqliteConnection($"Data Source={metaPath}"))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = sql;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fileDir = reader.GetString(0);
                            var sourceFileName = reader.GetString(1);
                            ProcessFileCopy(fileDir, sourceFileName, dumpTarget);
                        }
                    }
                }
            }

            Console.WriteLine($"Completed. Total files copied: {copiedFilesAmount}");

            // Export encryption keys for Global region
            if (region == Region.Global && encryptionKeys.Count > 0)
            {
                var keysPath = Path.Combine(extractFolderName, "keys.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(keysPath, JsonSerializer.Serialize(encryptionKeys, options));
                Console.WriteLine($"Exported {encryptionKeys.Count} encryption keys to {keysPath}");
            }
        }

        /// <summary>
        /// Export all encryption keys from meta.db without copying any files.
        /// Much faster than full extraction - only reads the database.
        /// </summary>
        public static void ExportKeysOnly(string outputPath = "keys.json")
        {
            Console.WriteLine("Reading encryption keys from meta.db...");
            encryptionKeys.Clear();

            var sql = $"SELECT {filePathColumn}, {encryptionKeyColumn} FROM {tableName} WHERE {encryptionKeyColumn} != 0";
            bool isEncrypted = IsDbEncrypted(metaPath);
            
            if (isEncrypted)
            {
                Console.WriteLine("Database is encrypted, using decryption...");
                // Use appropriate key based on region
                var dbKey = region == Region.Global ? GlobalDBKey : DBKey;
                var finalKey = GenFinalKey(dbKey);
                IntPtr db = IntPtr.Zero;
                try
                {
                    db = Sqlite3MC.Open(metaPath);
                    Sqlite3MC.MC_Config(db, "cipher", 3);
                    int rcKey = Sqlite3MC.Key_SetBytes(db, finalKey);
                    if (rcKey != Sqlite3MC.SQLITE_OK)
                        throw new InvalidOperationException($"sqlite3_key failed: {Sqlite3MC.GetErrMsg(db)}");
                    if (!Sqlite3MC.ValidateReadable(db, out string? err))
                        throw new InvalidOperationException($"DB validation failed: {err}");

                    Sqlite3MC.ForEachRow(sql, db, (stmt) =>
                    {
                        var fileDir = Sqlite3MC.ColumnText(stmt, 0);
                        var encKey = Sqlite3MC.ColumnInt64(stmt, 1);
                        if (fileDir != null && encKey != 0)
                            encryptionKeys[fileDir] = encKey;
                    });
                }
                finally
                {
                    if (db != IntPtr.Zero) Sqlite3MC.Close(db);
                }
            }
            else
            {
                Console.WriteLine("Database is not encrypted.");
                // Unencrypted database
                using (var connection = new SqliteConnection($"Data Source={metaPath}"))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = sql;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fileDir = reader.GetString(0);
                            var encKey = reader.GetInt64(1);
                            if (encKey != 0)
                                encryptionKeys[fileDir] = encKey;
                        }
                    }
                }
            }

            if (encryptionKeys.Count > 0)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(outputPath, JsonSerializer.Serialize(encryptionKeys, options));
                Console.WriteLine($"Exported {encryptionKeys.Count} encryption keys to {outputPath}");
            }
            else
            {
                Console.WriteLine("No encryption keys found in database.");
            }
        }

        private static void ProcessFileCopy(string fileDir, string sourceFileName, string dumpTarget)
        {
            UpdateConsoleTitle("copying");

            var sourceFileDir = datPath + "\\" + sourceFileName.Substring(0, 2) + "\\" + sourceFileName;
            var copyFilePath = Path.Combine(extractFolderName, fileDir);

            if (!fileDir.Split("/").Last().Contains(dumpTarget) && isDumpTargetFile)
            {
                PrintLogIfVerboseModeIsOn($"{fileDir} does not contain \"{dumpTarget}\" in name, skipping.");
                willBeCopiedFilesAmount--;
                skippedFilesAmount++;
                return;
            }

            if (File.Exists(copyFilePath))
            {
                if (FileCompare(sourceFileDir, copyFilePath))
                {
                    PrintLogIfVerboseModeIsOn($"{fileDir} already copied, skipping.");
                    willBeCopiedFilesAmount--;
                    skippedFilesAmount++;
                    return;
                }

                PrintLogIfVerboseModeIsOn($"{fileDir} exists but differs, replacing.");
                File.Delete(copyFilePath);
            }

            PrintLogIfVerboseModeIsOn($"{sourceFileName} -> {fileDir}");

            Directory.CreateDirectory(Path.Combine(extractFolderName, String.Join("\\", fileDir.Split("/").SkipLast(1))));

            File.Copy(sourceFileDir, copyFilePath);

            copiedFilesAmount++;
        }

        // FileCompare function adapted from Microsoft's "Create a File-Compare function using Visual C#".

        // This method accepts two strings the represent two files to
        // compare. A return value of 0 indicates that the contents of the files
        // are the same. A return value of any other value indicates that the
        // files are not the same.
        public static bool FileCompare(string file1, string file2)
        {
            int file1byte;
            int file2byte;
            FileStream fs1;
            FileStream fs2;

            // Determine if the same file was referenced two times.
            if (file1 == file2)
            {
                // Return true to indicate that the files are the same.
                return true;
            }

            // Open the two files.
            fs1 = new FileStream(file1, FileMode.Open);
            fs2 = new FileStream(file2, FileMode.Open);

            // Check the file sizes. If they are not the same, the files
            // are not the same.
            if (fs1.Length != fs2.Length)
            {
                // Close the file
                fs1.Close();
                fs2.Close();

                // Return false to indicate files are different
                return false;
            }

            // Read and compare a byte from each file until either a
            // non-matching set of bytes is found or until the end of
            // file1 is reached.
            do
            {
                // Read one byte from each file.
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));

            // Close the files.
            fs1.Close();
            fs2.Close();

            // Return the success of the comparison. "file1byte" is
            // equal to "file2byte" at this point only if the files are
            // the same.
            return ((file1byte - file2byte) == 0);
        }

        /// <summary>
        /// Prints log only if verbose mode is enabled. Returns true if printed, false otherwise.
        /// </summary>
        public static bool PrintLogIfVerboseModeIsOn(string log)
        {
            if (!verboseMode)
                return false;

            Console.WriteLine(log);
            return true;
        }

        /// <summary>
        /// Updates the console title.
        /// </summary>
        /// <param name="status">copying: in progress, done: completed, empty: app name only</param>
        public static void UpdateConsoleTitle(string status = "")
        {
            switch(status)
            {
                case "copying":
                {
                    int donePer = (int)(copiedFilesAmount / (float)willBeCopiedFilesAmount * 100);

                    Console.Title = $"{appName} - Remaining {copiedFilesAmount} / {willBeCopiedFilesAmount} - {donePer}% done - Skipped: {skippedFilesAmount}";
                    break;
                }

                case "done":
                {
                    Console.Title = $"{appName} - Done {copiedFilesAmount} / {willBeCopiedFilesAmount} - 100% complete - Skipped: {skippedFilesAmount}";
                    break;
                }

                default:
                {
                    Console.Title = $"{appName}";
                    break;
                }
            }
        }

        public static void PrintFolders()
        {
            Console.WriteLine("Available folders:");

            var sql = $@"SELECT {filePathColumn} FROM {tableName} WHERE {fileTypeColumn} = 'manifest'";

            if (region == Region.Global)
            {
                // Use encrypted database access for Global
                var dbKey = GlobalDBKey;
                var finalKey = GenFinalKey(dbKey);
                IntPtr db = IntPtr.Zero;
                try
                {
                    db = Sqlite3MC.Open(metaPath);
                    Sqlite3MC.MC_Config(db, "cipher", 3);
                    int rcKey = Sqlite3MC.Key_SetBytes(db, finalKey);
                    if (rcKey != Sqlite3MC.SQLITE_OK)
                        throw new InvalidOperationException($"sqlite3_key failed: {Sqlite3MC.GetErrMsg(db)}");
                    if (!Sqlite3MC.ValidateReadable(db, out string? err))
                        throw new InvalidOperationException($"DB validation failed: {err}");

                    Sqlite3MC.ForEachRow(sql, db, (stmt) =>
                    {
                        var folder = Sqlite3MC.ColumnText(stmt, 0);
                        if (folder != null)
                            Console.WriteLine(folder.Replace("//", ""));
                    });
                }
                finally
                {
                    if (db != IntPtr.Zero) Sqlite3MC.Close(db);
                }
            }
            else
            {
                // Use standard SqliteConnection for Japan
                using (var connection = new SqliteConnection($"Data Source={metaPath}"))
                {
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = sql;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine(reader.GetString(0).Replace("//", ""));
                        }
                    }
                }
            }
        }

        public static string GetRefinementString(string dumpTarget)
        {
            var refinementString = "";
            if (dumpTarget != "")
                if (isDumpTargetFile)
                    refinementString += @$"
                                                    AND {filePathColumn} LIKE '%{dumpTarget}%'
                                               ";
                else
                    refinementString += @$"
                                                    AND {filePathColumn} LIKE '{dumpTarget}/%'
                                               ";

            return refinementString;
        }
    }

    public enum Region
    {
        Jp,
        Global
    }
}
