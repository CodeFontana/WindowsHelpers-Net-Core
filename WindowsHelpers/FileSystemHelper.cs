﻿using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace WindowsLibrary;

public class FileSystemHelper
{
    private readonly ILogger _logger;
    private readonly WindowsHelper _winHelper;
    private readonly ProcessHelper _processHelper;

    public FileSystemHelper(ILogger<FileSystemHelper> logger,
                            WindowsHelper winHelper,
                            ProcessHelper processHelper)
    {
        _logger = logger;
        _winHelper = winHelper;
        _processHelper = processHelper;
    }

    public bool AddDirectorySecurity(string fileOrFolder,
                                     string userAccount,
                                     FileSystemRights requestedRights,
                                     AccessControlType controlType,
                                     InheritanceFlags inheritFlag = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                     PropagationFlags propFlag = PropagationFlags.None,
                                     bool forcePermissions = false)
    {
        try
        {
            if (File.Exists(fileOrFolder))
            {
                var fInfo = new FileInfo(fileOrFolder);
                var fSecurity = fInfo.GetAccessControl();
                fSecurity.SetAccessRuleProtection(false, true);
                fSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, requestedRights, controlType));
                fInfo.SetAccessControl(fSecurity);
            }
            else if (Directory.Exists(fileOrFolder))
            {
                var dInfo = new DirectoryInfo(fileOrFolder);
                var dSecurity = dInfo.GetAccessControl();
                //dSecurity.SetAccessRuleProtection(false, true); // This option appears to flip the enable/disable inheritance option.
                dSecurity.AddAccessRule(new FileSystemAccessRule(userAccount, requestedRights, inheritFlag, propFlag, controlType));
                dInfo.SetAccessControl(dSecurity);
            }
            else
            {
                _logger.LogError($"Specified file or folder [{fileOrFolder}] does not exist");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            // Force permissions? (e.g. take ownership and try again)
            if (forcePermissions)
            {
                if (NativeMethods.OpenProcessToken(
                    Process.GetCurrentProcess().Handle,
                    NativeMethods.TOKEN_ALL_ACCESS,
                    out IntPtr hToken) == false)
                {
                    _logger.LogInformation($"Unable to open specified process token [OpenProcessToken={Marshal.GetLastWin32Error()}]");
                    return false;
                }

                if (_winHelper.EnablePrivilege(hToken, NativeMethods.SE_TAKE_OWNERSHIP_NAME) == false)
                {
                    _logger.LogError("Failed to enable privilege [SeTakeOwnershipPrivilege]");
                    Marshal.FreeHGlobal(hToken);
                    return false;
                }

                // Administrators group trustee control information.
                NativeMethods.EXPLICIT_ACCESS adminGroupAccess = new();
                NativeMethods.BuildExplicitAccessWithName(
                    ref adminGroupAccess,
                    "Administrators",
                    NativeMethods.ACCESS_MASK.GENERIC_ALL,
                    NativeMethods.ACCESS_MODE.SET_ACCESS,
                    NativeMethods.NO_INHERITANCE);

                IntPtr acl = IntPtr.Zero;
                NativeMethods.SetEntriesInAcl(1, ref adminGroupAccess, IntPtr.Zero, ref acl);

                // Allocate SID -- BUILTIN\Administrators.
                NativeMethods.SID_IDENTIFIER_AUTHORITY sidNTAuthority = NativeMethods.SECURITY_NT_AUTHORITY;
                IntPtr sidAdministrators = IntPtr.Zero;
                NativeMethods.AllocateAndInitializeSid(ref sidNTAuthority,
                    2,
                    NativeMethods.SECURITY_BUILTIN_DOMAIN_RID,
                    NativeMethods.DOMAIN_ALIAS_RID_ADMINS,
                    0, 0, 0, 0, 0, 0,
                    ref sidAdministrators);

                // Set the owner in the object's security descriptor.
                NativeMethods.SetNamedSecurityInfo(
                    fileOrFolder,
                    NativeMethods.SE_OBJECT_TYPE.SE_FILE_OBJECT,
                    NativeMethods.SECURITY_INFORMATION.OWNER_SECURITY_INFORMATION,
                    sidAdministrators,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                /*// Modify the object's DACL.
                NativeMethods.SetNamedSecurityInfo(
                    fileOrFolder,
                    NativeMethods.SE_OBJECT_TYPE.SE_FILE_OBJECT,
                    NativeMethods.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    acl,
                    IntPtr.Zero);*/

                NativeMethods.FreeSid(sidAdministrators);
                NativeMethods.LocalFree(acl);

                return AddDirectorySecurity(fileOrFolder, userAccount, requestedRights, controlType, inheritFlag, propFlag, false);
            }
            else
            {
                _logger.LogError(e, $"Failed to add filesystem permissions to [{fileOrFolder}]");
                return false;
            }
        }
    }

