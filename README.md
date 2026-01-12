# Umamusume Assets Extractor

Extracts asset files from Umamusume Pretty Derby (copies files from the dat folder with their original names).

## Features

- **Auto-detect game installation** - Automatically finds Japan Steam, Japan DMM, and Global Steam installations
- **Encrypted database support** - Works with both encrypted and unencrypted meta.db files
- **Export encryption keys** - Exports per-file encryption keys to `keys.json` for asset decryption
- **Folder/File dump modes** - Extract specific folders or search for files by name

## What does it do?

Umamusume stores asset files with hashed names in the `dat` folder:

![image](https://user-images.githubusercontent.com/90076182/186933969-5f3a6ca7-61cc-481d-838f-8528789ee180.png)

For example, `sound/l/1001/snd_bgm_live_1001_oke_01.awb` is actually stored as:

![image](https://user-images.githubusercontent.com/90076182/186935145-6c28ef28-6d16-40c3-8bc2-e32ec7bc99a4.png)

This tool reads the `meta` database and copies files with their original names:

![image](https://user-images.githubusercontent.com/90076182/186937978-bc7c62ba-1fc0-4f5a-9aa2-bb5e268610ce.png)

## Download

Download the latest release from the Releases section:

![image](https://user-images.githubusercontent.com/90076182/187061141-98daf275-ddd1-457d-9bba-2bdd649139fc.png)

## Usage

1. **Select installation** - If multiple installations are detected, choose which one to use
2. **Select region** - For shared paths (AppData), choose between Global or Japan
3. **Select mode**:
   - Extract files (copies files + exports keys.json)
   - Export keys only (fast - just creates keys.json)
4. **Enable logs** (optional) - Show detailed progress in console
5. **Choose dump mode**:
   - Folder dump: Extract a specific folder (e.g., `sound` for all audio files)
   - File dump: Extract files containing a specific string (e.g., `chr1032` for Agnes Tachyon)
6. **Enter target** - Specify folder/file name, or leave empty to dump everything
   - Type `list` to see available folders

## Notes

### Files must be downloaded in-game
Only downloaded assets can be extracted. If you can't find specific files, try doing a bulk download in the game first.

## Requirements

Requires .NET 7.0 Runtime: https://dotnet.microsoft.com/download/dotnet/7.0

![image](https://user-images.githubusercontent.com/90076182/229263290-757a40f5-65cb-4140-84d8-0b13a2c8e448.png)

## Credits

- Original tool by [Endergreen12](https://github.com/Endergreen12)
- Path detection from [mori2163](https://github.com/mori2163/umamusume-assets-extractor)
- Encrypted database support from [UmaViewer](https://github.com/katboi01/UmaViewer)