    public string BytesToReadableValue(long numBytes)
    {
        var suffixes = new List<string> { " B ", " KB", " MB", " GB", " TB", " PB" };

        for (int i = 0; i < suffixes.Count; i++)
        {
            // Divide by powers of 1024, as we move through the scales
            long temp = Math.Abs(numBytes / (long)Math.Pow(1024, i + 1));

            // Have we gone off scale?
            if (temp <= 0)
            {
                // Return prior suffix value
                return String.Format("{0,9}", 
                    String.Format("{0:0.00}", 
                        Math.Round((double)numBytes / Math.Pow(1024, i), 2)) 
                    + suffixes[i]);
            }
        }

        return numBytes.ToString();
    }

    public async Task<bool> CheckFileSystemHealth()
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();

        foreach (DriveInfo d in allDrives)
        {
            if (d.DriveType.ToString().ToLower().Equals("fixed"))
            {
                bool fsHealthy = false;

                // As read-only Chkdsk is prone to reporting spurious errors,
                // we will make up to (3) attempts for a positive result.
                for (int i = 0; i < 3; i++)
                {
                    _logger.LogInformation($"Check drive [read-only]: {d.Name}");

                    Tuple<long, string> result = _processHelper.RunProcess(
                        "chkdsk.exe",
                        d.Name.Substring(0, 2),
                        $@"{Environment.GetEnvironmentVariable("windir")}\System32",
                        1200, true, true, false);

                    if (result.Item2.ToLower().Contains("windows has scanned the file system and found no problems"))
                    {
                        _logger.LogInformation("CHKDSK result: OK");
                        fsHealthy |= true;
                        break;
                    }
                    else
                    {
                        _logger.LogDebug(result.Item2);
                        _logger.LogInformation("CHKDSK result: FAIL");
                        fsHealthy |= false;
                    }

                    await Task.Delay(10000);
                }

                if (fsHealthy == false)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool CheckSmartStatus()
    {
        try
        {
            ManagementObjectSearcher wmiQuery = new(
                "SELECT Model,SerialNumber,InterfaceType,Partitions,Status,Size FROM Win32_DiskDrive");
            bool smartOK = true;

            foreach (ManagementObject drive in wmiQuery.Get())
            {
                var model = drive["Model"];
                var serial = drive["SerialNumber"];
                var interfacetype = drive["InterfaceType"];
                var partitions = drive["Partitions"];
                var smart = drive["Status"];
                var sizeInBytes = drive["Size"];

                _logger.LogInformation($"Found drive: {model}");

                if (serial != null)
                {
                    _logger.LogInformation($"  Serial: {serial}");
                }

                if (interfacetype != null)
                {
                    _logger.LogInformation($"  Interface: {interfacetype}");
                }

                if (partitions != null)
                {
                    _logger.LogInformation($"  Partitions: {partitions}");
                }

                if (sizeInBytes != null)
                {
                    _logger.LogInformation($"  Size: {BytesToReadableValue(long.Parse(sizeInBytes.ToString().Trim()))}");
                }

                if (smart != null)
                {
                    _logger.LogInformation($"  SMART: {smart}");

                    if (smart.ToString().ToLower().Equals("ok") == false)
                    {
                        smartOK = false;
                    }
                }
            }

            wmiQuery.Dispose();

            if (smartOK == false)
            {
                _logger.LogError("SMART status failure detected");
                return false;
            }
            else
            {
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to verify drive SMART status");
            return false;
        }
    }

    public bool CopyFile(string sourceFileName,
                         string destFileName,
                         bool overWrite = true,
                         bool handleInUseOnReboot = false)
    {
        _logger.LogInformation($"Copy file: {sourceFileName}");
        _logger.LogInformation($"       To: {destFileName}");

        try
        {
            try
            {
                if (File.Exists(sourceFileName) == false)
                {
                    _logger.LogError($"Source file does not exist [{sourceFileName}]");
                    return false;
                }

                if (sourceFileName.ToLower().Equals(destFileName.ToLower()))
                {
                    _logger.LogError($"Source and destination files must be different [{sourceFileName}]");
                    return false;
                }

                if (Directory.Exists(Path.GetDirectoryName(destFileName)) == false)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destFileName));
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to create target directory");
                        return false;
                    }
                }

                File.Copy(sourceFileName, destFileName, overWrite);

                foreach (string file in Directory.GetFiles(Path.GetDirectoryName(destFileName)))
                {
                    if (file.ToLower().Contains(".delete_on_reboot"))
                    {
                        DeleteFile(file, false, true);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                if (IsFileInUse(destFileName) && overWrite)
                {
                    try
                    {
                        string incrementFilename = destFileName + ".delete_on_reboot";
                        int fileIncrement = 0;

                        while (true)
                        {
                            if (File.Exists(incrementFilename))
                            {
                                incrementFilename = $"{destFileName}.delete_on_reboot_{fileIncrement++}";
                            }
                            else
                            {
                                break;
                            }
                        }

                        // Attempt to rename destination file.
                        // --> This may or may not succeed depending on type of
                        //     lock on the destination file.
                        File.Move(destFileName, incrementFilename);

                        // Schedule original file for deletion on next reboot.
                        NativeMethods.MoveFileEx(
                            incrementFilename,
                            null,
                            NativeMethods.MoveFileFlags.DelayUntilReboot);

                        _logger.LogInformation($"Delete after reboot: {incrementFilename}");
                    }
                    catch (Exception)
                    {
                        string pendingFilename = $"{destFileName}.pending";
                        int fileIncrement = 0;

                        while (true)
                        {
                            if (File.Exists(pendingFilename))
                            {
                                pendingFilename = $"{destFileName}.pending_{fileIncrement}";
                            }
                            else
                            {
                                break;
                            }
                        }

                        try
                        {
                            // Copy the file as a pending replacement.
                            File.Copy(sourceFileName, pendingFilename, true);

                            // Attempt in-place file replacement (as alternative to copy/replacement).
                            bool moveSuccess = NativeMethods.MoveFileEx(
                                pendingFilename,
                                destFileName,
                                NativeMethods.MoveFileFlags.ReplaceExisting);

                            if (moveSuccess == false && handleInUseOnReboot)
                            {
                                // Schedule deletion of original file.
                                NativeMethods.MoveFileEx(
                                    destFileName,
                                    null,
                                    NativeMethods.MoveFileFlags.DelayUntilReboot);

                                // Schedule rename of pending file, to replace original destination.
                                NativeMethods.MoveFileEx(
                                    pendingFilename,
                                    destFileName,
                                    NativeMethods.MoveFileFlags.DelayUntilReboot);

                                _logger.LogInformation($"Reboot required: {destFileName}");
                                return true;
                            }
                            else if (moveSuccess == false && handleInUseOnReboot == false)
                            {
                                _logger.LogError($"Destination file is in-use [{destFileName}]");
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, $"Unable to schedule file replacement for in-use file [{destFileName}]");
                            return false;
                        }
                    }
                }

                File.Copy(sourceFileName, destFileName, overWrite);
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to copy file [{Path.GetFileName(sourceFileName)}] to destination");
        }

        return false;
    }

    public bool CopyFolderContents(string sourceFolder,
                                   string targetFolder,
                                   string[] reservedItems = null,
                                   bool verboseOutput = true,
                                   bool recursiveCopy = true,
                                   bool handleInUseOnReboot = false)
    {
        if (Directory.Exists(sourceFolder) == false)
        {
            _logger.LogError($"Source folder does not exist [{sourceFolder}]");
            return false;
        }

        if (sourceFolder.ToLower().Equals(targetFolder.ToLower()))
        {
            _logger.LogError($"Source and destination folders must be different [{sourceFolder}]");
            return false;
        }

        if (Directory.Exists(targetFolder) == false)
        {
            try
            {
                Directory.CreateDirectory(targetFolder);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to create target directory");
                return false;
            }
        }

        bool skipItem = false;

        try
        {
            string[] fileList = Directory.GetFiles(sourceFolder);

            foreach (string sourceFile in fileList)
            {
                if (reservedItems != null)
                {
                    foreach (string str in reservedItems)
                    {
                        if (sourceFile.ToLower().EndsWith(str.ToLower()))
                        {
                            _logger.LogError($"Reserved file: {sourceFile}");
                            skipItem = true;
                        }
                    }
                }

                if (skipItem == false)
                {
                    string destinationFile = Path.Combine(targetFolder, sourceFile.Substring(sourceFile.LastIndexOf("\\") + 1));
                    CopyFile(sourceFile, destinationFile, true, handleInUseOnReboot);
                }

                skipItem = false;
            }

            string[] folderList = null;

            if (recursiveCopy)
            {
                folderList = Directory.GetDirectories(sourceFolder);

                foreach (string sourceDir in folderList)
                {
                    if (reservedItems != null)
                    {
                        foreach (string str in reservedItems)
                        {
                            if (sourceDir.ToLower().EndsWith(str.ToLower()))
                            {
                                if (verboseOutput)
                                {
                                    _logger.LogDebug($"Reserved folder: {sourceDir}");
                                }

                                skipItem = true;
                            }
                        }
                    }

                    // SPECIAL CASE: System Volume Information
                    if (sourceDir.ToLower().Contains("system volume information"))
                    {
                        if (verboseOutput)
                        {
                            _logger.LogDebug($"Reserved folder: {sourceDir}");
                        }

                        skipItem = true;
                    }

                    // SPECIAL CASE: System Volume Information
                    if (sourceDir.ToLower().Contains("$recycle"))
                    {
                        if (verboseOutput)
                        {
                            _logger.LogDebug($"Reserved folder: {sourceDir}");
                        }

                        skipItem = true;
                    }

                    if (skipItem == false)
                    {
                        string destinationPath = Path.Combine(targetFolder, sourceDir.Substring(sourceDir.LastIndexOf("\\") + 1));

                        if (verboseOutput)
                        {
                            _logger.LogInformation($"Copy folder: {sourceDir}");
                            _logger.LogInformation($"         To: {destinationPath}");
                        }

                        try
                        {
                            CopyFolderContents(sourceDir,
                                               destinationPath,
                                               reservedItems,
                                               verboseOutput,
                                               recursiveCopy,
                                               handleInUseOnReboot);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, $"Failed to copy folder [{sourceDir}] to desintation");
                        }
                    }

                    skipItem = false;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to copy directory to destination");
            return false;
        }

        return true;
    }

    public bool DeleteFile(string fileName,
                           bool raiseException = false,
                           bool handleInUseOnReboot = false)
    {
        bool fileDeleted = false;

        if (File.Exists(fileName))
        {
            try
            {
                try
                {
                    File.SetAttributes(fileName, FileAttributes.Normal);
                    File.Delete(fileName);
                    _logger.LogInformation($"Deleted file: {fileName}");
                    fileDeleted = true;
                }
                catch (Exception)
                {
                    // Is the specified file in-use?
                    if (IsFileInUse(fileName) 
                        && handleInUseOnReboot 
                        && fileName.ToLower().Contains(".delete_on_reboot") == false) // Avoid double-scheduling
                    {
                        try
                        {
                            string deleteFilename = $"{fileName}.delete_on_reboot";
                            int fileIncrement = 0;

                            while (true)
                            {
                                if (File.Exists(deleteFilename))
                                {
                                    deleteFilename = $"{fileName}.delete_on_reboot_{fileIncrement++}";
                                }
                                else
                                {
                                    break;
                                }
                            }

                            // Attempt to rename file.
                            // --> This may or may not succeed depending on type of
                            //     lock on the file.
                            File.Move(fileName, deleteFilename);

                            // Schedule deletion on next reboot.
                            bool scheduleDeleteion = NativeMethods.MoveFileEx(deleteFilename,
                                                                              null,
                                                                              NativeMethods.MoveFileFlags.DelayUntilReboot);

                            _logger.LogInformation($"Delete after reboot: {deleteFilename}");
                        }
                        catch (Exception)
                        {
                            // Schedule in-place deletion on next reboot.
                            NativeMethods.MoveFileEx(fileName,
                                                     null,
                                                     NativeMethods.MoveFileFlags.DelayUntilReboot);

                            _logger.LogInformation($"Delete after reboot: {fileName}");
                        }
                    }
                    else if (fileName.ToLower().Contains(".delete_on_reboot"))
                    {
                        fileDeleted = false;
                        _logger.LogDebug($"Deleted after reboot: {fileName}");
                    }
                    else
                    {
                        File.Delete(fileName);
                        _logger.LogInformation($"Deleted file: {fileName}");
                        fileDeleted = true;
                    }
                }
            }
            catch (Exception e)
            {
                fileDeleted = false;
                _logger.LogError(e, "Exception caught deleting file");

                if (raiseException)
                {
                    throw;
                }
            }
        }

        return fileDeleted;
    }

    public bool DeleteFilePattern(string folderName, string startsWith = "", string endsWith = "", bool raiseException = false)
    {
        bool fileDeleted = false;

        if (string.IsNullOrWhiteSpace(startsWith) && string.IsNullOrWhiteSpace(endsWith))
        {
            _logger.LogError("No file pattern provided");
            return false;
        }

        if (string.IsNullOrWhiteSpace(folderName))
        {
            _logger.LogError("Directory name cannot be empty");
            return false;
        }
        else if (Directory.Exists(folderName) == false)
        {
            _logger.LogError($"Specified directory {folderName} does not exist");
            return false;
        }

        try
        {
            string[] fileList = Directory.GetFiles(folderName);

            if (fileList.Length > 0)
            {
                string strFile;

                for (int n = 0; n <= fileList.Length - 1; n++)
                {
                    strFile = fileList[n].ToString().ToLower();
                    strFile = strFile.Substring(strFile.LastIndexOf("\\") + 1);

                    if (startsWith.Length > 0 && endsWith.Length > 0)
                    {
                        if (strFile.ToLower().StartsWith(startsWith.ToLower())
                            && strFile.ToLower().EndsWith(endsWith.ToLower()))
                        {
                            DeleteFile(fileList[n]);
                            fileDeleted = true;
                        }
                    }
                    else if (startsWith.Length > 0)
                    {
                        if (strFile.ToLower().StartsWith(startsWith.ToLower()))
                        {
                            DeleteFile(fileList[n]);
                            fileDeleted = true;
                        }
                    }
                    else if (endsWith.Length > 0)
                    {
                        if (strFile.ToLower().EndsWith(endsWith.ToLower()))
                        {
                            DeleteFile(fileList[n]);
                            fileDeleted = true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception caught deleting file");

            if (raiseException)
            {
                throw;
            }
        }

        return fileDeleted;
    }

    public bool DeleteFolder(string folderName, bool raiseException = false)
    {
        bool folderDeleted = false;

        if (Directory.Exists(folderName))
        {
            try
            {
                _logger.LogInformation($"Delete folder: {folderName}");
                DeleteFolderContents(folderName, null, true);
                Directory.Delete(folderName, true);
                folderDeleted = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception caught deleting folder");

                if (raiseException)
                {
                    throw;
                }
            }
        }
        else
        {
            folderDeleted = true;
        }

        return folderDeleted;
    }

    public void DeleteFolderContents(string targetFolder,
                                     string[] reservedItems,
                                     bool verboseOutput = true,
                                     bool recurseReservedItems = true)
    {
        if (Directory.Exists(targetFolder) == false)
        {
            return;
        }

        string[] fileList = Directory.GetFiles(targetFolder);
        string[] folderList = Directory.GetDirectories(targetFolder);

        try
        {
            // Adjust TargetFolder ACL, add permissions for BUILTIN\Administrators group.
            DirectoryInfo targetFolderInfo = new(targetFolder);
            DirectorySecurity targetFolderACL = new(targetFolder, AccessControlSections.Access);
            targetFolderACL.AddAccessRule(
                new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));
            targetFolderInfo.SetAccessControl(targetFolderACL);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set target folder access control list");
        }

        bool skipItem = false;

        if (folderList.Length > 0)
        {
            for (int n = 0; n <= folderList.Length - 1; n++)
            {
                try
                {
                    if (reservedItems != null)
                    {
                        foreach (string str in reservedItems)
                        {
                            if (folderList[n].ToString().ToLower().EndsWith(str.ToLower()))
                            {
                                _logger.LogDebug($"Reserved folder: {folderList[n]}");
                                skipItem = true;
                            }
                        }
                    }

                    if (skipItem == false)
                    {
                        DirectoryInfo folderInfo = new(folderList[n].ToString());

                        if (folderInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            try
                            {
                                _logger.LogInformation($"Remove junction: {folderList[n]}");
                                RemoveJunction(folderList[n].ToString());
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Exception caught removing NTFS junction: {folderList[n]}");
                            }
                        }
                        else
                        {
                            if (recurseReservedItems)
                            {
                                DeleteFolderContents(folderList[n].ToString(), reservedItems, false);
                            }
                            else
                            {
                                DeleteFolderContents(folderList[n].ToString(), null, false);
                            }
                        }

                        if (verboseOutput)
                        {
                            _logger.LogInformation($"Delete folder: {folderList[n]}");
                        }

                        Directory.Delete(folderList[n]);
                    }

                    skipItem = false;
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, $"Exception caught deleting folder: {folderList[n]}");
                }
            }
        }

        if (fileList.Length > 0)
        {
            for (int n = 0; n <= fileList.Length - 1; n++)
            {
                try
                {
                    if (reservedItems != null)
                    {
                        foreach (string str in reservedItems)
                        {
                            if (fileList[n].ToString().ToLower().EndsWith(str.ToLower()))
                            {
                                _logger.LogDebug($"Reserved file: {fileList[n]}");
                                skipItem = true;
                            }
                        }
                    }

                    if (skipItem == false)
                    {
                        if (verboseOutput)
                        {
                            _logger.LogInformation($"Delete file: {fileList[n]}");
                        }

                        File.SetAttributes(fileList[n], FileAttributes.Normal);
                        File.Delete(fileList[n]);
                    }

                    skipItem = false;
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, $"Exception caught deleting file: {fileList[n]}");
                }
            }
        }
    }

    public string GetAceInformation(FileSystemAccessRule ace)
    {
        StringBuilder info = new();
        info.AppendLine(string.Format("Account: {0}", ace.IdentityReference.Value));
        info.AppendLine(string.Format("Type: {0}", ace.AccessControlType));
        info.AppendLine(string.Format("Rights: {0}", ace.FileSystemRights));
        info.AppendLine(string.Format("Inherited ACE: {0}", ace.IsInherited));
        return info.ToString();
    }

    public bool IsFileInUse(string fileName)
    {
        if (File.Exists(fileName))
        {
            try
            {
                FileInfo fileInfo = new(fileName);
                FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                fileStream.Dispose();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
        else
        {
            return false;
        }
    }

    public string ListFolderContents(string folderPath)
    {
        List<string[]> foldersAndFiles = new();

        try
        {
            if (Directory.Exists(folderPath) == false)
            {
                return "Specified folder was not found [" + folderPath + "]";
            }

            foldersAndFiles.Add(new string[] { "Folder(s)", "" });
            foldersAndFiles.Add(new string[] { "---------", "" });

            foreach (string folder in Directory.GetDirectories(folderPath.Trim('\"')))
            {
                try
                {
                    foldersAndFiles.Add(new string[] { folder.Substring(folder.LastIndexOf("\\") + 1), BytesToReadableValue(SizeOfFileOrFolder(folder)) });
                }
                catch (Exception)
                {
                    foldersAndFiles.Add(new string[] { folder.Substring(folder.LastIndexOf("\\") + 1), "<Size unavailable>" });
                }
            }

            foldersAndFiles.Add(new string[] { "", "" });
            foldersAndFiles.Add(new string[] { "File(s)", "" });
            foldersAndFiles.Add(new string[] { "-------", "" });

            foreach (string file in Directory.GetFiles(folderPath))
            {
                try
                {
                    foldersAndFiles.Add(new string[] { Path.GetFileName(file), BytesToReadableValue(SizeOfFileOrFolder(file)) });
                }
                catch (Exception)
                {
                    foldersAndFiles.Add(new string[] { Path.GetFileName(file), "<Size unavailable>" });
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to iterate file(s) or folder(s) for [{folderPath}]");
        }

        string paddedTable = "";

        try
        {
            paddedTable = DotNetHelper.PadListElements(foldersAndFiles, 5);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to construct padded elements list");
            string returnString = "";

            foreach (string[] s in foldersAndFiles)
            {
                string unPaddedLine = string.Join(" ", s);
                returnString += unPaddedLine + Environment.NewLine;
            }

            return returnString;
        }

        return paddedTable;
    }

    public bool MoveFile(string sourceFileName,
                         string destFileName,
                         bool overWrite = true)
    {
        _logger.LogInformation($"Move file: {sourceFileName}");
        _logger.LogInformation($"       To: {destFileName}");

        try
        {
            if (overWrite)
            {
                DeleteFile(destFileName);
            }

            File.Move(sourceFileName, destFileName);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to move file [{Path.GetFileName(sourceFileName)}] to destination");
        }

        return false;
    }

    private static SafeFileHandle OpenReparsePoint(string reparsePoint, NativeMethods.EFileAccess accessMode)
    {
        // Open handle to reparse point.
        SafeFileHandle reparsePointHandle = new(
            NativeMethods.CreateFile(reparsePoint,
                                     accessMode,
                                     NativeMethods.EFileShare.Read | NativeMethods.EFileShare.Write | NativeMethods.EFileShare.Delete,
                                     IntPtr.Zero,
                                     NativeMethods.ECreationDisposition.OpenExisting,
                                     NativeMethods.EFileAttributes.BackupSemantics | NativeMethods.EFileAttributes.OpenReparsePoint,
                                     IntPtr.Zero),
            true);

        // Reparse point opened OK?
        if (Marshal.GetLastWin32Error() != 0)
        {
            throw new Win32Exception("Unable to open reparse point");
        }

        return reparsePointHandle;
    }

    public bool RemoveDirectorySecurity(string folderName,
                                        string userAccount,
                                        FileSystemRights revokedRights,
                                        AccessControlType controlType)
    {
        try
        {
            var dInfo = new DirectoryInfo(folderName);
            var dSecurity = dInfo.GetAccessControl();

            foreach (FileSystemAccessRule ace in dSecurity.GetAccessRules(true, true, typeof(NTAccount)))
            {
                if (ace.FileSystemRights.Equals(revokedRights) &&
                    ace.AccessControlType.Equals(controlType) &&
                    ace.IdentityReference.Translate(typeof(NTAccount)).Value.ToLower().Equals(userAccount.ToLower()))
                {
                    dSecurity.RemoveAccessRule(ace);
                    break;
                }
            }

            dInfo.SetAccessControl(dSecurity);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to revoke folder permissions from [{folderName}]");
            return false;
        }
    }

    public void RemoveJunction(string junctionPoint)
    {
        if (Directory.Exists(junctionPoint) == false && File.Exists(junctionPoint) == false)
        {
            return;
        }

        // Open the junction point.
        SafeFileHandle fileHandle = OpenReparsePoint(junctionPoint, NativeMethods.EFileAccess.GenericWrite);

        // Setup reparse structure.
        NativeMethods.REPARSE_DATA_BUFFER reparseDataBuffer = new NativeMethods.REPARSE_DATA_BUFFER
        {
            reparseTag = NativeMethods.IO_REPARSE_TAG_MOUNT_POINT,
            reparseDataLength = 0,
            pathBuffer = new byte[0x3FF0]
        };

        // Calculate buffer size and allocate.
        int inBufferSize = Marshal.SizeOf(reparseDataBuffer);
        IntPtr inBuffer = Marshal.AllocHGlobal(inBufferSize);

        try
        {
            // Create the pointer.
            Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

            // Delete the reparse point.
            bool result = NativeMethods.DeviceIoControl(
                fileHandle.DangerousGetHandle(),
                NativeMethods.FSCTL_DELETE_REPARSE_POINT,
                inBuffer, 8, IntPtr.Zero, 0, out int BytesReturned, IntPtr.Zero);

            if (result == false)
            {
                throw new Win32Exception("Unable to delete reparse point");
            }
        }
        finally
        {
            fileHandle.Dispose();
            Marshal.FreeHGlobal(inBuffer);
        }
    }

    public void ReplaceFileIn(string baseFolder,
                              string replaceFile,
                              string[] additionalFiles = null)
    {
        try
        {
            if (File.Exists(replaceFile) == false)
            {
                return;
            }

            foreach (string subFolder in Directory.GetDirectories(baseFolder))
            {
                ReplaceFileIn(subFolder, replaceFile, additionalFiles);
            }

            foreach (string someFile in Directory.GetFiles(baseFolder))
            {
                if (Path.GetFileName(someFile).ToLower().Equals(Path.GetFileName(replaceFile).ToLower()))
                {
                    _logger.LogInformation($"Replace file: {someFile}");
                    CopyFile(replaceFile, someFile, true);

                    if (additionalFiles != null)
                    {
                        foreach (string addFile in additionalFiles)
                        {
                            if (File.Exists(addFile))
                            {
                                string addFileDest = Path.GetDirectoryName(someFile) + "\\" + Path.GetFileName(addFile);
                                _logger.LogInformation("Replace file: " + addFileDest);
                                CopyFile(addFile, addFileDest, true);
                            }
                        }
                    }

                    break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Recursive file replacement failure");
        }
    }

    public long SizeOfFileOrFolder(string fileOrFolder)
    {
        try
        {
            if (File.Exists(fileOrFolder))
            {
                return new FileInfo(fileOrFolder).Length;
            }
            else if (Directory.Exists(fileOrFolder))
            {
                long totalSize = 0;
                DirectoryInfo dirInfo = new(fileOrFolder);
                FileInfo[] files = dirInfo.GetFiles();

                foreach (FileInfo fi in files)
                {
                    totalSize += fi.Length;
                }

                DirectoryInfo[] directories = dirInfo.GetDirectories();

                foreach (DirectoryInfo di in directories)
                {
                    totalSize += SizeOfFileOrFolder(di.FullName);
                }

                return totalSize;
            }
        }
        catch (Exception) { }
        return 0;
    }

    public bool VerifyAccess(string fileName)
    {
        try
        {
            if (File.Exists(fileName))
            {
                var fileInfo = new FileInfo(fileName);
                var ac = fileInfo.GetAccessControl();
                FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                fileStream.Dispose();
            }
            else
            {
                return false;
            }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
